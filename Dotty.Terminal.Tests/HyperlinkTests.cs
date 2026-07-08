using System.Text;
using Dotty.Terminal;
using Dotty.Terminal.Rendering;
using Xunit;

namespace Dotty.Terminal.Tests;

public class HyperlinkTests
{
    private static Terminal Feed(params string[] chunks)
    {
        var term = new Terminal(new GridSize(80, 24));
        foreach (var chunk in chunks)
            term.ProcessPtyOutput(Encoding.UTF8.GetBytes(chunk));
        return term;
    }

    // OSC 8 hyperlink: ESC ] 8 ; params ; URI ST  (ST = ESC \). An empty URI closes.
    private static string Open(string uri, string? id = null) =>
        $"\x1b]8;{(id is null ? "" : $"id={id}")};{uri}\x1b\\";

    private static readonly string Close = "\x1b]8;;\x1b\\";

    [Fact]
    public void LinkedCellsShareOneIdAndResolveToUri()
    {
        var term = Feed(Open("https://example.com"), "link", Close, "x");

        var row = term.RowCells(0);
        var id = row[0].HyperlinkId;

        Assert.NotEqual(0, id);
        Assert.Equal(id, row[1].HyperlinkId);
        Assert.Equal(id, row[2].HyperlinkId);
        Assert.Equal(id, row[3].HyperlinkId);
        Assert.Equal("https://example.com", term.Hyperlinks[id]);

        // Text after the close carries no link.
        Assert.Equal(0, row[4].HyperlinkId);
    }

    [Fact]
    public void PlainTextHasNoLink()
    {
        var term = Feed("plain text");

        Assert.Equal(0, term.RowCells(0)[0].HyperlinkId);
        Assert.Empty(term.Hyperlinks);
    }

    [Fact]
    public void EmptyUriClosesTheCurrentLink()
    {
        var term = Feed(Open("https://a.test"), "A", Close, "B");

        var row = term.RowCells(0);
        Assert.NotEqual(0, row[0].HyperlinkId); // A
        Assert.Equal(0, row[1].HyperlinkId);    // B
    }

    [Fact]
    public void ExplicitIdGroupsSpansAcrossRows()
    {
        // Same id= on two spans (a link split across a newline) => one link id.
        var term = Feed(
            Open("https://wrapped.test", id: "L1"), "AA", Close,
            "\r\n",
            Open("https://wrapped.test", id: "L1"), "BB", Close);

        var first = term.RowCells(0)[0].HyperlinkId;
        var second = term.RowCells(1)[0].HyperlinkId;

        Assert.NotEqual(0, first);
        Assert.Equal(first, second);
        Assert.Single(term.Hyperlinks);
    }

    [Fact]
    public void SameUriWithoutIdIsDeduplicated()
    {
        var term = Feed(
            Open("https://dup.test"), "A", Close,
            Open("https://dup.test"), "B", Close);

        Assert.Single(term.Hyperlinks);
    }

    [Fact]
    public void LongUriWithinBufferCapIsStored()
    {
        var uri = "https://example.com/" + new string('a', 2000);
        var term = Feed(Open(uri), "x", Close);

        var id = term.RowCells(0)[0].HyperlinkId;
        Assert.Equal(uri, term.Hyperlinks[id]);
    }

    [Fact]
    public void MalformedOsc8WithoutSeparatorIsIgnored()
    {
        // No second ';' — nothing to open, and it must not throw.
        var term = Feed("\x1b]8\x1b\\", "text");

        Assert.Equal(0, term.RowCells(0)[0].HyperlinkId);
        Assert.Empty(term.Hyperlinks);
    }

    [Fact]
    public void ResetClearsHyperlinkTable()
    {
        var term = Feed(Open("https://a.test"), "A", Close);
        Assert.NotEmpty(term.Hyperlinks);

        // RIS (ESC c) — full reset. Explicit bytes avoid the \x1b greedy-hex
        // hazard where 'c' would be read as a hex digit.
        term.ProcessPtyOutput(new byte[] { 0x1b, (byte)'c' });
        Assert.Empty(term.Hyperlinks);
    }

    [Fact]
    public void SnapshotExposesLinkUriPerCell()
    {
        var term = Feed(Open("https://snap.test"), "hi", Close, "!");
        var snapshot = TerminalScreenSnapshot.FromTerminal(term);

        Assert.Equal("https://snap.test", snapshot.HyperlinkAt(0, 0));
        Assert.Equal("https://snap.test", snapshot.HyperlinkAt(1, 0));
        Assert.Null(snapshot.HyperlinkAt(2, 0)); // the '!' after close
    }
}
