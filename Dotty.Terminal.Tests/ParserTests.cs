using Dotty.Terminal;
using Dotty.Terminal.Parser;
using Xunit;

namespace Dotty.Terminal.Tests;

public class ParserTests
{
    [Fact]
    public void ParsePlainText()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("ABC"u8);
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(0)[1].Codepoint);
        Assert.Equal('C', term.RowCells(0)[2].Codepoint);
    }

    [Fact]
    public void ParseControlChars()
    {
        var term = new Terminal(new GridSize(80, 24));
        // BEL, BS, HT, LF, CR should be handled
        term.ProcessPtyOutput("A\u0008B"u8); // A, backspace, B overwrites A
        Assert.Equal('B', term.RowCells(0)[0].Codepoint);
    }

    [Fact]
    public void ParseCsiSequence()
    {
        var term = new Terminal(new GridSize(80, 24));
        // CSI 5;10 H (CUP)
        term.ProcessPtyOutput("\x1b[5;10H"u8);
        Assert.Equal(new GridPosition(9, 4), term.CursorPos);
    }

    [Fact]
    public void ParseCsiDefaultParams()
    {
        var term = new Terminal(new GridSize(80, 24));
        // CSI H with no params should go to 0,0
        term.ProcessPtyOutput("\x1b[5;5H"u8);
        term.ProcessPtyOutput("\x1b[H"u8);
        Assert.Equal(new GridPosition(0, 0), term.CursorPos);
    }

    [Fact]
    public void ParseEscSequence()
    {
        var term = new Terminal(new GridSize(80, 24));
        // ESC M = reverse index
        term.ProcessPtyOutput("\x1b[3;1H"u8); // Move to row 2
        term.ProcessPtyOutput("\x1bM"u8); // Reverse index
        Assert.Equal((ushort)1, term.CursorPos.Row);
    }

    [Fact]
    public void ParseOscWithBel()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b]0;Test Title\x07"u8);
        Assert.Equal("Test Title", term.Title);
    }

    [Fact]
    public void ParseMultipleSgrParams()
    {
        var term = new Terminal(new GridSize(80, 24));
        // Bold + red fg + green bg
        term.ProcessPtyOutput("\x1b[1;31;42mX"u8);
        var cell = term.RowCells(0)[0];
        Assert.True(cell.Attrs.HasFlag(CellAttributes.Bold));
        Assert.Equal(Color.Ansi(1), cell.Fg);
        Assert.Equal(Color.Ansi(2), cell.Bg);
    }

    [Fact]
    public void ParseBrightColors()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[91mX"u8);
        Assert.Equal(Color.AnsiBright(1), term.RowCells(0)[0].Fg);
        term.ProcessPtyOutput("\x1b[104mY"u8);
        Assert.Equal(Color.AnsiBright(4), term.RowCells(0)[1].Bg);
    }
}
