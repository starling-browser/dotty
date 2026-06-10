using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Dotty.Terminal.Hosting;
using Dotty.Input;
using Dotty.Rendering;
using Dotty.Theme;
using GridSize = Dotty.Terminal.GridSize;
using Palette = Dotty.Terminal.Palette;
using HostedTerminalSession = Dotty.Terminal.Hosting.TerminalSession;

namespace Dotty.Controls;

public class TerminalControl : Control
{
    private HostedTerminalSession? _session;
    private TerminalTheme _theme;
    private CellMetrics _metrics;
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
        _session?.SetFocus(focused: true);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _session?.SetFocus(focused: false);
    }

    private void StartTerminal(GridSize size)
    {
        if (_session != null) return;

        _lastSize = size;

        _session = new HostedTerminalSession(new TerminalSessionOptions { Size = size });
        _session.ScreenChanged += OnSessionScreenChanged;
        _session.Bell += OnSessionBell;
        _session.ClipboardWriteRequested += OnSessionClipboardWriteRequested;
        _session.Exited += OnSessionExited;
        _session.Start();

        // Render timer at ~60fps
        _renderTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        _renderTimer.Tick += (_, _) =>
        {
            _session?.CheckChild();
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

            if (_session?.HasDamage() == true)
                InvalidateVisual();
        };
        _renderTimer.Start();

        // Cursor blink timer at 530ms (standard terminal blink rate)
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(530) };
        _blinkTimer.Tick += (_, _) =>
        {
            if (_session?.ReadTerminal(static terminal => terminal.Cursor.Blinking) == true)
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
        _renderTimer?.Stop();
        _renderTimer = null;
        _resizeTimer?.Stop();
        _resizeTimer = null;
        _blinkTimer?.Stop();
        _blinkTimer = null;

        if (_session != null)
        {
            _session.ScreenChanged -= OnSessionScreenChanged;
            _session.Bell -= OnSessionBell;
            _session.ClipboardWriteRequested -= OnSessionClipboardWriteRequested;
            _session.Exited -= OnSessionExited;
            _session.Dispose();
            _session = null;
        }
    }

    private void OnSessionScreenChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(InvalidateVisual);

    private void OnSessionBell(object? sender, TerminalBellEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            _bellFlashUntil = DateTime.UtcNow.AddMilliseconds(120);
            InvalidateVisual();
        });

    private void OnSessionClipboardWriteRequested(object? sender, TerminalClipboardEventArgs e) =>
        Dispatcher.UIThread.Post(() => SetClipboardAsync(e.Text));

    private void OnSessionExited(object? sender, TerminalExitEventArgs e) =>
        Dispatcher.UIThread.Post(() =>
        {
            if (_exitNotified) return;
            _exitNotified = true;
            InvalidateVisual();
            ShellExited?.Invoke();
        });

    public override void Render(DrawingContext context)
    {
        if (_metrics.FontSize != FontSize)
            _metrics = new CellMetrics(FontSize);
        ushort newCols = _metrics.ColumnsForWidth(Bounds.Width);
        ushort newRows = _metrics.RowsForHeight(Bounds.Height);
        var newSize = new GridSize(newCols, newRows);

        // Defer terminal startup until first render when bounds are known
        if (_session == null)
        {
            if (newCols > 0 && newRows > 0)
                StartTerminal(newSize);
            return;
        }

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
                _session?.Resize(_pendingSize);
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
        var snapshot = _session.CreateSnapshot();
        TerminalRenderer.Draw(context, snapshot, _theme, _metrics, Bounds.Size, _cursorBlinkVisible, bellFlash);
        _session.AcknowledgeDamage();
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        if (_session == null) { base.OnPointerWheelChanged(e); return; }

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
        _session.UpdateTerminal(terminal => terminal.ScrollViewport(delta));
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (_session == null) return;

        _keyDownHandled = false;
        bool isFinished = _session.ReadTerminal(static terminal => terminal.IsFinished);

        // If shell has exited and user presses Ctrl+C, fire CloseRequested
        if (isFinished && e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CloseRequested?.Invoke();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Block all input after shell exits
        if (isFinished)
        {
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Shift+PageUp/PageDown/Home/End: scrollback navigation
        bool shiftOnly = e.KeyModifiers == KeyModifiers.Shift;

        if (shiftOnly && e.Key == Key.PageUp)
        {
            var rows = _session.ReadTerminal(static terminal => terminal.GridSize.Rows);
            _session.UpdateTerminal(terminal => terminal.ScrollViewport(-rows));
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        if (shiftOnly && e.Key == Key.PageDown)
        {
            var rows = _session.ReadTerminal(static terminal => terminal.GridSize.Rows);
            _session.UpdateTerminal(terminal => terminal.ScrollViewport(rows));
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        if (shiftOnly && e.Key == Key.Home)
        {
            var scrollbackLen = _session.ReadTerminal(static terminal => terminal.ScrollbackLen);
            _session.UpdateTerminal(terminal => terminal.ScrollViewport(-scrollbackLen));
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        if (shiftOnly && e.Key == Key.End)
        {
            _session.UpdateTerminal(static terminal => terminal.ResetViewport());
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Shift+Arrow: text selection
        bool isArrow = e.Key is Key.Left or Key.Right or Key.Up or Key.Down;

        if (isArrow && shiftOnly)
        {
            _session.UpdateTerminal(terminal =>
            {
                var grid = terminal.GridSize;
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
            });
            InvalidateVisual();
            e.Handled = true;
            _keyDownHandled = true;
            return;
        }

        // Plain arrow clears any active selection
        if (isArrow && e.KeyModifiers == KeyModifiers.None
            && _session.ReadTerminal(static terminal => terminal.Selection != null))
        {
            _session.UpdateTerminal(static terminal => terminal.ClearSelection());
            _selectionEnd = null;
            InvalidateVisual();
        }

        var modes = _session.ReadTerminal(static terminal => terminal.Modes);
        var encoded = KeyEncoder.Encode(e.Key, e.KeyModifiers, e.KeySymbol, modes);
        if (encoded != null)
        {
            if (_session.ReadTerminal(static terminal => terminal.IsScrolledBack))
            {
                _session.UpdateTerminal(static terminal => terminal.ResetViewport());
                InvalidateVisual();
            }

            _cursorBlinkVisible = true;
            _session.Write(encoded);
            e.Handled = true;
            _keyDownHandled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        if (_session == null || string.IsNullOrEmpty(e.Text)) return;
        if (_keyDownHandled) return; // Already handled by OnKeyDown

        if (_session.ReadTerminal(static terminal => terminal.IsScrolledBack))
        {
            _session.UpdateTerminal(static terminal => terminal.ResetViewport());
            InvalidateVisual();
        }

        _session.SendText(e.Text);
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
        var grid = _session?.ReadTerminal(static terminal => terminal.GridSize) ?? new GridSize(80, 24);
        col = Math.Clamp(col, 0, grid.Cols - 1);
        int clampedRow = Math.Clamp(rawRow, 0, grid.Rows - 1);
        return (new Dotty.Terminal.GridPosition((ushort)col, (ushort)clampedRow), rawRow);
    }

    private Dotty.Terminal.GridPosition HitTestCell(Point point) => HitTestCellRaw(point).Pos;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus();
        if (_session == null) { base.OnPointerPressed(e); return; }

        var props = e.GetCurrentPoint(this).Properties;
        if (!props.IsLeftButtonPressed) { base.OnPointerPressed(e); return; }

        var pos = HitTestCell(e.GetPosition(this));

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
            _session.UpdateTerminal(terminal => terminal.StartSelection(pos, Dotty.Terminal.SelectionMode.Line));
            _mouseSelecting = false;
            _selectionEnd = null;
            InvalidateVisual();
        }
        else if (e.ClickCount == 2)
        {
            _session.UpdateTerminal(terminal => terminal.SelectWord(pos));
            _mouseSelecting = false;
            _selectionEnd = null;
            InvalidateVisual();
        }
        else
        {
            _session.UpdateTerminal(terminal =>
            {
                terminal.ClearSelection();
                terminal.StartSelection(pos, Dotty.Terminal.SelectionMode.Normal);
            });
            _mouseSelecting = true;
            _selectionEnd = pos;
        }

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (_session != null && IsMouseTracking)
        {
            var modes = _session.ReadTerminal(static terminal => terminal.Modes);
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

        if (_mouseSelecting && _session != null)
        {
            var (pos, rawRow) = HitTestCellRaw(e.GetPosition(this));
            _session.UpdateTerminal(terminal =>
            {
                int rows = terminal.GridSize.Rows;
                if (rawRow < 0)
                    terminal.ScrollViewport(-Math.Max(1, -rawRow / 2));
                else if (rawRow >= rows)
                    terminal.ScrollViewport(Math.Max(1, (rawRow - rows + 1) / 2));

                terminal.UpdateSelection(pos);
            });
            _selectionEnd = pos;
            InvalidateVisual();
        }
        base.OnPointerMoved(e);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_session != null && IsMouseTracking && _lastMouseButton >= 0)
        {
            var pos = HitTestCell(e.GetPosition(this));
            SendMouseEvent(_lastMouseButton, pos.Col, pos.Row, false);
            _lastMouseButton = -1;
            e.Handled = true;
            return;
        }

        if (_mouseSelecting && _session != null)
        {
            _mouseSelecting = false;
            _session.UpdateTerminal(terminal =>
            {
                var sel = terminal.Selection;
                if (sel is { } s)
                {
                    var (start, end) = s.Normalized();
                    if (start.Col == end.Col && start.Row == end.Row)
                    {
                        terminal.ClearSelection();
                        _selectionEnd = null;
                    }
                }
            });
            InvalidateVisual();
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
        var text = _session?.ReadTerminal(static terminal => terminal.SelectionText());
        if (text != null && window.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public async void PasteFromClipboard(Window window)
    {
        if (_session == null || window.Clipboard is not { } clipboard) return;

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrEmpty(text)) return;

        _session.PasteText(text);
    }

    private async void SetClipboardAsync(string text)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.Clipboard is { } clipboard)
            await clipboard.SetTextAsync(text);
    }

    public void ResetTerminal()
    {
        _session?.UpdateTerminal(static terminal => terminal.FullReset());
        InvalidateVisual();
    }

    public void ClearTerminal()
    {
        _session?.UpdateTerminal(static terminal => terminal.ClearScreen(true));
        InvalidateVisual();
    }

    public string GetVisibleText() => _session?.GetVisibleText() ?? "";

    public void WriteToPty(byte[] data) => _session?.Write(data);

    public void ApplyTheme(TerminalTheme theme)
    {
        _theme = theme;
        InvalidateVisual();
    }

    private bool IsMouseTracking => _session?.IsMouseTracking() == true;

    private void SendMouseEvent(int button, int col, int row, bool press)
    {
        _session?.SendMouseButton(button, col, row, press);
    }

    private void SendMouseMotion(int button, int col, int row)
    {
        _session?.SendMouseMotion(button, col, row);
    }

    protected override Size MeasureOverride(Size availableSize) => availableSize;
}
