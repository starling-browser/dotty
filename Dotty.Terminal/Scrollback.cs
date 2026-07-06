namespace Dotty.Terminal;

/// <summary>
/// Circular array ring buffer for scrollback history.
/// Provides O(1) indexed access, push, and pop.
/// </summary>
public class ScrollbackBuffer
{
    private readonly Row?[] _buffer;
    private readonly int _maxLines;
    private int _head; // index of next write slot
    private int _count;

    public ScrollbackBuffer(int maxLines = 10_000)
    {
        _maxLines = maxLines;
        _buffer = new Row?[maxLines];
        _head = 0;
        _count = 0;
    }

    public void Push(Row row)
    {
        _buffer[_head] = row;
        _head = (_head + 1) % _maxLines;
        if (_count < _maxLines)
            _count++;
    }

    public Row? Pop()
    {
        if (_count == 0) return null;
        // Most recent is at (_head - 1 + _maxLines) % _maxLines
        _head = (_head - 1 + _maxLines) % _maxLines;
        var row = _buffer[_head];
        _buffer[_head] = null;
        _count--;
        return row;
    }

    public int Count => _count;

    public bool IsEmpty => _count == 0;

    /// <summary>
    /// Get a scrollback row by index (0 = most recent). O(1).
    /// </summary>
    public Row? Get(int index)
    {
        if (index < 0 || index >= _count) return null;
        // Most recent is at (_head - 1), second most recent at (_head - 2), etc.
        // index can reach _count-1 (≤ _maxLines-1), so _head-1-index bottoms out
        // near -_maxLines; bias by 2*_maxLines to keep the operand non-negative.
        int actual = (_head - 1 - index + _maxLines * 2) % _maxLines;
        return _buffer[actual];
    }

    public void Clear()
    {
        Array.Clear(_buffer, 0, _buffer.Length);
        _head = 0;
        _count = 0;
    }

    public int MaxLines => _maxLines;
}
