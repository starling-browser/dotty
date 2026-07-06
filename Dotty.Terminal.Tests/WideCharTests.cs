using System.Text;
using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class WideCharTests
{
    private static Terminal NewTerminal(string text, ushort cols = 10, ushort rows = 4)
    {
        var term = new Terminal(new GridSize(cols, rows));
        term.ProcessPtyOutput(Encoding.UTF8.GetBytes(text));
        return term;
    }

    [Fact]
    public void WideChar_OccupiesHeadAndSpacerCells()
    {
        var term = NewTerminal("漢x");
        var cells = term.RowCells(0);
        Assert.Equal('漢', cells[0].Codepoint);
        Assert.True(cells[0].Attrs.HasFlag(CellAttributes.Wide));
        Assert.True(cells[1].Attrs.HasFlag(CellAttributes.WideSpacer));
        Assert.Equal('x', cells[2].Codepoint);
        Assert.Equal(new GridPosition(3, 0), term.CursorPos);
    }

    [Fact]
    public void WideChar_CarriesThePenColors()
    {
        var term = NewTerminal("\x1b[31;41m漢");
        var cells = term.RowCells(0);
        Assert.Equal(Color.Ansi(1), cells[0].Fg);
        Assert.Equal(Color.Ansi(1), cells[1].Bg); // spacer paints the same background
    }

    [Fact]
    public void WideChar_AtLastColumn_WrapsEarly()
    {
        // Column 4 of a 5-wide grid can't host a wide glyph: it wraps whole.
        var term = NewTerminal("abcd漢", cols: 5);
        Assert.Equal('d', term.RowCells(0)[3].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[4].Codepoint);
        Assert.Equal('漢', term.RowCells(1)[0].Codepoint);
        Assert.True(term.RowCells(1)[0].Attrs.HasFlag(CellAttributes.Wide));
    }

    [Fact]
    public void WideChar_FittingExactlyInLastColumns_StaysOnTheRowWithCursorAtRightEdge()
    {
        // Cursor at Cols-2 (col 4 of a 6-wide grid): the glyph fits without
        // wrapping. It occupies cols 4-5, and the cursor parks on the last
        // column it filled (Cols-1), matching a narrow glyph's deferred wrap.
        var term = NewTerminal("abcd漢", cols: 6, rows: 2);
        var row = term.RowCells(0);
        Assert.Equal('漢', row[4].Codepoint);
        Assert.True(row[4].Attrs.HasFlag(CellAttributes.Wide));
        Assert.True(row[5].Attrs.HasFlag(CellAttributes.WideSpacer));
        Assert.Equal(new GridPosition(5, 0), term.CursorPos);
        Assert.Equal(' ', term.RowCells(1)[0].Codepoint); // nothing wrapped down yet

        // The pending wrap fires on the next print.
        term.ProcessPtyOutput("x"u8);
        Assert.Equal('x', term.RowCells(1)[0].Codepoint);
    }

    [Fact]
    public void OverwritingTheHead_ClearsTheSpacer()
    {
        var term = NewTerminal("漢\r"); // print, then return to column 0
        term.ProcessPtyOutput("x"u8);
        var cells = term.RowCells(0);
        Assert.Equal('x', cells[0].Codepoint);
        Assert.False(cells[0].Attrs.HasFlag(CellAttributes.Wide));
        Assert.Equal(' ', cells[1].Codepoint);
        Assert.False(cells[1].Attrs.HasFlag(CellAttributes.WideSpacer));
    }

    [Fact]
    public void OverwritingTheSpacer_ClearsTheHead()
    {
        var term = NewTerminal("漢\x1b[1;2H"); // print, cursor onto the spacer cell
        term.ProcessPtyOutput("x"u8);
        var cells = term.RowCells(0);
        Assert.Equal(' ', cells[0].Codepoint);
        Assert.False(cells[0].Attrs.HasFlag(CellAttributes.Wide));
        Assert.Equal('x', cells[1].Codepoint);
        Assert.False(cells[1].Attrs.HasFlag(CellAttributes.WideSpacer));
    }

    [Fact]
    public void CombiningMarks_DoNotProduceCells()
    {
        // "e" followed by a combining acute: the mark can't compose into a
        // single-char cell, so it is dropped rather than smeared into its own.
        var term = NewTerminal("éx");
        var cells = term.RowCells(0);
        Assert.Equal('e', cells[0].Codepoint);
        Assert.Equal('x', cells[1].Codepoint);
        Assert.Equal(new GridPosition(2, 0), term.CursorPos);
    }

    [Fact]
    public void SelectionText_DoesNotTurnSpacersIntoSpaces()
    {
        var term = NewTerminal("漢字x");
        term.StartSelection(new GridPosition(0, 0), SelectionMode.Normal);
        term.UpdateSelection(new GridPosition(4, 0));
        Assert.Equal("漢字x", term.SelectionText());
    }
}
