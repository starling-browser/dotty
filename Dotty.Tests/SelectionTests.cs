using Xunit;

namespace Dotty.Tests;

public class SelectionTests
{
    private static Dotty.Terminal.Terminal TermWithText(string text, ushort cols = 80, ushort rows = 24)
    {
        var term = new Dotty.Terminal.Terminal(new Dotty.Terminal.GridSize(cols, rows));
        term.ProcessPtyOutput(System.Text.Encoding.UTF8.GetBytes(text));
        return term;
    }

    [Fact]
    public void SelectSingleWord()
    {
        var term = TermWithText("hello world");

        term.StartSelection(new Dotty.Terminal.GridPosition(0, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(4, 0));

        Assert.Equal("hello", term.SelectionText());
    }

    [Fact]
    public void SelectMiddleOfLine()
    {
        var term = TermWithText("hello world");

        term.StartSelection(new Dotty.Terminal.GridPosition(6, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(10, 0));

        Assert.Equal("world", term.SelectionText());
    }

    [Fact]
    public void SelectMultipleLines()
    {
        var term = TermWithText("line one\r\nline two\r\nline three");

        term.StartSelection(new Dotty.Terminal.GridPosition(5, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(7, 1));

        Assert.Equal("one\nline two", term.SelectionText());
    }

    [Fact]
    public void SelectionTextReturnsNullWhenNoSelection()
    {
        var term = TermWithText("hello");

        Assert.Null(term.SelectionText());
    }

    [Fact]
    public void ClearSelectionRemovesSelection()
    {
        var term = TermWithText("hello");

        term.StartSelection(new Dotty.Terminal.GridPosition(0, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(4, 0));
        Assert.NotNull(term.Selection);

        term.ClearSelection();

        Assert.Null(term.Selection);
        Assert.Null(term.SelectionText());
    }

    [Fact]
    public void SelectionCanBeReversed()
    {
        var term = TermWithText("hello world");

        // Select right-to-left
        term.StartSelection(new Dotty.Terminal.GridPosition(4, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(0, 0));

        Assert.Equal("hello", term.SelectionText());
    }

    [Fact]
    public void SelectEntireLine()
    {
        var term = TermWithText("abcdef");

        term.StartSelection(new Dotty.Terminal.GridPosition(0, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(5, 0));

        Assert.Equal("abcdef", term.SelectionText());
    }

    [Fact]
    public void SelectSingleCharacter()
    {
        var term = TermWithText("hello");

        term.StartSelection(new Dotty.Terminal.GridPosition(2, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(2, 0));

        Assert.Equal("l", term.SelectionText());
    }

    [Fact]
    public void UpdateSelectionExpandsRange()
    {
        var term = TermWithText("hello world");

        term.StartSelection(new Dotty.Terminal.GridPosition(0, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(2, 0));
        Assert.Equal("hel", term.SelectionText());

        // Expand further
        term.UpdateSelection(new Dotty.Terminal.GridPosition(4, 0));
        Assert.Equal("hello", term.SelectionText());
    }

    [Fact]
    public void SelectAcrossMultipleLinesTrimsTrailingSpaces()
    {
        var term = TermWithText("abc\r\nxyz");

        term.StartSelection(new Dotty.Terminal.GridPosition(0, 0), Dotty.Terminal.SelectionMode.Normal);
        term.UpdateSelection(new Dotty.Terminal.GridPosition(2, 1));

        var text = term.SelectionText();
        Assert.NotNull(text);
        var lines = text!.Split('\n');
        Assert.Equal(2, lines.Length);
        Assert.Equal("abc", lines[0]);
        Assert.StartsWith("xyz", lines[1]);
    }
}
