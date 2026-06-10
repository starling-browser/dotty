using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class TextPositionTests
{
    // ── Basic text positioning ──

    [Fact]
    public void TextWrittenAtOrigin()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("ABC"u8);

        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(0)[1].Codepoint);
        Assert.Equal('C', term.RowCells(0)[2].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[3].Codepoint);
    }

    [Fact]
    public void CursorAdvancesWithEachCharacter()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("Hello"u8);
        Assert.Equal(new GridPosition(5, 0), term.CursorPos);
    }

    [Fact]
    public void TextAtExplicitCursorPosition()
    {
        var term = new Terminal(new GridSize(80, 24));
        // CUP: move to row 3, col 10 (VT uses 1-based)
        term.ProcessPtyOutput("\x1b[4;11HX"u8);

        Assert.Equal('X', term.RowCells(3)[10].Codepoint);
        // Surrounding cells should remain empty
        Assert.Equal(' ', term.RowCells(3)[9].Codepoint);
        Assert.Equal(' ', term.RowCells(3)[11].Codepoint);
    }

    [Fact]
    public void MultipleLinePositioning()
    {
        var term = new Terminal(new GridSize(20, 10));
        term.ProcessPtyOutput("Row0"u8);
        term.ProcessPtyOutput("\x1b[2;1HRow1"u8); // Row 1 (0-based)
        term.ProcessPtyOutput("\x1b[5;1HRow4"u8); // Row 4 (0-based)

        Assert.Equal('R', term.RowCells(0)[0].Codepoint);
        Assert.Equal('R', term.RowCells(1)[0].Codepoint);
        Assert.Equal(' ', term.RowCells(2)[0].Codepoint);
        Assert.Equal(' ', term.RowCells(3)[0].Codepoint);
        Assert.Equal('R', term.RowCells(4)[0].Codepoint);
    }

    [Fact]
    public void CrLfMovesToStartOfNextRow()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("AB\r\nCD\r\nEF"u8);

        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(0)[1].Codepoint);
        Assert.Equal('C', term.RowCells(1)[0].Codepoint);
        Assert.Equal('D', term.RowCells(1)[1].Codepoint);
        Assert.Equal('E', term.RowCells(2)[0].Codepoint);
        Assert.Equal('F', term.RowCells(2)[1].Codepoint);
        Assert.Equal(new GridPosition(2, 2), term.CursorPos);
    }

    // ── Line wrapping ──

    [Fact]
    public void TextWrapsAtEndOfRow()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("ABCDEFGH"u8);

        // First row: ABCDE
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('E', term.RowCells(0)[4].Codepoint);
        // Second row: FGH
        Assert.Equal('F', term.RowCells(1)[0].Codepoint);
        Assert.Equal('H', term.RowCells(1)[2].Codepoint);
        Assert.True(term.GridRef.RowRef(0).Wrapped);
        Assert.False(term.GridRef.RowRef(1).Wrapped);
    }

    [Fact]
    public void CursorPositionAfterWrap()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("ABCDEFG"u8);
        // After wrapping: cursor is on row 1, col 2 (after 'G')
        Assert.Equal(new GridPosition(2, 1), term.CursorPos);
    }

    [Fact]
    public void WrapCausesScrollWhenGridFull()
    {
        var term = new Terminal(new GridSize(5, 2));
        // Fill 2 rows (10 chars) then write 3 more to trigger scroll
        term.ProcessPtyOutput("AAAAABBBBBCCC"u8);

        // Row 0 scrolled to scrollback, row 0 is now BBBBB, row 1 has CCC
        Assert.Equal('B', term.RowCells(0)[0].Codepoint);
        Assert.Equal('C', term.RowCells(1)[0].Codepoint);
        Assert.Equal(1, term.ScrollbackLen);
    }

    // ── Resize wider: text reflows into fewer rows ──

    [Fact]
    public void ResizeWiderUnwrapsText()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("ABCDEFGHIJ"u8);

        // Before: row 0 = ABCDE (wrapped), row 1 = FGHIJ
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('F', term.RowCells(1)[0].Codepoint);

        term.Resize(new GridSize(10, 5));

        // After: single row ABCDEFGHIJ
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('F', term.RowCells(0)[5].Codepoint);
        Assert.Equal('J', term.RowCells(0)[9].Codepoint);
        // Row 1 should be empty
        Assert.Equal(' ', term.RowCells(1)[0].Codepoint);
    }

    [Fact]
    public void CursorFollowsTextOnResizeWider()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("ABCDEFGH"u8);
        // Cursor at col 3, row 1 (after 'H' on the wrapped line)
        Assert.Equal(new GridPosition(3, 1), term.CursorPos);

        term.Resize(new GridSize(10, 5));
        // Text is now on one row: ABCDEFGH, cursor at col 8, row 0
        Assert.Equal(new GridPosition(8, 0), term.CursorPos);
    }

    // ── Resize narrower: text reflows into more rows ──

    [Fact]
    public void ResizeNarrowerWrapsText()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("ABCDEFGHIJ"u8);

        // Before: single row
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('J', term.RowCells(0)[9].Codepoint);

        term.Resize(new GridSize(5, 5));

        // After: row 0 = ABCDE (wrapped), row 1 = FGHIJ
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('E', term.RowCells(0)[4].Codepoint);
        Assert.Equal('F', term.RowCells(1)[0].Codepoint);
        Assert.Equal('J', term.RowCells(1)[4].Codepoint);
        Assert.True(term.GridRef.RowRef(0).Wrapped);
    }

    [Fact]
    public void CursorFollowsTextOnResizeNarrower()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("ABCDEFGH"u8);
        Assert.Equal(new GridPosition(8, 0), term.CursorPos);

        term.Resize(new GridSize(5, 5));
        // ABCDE on row 0 (wrapped), FGH on row 1 — cursor after 'H' at col 3, row 1
        Assert.Equal(new GridPosition(3, 1), term.CursorPos);
    }

    // ── Resize taller: scrollback pulled back into view ──

    [Fact]
    public void ResizeTallerRestoresScrollbackContent()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);

        // AAAAA in scrollback, grid: BBBBB, CCCCC, DDDDD
        Assert.Equal(1, term.ScrollbackLen);
        Assert.Equal('B', term.RowCells(0)[0].Codepoint);

        term.Resize(new GridSize(5, 5));

        // All lines now visible
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(1)[0].Codepoint);
        Assert.Equal('C', term.RowCells(2)[0].Codepoint);
        Assert.Equal('D', term.RowCells(3)[0].Codepoint);
        Assert.Equal(0, term.ScrollbackLen);
    }

    [Fact]
    public void CursorPositionCorrectAfterResizeTaller()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\n$ "u8);

        Assert.Equal(new GridPosition(2, 2), term.CursorPos);

        term.Resize(new GridSize(5, 6));

        // Scrollback pulled in, cursor tracks the prompt line
        Assert.Equal('$', term.RowCells(term.CursorPos.Row)[0].Codepoint);
        Assert.Equal(new GridPosition(2, term.CursorPos.Row), term.CursorPos);
    }

    // ── Resize shorter: content pushed to scrollback ──

    [Fact]
    public void ResizeShorterPushesContentToScrollback()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\n$ "u8);

        Assert.Equal(new GridPosition(2, 4), term.CursorPos);

        term.Resize(new GridSize(5, 2));

        // Only last 2 rows visible: DDDDD and $ prompt
        Assert.Equal('D', term.RowCells(0)[0].Codepoint);
        Assert.Equal('$', term.RowCells(1)[0].Codepoint);
        Assert.Equal(new GridPosition(2, 1), term.CursorPos);
        Assert.True(term.ScrollbackLen >= 3);
    }

    [Fact]
    public void CursorClampedWhenResizeShorterThanCursorRow()
    {
        var term = new Terminal(new GridSize(10, 10));
        // Place cursor on row 8
        term.ProcessPtyOutput("\x1b[9;1HHello"u8);
        Assert.Equal((ushort)8, term.CursorPos.Row);

        term.Resize(new GridSize(10, 3));

        // Cursor row must be within new grid bounds
        Assert.True(term.CursorPos.Row < 3);
    }

    // ── Combined width + height resize ──

    [Fact]
    public void ResizeWidthAndHeightSimultaneously()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("ABCDEFGHIJ\r\nKLMNO"u8);

        // Row 0 = ABCDE (wrapped), Row 1 = FGHIJ, Row 2 = KLMNO
        term.Resize(new GridSize(10, 5));

        // ABCDEFGHIJ should be one row, KLMNO on next
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('J', term.RowCells(0)[9].Codepoint);
        Assert.Equal('K', term.RowCells(1)[0].Codepoint);
        Assert.Equal('O', term.RowCells(1)[4].Codepoint);
    }

    // ── Round-trip resize preserves content ──

    [Fact]
    public void ResizeRoundTripPreservesText()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("HelloWorld\r\nLine Two!"u8);

        term.Resize(new GridSize(5, 5));
        term.Resize(new GridSize(10, 5));

        Assert.Equal('H', term.RowCells(0)[0].Codepoint);
        Assert.Equal('W', term.RowCells(0)[5].Codepoint);
        Assert.Equal('L', term.RowCells(1)[0].Codepoint);
    }

    [Fact]
    public void MultipleResizeCyclesPreserveContent()
    {
        var term = new Terminal(new GridSize(8, 4));
        term.ProcessPtyOutput("ABCDEFGH\r\n12345678"u8);

        // Narrow -> wide -> narrow -> wide
        term.Resize(new GridSize(4, 4));
        term.Resize(new GridSize(12, 4));
        term.Resize(new GridSize(4, 6));
        term.Resize(new GridSize(8, 4));

        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('H', term.RowCells(0)[7].Codepoint);
        Assert.Equal('1', term.RowCells(1)[0].Codepoint);
        Assert.Equal('8', term.RowCells(1)[7].Codepoint);
    }

    // ── Text attributes survive resize ──

    [Fact]
    public void TextAttributesPreservedAcrossResize()
    {
        var term = new Terminal(new GridSize(10, 5));
        // Bold red 'X'
        term.ProcessPtyOutput("\x1b[1;31mX"u8);

        var cellBefore = term.RowCells(0)[0];
        Assert.True(cellBefore.Attrs.HasFlag(CellAttributes.Bold));
        Assert.Equal(Color.Ansi(1), cellBefore.Fg);

        term.Resize(new GridSize(20, 5));

        var cellAfter = term.RowCells(0)[0];
        Assert.Equal('X', cellAfter.Codepoint);
        Assert.True(cellAfter.Attrs.HasFlag(CellAttributes.Bold));
        Assert.Equal(Color.Ansi(1), cellAfter.Fg);
    }

    // ── Damage tracking on resize ──

    [Fact]
    public void ResizeMarksAllRowsDirty()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("Hello"u8);
        term.AcknowledgeDamage();

        term.Resize(new GridSize(20, 5));

        Assert.True(term.Damage.Resized);
        for (ushort r = 0; r < 5; r++)
            Assert.True(term.Damage.DirtyRows.IsSet(r));
    }

    [Fact]
    public void SameGridSizeResizeIsNoOp()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("Hello"u8);
        term.AcknowledgeDamage();

        term.Resize(new GridSize(10, 5));

        // No damage should be reported for same-size resize
        Assert.False(term.Damage.Resized);
    }

    // ── Explicit positioning sequences after resize ──

    [Fact]
    public void CupWorksAfterResize()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.Resize(new GridSize(40, 12));

        // CUP to row 5, col 10 (1-based)
        term.ProcessPtyOutput("\x1b[6;11HX"u8);
        Assert.Equal('X', term.RowCells(5)[10].Codepoint);
        Assert.Equal(new GridPosition(11, 5), term.CursorPos);
    }

    [Fact]
    public void CursorClampedToNewGridBoundsOnResize()
    {
        var term = new Terminal(new GridSize(80, 24));
        // Move cursor to col 50, row 20
        term.ProcessPtyOutput("\x1b[21;51H"u8);
        Assert.Equal(new GridPosition(50, 20), term.CursorPos);

        term.Resize(new GridSize(30, 10));

        // Cursor col and row should be clamped to new bounds
        Assert.True(term.CursorPos.Col < 30);
        Assert.True(term.CursorPos.Row < 10);
    }

    // ── Window "move" (no size change) preserves everything ──

    [Fact]
    public void NoSizeChangePreservesAllTextPositions()
    {
        var term = new Terminal(new GridSize(20, 10));
        term.ProcessPtyOutput("Line 0"u8);
        term.ProcessPtyOutput("\x1b[3;1HLine 2"u8);
        term.ProcessPtyOutput("\x1b[5;10HMiddle"u8);

        // Capture state
        var row0Cells = (Cell[])term.RowCells(0).Clone();
        var row2Cells = (Cell[])term.RowCells(2).Clone();
        var row4Cells = (Cell[])term.RowCells(4).Clone();
        var cursorBefore = term.CursorPos;

        // "Move" the window — same size resize is a no-op
        term.Resize(new GridSize(20, 10));

        // Verify nothing changed
        for (int i = 0; i < 20; i++)
        {
            Assert.Equal(row0Cells[i].Codepoint, term.RowCells(0)[i].Codepoint);
            Assert.Equal(row2Cells[i].Codepoint, term.RowCells(2)[i].Codepoint);
            Assert.Equal(row4Cells[i].Codepoint, term.RowCells(4)[i].Codepoint);
        }
        Assert.Equal(cursorBefore, term.CursorPos);
    }

    // ── Scroll region interactions with resize ──

    [Fact]
    public void ScrollRegionResetOnResize()
    {
        var term = new Terminal(new GridSize(10, 5));
        // Set scroll region to rows 2-4 (1-based)
        term.ProcessPtyOutput("\x1b[2;4r"u8);
        Assert.Equal((ushort)1, term.GridRef.ScrollTop);
        Assert.Equal((ushort)4, term.GridRef.ScrollBottom);

        term.Resize(new GridSize(10, 8));

        // Scroll region should be reset to full grid
        Assert.Equal((ushort)0, term.GridRef.ScrollTop);
        Assert.Equal((ushort)8, term.GridRef.ScrollBottom);
    }

    // ── Tab stops rebuilt on resize ──

    [Fact]
    public void TabStopsWorkAfterResizeWider()
    {
        var term = new Terminal(new GridSize(20, 5));
        term.Resize(new GridSize(40, 5));

        term.ProcessPtyOutput("\t"u8);
        Assert.Equal((ushort)8, term.CursorPos.Col);
        term.ProcessPtyOutput("\t"u8);
        Assert.Equal((ushort)16, term.CursorPos.Col);
        term.ProcessPtyOutput("\t"u8);
        Assert.Equal((ushort)24, term.CursorPos.Col);
    }

    // ── Edge case: single-column terminal ──

    [Fact]
    public void SingleColumnTerminalStacksCharacters()
    {
        var term = new Terminal(new GridSize(1, 5));
        term.ProcessPtyOutput("ABC"u8);

        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(1)[0].Codepoint);
        Assert.Equal('C', term.RowCells(2)[0].Codepoint);
    }

    // ── Resize with alt screen ──

    [Fact]
    public void ResizeOnAltScreenUpdatesGridSize()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("MainText"u8);

        // Switch to alt screen, reset cursor, and write
        term.ProcessPtyOutput("\x1b[?1049h"u8);
        term.ProcessPtyOutput("\x1b[1;1H"u8); // CUP to origin
        term.ProcessPtyOutput("Alt"u8);
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);

        // Resize while on alt screen
        term.Resize(new GridSize(20, 5));

        // Grid size updated, alt content reflowed
        Assert.Equal(new GridSize(20, 5), term.GridSize);
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
    }

    // ── Explicit lines don't unwrap ──

    [Fact]
    public void ExplicitNewlinesNotUnwrappedOnResizeWider()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("AB\r\nCD"u8);

        term.Resize(new GridSize(10, 5));

        // AB and CD should remain on separate rows (not joined)
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(0)[1].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[2].Codepoint);
        Assert.Equal('C', term.RowCells(1)[0].Codepoint);
        Assert.Equal('D', term.RowCells(1)[1].Codepoint);
    }
}
