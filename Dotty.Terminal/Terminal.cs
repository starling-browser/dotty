using Dotty.Terminal.Parser;

namespace Dotty.Terminal;

/// <summary>
/// The main terminal state machine.
/// Ported from reference terminal.rs.
/// </summary>
public class Terminal
{
    private Grid _grid;
    private Grid _altGrid;
    private CursorState _cursor;
    private SavedCursor _savedCursor;
    private SavedCursor _savedCursorAlt;
    private readonly ScrollbackBuffer _scrollback;
    private readonly DamageReport _damage;
    private TerminalModes _modes;
    private int _viewportOffset; // 0 = at bottom, >0 = scrolled back N rows

    // Current pen attributes for new characters
    private Color _penFg;
    private Color _penBg;
    private CellAttributes _penAttrs;

    // Selection state
    private SelectionRange? _selection;

    // Window title (set by OSC 0/2)
    private string _title = "";

    // Working directory (set by OSC 7)
    private string? _workingDirectory;

    // VT parser
    private readonly VtStateMachine _parser;

    // Whether the last character triggered the pending wrap state
    private bool _wrapPending;

    // Character set state: G0 and G1 designations, and which is active (GL)
    private CharacterSet _g0 = CharacterSet.Ascii;
    private CharacterSet _g1 = CharacterSet.Ascii;
    private int _glSet; // 0 = G0 active, 1 = G1 active

    // Last printed character (for REP — CSI b)
    private char _lastPrintedChar = ' ';

    // Tab stops
    private bool[] _tabStops;

    // PTY output buffer (responses like DSR)
    private readonly List<byte> _responseBuffer = new();

    // Clipboard write via OSC 52
    private string? _pendingClipboard;

    // Process state
    private bool _finished;
    private uint? _exitCode;

    public Terminal(GridSize size)
    {
        _grid = new Grid(size);
        _altGrid = new Grid(size);
        _cursor = CursorState.Default;
        _savedCursor = SavedCursor.Default;
        _savedCursorAlt = SavedCursor.Default;
        _scrollback = new ScrollbackBuffer();
        _damage = new DamageReport();
        _modes = TerminalModesExtensions.Initial;
        _penFg = Color.DefaultColor;
        _penBg = Color.DefaultColor;
        _penAttrs = CellAttributes.None;
        _parser = new VtStateMachine();
        _wrapPending = false;

        _tabStops = new bool[size.Cols];
        for (int i = 0; i < size.Cols; i += 8)
            _tabStops[i] = true;
    }

    // ── Public read-only properties ──

    public TerminalModes Modes => _modes;
    public GridSize Size => _grid.Size;
    public GridPosition CursorPos => _cursor.Position;
    public CursorState Cursor => _cursor;
    public DamageReport Damage => _damage;
    public string Title => _title;
    public string? WorkingDirectory => _workingDirectory;
    public bool IsFinished => _finished;
    public uint? ExitStatus => _exitCode;
    public int ViewportOffset => _viewportOffset;
    public bool IsScrolledBack => _viewportOffset > 0;

    // ── Internal accessors used by the VT handler ──

    internal Grid GridRef => _grid;
    internal Grid GridMut => _grid;
    internal DamageReport DamageReportRef => _damage;
    internal ushort ScrollTopRow => _grid.ScrollTop;
    internal ushort ScrollBottomRow => _grid.ScrollBottom;
    internal bool WrapPending => _wrapPending;
    internal char LastPrintedChar => _lastPrintedChar;

    // ── Public methods ──

    public Cell[] RowCells(ushort row) => _grid.RowSlice(row);

    public GridSize GridSize => _grid.Size;

    public SelectionRange? Selection => _selection;

    public Cell[]? ScrollbackRow(int index) => _scrollback.Get(index)?.Cells;

    public int ScrollbackLen => _scrollback.Count;

    /// <summary>
    /// Adjust viewport offset. Negative delta = scroll toward history (up),
    /// positive = toward bottom (down). Clamped to [0, ScrollbackLen].
    /// </summary>
    public void ScrollViewport(int delta)
    {
        _viewportOffset = Math.Clamp(_viewportOffset - delta, 0, _scrollback.Count);
    }

    /// <summary>
    /// Returns the correct Cell[] for a given viewport row, compositing
    /// scrollback rows (at the top) and active grid rows (at the bottom).
    /// </summary>
    public Cell[] ViewportRowCells(ushort viewportRow)
    {
        if (_viewportOffset == 0)
            return _grid.RowSlice(viewportRow);

        // How many rows of the viewport come from scrollback
        int scrollbackRowsVisible = Math.Min(_viewportOffset, _grid.Size.Rows);

        if (viewportRow < scrollbackRowsVisible)
        {
            // This row comes from scrollback.
            // scrollback index: offset counts from bottom of scrollback.
            // viewportRow 0 = oldest visible row = scrollback index (offset - 1)
            // viewportRow (scrollbackRowsVisible-1) = most recent visible scrollback = index (offset - scrollbackRowsVisible)
            int sbIndex = _viewportOffset - 1 - viewportRow;
            var row = _scrollback.Get(sbIndex);
            return row?.Cells ?? new Row(_grid.Size.Cols).Cells;
        }
        else
        {
            // This row comes from the active grid
            ushort gridRow = (ushort)(viewportRow - scrollbackRowsVisible);
            return _grid.RowSlice(gridRow);
        }
    }

    /// <summary>
    /// Snap viewport to bottom (most recent output).
    /// </summary>
    public void ResetViewport() => _viewportOffset = 0;

    public void ClearScreen(bool clearScrollback = false)
        => EraseInDisplay(clearScrollback ? (ushort)3 : (ushort)2);

    public void FullReset() => Reset();

    public void AcknowledgeDamage() => _damage.Reset();

    public byte[] TakeResponse()
    {
        var response = _responseBuffer.ToArray();
        _responseBuffer.Clear();
        return response;
    }

    public void SetFinished(uint? exitCode)
    {
        _finished = true;
        _exitCode = exitCode;
    }

    public string? SelectionText()
    {
        if (_selection is not { } sel) return null;
        var (start, end) = sel.Normalized();

        // Line mode highlights whole rows (anchor == endpoint on a triple
        // click), so the copied text must span whole rows too.
        var lineMode = sel.Mode == SelectionMode.Line;
        if (lineMode)
        {
            start = new GridPosition(0, start.Row);
            end = new GridPosition((ushort)(_grid.Size.Cols - 1), end.Row);
        }

        var sb = new System.Text.StringBuilder();

        for (ushort row = start.Row; row <= end.Row; row++)
        {
            // Use ViewportRowCells so selection works correctly when scrolled back
            var cells = ViewportRowCells(row);
            ushort rowStart = row == start.Row ? start.Col : (ushort)0;
            ushort rowEnd = row == end.Row ? end.Col : (ushort)(cells.Length - 1);

            var line = new System.Text.StringBuilder();
            for (ushort col = rowStart; col <= rowEnd; col++)
            {
                // Wide glyphs occupy two cells; the spacer half is presentation
                // only and must not become a space in the copied text.
                if (!cells[col].Attrs.HasFlag(CellAttributes.WideSpacer))
                    line.Append(cells[col].Codepoint);
            }

            if (row != end.Row)
            {
                sb.Append(line.ToString().TrimEnd());
                sb.Append('\n');
            }
            else
            {
                // Line mode always ends at the grid edge — trim the padding
                // cells that were never part of the printed line.
                sb.Append(lineMode ? line.ToString().TrimEnd() : line.ToString());
            }
        }

        var text = sb.ToString();
        return string.IsNullOrEmpty(text) ? null : text;
    }

    // ── Selection methods ──

    public void StartSelection(GridPosition pos, SelectionMode mode)
    {
        _selection = new SelectionRange(pos, pos, mode);
    }

    public void UpdateSelection(GridPosition pos)
    {
        if (_selection is { } sel)
        {
            sel.End = pos;
            _selection = sel;
        }
    }

    public void ClearSelection() => _selection = null;

    /// <summary>
    /// Select the word at the given viewport position.
    /// Word boundaries are spaces and common punctuation.
    /// </summary>
    public void SelectWord(GridPosition pos)
    {
        var cells = ViewportRowCells(pos.Row);
        int col = pos.Col;
        if (col >= cells.Length) return;

        // Find word boundaries (non-word chars: space and common delimiters)
        static bool IsWordChar(char c) => c != ' ' && c != '\t' && c != '"' && c != '\''
            && c != '(' && c != ')' && c != '[' && c != ']' && c != '{' && c != '}'
            && c != '<' && c != '>' && c != '|' && c != ';' && c != '&';

        if (!IsWordChar(cells[col].Codepoint))
        {
            // Clicked on a non-word char — select just that char
            _selection = new SelectionRange(pos, pos, SelectionMode.Word);
            return;
        }

        int start = col;
        while (start > 0 && IsWordChar(cells[start - 1].Codepoint))
            start--;

        int end = col;
        while (end < cells.Length - 1 && IsWordChar(cells[end + 1].Codepoint))
            end++;

        _selection = new SelectionRange(
            new GridPosition((ushort)start, pos.Row),
            new GridPosition((ushort)end, pos.Row),
            SelectionMode.Word);
    }

    // ── PTY processing ──

    public void ProcessPtyOutput(ReadOnlySpan<byte> data)
    {
        var handler = new VtHandler(this);
        _parser.Advance(handler, data);
    }

    // ── Cursor movement (internal, called by VtHandler) ──

    internal void SetCursorPos(ushort col, ushort row)
    {
        var size = _grid.Size;
        _cursor.Position = new GridPosition(
            Math.Min(col, (ushort)(size.Cols - 1)),
            Math.Min(row, (ushort)(size.Rows - 1)));
        _wrapPending = false;
    }

    internal void SetCursorCol(ushort col)
    {
        _cursor.Position = _cursor.Position with { Col = Math.Min(col, (ushort)(_grid.Size.Cols - 1)) };
        _wrapPending = false;
    }

    internal void SetCursorRow(ushort row)
    {
        _cursor.Position = _cursor.Position with { Row = Math.Min(row, (ushort)(_grid.Size.Rows - 1)) };
        _wrapPending = false;
    }

    internal void MoveCursorUp(ushort n)
    {
        ushort newRow = (ushort)Math.Max(_cursor.Position.Row - n, 0);
        _cursor.Position = _cursor.Position with { Row = Math.Max(newRow, _grid.ScrollTop) };
        _wrapPending = false;
    }

    internal void MoveCursorDown(ushort n)
    {
        ushort newRow = (ushort)(_cursor.Position.Row + n);
        _cursor.Position = _cursor.Position with { Row = Math.Min(newRow, (ushort)(_grid.ScrollBottom - 1)) };
        _wrapPending = false;
    }

    internal void MoveCursorForward(ushort n)
    {
        ushort max = (ushort)(_grid.Size.Cols - 1);
        _cursor.Position = _cursor.Position with { Col = Math.Min((ushort)(_cursor.Position.Col + n), max) };
        _wrapPending = false;
    }

    internal void MoveCursorBackward(ushort n)
    {
        _cursor.Position = _cursor.Position with { Col = (ushort)Math.Max(_cursor.Position.Col - n, 0) };
        _wrapPending = false;
    }

    // ── Character output ──

    internal void PutChar(char c)
    {
        // Apply character set translation (ACS line drawing)
        var activeCs = _glSet == 0 ? _g0 : _g1;
        if (activeCs == CharacterSet.LineDrawing && c >= '`' && c <= '~')
            c = LineDrawingMap.Translate(c);

        var size = _grid.Size;
        ushort cols = size.Cols;

        var width = Wcwidth.Width(c);
        // Zero-width characters (combining marks, format chars) occupy no cell.
        // Cells hold a single char, so they can't compose onto the previous
        // glyph — dropping them keeps the grid clean instead of smearing them
        // into their own garbage cells.
        if (width == 0)
            return;

        var wide = width == 2;
        // A wide glyph needs two cells; a one-column grid can never host one.
        if (wide && cols < 2)
            return;

        // A wide glyph that doesn't fit before the right edge wraps early
        // (autowrap) or is dropped, like xterm.
        if (wide && !_wrapPending && _cursor.Position.Col + 2 > cols)
        {
            if (!_modes.HasFlag(TerminalModes.AutoWrap))
                return;
            _wrapPending = true;
        }

        // Handle pending wrap
        if (_wrapPending)
        {
            _grid.RowMut(_cursor.Position.Row).Wrapped = true;
            _cursor.Position = _cursor.Position with { Col = 0 };
            _cursor.Position = _cursor.Position with { Row = (ushort)(_cursor.Position.Row + 1) };

            if (_cursor.Position.Row >= _grid.ScrollBottom)
            {
                _cursor.Position = _cursor.Position with { Row = (ushort)(_grid.ScrollBottom - 1) };
                var scrolled = _grid.ScrollUp(1);
                foreach (var row in scrolled)
                {
                    if (!_modes.HasFlag(TerminalModes.AltScreen))
                    {
                        _scrollback.Push(row);
                        if (_viewportOffset > 0)
                            _viewportOffset++;
                    }
                }
                for (ushort r = _grid.ScrollTop; r < _grid.ScrollBottom; r++)
                    _damage.MarkRow(r);
            }
            _wrapPending = false;
        }

        // Insert mode
        if (_modes.HasFlag(TerminalModes.InsertMode))
            _grid.InsertCells(_cursor.Position.Col, _cursor.Position.Row, (ushort)(wide ? 2 : 1));

        // Overwriting half of an existing wide glyph must not leave the other
        // half orphaned on screen.
        ClearWideGlyphHalves(_cursor.Position.Col, _cursor.Position.Row, wide);

        // Write the character
        _lastPrintedChar = c;
        ref var cell = ref _grid.CellAt(_cursor.Position.Col, _cursor.Position.Row);
        cell.Codepoint = c;
        cell.Fg = _penFg;
        cell.Bg = _penBg;
        cell.Attrs = wide ? _penAttrs | CellAttributes.Wide : _penAttrs;

        if (wide)
        {
            // The presentation-only second half: same pen, no codepoint of its own.
            ref var spacer = ref _grid.CellAt((ushort)(_cursor.Position.Col + 1), _cursor.Position.Row);
            spacer.Codepoint = ' ';
            spacer.Fg = _penFg;
            spacer.Bg = _penBg;
            spacer.Attrs = _penAttrs | CellAttributes.WideSpacer;
        }

        _damage.MarkRow(_cursor.Position.Row);

        // Advance cursor
        if (_cursor.Position.Col + width >= cols)
        {
            if (_modes.HasFlag(TerminalModes.AutoWrap))
                _wrapPending = true;
        }
        else
        {
            _cursor.Position = _cursor.Position with { Col = (ushort)(_cursor.Position.Col + width) };
        }
    }

    /// <summary>
    /// Clears the counterpart cells of any wide glyph the write at
    /// (<paramref name="col"/>, <paramref name="row"/>) is about to clip:
    /// the head when overwriting a spacer, the spacer when overwriting a head,
    /// and — for a wide write — whatever the second cell clips too.
    /// </summary>
    private void ClearWideGlyphHalves(ushort col, ushort row, bool wide)
    {
        ClearClippedGlyph(col, row);
        if (wide)
            ClearClippedGlyph((ushort)(col + 1), row);
    }

    private void ClearClippedGlyph(ushort col, ushort row)
    {
        ref var target = ref _grid.CellAt(col, row);
        if (target.Attrs.HasFlag(CellAttributes.WideSpacer) && col > 0)
        {
            ref var head = ref _grid.CellAt((ushort)(col - 1), row);
            if (head.Attrs.HasFlag(CellAttributes.Wide))
            {
                head.Codepoint = ' ';
                head.Attrs &= ~CellAttributes.Wide;
            }

            target.Attrs &= ~CellAttributes.WideSpacer;
        }

        if (target.Attrs.HasFlag(CellAttributes.Wide) && col + 1 < _grid.Size.Cols)
        {
            ref var spacer = ref _grid.CellAt((ushort)(col + 1), row);
            if (spacer.Attrs.HasFlag(CellAttributes.WideSpacer))
            {
                spacer.Codepoint = ' ';
                spacer.Attrs &= ~CellAttributes.WideSpacer;
            }

            target.Attrs &= ~CellAttributes.Wide;
        }
    }

    internal void Linefeed()
    {
        if (_cursor.Position.Row + 1 >= _grid.ScrollBottom)
        {
            var scrolled = _grid.ScrollUp(1);
            foreach (var row in scrolled)
            {
                if (!_modes.HasFlag(TerminalModes.AltScreen))
                {
                    _scrollback.Push(row);
                    if (_viewportOffset > 0)
                        _viewportOffset++;
                }
            }
            for (ushort r = _grid.ScrollTop; r < _grid.ScrollBottom; r++)
                _damage.MarkRow(r);
        }
        else
        {
            _cursor.Position = _cursor.Position with { Row = (ushort)(_cursor.Position.Row + 1) };
        }
        _wrapPending = false;

        if (_modes.HasFlag(TerminalModes.LinefeedNewline))
            _cursor.Position = _cursor.Position with { Col = 0 };
    }

    internal void CarriageReturn()
    {
        _cursor.Position = _cursor.Position with { Col = 0 };
        _wrapPending = false;
    }

    internal void Backspace()
    {
        if (_cursor.Position.Col > 0)
            _cursor.Position = _cursor.Position with { Col = (ushort)(_cursor.Position.Col - 1) };
        _wrapPending = false;
    }

    internal void Tab()
    {
        ushort cols = _grid.Size.Cols;
        ushort col = (ushort)(_cursor.Position.Col + 1);
        while (col < cols)
        {
            if (col < _tabStops.Length && _tabStops[col])
                break;
            col++;
        }
        _cursor.Position = _cursor.Position with { Col = Math.Min(col, (ushort)(cols - 1)) };
        _wrapPending = false;
    }

    internal void ReverseIndex()
    {
        if (_cursor.Position.Row == _grid.ScrollTop)
        {
            _grid.ScrollDown(1);
            for (ushort r = _grid.ScrollTop; r < _grid.ScrollBottom; r++)
                _damage.MarkRow(r);
        }
        else if (_cursor.Position.Row > 0)
        {
            _cursor.Position = _cursor.Position with { Row = (ushort)(_cursor.Position.Row - 1) };
        }
        _wrapPending = false;
    }

    internal void EraseInDisplay(ushort mode)
    {
        var size = _grid.Size;
        switch (mode)
        {
            case 0: // From cursor to end
                for (ushort col = _cursor.Position.Col; col < size.Cols; col++)
                    _grid.CellAt(col, _cursor.Position.Row).Reset();
                _damage.MarkRow(_cursor.Position.Row);
                for (ushort row = (ushort)(_cursor.Position.Row + 1); row < size.Rows; row++)
                {
                    _grid.ClearRow(row);
                    _damage.MarkRow(row);
                }
                break;

            case 1: // From start to cursor
                for (ushort row = 0; row < _cursor.Position.Row; row++)
                {
                    _grid.ClearRow(row);
                    _damage.MarkRow(row);
                }
                for (ushort col = 0; col <= _cursor.Position.Col; col++)
                    _grid.CellAt(col, _cursor.Position.Row).Reset();
                _damage.MarkRow(_cursor.Position.Row);
                break;

            case 2:
            case 3: // Entire display
                for (ushort row = 0; row < size.Rows; row++)
                {
                    _grid.ClearRow(row);
                    _damage.MarkRow(row);
                }
                if (mode == 3)
                    _scrollback.Clear();
                break;
        }
    }

    internal void EraseInLine(ushort mode)
    {
        ushort cols = _grid.Size.Cols;
        ushort row = _cursor.Position.Row;
        switch (mode)
        {
            case 0: // From cursor to end of line
                for (ushort col = _cursor.Position.Col; col < cols; col++)
                    _grid.CellAt(col, row).Reset();
                break;
            case 1: // From start to cursor
                for (ushort col = 0; col <= _cursor.Position.Col; col++)
                    _grid.CellAt(col, row).Reset();
                break;
            case 2: // Entire line
                _grid.ClearRow(row);
                break;
        }
        _damage.MarkRow(row);
    }

    internal void SetMode(TerminalModes mode, bool enabled)
    {
        if (enabled)
            _modes |= mode;
        else
            _modes &= ~mode;
    }

    internal void SwitchAltScreen(bool enable)
    {
        if (enable && !_modes.HasFlag(TerminalModes.AltScreen))
        {
            SaveCursor();
            _viewportOffset = 0;
            (_grid, _altGrid) = (_altGrid, _grid);
            _grid.ClearAll();
            _modes |= TerminalModes.AltScreen;
            _damage.ScreenSwapped = true;
            _damage.MarkAllRows(_grid.Size.Rows);
        }
        else if (!enable && _modes.HasFlag(TerminalModes.AltScreen))
        {
            (_grid, _altGrid) = (_altGrid, _grid);
            _modes &= ~TerminalModes.AltScreen;
            RestoreCursor();
            _damage.ScreenSwapped = true;
            _damage.MarkAllRows(_grid.Size.Rows);
        }
    }

    internal void SaveCursor()
    {
        ref var sc = ref _modes.HasFlag(TerminalModes.AltScreen) ? ref _savedCursorAlt : ref _savedCursor;
        sc.Position = _cursor.Position;
        sc.Fg = _penFg;
        sc.Bg = _penBg;
        sc.Attrs = _penAttrs;
        sc.OriginMode = _modes.HasFlag(TerminalModes.OriginMode);
        sc.AutoWrap = _modes.HasFlag(TerminalModes.AutoWrap);
    }

    internal void RestoreCursor()
    {
        var sc = _modes.HasFlag(TerminalModes.AltScreen) ? _savedCursorAlt : _savedCursor;
        _cursor.Position = sc.Position;
        _penFg = sc.Fg;
        _penBg = sc.Bg;
        _penAttrs = sc.Attrs;
        SetMode(TerminalModes.OriginMode, sc.OriginMode);
        SetMode(TerminalModes.AutoWrap, sc.AutoWrap);
        _wrapPending = false;
    }

    internal void Reset()
    {
        var size = _grid.Size;
        _grid = new Grid(size);
        _altGrid = new Grid(size);
        _cursor = CursorState.Default;
        _savedCursor = SavedCursor.Default;
        _savedCursorAlt = SavedCursor.Default;
        _modes = TerminalModesExtensions.Initial;
        _penFg = Color.DefaultColor;
        _penBg = Color.DefaultColor;
        _penAttrs = CellAttributes.None;
        _selection = null;
        _title = "";
        _workingDirectory = null;
        _wrapPending = false;
        _g0 = CharacterSet.Ascii;
        _g1 = CharacterSet.Ascii;
        _glSet = 0;
        _tabStops = new bool[size.Cols];
        for (int i = 0; i < size.Cols; i += 8)
            _tabStops[i] = true;
        _responseBuffer.Clear();
        _damage.MarkAllRows(size.Rows);
    }

    internal void SetCursorShape(CursorShape shape) => _cursor.Shape = shape;

    internal void SetCursorBlinking(bool blinking) => _cursor.Blinking = blinking;

    internal void DesignateCharset(int gSet, CharacterSet cs)
    {
        if (gSet == 0) _g0 = cs;
        else _g1 = cs;
    }

    internal void SetActiveCharset(int glSet) => _glSet = glSet;

    internal void SetCursorVisible(bool visible)
    {
        _cursor.Visible = visible;
        SetMode(TerminalModes.CursorVisible, visible);
    }

    internal void SetTitle(string title) => _title = title;

    internal void SetWorkingDirectory(string dir) => _workingDirectory = dir;

    internal void PushResponse(ReadOnlySpan<byte> data) => _responseBuffer.AddRange(data);

    // Bell event — UI layer can subscribe to trigger visual bell
    private bool _bellPending;
    public bool TakeBell() { bool b = _bellPending; _bellPending = false; return b; }
    internal void Bell() => _bellPending = true;

    // OSC 52 clipboard
    internal void SetClipboard(string text) => _pendingClipboard = text;
    public string? TakeClipboard() { var c = _pendingClipboard; _pendingClipboard = null; return c; }

    // Tab stop management
    internal void SetTabStop(ushort col)
    {
        if (col < _tabStops.Length)
            _tabStops[col] = true;
    }

    internal void ClearTabStop(ushort col)
    {
        if (col < _tabStops.Length)
            _tabStops[col] = false;
    }

    internal void ClearAllTabStops()
    {
        Array.Clear(_tabStops);
    }

    internal void SetPenFg(Color color) => _penFg = color;
    internal void SetPenBg(Color color) => _penBg = color;

    internal void SetPenAttr(CellAttributes attr, bool enabled)
    {
        if (enabled)
            _penAttrs |= attr;
        else
            _penAttrs &= ~attr;
    }

    internal void ResetPen()
    {
        _penFg = Color.DefaultColor;
        _penBg = Color.DefaultColor;
        _penAttrs = CellAttributes.None;
    }

    // ── Resize with reflow ──

    public void Resize(GridSize size)
    {
        var oldSize = _grid.Size;
        if (size == oldSize) return;

        // 1. Collect all rows (scrollback + active grid)
        var allRows = new List<Row>();
        int scrollbackCount = _scrollback.Count;
        Row? sbRow;
        while ((sbRow = _scrollback.Pop()) != null)
            allRows.Add(sbRow);
        allRows.Reverse();

        // Find last content row
        ushort lastContentRow = _cursor.Position.Row;
        for (ushort r = (ushort)(oldSize.Rows - 1); r > _cursor.Position.Row; r--)
        {
            bool isEmpty = true;
            foreach (var cell in _grid.RowSlice(r))
            {
                if (!cell.IsEmpty) { isEmpty = false; break; }
            }
            if (!isEmpty) lastContentRow = Math.Max(lastContentRow, r);
        }

        for (ushort r = 0; r <= lastContentRow; r++)
            allRows.Add(_grid.RowRef(r).Clone());

        // 2. Group into logical lines
        var logicalLines = new List<List<Cell>>();
        var currentLogicalLine = new List<Cell>();
        int rowIdx = 0;
        int oldCursorLogicalIdx = 0;
        int oldCursorLogicalCol = 0;

        foreach (var row in allRows)
        {
            bool isCursorRow = rowIdx == scrollbackCount + _cursor.Position.Row;
            if (isCursorRow)
            {
                oldCursorLogicalIdx = logicalLines.Count;
                oldCursorLogicalCol = currentLogicalLine.Count + _cursor.Position.Col;
            }

            var cells = new List<Cell>(row.Cells);
            if (!row.Wrapped)
            {
                while (cells.Count > 0 && cells[^1].IsEmpty)
                    cells.RemoveAt(cells.Count - 1);
            }
            currentLogicalLine.AddRange(cells);

            if (!row.Wrapped)
            {
                if (oldCursorLogicalIdx == logicalLines.Count)
                {
                    while (currentLogicalLine.Count <= oldCursorLogicalCol)
                        currentLogicalLine.Add(Cell.Default);
                }
                logicalLines.Add(currentLogicalLine);
                currentLogicalLine = new List<Cell>();
            }

            rowIdx++;
        }

        if (oldCursorLogicalIdx == logicalLines.Count)
        {
            while (currentLogicalLine.Count <= oldCursorLogicalCol)
                currentLogicalLine.Add(Cell.Default);
        }
        if (currentLogicalLine.Count > 0)
            logicalLines.Add(currentLogicalLine);

        // 3. Re-wrap into new rows
        var newRows = new List<Row>();
        int newCols = size.Cols;
        int newCursorRow = 0;
        int newCursorCol = 0;

        for (int logicalIdx = 0; logicalIdx < logicalLines.Count; logicalIdx++)
        {
            var line = logicalLines[logicalIdx];
            if (line.Count == 0)
            {
                if (logicalIdx == oldCursorLogicalIdx)
                {
                    int extraRows = oldCursorLogicalCol / Math.Max(newCols, 1);
                    int remainder = oldCursorLogicalCol % Math.Max(newCols, 1);
                    for (int j = 0; j < extraRows; j++)
                        newRows.Add(new Row(size.Cols));
                    newCursorRow = newRows.Count;
                    newCursorCol = remainder;
                }
                newRows.Add(new Row(size.Cols));
                continue;
            }

            int charIdx = 0;
            while (line.Count > 0)
            {
                int chunkSize = Math.Min(line.Count, newCols);
                var chunk = line.GetRange(0, chunkSize);
                line.RemoveRange(0, chunkSize);
                bool isLastChunk = line.Count == 0;

                if (logicalIdx == oldCursorLogicalIdx
                    && oldCursorLogicalCol >= charIdx
                    && oldCursorLogicalCol < charIdx + newCols)
                {
                    newCursorRow = newRows.Count;
                    newCursorCol = oldCursorLogicalCol - charIdx;
                }

                var newRow = new Row(size.Cols);
                for (int i = 0; i < chunk.Count; i++)
                    newRow.Cells[i] = chunk[i];
                newRow.Wrapped = !isLastChunk;
                newRows.Add(newRow);

                charIdx += newCols;
            }
        }

        // 4. Distribute back to scrollback and active grid
        _scrollback.Clear();
        _grid.ReplaceWith(size);
        _altGrid = new Grid(size);

        int activeRowsNeeded = size.Rows;

        if (newRows.Count <= activeRowsNeeded)
        {
            for (int i = 0; i < newRows.Count; i++)
                _grid.SetRow((ushort)i, newRows[i]);
        }
        else
        {
            int splitPoint = newRows.Count - activeRowsNeeded;
            for (int i = 0; i < splitPoint; i++)
                _scrollback.Push(newRows[i]);
            for (int i = 0; i < activeRowsNeeded; i++)
                _grid.SetRow((ushort)i, newRows[splitPoint + i]);
            newCursorRow -= splitPoint;
        }

        // Reset tab stops
        _tabStops = new bool[size.Cols];
        for (int i = 0; i < size.Cols; i += 8)
            _tabStops[i] = true;

        _cursor.Position = new GridPosition(
            Math.Min((ushort)newCursorCol, (ushort)(size.Cols - 1)),
            Math.Min((ushort)Math.Max(newCursorRow, 0), (ushort)(size.Rows - 1)));

        // Clamp viewport offset to new scrollback length
        _viewportOffset = Math.Clamp(_viewportOffset, 0, _scrollback.Count);

        _damage.Resized = true;
        _damage.MarkAllRows(size.Rows);
        _wrapPending = false;
    }
}
