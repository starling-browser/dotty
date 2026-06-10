namespace Dotty.Terminal;

/// <summary>
/// Bitset supporting up to 256 rows for tracking dirty rows.
/// </summary>
public class RowBitset
{
    private readonly long[] _bits = new long[4];

    public void Set(ushort row)
    {
        int idx = row / 64;
        int bit = row % 64;
        if (idx < 4)
            Interlocked.Or(ref _bits[idx], 1L << bit);
    }

    public bool IsSet(ushort row)
    {
        int idx = row / 64;
        int bit = row % 64;
        if (idx < 4)
            return (Interlocked.Read(ref _bits[idx]) & (1L << bit)) != 0;
        return false;
    }

    public void MarkAll(ushort numRows)
    {
        for (ushort r = 0; r < Math.Min(numRows, (ushort)256); r++)
            Set(r);
    }

    public void Clear()
    {
        for (int i = 0; i < 4; i++)
            Interlocked.Exchange(ref _bits[i], 0);
    }
}

/// <summary>
/// Damage report for the renderer.
/// </summary>
public class DamageReport
{
    public RowBitset DirtyRows { get; } = new();
    public bool Resized;
    public bool ScreenSwapped;

    public bool HasDamage(ushort numRows)
    {
        if (Resized || ScreenSwapped) return true;
        for (ushort r = 0; r < Math.Min(numRows, (ushort)256); r++)
        {
            if (DirtyRows.IsSet(r)) return true;
        }
        return false;
    }

    public void MarkRow(ushort row) => DirtyRows.Set(row);

    public void MarkAllRows(ushort numRows) => DirtyRows.MarkAll(numRows);

    public void Reset()
    {
        DirtyRows.Clear();
        Resized = false;
        ScreenSwapped = false;
    }
}
