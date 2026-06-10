namespace Dotty.Terminal;

public enum CursorShape
{
    Block,
    Underline,
    Bar,
}

public struct CursorState
{
    public GridPosition Position;
    public CursorShape Shape;
    public bool Visible;
    public bool Blinking;

    public static CursorState Default => new()
    {
        Position = GridPosition.Default,
        Shape = CursorShape.Block,
        Visible = true,
        Blinking = true,
    };
}

public struct SavedCursor
{
    public GridPosition Position;
    public Color Fg;
    public Color Bg;
    public CellAttributes Attrs;
    public bool OriginMode;
    public bool AutoWrap;

    public static SavedCursor Default => new()
    {
        Position = GridPosition.Default,
        Fg = Color.DefaultColor,
        Bg = Color.DefaultColor,
        Attrs = CellAttributes.None,
        OriginMode = false,
        AutoWrap = true,
    };
}
