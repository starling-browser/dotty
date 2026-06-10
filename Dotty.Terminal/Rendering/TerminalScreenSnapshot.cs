namespace Dotty.Terminal.Rendering;

public sealed record TerminalScreenSnapshot(
    GridSize Size,
    IReadOnlyList<TerminalRenderCell> Cells,
    TerminalRenderCursor Cursor,
    bool IsScrolledBack,
    string? ExitMessage)
{
    public TerminalRenderCell CellAt(ushort col, ushort row) => Cells[row * Size.Cols + col];

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
                    selection?.Contains(pos) ?? false);
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
            exitMessage);
    }
}

public readonly record struct TerminalRenderCell(
    GridPosition Position,
    char Codepoint,
    Color Foreground,
    Color Background,
    CellAttributes Attributes,
    bool IsSelected);

public readonly record struct TerminalRenderCursor(
    GridPosition Position,
    CursorShape Shape,
    bool Visible,
    bool Blinking);
