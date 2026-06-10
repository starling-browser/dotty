using System;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using Dotty.Input;
using Dotty.Rendering;
using Dotty.Theme;
using GridSize = Dotty.Terminal.GridSize;
using Palette = Dotty.Terminal.Palette;
using TerminalDriver = Dotty.Terminal.Pty.TerminalDriver;
using PtyConfig = Dotty.Terminal.Pty.PtyConfig;

namespace Dotty.Controls;

public class TerminalControl : Control
{
    private TerminalDriver? _driver;
    private TerminalTheme _theme;
    private CellMetrics _metrics;
    private Thread? _ptyReaderThread;
    private volatile bool _running;
    private DispatcherTimer? _renderTimer;
    private DispatcherTimer? _resizeTimer;
    private DispatcherTimer? _blinkTimer;
    private bool _cursorBlinkVisible = true;
    private DateTime _bellFlashUntil;
    private GridSize _lastSize;
    private GridSize _pendingSize;
    private bool _keyDownHandled;
    private Dotty.Terminal.GridPosition? _selectionEnd;
    private bool _mouseSelecting;
    private bool _exitNotified;
    private int _lastMouseButton = -1;
    private Dotty.Terminal.GridPosition _lastMousePos;
    private DateTime _suppressRenderUntil = DateTime.MinValue;

    public event Action? ShellExited;
    public event Action? CloseRequested;

    public double FontSize { get; set; } = 16.0;

    public TerminalControl()
    {
        Focusable = true;
        ClipToBounds = true;
        _theme = new TerminalTheme(Palette.CatppuccinMocha());
        _metrics = new CellMetrics(FontSize);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // Terminal startup is deferred to the first Render() call
        // where actual bounds are known, avoiding a spurious resize + SIGWINCH.
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        StopTerminal();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_driver?.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.FocusTracking) == true)
            _driver.WriteToPty("\x1b[I"u8.ToArray());
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        if (_driver?.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.FocusTracking) == true)
            _driver.WriteToPty("\x1b[O"u8.ToArray());
    }

    private void StartTerminal(GridSize size)
    {
        if (_driver != null) return;

        _lastSize = size;

        var config = new PtyConfig { Size = size };
        _driver = TerminalDriver.Create(config);
        _running = true;

        // PTY reader thread
        _ptyReaderThread = new Thread(PtyReaderLoop)
        {
            IsBackground = true,
            Name = "PTY Reader"
        };
        _ptyReaderThread.Start();

        // Render timer at ~60fps
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += (_, _) =>
        {
            _driver?.CheckChild();
            if (!_exitNotified && _driver?.Terminal.IsFinished == true)
            {
                _exitNotified = true;
                InvalidateVisual();
                ShellExited?.Invoke();
            }
            // Check for visual bell
            if (_driver?.Terminal.TakeBell() == true)
            {
                _bellFlashUntil = DateTime.UtcNow.AddMilliseconds(120);
                InvalidateVisual();
            }
            // Clear bell flash
            if (_bellFlashUntil > DateTime.MinValue && DateTime.UtcNow >= _bellFlashUntil)
            {
                _bellFlashUntil = DateTime.MinValue;
                InvalidateVisual();
            }
            // Suppress renders during post-resize settle window so partial child-process
            // redraws (e.g. just the horizontal separator lines) are never shown.
            if (_suppressRenderUntil != DateTime.MinValue)
            {
                if (DateTime.UtcNow < _suppressRenderUntil)
                    return;
                _suppressRenderUntil = DateTime.MinValue;
            }

            if (_driver?.Terminal.Damage.HasDamage(_driver.Terminal.GridSize.Rows) == true)
                InvalidateVisual();
        };
        _renderTimer.Start();

        // Cursor blink timer at 530ms (standard terminal blink rate)
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _blinkTimer.Tick += (_, _) =>
        {
            if (_driver?.Terminal.Cursor.Blinking == true)
            {
                _cursorBlinkVisible = !_cursorBlinkVisible;
                InvalidateVisual();
            }
            else if (!_cursorBlinkVisible)
            {
                _cursorBlinkVisible = true;
                InvalidateVisual();
            }
        };
        _blinkTimer.Start();

        Focus();
    }

    private void StopTerminal()
    {
        _running = false;
        _renderTimer?.Stop();
        _renderTimer = null;
        _resizeTimer?.Stop();
        _resizeTimer = null;
        _blinkTimer?.Stop();
        _blinkTimer = null;
        _driver?.Dispose();
        _ptyReaderThread?.Join(TimeSpan.FromSeconds(1));
        _driver = null;
    }

    private void PtyReaderLoop()
    {
        var buf = new byte[4096];
        while (_running && _driver != null)
        {
            try
            {
                int n = _driver.Pty?.Read(buf) ?? 0;
                if (n > 0)
                {
                    var data = buf.AsSpan(0, n).ToArray();
                    Dispatcher.UIThread.Post(() =>
                    {
                        _driver?.Terminal.ProcessPtyOutput(data);

                        // Send response data back to PTY
                        var response = _driver?.Terminal.TakeResponse();
                        if (response is { Length: > 0 })
                            _driver?.WriteToPty(response);

                        // Handle OSC 52 clipboard write
                        var clipText = _driver?.Terminal.TakeClipboard();
                        if (clipText != null)
                            SetClipboardAsync(clipText);

                        // Render timer at 60fps checks damage and invalidates;
                        // no need to call InvalidateVisual() here per chunk.
                    });
                }
                else if (n == 0)
                {
                    Thread.Sleep(1);
                }
            }
            catch (IOException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
        }
    }

    public override void Render(DrawingContext context)
    {
        if (_metrics.FontSize != FontSize)
            _metrics = new CellMetrics(FontSize);
        ushort newCols = _metrics.ColumnsForWidth(Bounds.Width);
        ushort newRows = _metrics.RowsForHeight(Bounds.Height);
        var newSize = new GridSize(newCols, newRows);

        // Defer terminal startup until first render when bounds are known
        if (_driver == null)
        {
            if (newCols > 0 && newRows > 0)
                StartTerminal(newSize);
            return;
        }

        var terminal = _driver.Terminal;

        // Resize if bounds changed — debounce to avoid expensive reflow on every pixel during drag
        if (newSize != _lastSize && newCols > 0 && newRows > 0)
        {
            _pendingSize = newSize;
            _lastSize = newSize;
            _resizeTimer?.Stop();
            _resizeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _resizeTimer.Tick += (_, _) =>
            {
                _resizeTimer!.Stop();
                _driver?.Resize(_pendingSize);
                _suppressRenderUntil = DateTime.UtcNow.AddMilliseconds(150);
                InvalidateVisual();
            };
            _resizeTimer.Start();
        }

        // During post-resize suppress window, show background only so partial child-process
        // redraws are never visible. Damage is left unacknowledged so the timer fires after
        // suppression and triggers a normal render with the fully-redrawn content.
        if (_suppressRenderUntil != DateTime.MinValue && DateTime.UtcNow < _suppressRenderUntil)
        {
            context.FillRectangle(_theme.BackgroundBrush, new Rect(0, 0, Bounds.Width, Bounds.Height));
            return;
        }

        bool bellFlash = _bellFlashUntil > DateTime.MinValue && DateTime.UtcNow < _bellFlashUntil;
        TerminalRenderer.Draw(context, terminal, _theme, _metrics, Bounds.Size, _cursorBlinkVisible, bellFlash);
        terminal.AcknowledgeDamage();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_driver == null) { base.OnPointerWheelChanged(e); return; }

        var terminal = _driver.Terminal;

        // Forward wheel to application if mouse tracking is active
        if (IsMouseTracking)
        {
            var pos = HitTestCell(e.GetPosition(this));
            int button = e.Delta.Y > 0 ? 64 : 65; // 64=wheel up, 65=wheel down
            SendMouseEvent(button, pos.Col, pos.Row, true);
            e.Handled = true;
            return;
        }

        int delta = e.Delta.Y > 0 ? -3 : 3;
        terminal.ScrollViewport(delta);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_driver == null) return;

        _keyDownHandled = false;
        var terminal = _driver.Terminal;

        // If shell has exited and user presses Ctrl+C, fire CloseRequested
        if (terminal.IsFinished && e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CloseRequested?.Invoke();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Block all input after shell exits
        if (terminal.IsFinished)
        {
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Shift+PageUp/PageDown/Home/End: scrollback navigation
        bool shiftOnly = e.KeyModifiers == KeyModifiers.Shift;

        if (shiftOnly && e.Key == Key.PageUp)
        {
            terminal.ScrollViewport(-terminal.GridSize.Rows);
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        if (shiftOnly && e.Key == Key.PageDown)
        {
            terminal.ScrollViewport(terminal.GridSize.Rows);
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        if (shiftOnly && e.Key == Key.Home)
        {
            terminal.ScrollViewport(-terminal.ScrollbackLen);
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        if (shiftOnly && e.Key == Key.End)
        {
            terminal.ResetViewport();
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Shift+Arrow: text selection
        bool isArrow = e.Key is Key.Left or Key.Right or Key.Up or Key.Down;

        if (isArrow && shiftOnly)
        {
            var grid = terminal.GridSize;

            // Start from the tracked endpoint, or the cursor if this is a fresh selection
            var from = _selectionEnd ?? terminal.CursorPos;
            int col = from.Col;
            int row = from.Row;
            switch (e.Key)
            {
                case Key.Left:  col = Math.Max(0, col - 1); break;
                case Key.Right: col = Math.Min(grid.Cols - 1, col + 1); break;
                case Key.Up:    row = Math.Max(0, row - 1); break;
                case Key.Down:  row = Math.Min(grid.Rows - 1, row + 1); break;
            }
            var newPos = new Dotty.Terminal.GridPosition((ushort)col, (ushort)row);

            if (terminal.Selection == null)
                terminal.StartSelection(terminal.CursorPos, Dotty.Terminal.SelectionMode.Normal);

            terminal.UpdateSelection(newPos);
            _selectionEnd = newPos;
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Plain arrow clears any active selection
        if (isArrow && e.KeyModifiers == KeyModifiers.None && terminal.Selection != null)
        {
            terminal.ClearSelection();
            _selectionEnd = null;
            InvalidateVisual();
        }

        var encoded = KeyEncoder.Encode(e.Key, e.KeyModifiers, e.KeySymbol, terminal.Modes);
        if (encoded != null)
        {
            if (terminal.IsScrolledBack)
            {
                terminal.ResetViewport();
                InvalidateVisual();
            }
            // Reset blink so cursor is visible when typing
            _cursorBlinkVisible = true;
            _driver.WriteToPty(encoded);
            e.Handled = true;
            _keyDownHandled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (_driver == null || string.IsNullOrEmpty(e.Text)) return;
        if (_keyDownHandled) return; // Already handled by OnKeyDown

        if (_driver.Terminal.IsScrolledBack)
        {
            _driver.Terminal.ResetViewport();
            InvalidateVisual();
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(e.Text);
        _driver.WriteToPty(bytes);
        e.Handled = true;
    }

    /// <summary>
    /// Hit-test a pixel point to a grid cell position.
    /// Returns the raw row (may be outside 0..rows-1), and clamps col.
    /// </summary>
    private (Dotty.Terminal.GridPosition Pos, int RawRow) HitTestCellRaw(Point point)
    {
        int col = (int)((point.X - _metrics.PadX) / _metrics.CellWidth);
        int rawRow = (int)((point.Y - _metrics.PadY) / _metrics.CellHeight);
        var grid = _driver?.Terminal.GridSize ?? new GridSize(80, 24);
        col = Math.Clamp(col, 0, grid.Cols - 1);
        int clampedRow = Math.Clamp(rawRow, 0, grid.Rows - 1);
        return (new Dotty.Terminal.GridPosition((ushort)col, (ushort)clampedRow), rawRow);
    }

    private Dotty.Terminal.GridPosition HitTestCell(Point point) => HitTestCellRaw(point).Pos;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();
        if (_driver == null) { base.OnPointerPressed(e); return; }

        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) { base.OnPointerPressed(e); return; }

        var pos = HitTestCell(e.GetPosition(this));
        var terminal = _driver.Terminal;

        // If mouse tracking is active, forward press to the application
        if (IsMouseTracking)
        {
            _lastMouseButton = 0; // left button
            SendMouseEvent(0, pos.Col, pos.Row, true);
            e.Handled = true;
            return;
        }

        if (e.ClickCount == 3)
        {
            // Triple-click: line select
            terminal.StartSelection(pos, Dotty.Terminal.SelectionMode.Line);
            _mouseSelecting = false;
            _selectionEnd = null;
            InvalidateVisual();
        }
        else if (e.ClickCount == 2)
        {
            // Double-click: word select
            terminal.SelectWord(pos);
            _mouseSelecting = false;
            _selectionEnd = null;
            InvalidateVisual();
        }
        else
        {
            // Single click: start normal selection
            terminal.ClearSelection();
            terminal.StartSelection(pos, Dotty.Terminal.SelectionMode.Normal);
            _mouseSelecting = true;
            _selectionEnd = pos;
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_driver != null && IsMouseTracking)
        {
            var modes = _driver.Terminal.Modes;
            bool buttonEvent = modes.HasFlag(Dotty.Terminal.TerminalModes.MouseButtonEvent);
            bool anyEvent = modes.HasFlag(Dotty.Terminal.TerminalModes.MouseAnyEvent);

            if (anyEvent || (buttonEvent && _lastMouseButton >= 0))
            {
                var pos = HitTestCell(e.GetPosition(this));
                if (pos.Col != _lastMousePos.Col || pos.Row != _lastMousePos.Row)
                {
                    int button = _lastMouseButton >= 0 ? _lastMouseButton : 3;
                    SendMouseMotion(button, pos.Col, pos.Row);
                    _lastMousePos = pos;
                }
            }
            e.Handled = true;
            return;
        }

        if (_mouseSelecting && _driver != null)
        {
            var (pos, rawRow) = HitTestCellRaw(e.GetPosition(this));
            var terminal = _driver.Terminal;
            int rows = terminal.GridSize.Rows;

            // Auto-scroll when dragging above or below viewport
            if (rawRow < 0)
                terminal.ScrollViewport(-Math.Max(1, -rawRow / 2));
            else if (rawRow >= rows)
                terminal.ScrollViewport(Math.Max(1, (rawRow - rows + 1) / 2));

            terminal.UpdateSelection(pos);
            _selectionEnd = pos;
            InvalidateVisual();
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_driver != null && IsMouseTracking && _lastMouseButton >= 0)
        {
            var pos = HitTestCell(e.GetPosition(this));
            SendMouseEvent(_lastMouseButton, pos.Col, pos.Row, false);
            _lastMouseButton = -1;
            e.Handled = true;
            return;
        }

        if (_mouseSelecting && _driver != null)
        {
            _mouseSelecting = false;
            // If start == end (no drag), clear selection
            var sel = _driver.Terminal.Selection;
            if (sel is { } s)
            {
                var (start, end) = s.Normalized();
                if (start.Col == end.Col && start.Row == end.Row)
                {
                    _driver.Terminal.ClearSelection();
                    _selectionEnd = null;
                    InvalidateVisual();
                }
            }
        }
        base.OnPointerReleased(e);
    }

    public void ChangeFontSize(double delta)
    {
        FontSize = Math.Clamp(FontSize + delta, 8, 48);
        _metrics = new CellMetrics(FontSize);
        InvalidateVisual();
    }

    public void ResetFontSize()
    {
        FontSize = 16;
        _metrics = new CellMetrics(FontSize);
        InvalidateVisual();
    }

    public async void CopySelection(Window window)
    {
        var text = _driver?.Terminal.SelectionText();
        if (text != null && window.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async void PasteFromClipboard(Window window)
    {
        if (_driver == null || window.Clipboard is not { } clipboard) return;

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text)) return;

        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        bool bracketed = _driver.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.BracketedPaste);

        if (bracketed)
            _driver.WriteToPty(System.Text.Encoding.UTF8.GetBytes("\x1b[200~"));

        _driver.WriteToPty(bytes);

        if (bracketed)
            _driver.WriteToPty(System.Text.Encoding.UTF8.GetBytes("\x1b[201~"));
    }

    private async void SetClipboardAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public void ResetTerminal()
    {
        _driver?.Terminal.FullReset();
        InvalidateVisual();
    }

    public void ClearTerminal()
    {
        _driver?.Terminal.ClearScreen(true);
        InvalidateVisual();
    }

    public string GetVisibleText()
    {
        if (_driver is null) return "";

        var terminal = _driver.Terminal;
        var size = terminal.GridSize;
        var sb = new System.Text.StringBuilder();

        for (ushort row = 0; row < size.Rows; row++)
        {
            var cells = terminal.RowCells(row);
            var line = new System.Text.StringBuilder();
            for (int col = 0; col < cells.Length; col++)
                line.Append(cells[col].Codepoint);

            // Trim trailing whitespace per line
            var trimmed = line.ToString().TrimEnd();
            sb.AppendLine(trimmed);
        }

        // Remove fully empty trailing lines
        var result = sb.ToString().TrimEnd('\r', '\n');
        return result.Length > 0 ? result + "\n" : "";
    }

    public void WriteToPty(byte[] data) => _driver?.WriteToPty(data);

    public void ApplyTheme(TerminalTheme theme)
    {
        _theme = theme;
        InvalidateVisual();
    }

    private bool IsMouseTracking =>
        _driver?.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.MouseX10) == true
        || _driver?.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.MouseNormal) == true
        || _driver?.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.MouseButtonEvent) == true
        || _driver?.Terminal.Modes.HasFlag(Dotty.Terminal.TerminalModes.MouseAnyEvent) == true;

    private void SendMouseEvent(int button, int col, int row, bool press)
    {
        if (_driver == null) return;
        var modes = _driver.Terminal.Modes;

        if (modes.HasFlag(Dotty.Terminal.TerminalModes.MouseSgr))
        {
            // SGR format: ESC [ < Pb ; Px ; Py M (press) or m (release)
            char suffix = press ? 'M' : 'm';
            var seq = $"\x1b[<{button};{col + 1};{row + 1}{suffix}";
            _driver.WriteToPty(System.Text.Encoding.UTF8.GetBytes(seq));
        }
        else
        {
            // X10/Normal format: ESC [ M Cb Cx Cy (all +32)
            if (!press && modes.HasFlag(Dotty.Terminal.TerminalModes.MouseX10))
                return; // X10 only reports presses

            int cb = press ? button : 3; // 3 = release in normal mode
            byte[] seq = [0x1b, (byte)'[', (byte)'M',
                (byte)(cb + 32),
                (byte)Math.Min(col + 33, 255),
                (byte)Math.Min(row + 33, 255)];
            _driver.WriteToPty(seq);
        }
    }

    private void SendMouseMotion(int button, int col, int row)
    {
        if (_driver == null) return;
        var modes = _driver.Terminal.Modes;

        if (modes.HasFlag(Dotty.Terminal.TerminalModes.MouseSgr))
        {
            var seq = $"\x1b[<{button + 32};{col + 1};{row + 1}M";
            _driver.WriteToPty(System.Text.Encoding.UTF8.GetBytes(seq));
        }
        else
        {
            byte[] seq = [0x1b, (byte)'[', (byte)'M',
                (byte)(button + 32 + 32),
                (byte)Math.Min(col + 33, 255),
                (byte)Math.Min(row + 33, 255)];
            _driver.WriteToPty(seq);
        }
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;
}
