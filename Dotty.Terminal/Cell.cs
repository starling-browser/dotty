namespace Dotty.Terminal;

[Flags]
public enum CellAttributes : ushort
{
    None = 0,
    Bold = 0x0001,
    Dim = 0x0002,
    Italic = 0x0004,
    Underline = 0x0008,
    Blink = 0x0010,
    Inverse = 0x0020,
    Hidden = 0x0040,
    Strikethrough = 0x0080,
    Wide = 0x0100,
    WideSpacer = 0x0200,
}

public struct Cell
{
    public char Codepoint;
    public Color Fg;
    public Color Bg;
    public CellAttributes Attrs;

    public static Cell Default => new()
    {
        Codepoint = ' ',
        Fg = Color.DefaultColor,
        Bg = Color.DefaultColor,
        Attrs = CellAttributes.None,
    };

    public void Reset()
    {
        Codepoint = ' ';
        Fg = Color.DefaultColor;
        Bg = Color.DefaultColor;
        Attrs = CellAttributes.None;
    }

    public bool IsEmpty =>
        Codepoint == ' '
        && Fg.IsDefault
        && Bg.IsDefault
        && Attrs == CellAttributes.None;
}
