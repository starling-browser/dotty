using System.Runtime.InteropServices;

namespace Dotty.Terminal.Pty;

/// <summary>
/// Combines Terminal + PTY into a unified driver.
/// </summary>
public class TerminalDriver : IDisposable
{
    private readonly Terminal _terminal;
    private readonly IPty? _pty;
    private bool _disposed;

    private TerminalDriver(Terminal terminal, IPty? pty)
    {
        _terminal = terminal;
        _pty = pty;
    }

    public static TerminalDriver Create(PtyConfig config)
    {
        var terminal = new Terminal(config.Size);
        IPty pty;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            pty = WindowsPty.Spawn(config);
        else
            pty = UnixPty.Spawn(config);

        return new TerminalDriver(terminal, pty);
    }

    public static TerminalDriver CreateWithoutPty(GridSize size)
    {
        return new TerminalDriver(new Terminal(size), null);
    }

    public Terminal Terminal => _terminal;
    public IPty? Pty => _pty;

    /// <summary>
    /// Read from PTY and process output. Returns number of bytes read.
    /// </summary>
    public int ProcessPty()
    {
        if (_pty is null) return 0;

        Span<byte> buf = stackalloc byte[4096];
        int n = _pty.Read(buf);

        if (n == 0)
        {
            _terminal.SetFinished(0);
            return 0;
        }

        _terminal.ProcessPtyOutput(buf[..n]);

        // Send any response data back to PTY
        var response = _terminal.TakeResponse();
        if (response.Length > 0)
            _pty.Write(response);

        return n;
    }

    /// <summary>
    /// Write data to the PTY.
    /// </summary>
    public void WriteToPty(ReadOnlySpan<byte> data)
    {
        _pty?.Write(data);
    }

    /// <summary>
    /// Resize both terminal and PTY.
    /// </summary>
    public void Resize(GridSize size)
    {
        _terminal.Resize(size);
        _pty?.Resize(size);
    }

    /// <summary>
    /// Check if child process has exited.
    /// </summary>
    public void CheckChild()
    {
        if (_pty is null) return;
        var exitCode = _pty.TryWaitExit();
        if (exitCode.HasValue)
            _terminal.SetFinished((uint)exitCode.Value);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _pty?.Dispose();
    }
}
