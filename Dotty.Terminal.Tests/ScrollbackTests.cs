using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class ScrollbackTests
{
    private static Row MakeRow(char fill, ushort cols = 5)
    {
        var row = new Row(cols);
        for (int i = 0; i < cols; i++)
            row.Cells[i].Codepoint = fill;
        return row;
    }

    [Fact]
    public void PushAndGetByIndex()
    {
        var sb = new ScrollbackBuffer(100);
        sb.Push(MakeRow('A'));
        sb.Push(MakeRow('B'));
        sb.Push(MakeRow('C'));
        sb.Push(MakeRow('D'));
        sb.Push(MakeRow('E'));

        Assert.Equal(5, sb.Count);
        // 0 = most recent
        Assert.Equal('E', sb.Get(0)!.Cells[0].Codepoint);
        Assert.Equal('D', sb.Get(1)!.Cells[0].Codepoint);
        Assert.Equal('C', sb.Get(2)!.Cells[0].Codepoint);
        Assert.Equal('B', sb.Get(3)!.Cells[0].Codepoint);
        Assert.Equal('A', sb.Get(4)!.Cells[0].Codepoint);
    }

    [Fact]
    public void RingEviction()
    {
        var sb = new ScrollbackBuffer(3);
        sb.Push(MakeRow('A'));
        sb.Push(MakeRow('B'));
        sb.Push(MakeRow('C'));
        sb.Push(MakeRow('D'));
        sb.Push(MakeRow('E'));

        Assert.Equal(3, sb.Count);
        // Oldest (A, B) evicted; most recent are E, D, C
        Assert.Equal('E', sb.Get(0)!.Cells[0].Codepoint);
        Assert.Equal('D', sb.Get(1)!.Cells[0].Codepoint);
        Assert.Equal('C', sb.Get(2)!.Cells[0].Codepoint);
    }

    [Fact]
    public void PopReturnsLastPushed()
    {
        var sb = new ScrollbackBuffer(100);
        sb.Push(MakeRow('A'));
        sb.Push(MakeRow('B'));
        sb.Push(MakeRow('C'));

        var popped = sb.Pop();
        Assert.NotNull(popped);
        Assert.Equal('C', popped!.Cells[0].Codepoint);
        Assert.Equal(2, sb.Count);

        popped = sb.Pop();
        Assert.Equal('B', popped!.Cells[0].Codepoint);
        Assert.Equal(1, sb.Count);
    }

    [Fact]
    public void GetOutOfBoundsReturnsNull()
    {
        var sb = new ScrollbackBuffer(100);
        sb.Push(MakeRow('A'));
        sb.Push(MakeRow('B'));

        Assert.Null(sb.Get(2));
        Assert.Null(sb.Get(100));
    }

    [Fact]
    public void ClearEmptiesBuffer()
    {
        var sb = new ScrollbackBuffer(100);
        sb.Push(MakeRow('A'));
        sb.Push(MakeRow('B'));
        sb.Push(MakeRow('C'));

        sb.Clear();
        Assert.Equal(0, sb.Count);
        Assert.True(sb.IsEmpty);
        Assert.Null(sb.Get(0));
    }
}
