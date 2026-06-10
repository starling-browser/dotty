namespace Dotty.Terminal;

public enum SelectionMode
{
    Normal,
    Block,
    Line,
    Word,
}

public struct SelectionRange
{
    public GridPosition Start;
    public GridPosition End;
    public SelectionMode Mode;

    public SelectionRange(GridPosition start, GridPosition end, SelectionMode mode)
    {
        Start = start;
        End = end;
        Mode = mode;
    }

    public (GridPosition Start, GridPosition End) Normalized()
    {
        if (Start.Row < End.Row || (Start.Row == End.Row && Start.Col <= End.Col))
            return (Start, End);
        return (End, Start);
    }

    public bool Contains(GridPosition pos)
    {
        var (start, end) = Normalized();
        return Mode switch
        {
            SelectionMode.Normal => ContainsNormal(pos, start, end),
            SelectionMode.Block => ContainsBlock(pos, start, end),
            SelectionMode.Line => pos.Row >= start.Row && pos.Row <= end.Row,
            _ => false,
        };
    }

    private static bool ContainsNormal(GridPosition pos, GridPosition start, GridPosition end)
    {
        if (pos.Row < start.Row || pos.Row > end.Row) return false;
        if (start.Row == end.Row) return pos.Col >= start.Col && pos.Col <= end.Col;
        if (pos.Row == start.Row) return pos.Col >= start.Col;
        if (pos.Row == end.Row) return pos.Col <= end.Col;
        return true;
    }

    private static bool ContainsBlock(GridPosition pos, GridPosition start, GridPosition end)
    {
        ushort left = Math.Min(start.Col, end.Col);
        ushort right = Math.Max(start.Col, end.Col);
        return pos.Row >= start.Row && pos.Row <= end.Row && pos.Col >= left && pos.Col <= right;
    }
}
