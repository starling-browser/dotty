using System.Text;
using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class SelectionTests
{
    private static Terminal NewTerminal(string text, ushort cols = 20, ushort rows = 5)
    {
        var term = new Terminal(new GridSize(cols, rows));
        term.ProcessPtyOutput(Encoding.UTF8.GetBytes(text));
        return term;
    }

    [Fact]
    public void WordSelection_ContainsItsCells()
    {
        // Word mode previously fell through SelectionRange.Contains to false,
        // so double-click selections copied but never highlighted.
        var term = NewTerminal("hello world");
        term.SelectWord(new GridPosition(8, 0));
        var selection = Assert.NotNull(term.Selection);
        Assert.True(selection.Contains(new GridPosition(6, 0)));  // w
        Assert.True(selection.Contains(new GridPosition(10, 0))); // d
        Assert.False(selection.Contains(new GridPosition(5, 0))); // space before
        Assert.False(selection.Contains(new GridPosition(11, 0)));
    }

    [Fact]
    public void WordSelection_CopiesTheWord()
    {
        var term = NewTerminal("hello world");
        term.SelectWord(new GridPosition(8, 0));
        Assert.Equal("world", term.SelectionText());
    }

    [Fact]
    public void LineSelection_CopiesTheWholeRow()
    {
        // A triple click anchors start == end at the clicked cell; Line mode
        // previously copied just that one character.
        var term = NewTerminal("hello world\r\nfoo bar");
        term.StartSelection(new GridPosition(5, 1), SelectionMode.Line);
        Assert.Equal("foo bar", term.SelectionText());
    }

    [Fact]
    public void LineSelection_SpanningRows_CopiesAllOfThem()
    {
        var term = NewTerminal("hello world\r\nfoo bar\r\nbaz");
        term.StartSelection(new GridPosition(9, 0), SelectionMode.Line);
        term.UpdateSelection(new GridPosition(1, 2));
        Assert.Equal("hello world\nfoo bar\nbaz", term.SelectionText());
    }

    [Fact]
    public void LineSelection_TrimsTrailingGridPadding()
    {
        var term = NewTerminal("hi");
        term.StartSelection(new GridPosition(0, 0), SelectionMode.Line);
        Assert.Equal("hi", term.SelectionText());
    }

    [Fact]
    public void NormalSelection_KeepsExactRange()
    {
        var term = NewTerminal("hello world");
        term.StartSelection(new GridPosition(0, 0), SelectionMode.Normal);
        term.UpdateSelection(new GridPosition(4, 0));
        Assert.Equal("hello", term.SelectionText());
    }
}
