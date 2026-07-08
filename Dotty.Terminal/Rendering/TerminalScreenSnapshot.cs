namespace Dotty.Terminal.Rendering;

public sealed record TerminalScreenSnapshot(
    GridSize Size,
    IReadOnlyList<TerminalRenderCell> Cells,
    TerminalRenderCursor Cursor,
    bool IsScrolledBack,
    string? ExitMessage,
    IReadOnlyDictionary<ushort, string> Hyperlinks)
{
    public TerminalRenderCell CellAt(ushort col, ushort row) => Cells[row * Size.Cols + col];

    /// <summary>The OSC 8 URI a cell links to, or null when it has no link.</summary>
    public string? HyperlinkAt(ushort col, ushort row)
    {
        var id = CellAt(col, row).HyperlinkId;
        return id != 0 && Hyperlinks.TryGetValue(id, out var uri) ? uri : null;
    }

    public static TerminalScreenSnapshot FromTerminal(Terminal terminal)
    {
        var size = terminal.GridSize;
        var cells = new TerminalRenderCell[size.TotalCells];
        var selection = terminal.Selection;

        for (ushort row = 0; row < size.Rows; row++)
        {
            var rowCells = terminal.ViewportRowCells(row);
            for (ushort col = 0; col < size.Cols; col++)
            {
                ref readonly var cell = ref rowCells[col];
                var pos = new GridPosition(col, row);
                cells[row * size.Cols + col] = new TerminalRenderCell(
                    pos,
                    cell.Codepoint,
                    cell.Fg,
                    cell.Bg,
                    cell.Attrs,
                    selection?.Contains(pos) ?? false,
                    cell.HyperlinkId);
            }
        }

        string? exitMessage = null;
        if (terminal.IsFinished)
        {
            var code = terminal.ExitStatus ?? 0;
            exitMessage = $"[Process exited with code {code}. Press Ctrl+C to close]";
        }

        var cursor = terminal.Cursor;
        return new TerminalScreenSnapshot(
            size,
            cells,
            new TerminalRenderCursor(cursor.Position, cursor.Shape, cursor.Visible, cursor.Blinking),
            terminal.IsScrolledBack,
            exitMessage,
            SnapshotHyperlinks(terminal.Hyperlinks));
    }

    // Copy the live link table so the snapshot stays a stable value even as the
    // terminal keeps mutating. Empty tables share one instance (no per-frame
    // allocation for the common no-links case).
    private static IReadOnlyDictionary<ushort, string> SnapshotHyperlinks(
        IReadOnlyDictionary<ushort, string> live) =>
        live.Count == 0
            ? EmptyHyperlinks
            : new Dictionary<ushort, string>(live);

    private static readonly IReadOnlyDictionary<ushort, string> EmptyHyperlinks =
        new Dictionary<ushort, string>();
}

public readonly record struct TerminalRenderCell(
    GridPosition Position,
    char Codepoint,
    Color Foreground,
    Color Background,
    CellAttributes Attributes,
    bool IsSelected,
    ushort HyperlinkId);

public readonly record struct TerminalRenderCursor(
    GridPosition Position,
    CursorShape Shape,
    bool Visible,
    bool Blinking);
