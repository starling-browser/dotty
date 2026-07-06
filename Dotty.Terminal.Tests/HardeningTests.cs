using System.Text;
using Dotty.Terminal;
using Dotty.Terminal.Hosting;
using Dotty.Terminal.Rendering;
using Xunit;

namespace Dotty.Terminal.Tests;

/// <summary>Guards for the robustness fixes: OSC 52 opt-in, REP clamp,
/// zero-dimension clamp, and wide-glyph text extraction.</summary>
public class HardeningTests
{
    private static Terminal NewTerminal(string ansi, ushort cols = 12, ushort rows = 4)
    {
        var term = new Terminal(new GridSize(cols, rows));
        term.ProcessPtyOutput(Encoding.UTF8.GetBytes(ansi));
        return term;
    }

    // OSC 52 payload: "hi" base64-encoded.
    private const string Osc52SetClipboard = "\x1b]52;c;aGk=\x07";

    [Fact]
    public void Osc52_IsIgnoredByDefault()
    {
        using var session = TerminalSession.CreateWithoutPty(new GridSize(20, 4));
        string? written = null;
        session.ClipboardWriteRequested += (_, e) => written = e.Text;
        session.UpdateTerminal(t => t.ProcessPtyOutput(Encoding.UTF8.GetBytes(Osc52SetClipboard)));
        Assert.Null(written);
    }

    [Fact]
    public void RepCount_IsClampedToRemainingColumns()
    {
        // ESC [ 65535 b would repeat 'X' 65535 times unclamped; the row is 5 wide
        // and 'X' already sits in column 0, so at most 4 more cells can fill.
        var term = NewTerminal("X\x1b[65535b", cols: 5, rows: 2);
        var cells = term.RowCells(0);
        for (var col = 0; col < 5; col++)
            Assert.Equal('X', cells[col].Codepoint);
        // Nothing wrapped onto the next row.
        Assert.Equal(' ', term.RowCells(1)[0].Codepoint);
    }

    [Fact]
    public void ZeroDimensions_AreClampedToOne()
    {
        var term = new Terminal(new GridSize(0, 0));
        Assert.True(term.GridSize.Cols >= 1 && term.GridSize.Rows >= 1);
        term.ProcessPtyOutput("a"u8); // must not throw

        term.Resize(new GridSize(0, 5));
        Assert.True(term.GridSize.Cols >= 1);
    }

    [Fact]
    public void GetVisibleText_DropsWideGlyphSpacers()
    {
        var term = NewTerminal("漢字", cols: 12, rows: 2);
        Assert.Equal("漢字", TerminalText.GetVisibleText(term).TrimEnd('\n'));
    }
}
