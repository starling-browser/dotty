namespace Dotty.Terminal;

public record struct GridSize(ushort Cols, ushort Rows)
{
    public static GridSize Default => new(80, 24);

    public int TotalCells => Cols * Rows;

    /// <summary>A grid with at least one row and column. A zero dimension would
    /// underflow the <c>Cols - 1</c> / <c>Rows - 1</c> arithmetic that indexes the
    /// grid (0 → 65535 as ushort), so callers clamp untrusted sizes through here.</summary>
    public GridSize AtLeastOne() => new(Math.Max(Cols, (ushort)1), Math.Max(Rows, (ushort)1));
}

public record struct GridPosition(ushort Col, ushort Row)
{
    public static GridPosition Default => new(0, 0);
}

public enum CharacterSet
{
    Ascii,
    LineDrawing, // DEC Special Character and Line Drawing (G0 = ESC ( 0)
}

public static class LineDrawingMap
{
    /// <summary>
    /// Maps ASCII characters 0x60-0x7E to DEC line drawing Unicode equivalents.
    /// </summary>
    public static char Translate(char c)
    {
        return c switch
        {
            '`' => '\u25C6', // ◆ diamond
            'a' => '\u2592', // ▒ checker board
            'b' => '\u2409', // HT symbol
            'c' => '\u240C', // FF symbol
            'd' => '\u240D', // CR symbol
            'e' => '\u240A', // LF symbol
            'f' => '\u00B0', // ° degree sign
            'g' => '\u00B1', // ± plus/minus
            'h' => '\u2424', // NL symbol
            'i' => '\u240B', // VT symbol
            'j' => '\u2518', // ┘ lower right corner
            'k' => '\u2510', // ┐ upper right corner
            'l' => '\u250C', // ┌ upper left corner
            'm' => '\u2514', // └ lower left corner
            'n' => '\u253C', // ┼ crossing lines
            'o' => '\u23BA', // ⎺ scan line 1
            'p' => '\u23BB', // ⎻ scan line 3
            'q' => '\u2500', // ─ horizontal line
            'r' => '\u23BC', // ⎼ scan line 7
            's' => '\u23BD', // ⎽ scan line 9
            't' => '\u251C', // ├ left tee
            'u' => '\u2524', // ┤ right tee
            'v' => '\u2534', // ┴ bottom tee
            'w' => '\u252C', // ┬ top tee
            'x' => '\u2502', // │ vertical line
            'y' => '\u2264', // ≤ less-than-or-equal
            'z' => '\u2265', // ≥ greater-than-or-equal
            '{' => '\u03C0', // π pi
            '|' => '\u2260', // ≠ not-equal
            '}' => '\u00A3', // £ pound sign
            '~' => '\u00B7', // · middle dot
            _ => c,
        };
    }
}
