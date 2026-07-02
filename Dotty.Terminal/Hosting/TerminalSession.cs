using System.Text;
using Dotty.Terminal.Input;
using Dotty.Terminal.Pty;
using Dotty.Terminal.Rendering;

namespace Dotty.Terminal.Hosting;

public sealed class TerminalSession : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly TerminalDriver _driver;
    private Thread? _readerThread;
    private volatile bool _running;
    private bool _disposed;
    private bool _exitRaised;
    private string _lastTitle;
    private string? _lastWorkingDirectory;
    private string? _promptShimDirectory;

    public TerminalSession(TerminalSessionOptions options)
        : this(CreateDriver(options, out string? promptShimDirectory))
    {
        _promptShimDirectory = promptShimDirectory;
    }

    private static TerminalDriver CreateDriver(TerminalSessionOptions options, out string? promptShimDirectory)
    {
        var config = options.ToPtyConfig();
        promptShimDirectory = ZshPromptShim.Apply(options.PromptHint, config);
        try
        {
            return TerminalDriver.Create(config);
        }
        catch
        {
            ZshPromptShim.Cleanup(promptShimDirectory);
            throw;
        }
    }

    private TerminalSession(TerminalDriver driver)
    {
        _driver = driver;
        _lastTitle = driver.Terminal.Title;
        _lastWorkingDirectory = driver.Terminal.WorkingDirectory;
    }

    public event EventHandler? ScreenChanged;
    public event EventHandler<TerminalBellEventArgs>? Bell;
    public event EventHandler<TerminalClipboardEventArgs>? ClipboardWriteRequested;
    public event EventHandler<TerminalExitEventArgs>? Exited;
    public event EventHandler<TerminalFaultEventArgs>? Faulted;
    public event EventHandler<TerminalTitleChangedEventArgs>? TitleChanged;
    public event EventHandler<TerminalWorkingDirectoryChangedEventArgs>? WorkingDirectoryChanged;

    public bool IsRunning => _running;

    public static TerminalSession CreateWithoutPty(GridSize size) =>
        new(TerminalDriver.CreateWithoutPty(size));

    public void Start()
    {
        ThrowIfDisposed();
        if (_running || _driver.Pty is null) return;

        _running = true;
        _readerThread = new Thread(PtyReaderLoop)
        {
            IsBackground = true,
            Name = "Dotty PTY Reader"
        };
        _readerThread.Start();
    }

    public void Stop()
    {
        if (!_running && _disposed) return;

        _running = false;
        _driver.Dispose();
        _readerThread?.Join(TimeSpan.FromSeconds(1));
        _readerThread = null;
    }

    public TResult ReadTerminal<TResult>(Func<Terminal, TResult> read)
    {
        lock (_syncRoot)
            return read(_driver.Terminal);
    }

    public void UpdateTerminal(Action<Terminal> update, bool notifyChanged = true)
    {
        lock (_syncRoot)
            update(_driver.Terminal);

        if (notifyChanged)
            OnScreenChanged();
    }

    public TerminalScreenSnapshot CreateSnapshot() =>
        ReadTerminal(TerminalScreenSnapshot.FromTerminal);

    public string GetVisibleText() =>
        ReadTerminal(TerminalText.GetVisibleText);

    public void AcknowledgeDamage() =>
        UpdateTerminal(static terminal => terminal.AcknowledgeDamage(), notifyChanged: false);

    public bool HasDamage() =>
        ReadTerminal(static terminal => terminal.Damage.HasDamage(terminal.GridSize.Rows));

    public void Resize(GridSize size)
    {
        lock (_syncRoot)
            _driver.Resize(size);

        OnScreenChanged();
    }

    public void CheckChild()
    {
        uint? exitCode = null;
        bool exited = false;

        lock (_syncRoot)
        {
            bool wasFinished = _driver.Terminal.IsFinished;
            _driver.CheckChild();
            exited = !wasFinished && _driver.Terminal.IsFinished;
            exitCode = _driver.Terminal.ExitStatus;
        }

        if (exited)
            RaiseExited(exitCode);
    }

    public void Write(ReadOnlySpan<byte> data) => _driver.WriteToPty(data);

    public ValueTask WriteAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(data);
        return ValueTask.CompletedTask;
    }

    public bool SendKey(TerminalKey key, TerminalKeyModifiers modifiers, string? text)
    {
        var modes = ReadTerminal(static terminal => terminal.Modes);
        var encoded = TerminalInputEncoder.Encode(key, modifiers, text, modes);
        if (encoded == null) return false;

        Write(encoded);
        return true;
    }

    public void SendText(string text) => Write(Encoding.UTF8.GetBytes(text));

    public void PasteText(string text)
    {
        bool bracketed = ReadTerminal(static terminal => terminal.Modes.HasFlag(TerminalModes.BracketedPaste));
        Write(TerminalInputEncoder.EncodePaste(text, bracketed));
    }

    public void SetFocus(bool focused)
    {
        bool focusTracking = ReadTerminal(static terminal => terminal.Modes.HasFlag(TerminalModes.FocusTracking));
        if (!focusTracking) return;

        Write(focused ? "\x1b[I"u8.ToArray() : "\x1b[O"u8.ToArray());
    }

    public bool SendMouseButton(int button, int col, int row, bool press)
    {
        var modes = ReadTerminal(static terminal => terminal.Modes);
        var encoded = TerminalMouseEncoder.EncodeButton(modes, button, col, row, press);
        if (encoded == null) return false;

        Write(encoded);
        return true;
    }

    public void SendMouseMotion(int button, int col, int row)
    {
        var modes = ReadTerminal(static terminal => terminal.Modes);
        Write(TerminalMouseEncoder.EncodeMotion(modes, button, col, row));
    }

    public bool IsMouseTracking() =>
        ReadTerminal(static terminal => TerminalMouseEncoder.IsMouseTracking(terminal.Modes));

    private void PtyReaderLoop()
    {
        var buf = new byte[4096];
        while (_running && _driver.Pty != null)
        {
            try
            {
                int n = _driver.Pty.Read(buf);
                if (n > 0)
                {
                    var data = buf.AsSpan(0, n).ToArray();
                    ProcessPtyOutput(data);
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
            catch (IOException ex)
            {
                if (_running)
                    Faulted?.Invoke(this, new TerminalFaultEventArgs(ex));
                break;
            }
            catch (ObjectDisposedException ex)
            {
                if (_running)
                    Faulted?.Invoke(this, new TerminalFaultEventArgs(ex));
                break;
            }
        }
    }

    private void ProcessPtyOutput(byte[] data)
    {
        byte[] response;
        string? clipboard;
        bool bell;
        bool titleChanged;
        bool workingDirectoryChanged;
        string title;
        string? workingDirectory;

        lock (_syncRoot)
        {
            _driver.Terminal.ProcessPtyOutput(data);
            response = _driver.Terminal.TakeResponse();
            clipboard = _driver.Terminal.TakeClipboard();
            bell = _driver.Terminal.TakeBell();
            title = _driver.Terminal.Title;
            workingDirectory = _driver.Terminal.WorkingDirectory;
            titleChanged = title != _lastTitle;
            workingDirectoryChanged = workingDirectory != _lastWorkingDirectory;
            _lastTitle = title;
            _lastWorkingDirectory = workingDirectory;
        }

        if (response.Length > 0)
            Write(response);

        if (clipboard != null)
            ClipboardWriteRequested?.Invoke(this, new TerminalClipboardEventArgs(clipboard));

        if (bell)
            Bell?.Invoke(this, new TerminalBellEventArgs());

        if (titleChanged)
            TitleChanged?.Invoke(this, new TerminalTitleChangedEventArgs(title));

        if (workingDirectoryChanged)
            WorkingDirectoryChanged?.Invoke(this, new TerminalWorkingDirectoryChangedEventArgs(workingDirectory));

        OnScreenChanged();
    }

    private void RaiseExited(uint? exitCode)
    {
        if (_exitRaised) return;
        _exitRaised = true;
        Exited?.Invoke(this, new TerminalExitEventArgs(exitCode));
        OnScreenChanged();
    }

    private void OnScreenChanged() => ScreenChanged?.Invoke(this, EventArgs.Empty);

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        Stop();
        ZshPromptShim.Cleanup(_promptShimDirectory);
        _promptShimDirectory = null;
        _disposed = true;
    }
}
