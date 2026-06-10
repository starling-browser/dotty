namespace Dotty.Terminal;

/// <summary>
/// A single row of characters in the grid.
/// </summary>
public class Row
{
    public Cell[] Cells;
    public bool Wrapped;

    public Row(ushort cols)
    {
        Cells = new Cell[cols];
        for (int i = 0; i < cols; i++)
            Cells[i] = Cell.Default;
        Wrapped = false;
    }

    public Row Clone()
    {
        var clone = new Row((ushort)Cells.Length);
        Array.Copy(Cells, clone.Cells, Cells.Length);
        clone.Wrapped = Wrapped;
        return clone;
    }
}

/// <summary>
/// Row-major 2D cell buffer with scroll region support.
/// </summary>
public class Grid
{
    private readonly List<Row> _rows;
    private GridSize _size;

    /// <summary>Scroll region top (inclusive).</summary>
    public ushort ScrollTop;
    /// <summary>Scroll region bottom (exclusive).</summary>
    public ushort ScrollBottom;

    public Grid(GridSize size)
    {
        _size = size;
        _rows = new List<Row>(size.Rows);
        for (int i = 0; i < size.Rows; i++)
            _rows.Add(new Row(size.Cols));
        ScrollTop = 0;
        ScrollBottom = size.Rows;
    }

    public GridSize Size => _size;

    public ref Cell CellAt(ushort col, ushort row) => ref _rows[row].Cells[col];

    public Cell[] RowSlice(ushort row) => _rows[row].Cells;

    public Row RowRef(ushort row) => _rows[row];

    public Row RowMut(ushort row) => _rows[row];

    public void ClearRow(ushort row)
    {
        var r = _rows[row];
        for (int i = 0; i < r.Cells.Length; i++)
            r.Cells[i].Reset();
        r.Wrapped = false;
    }

    public void ClearAll()
    {
        foreach (var row in _rows)
        {
            for (int i = 0; i < row.Cells.Length; i++)
                row.Cells[i].Reset();
            row.Wrapped = false;
        }
    }

    /// <summary>
    /// Scroll the scroll region up by n lines. Returns scrolled-out rows.
    /// </summary>
    public List<Row> ScrollUp(ushort n)
    {
        var scrolledOut = new List<Row>();
        int top = ScrollTop;
        int bottom = ScrollBottom;

        for (int i = 0; i < Math.Min(n, bottom - top); i++)
        {
            scrolledOut.Add(_rows[top]);
            _rows.RemoveAt(top);
            _rows.Insert(bottom - 1, new Row(_size.Cols));
        }

        return scrolledOut;
    }

    /// <summary>
    /// Scroll the scroll region down by n lines.
    /// </summary>
    public void ScrollDown(ushort n)
    {
        int top = ScrollTop;
        int bottom = ScrollBottom;

        for (int i = 0; i < Math.Min(n, bottom - top); i++)
        {
            _rows.RemoveAt(bottom - 1);
            _rows.Insert(top, new Row(_size.Cols));
        }
    }

    public void ResetScrollRegion()
    {
        ScrollTop = 0;
        ScrollBottom = _size.Rows;
    }

    public void SetScrollRegion(ushort top, ushort bottom)
    {
        top = Math.Min(top, (ushort)(_size.Rows - 1));
        bottom = Math.Min(bottom, _size.Rows);
        if (top < bottom)
        {
            ScrollTop = top;
            ScrollBottom = bottom;
        }
    }

    /// <summary>
    /// Insert n blank lines at the given row, within the scroll region.
    /// </summary>
    public void InsertLines(ushort row, ushort n)
    {
        if (row < ScrollTop || row >= ScrollBottom) return;
        ushort savedTop = ScrollTop;
        ScrollTop = row;
        ScrollDown(n);
        ScrollTop = savedTop;
    }

    /// <summary>
    /// Delete n lines at the given row, within the scroll region.
    /// </summary>
    public void DeleteLines(ushort row, ushort n)
    {
        if (row < ScrollTop || row >= ScrollBottom) return;
        ushort savedTop = ScrollTop;
        ScrollTop = row;
        ScrollUp(n);
        ScrollTop = savedTop;
    }

    /// <summary>
    /// Insert n blank cells at position, shifting cells right.
    /// </summary>
    public void InsertCells(ushort col, ushort row, ushort n)
    {
        var cells = _rows[row].Cells;
        int cols = _size.Cols;
        int insertAt = col;
        int shift = Math.Min(n, cols - col);

        // Shift cells right
        Array.Copy(cells, insertAt, cells, insertAt + shift, cols - insertAt - shift);
        // Clear inserted cells
        for (int i = 0; i < shift; i++)
            cells[insertAt + i].Reset();
    }

    /// <summary>
    /// Delete n cells at position, shifting cells left.
    /// </summary>
    public void DeleteCells(ushort col, ushort row, ushort n)
    {
        var cells = _rows[row].Cells;
        int cols = _size.Cols;
        int deleteAt = col;
        int shift = Math.Min(n, cols - col);

        // Shift cells left
        Array.Copy(cells, deleteAt + shift, cells, deleteAt, cols - deleteAt - shift);
        // Clear trailing cells
        for (int i = 0; i < shift; i++)
            cells[cols - shift + i].Reset();
    }

    /// <summary>
    /// Replace grid with new size, used during resize.
    /// </summary>
    public void ReplaceWith(GridSize newSize)
    {
        _rows.Clear();
        _size = newSize;
        for (int i = 0; i < newSize.Rows; i++)
            _rows.Add(new Row(newSize.Cols));
        ResetScrollRegion();
    }

    /// <summary>
    /// Set a specific row (used during resize reflow).
    /// </summary>
    public void SetRow(ushort index, Row row)
    {
        _rows[index] = row;
    }
}
