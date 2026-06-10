using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class TerminalTests
{
    [Fact]
    public void NewTerminalDefaults()
    {
        var term = new Terminal(new GridSize(80, 24));
        Assert.Equal(new GridSize(80, 24), term.GridSize);
        Assert.Equal(new GridPosition(0, 0), term.CursorPos);
        Assert.True(term.Cursor.Visible);
        Assert.True(term.Modes.HasFlag(TerminalModes.AutoWrap));
    }

    [Fact]
    public void ProcessSimpleText()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("Hello, World!"u8);
        Assert.Equal('H', term.RowCells(0)[0].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[6].Codepoint);
        Assert.Equal('W', term.RowCells(0)[7].Codepoint);
    }

    [Fact]
    public void ProcessCrLf()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("Line1\r\nLine2"u8);
        Assert.Equal('L', term.RowCells(0)[0].Codepoint);
        Assert.Equal('1', term.RowCells(0)[4].Codepoint);
        Assert.Equal('L', term.RowCells(1)[0].Codepoint);
        Assert.Equal('2', term.RowCells(1)[4].Codepoint);
    }

    [Fact]
    public void CursorMovementCsi()
    {
        var term = new Terminal(new GridSize(80, 24));
        // CUP: move to row 5, col 10 (1-based in VT)
        term.ProcessPtyOutput("\x1b[6;11H"u8);
        Assert.Equal(new GridPosition(10, 5), term.CursorPos);
    }

    [Fact]
    public void SgrColors()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[31mR"u8);
        Assert.Equal('R', term.RowCells(0)[0].Codepoint);
        Assert.Equal(Color.Ansi(1), term.RowCells(0)[0].Fg);
    }

    [Fact]
    public void AltScreen()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("Main"u8);
        term.ProcessPtyOutput("\x1b[?1049h"u8);
        Assert.True(term.Modes.HasFlag(TerminalModes.AltScreen));
        Assert.Equal(' ', term.RowCells(0)[0].Codepoint);
        term.ProcessPtyOutput("\x1b[?1049l"u8);
        Assert.False(term.Modes.HasFlag(TerminalModes.AltScreen));
        Assert.Equal('M', term.RowCells(0)[0].Codepoint);
    }

    [Fact]
    public void ScrollRegion()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("\x1b[2;4r"u8);
        Assert.Equal((ushort)1, term.GridRef.ScrollTop);
        Assert.Equal((ushort)4, term.GridRef.ScrollBottom);
    }

    [Fact]
    public void EraseInLine()
    {
        var term = new Terminal(new GridSize(10, 1));
        term.ProcessPtyOutput("ABCDEFGHIJ"u8);
        term.ProcessPtyOutput("\x1b[6G\x1b[0K"u8);
        Assert.Equal('E', term.RowCells(0)[4].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[5].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[9].Codepoint);
    }

    [Fact]
    public void Sgr256Color()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[38;5;196mX"u8);
        Assert.Equal(Color.Indexed(196), term.RowCells(0)[0].Fg);
    }

    [Fact]
    public void SgrTrueColor()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[38;2;100;200;50mX"u8);
        Assert.Equal(Color.FromRgb(100, 200, 50), term.RowCells(0)[0].Fg);
    }

    [Fact]
    public void SgrBoldItalic()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[1;3mX"u8);
        var cell = term.RowCells(0)[0];
        Assert.True(cell.Attrs.HasFlag(CellAttributes.Bold));
        Assert.True(cell.Attrs.HasFlag(CellAttributes.Italic));
    }

    [Fact]
    public void SgrReset()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[1;31mA\x1b[0mB"u8);
        var a = term.RowCells(0)[0];
        Assert.True(a.Attrs.HasFlag(CellAttributes.Bold));
        Assert.Equal(Color.Ansi(1), a.Fg);
        var b = term.RowCells(0)[1];
        Assert.False(b.Attrs.HasFlag(CellAttributes.Bold));
        Assert.Equal(Color.DefaultColor, b.Fg);
    }

    [Fact]
    public void CursorSaveRestore()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[10;20H"u8);
        term.ProcessPtyOutput("\u001b7"u8);
        term.ProcessPtyOutput("\x1b[1;1H"u8);
        Assert.Equal(new GridPosition(0, 0), term.CursorPos);
        term.ProcessPtyOutput("\u001b8"u8);
        Assert.Equal(new GridPosition(19, 9), term.CursorPos);
    }

    [Fact]
    public void ReverseIndex()
    {
        var term = new Terminal(new GridSize(10, 5));
        term.ProcessPtyOutput("\x1bM"u8);
        Assert.Equal(' ', term.RowCells(0)[0].Codepoint);
    }

    [Fact]
    public void TabStops()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\t"u8);
        Assert.Equal((ushort)8, term.CursorPos.Col);
        term.ProcessPtyOutput("\t"u8);
        Assert.Equal((ushort)16, term.CursorPos.Col);
    }

    [Fact]
    public void BackspaceTest()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("AB\u0008C"u8);
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('C', term.RowCells(0)[1].Codepoint);
    }

    [Fact]
    public void InsertDeleteChars()
    {
        var term = new Terminal(new GridSize(10, 1));
        term.ProcessPtyOutput("ABCDE"u8);
        term.ProcessPtyOutput("\x1b[1G"u8);
        term.ProcessPtyOutput("\x1b[2@"u8);
        Assert.Equal(' ', term.RowCells(0)[0].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[1].Codepoint);
        Assert.Equal('A', term.RowCells(0)[2].Codepoint);
    }

    [Fact]
    public void DeviceStatusReport()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[5;10H"u8);
        term.ProcessPtyOutput("\x1b[6n"u8);
        var response = term.TakeResponse();
        Assert.Equal("\x1b[5;10R"u8.ToArray(), response);
    }

    [Fact]
    public void OscTitle()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b]0;My Terminal\x07"u8);
        Assert.Equal("My Terminal", term.Title);
    }

    [Fact]
    public void FullReset()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[31mHello"u8);
        term.ProcessPtyOutput("\u001bc"u8);
        Assert.Equal(new GridPosition(0, 0), term.CursorPos);
        Assert.Equal(' ', term.RowCells(0)[0].Codepoint);
    }

    [Fact]
    public void Scrollback()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);
        Assert.Equal(1, term.ScrollbackLen);
        var sbRow = term.ScrollbackRow(0);
        Assert.NotNull(sbRow);
        Assert.Equal('A', sbRow![0].Codepoint);
    }

    [Fact]
    public void CursorShapeChange()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[6 q"u8);
        Assert.Equal(CursorShape.Bar, term.Cursor.Shape);
        term.ProcessPtyOutput("\x1b[2 q"u8);
        Assert.Equal(CursorShape.Block, term.Cursor.Shape);
    }

    [Fact]
    public void DamageTracking()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.AcknowledgeDamage();
        term.ProcessPtyOutput("Hello"u8);
        Assert.True(term.Damage.DirtyRows.IsSet(0));
        Assert.False(term.Damage.DirtyRows.IsSet(1));
    }

    [Fact]
    public void EraseFromCursorToEnd()
    {
        var term = new Terminal(new GridSize(10, 3));
        term.ProcessPtyOutput("AAAAAAAAAA\r\nBBBBBBBBBB\r\nCCCCCCCCCC"u8);
        term.ProcessPtyOutput("\x1b[2;6H\x1b[0J"u8);
        Assert.Equal('B', term.RowCells(1)[4].Codepoint);
        Assert.Equal(' ', term.RowCells(1)[5].Codepoint);
        Assert.Equal(' ', term.RowCells(2)[0].Codepoint);
    }

    [Fact]
    public void VpaAndCha()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[10d"u8);
        Assert.Equal((ushort)9, term.CursorPos.Row);
        term.ProcessPtyOutput("\x1b[20G"u8);
        Assert.Equal((ushort)19, term.CursorPos.Col);
    }

    [Fact]
    public void CursorMovementArrows()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[10;10H"u8);
        term.ProcessPtyOutput("\x1b[3A"u8);
        Assert.Equal((ushort)6, term.CursorPos.Row);
        term.ProcessPtyOutput("\x1b[2B"u8);
        Assert.Equal((ushort)8, term.CursorPos.Row);
        term.ProcessPtyOutput("\x1b[5C"u8);
        Assert.Equal((ushort)14, term.CursorPos.Col);
        term.ProcessPtyOutput("\x1b[3D"u8);
        Assert.Equal((ushort)11, term.CursorPos.Col);
    }

    [Fact]
    public void ResizeReflow()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("123456789012"u8);

        Assert.Equal('1', term.RowCells(0)[0].Codepoint);
        Assert.Equal('6', term.RowCells(1)[0].Codepoint);
        Assert.Equal('1', term.RowCells(2)[0].Codepoint);
        Assert.True(term.GridRef.RowRef(0).Wrapped);
        Assert.True(term.GridRef.RowRef(1).Wrapped);
        Assert.False(term.GridRef.RowRef(2).Wrapped);

        term.Resize(new GridSize(10, 5));

        Assert.Equal('1', term.RowCells(0)[0].Codepoint);
        Assert.Equal('6', term.RowCells(0)[5].Codepoint);
        Assert.Equal('1', term.RowCells(1)[0].Codepoint);
        Assert.True(term.GridRef.RowRef(0).Wrapped);
        Assert.False(term.GridRef.RowRef(1).Wrapped);

        term.Resize(new GridSize(5, 5));

        Assert.Equal('1', term.RowCells(0)[0].Codepoint);
        Assert.Equal('6', term.RowCells(1)[0].Codepoint);
        Assert.Equal('1', term.RowCells(2)[0].Codepoint);
        Assert.True(term.GridRef.RowRef(0).Wrapped);
        Assert.True(term.GridRef.RowRef(1).Wrapped);
        Assert.False(term.GridRef.RowRef(2).Wrapped);
    }

    [Fact]
    public void ResizeWithScrollbackPreservesCursorPosition()
    {
        // 5 cols x 3 rows — fill enough lines to push content into scrollback
        var term = new Terminal(new GridSize(5, 3));
        // Write 5 lines: lines A-C fill the grid, lines D-E push A-B into scrollback
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\n$ "u8);

        // Scrollback should have lines A and B
        Assert.Equal(2, term.ScrollbackLen);
        // Grid: row 0 = CCCCC, row 1 = DDDDD, row 2 = "$ "
        Assert.Equal('C', term.RowCells(0)[0].Codepoint);
        Assert.Equal('D', term.RowCells(1)[0].Codepoint);
        Assert.Equal('$', term.RowCells(2)[0].Codepoint);
        // Cursor should be after "$ " at col 2, row 2
        Assert.Equal(new GridPosition(2, 2), term.CursorPos);

        // Resize taller: 5 cols x 6 rows — scrollback should flow back into view
        term.Resize(new GridSize(5, 6));

        // All 5 lines should now be visible (scrollback pulled back in)
        Assert.Equal('A', term.RowCells(0)[0].Codepoint);
        Assert.Equal('B', term.RowCells(1)[0].Codepoint);
        Assert.Equal('C', term.RowCells(2)[0].Codepoint);
        Assert.Equal('D', term.RowCells(3)[0].Codepoint);
        Assert.Equal('$', term.RowCells(4)[0].Codepoint);
        // Cursor must follow the prompt, not jump to a scrollback row
        Assert.Equal(new GridPosition(2, 4), term.CursorPos);
    }

    [Fact]
    public void ResizeWithScrollbackShorter()
    {
        var term = new Terminal(new GridSize(5, 3));
        // Write 5 lines, pushing A-B into scrollback
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\n$ "u8);
        Assert.Equal(2, term.ScrollbackLen);
        Assert.Equal(new GridPosition(2, 2), term.CursorPos);

        // Resize shorter: 5 cols x 2 rows
        term.Resize(new GridSize(5, 2));

        // Visible grid should show the last 2 rows: DDDDD and "$ "
        Assert.Equal('D', term.RowCells(0)[0].Codepoint);
        Assert.Equal('$', term.RowCells(1)[0].Codepoint);
        // Cursor should be on the prompt row
        Assert.Equal(new GridPosition(2, 1), term.CursorPos);
        // Earlier lines should be in scrollback
        Assert.True(term.ScrollbackLen >= 3);
    }

    // ── Insert / Delete Lines ──

    [Fact]
    public void InsertLines()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE"u8);
        // Move cursor to row 1 (line B), insert 2 lines
        term.ProcessPtyOutput("\x1b[2;1H\x1b[2L"u8);
        // Row 1 and 2 should now be blank; row 3 should be old B
        Assert.Equal(' ', term.RowCells(1)[0].Codepoint);
        Assert.Equal(' ', term.RowCells(2)[0].Codepoint);
        Assert.Equal('B', term.RowCells(3)[0].Codepoint);
    }

    [Fact]
    public void DeleteLines()
    {
        var term = new Terminal(new GridSize(5, 5));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE"u8);
        // Move cursor to row 1 (line B), delete 1 line
        term.ProcessPtyOutput("\x1b[2;1H\x1b[1M"u8);
        // Row 1 should now be C (old row 2)
        Assert.Equal('C', term.RowCells(1)[0].Codepoint);
        Assert.Equal('D', term.RowCells(2)[0].Codepoint);
        Assert.Equal('E', term.RowCells(3)[0].Codepoint);
        // Last row should be blank
        Assert.Equal(' ', term.RowCells(4)[0].Codepoint);
    }

    // ── Scroll Up / Down ──

    [Fact]
    public void ScrollUp()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC"u8);
        term.ProcessPtyOutput("\x1b[1S"u8); // Scroll up 1
        // Row 0 should now be B, row 1 = C, row 2 = blank
        Assert.Equal('B', term.RowCells(0)[0].Codepoint);
        Assert.Equal('C', term.RowCells(1)[0].Codepoint);
        Assert.Equal(' ', term.RowCells(2)[0].Codepoint);
    }

    [Fact]
    public void ScrollDown()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC"u8);
        term.ProcessPtyOutput("\x1b[1T"u8); // Scroll down 1
        // Row 0 = blank, row 1 = A, row 2 = B
        Assert.Equal(' ', term.RowCells(0)[0].Codepoint);
        Assert.Equal('A', term.RowCells(1)[0].Codepoint);
        Assert.Equal('B', term.RowCells(2)[0].Codepoint);
    }

    // ── Erase Characters ──

    [Fact]
    public void EraseCharacters()
    {
        var term = new Terminal(new GridSize(10, 1));
        term.ProcessPtyOutput("ABCDEFGHIJ"u8);
        term.ProcessPtyOutput("\x1b[4G\x1b[3X"u8); // Move to col 3, erase 3 chars
        Assert.Equal('C', term.RowCells(0)[2].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[3].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[4].Codepoint);
        Assert.Equal(' ', term.RowCells(0)[5].Codepoint);
        Assert.Equal('G', term.RowCells(0)[6].Codepoint);
    }

    // ── Insert Mode Toggle ──

    [Fact]
    public void InsertModeToggle()
    {
        var term = new Terminal(new GridSize(10, 1));
        // CSI 4h enables insert mode
        term.ProcessPtyOutput("\x1b[4h"u8);
        Assert.True(term.Modes.HasFlag(TerminalModes.InsertMode));
        // CSI 4l disables insert mode
        term.ProcessPtyOutput("\x1b[4l"u8);
        Assert.False(term.Modes.HasFlag(TerminalModes.InsertMode));
    }

    // ── OSC 7: Working Directory ──

    [Fact]
    public void Osc7WorkingDirectory()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b]7;file:///home/user/projects\x07"u8);
        Assert.Equal("file:///home/user/projects", term.WorkingDirectory);
    }

    // ── SGR Attributes ──

    [Fact]
    public void SgrDim()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[2mX"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Dim));
    }

    [Fact]
    public void SgrUnderline()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[4mX"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Underline));
    }

    [Fact]
    public void SgrBlink()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[5mX"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Blink));
    }

    [Fact]
    public void SgrHidden()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[8mX"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Hidden));
    }

    [Fact]
    public void SgrStrikethrough()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[9mX"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Strikethrough));
    }

    [Fact]
    public void SgrResetDim()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[2mA\x1b[22mB"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Dim));
        Assert.False(term.RowCells(0)[1].Attrs.HasFlag(CellAttributes.Dim));
    }

    [Fact]
    public void SgrResetUnderline()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[4mA\x1b[24mB"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Underline));
        Assert.False(term.RowCells(0)[1].Attrs.HasFlag(CellAttributes.Underline));
    }

    [Fact]
    public void SgrResetStrikethrough()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("\x1b[9mA\x1b[29mB"u8);
        Assert.True(term.RowCells(0)[0].Attrs.HasFlag(CellAttributes.Strikethrough));
        Assert.False(term.RowCells(0)[1].Attrs.HasFlag(CellAttributes.Strikethrough));
    }

    // ── Bright Background Colors ──

    [Fact]
    public void BrightBackgroundColor()
    {
        var term = new Terminal(new GridSize(80, 24));
        // 100 = bright black background
        term.ProcessPtyOutput("\x1b[100mX"u8);
        Assert.Equal(Color.AnsiBright(0), term.RowCells(0)[0].Bg);
    }

    [Fact]
    public void BrightBackgroundColor107()
    {
        var term = new Terminal(new GridSize(80, 24));
        // 107 = bright white background
        term.ProcessPtyOutput("\x1b[107mX"u8);
        Assert.Equal(Color.AnsiBright(7), term.RowCells(0)[0].Bg);
    }

    // ── DECKPAM / DECKPNM ──

    [Fact]
    public void DeckpamDeckpnm()
    {
        var term = new Terminal(new GridSize(80, 24));
        // ESC = → DECKPAM (application keypad)
        term.ProcessPtyOutput("\x1b="u8);
        Assert.True(term.Modes.HasFlag(TerminalModes.AppKeypad));
        // ESC > → DECKPNM (numeric keypad)
        term.ProcessPtyOutput("\x1b>"u8);
        Assert.False(term.Modes.HasFlag(TerminalModes.AppKeypad));
    }

    // ── Damage tracking ──

    [Fact]
    public void DamageReportedAfterPtyOutput()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.AcknowledgeDamage(); // clear initial damage
        term.ProcessPtyOutput("hello"u8);
        Assert.True(term.Damage.HasDamage(24));
    }

    [Fact]
    public void AcknowledgeDamageClearsDirtyState()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.AcknowledgeDamage(); // clear initial
        term.ProcessPtyOutput("hello"u8);
        Assert.True(term.Damage.HasDamage(24));

        term.AcknowledgeDamage();
        Assert.False(term.Damage.HasDamage(24));
    }

    [Fact]
    public void ResizeFlagSetAfterResize()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.AcknowledgeDamage();
        term.Resize(new GridSize(100, 30));
        Assert.True(term.Damage.Resized);
        Assert.True(term.Damage.HasDamage(30));
    }

    // ── Viewport / Scrollback Viewing ──

    [Fact]
    public void ViewportOffsetStartsAtZero()
    {
        var term = new Terminal(new GridSize(80, 24));
        Assert.Equal(0, term.ViewportOffset);
        Assert.False(term.IsScrolledBack);
    }

    [Fact]
    public void ScrollViewportUp()
    {
        // 5 cols x 3 rows, push enough to get 3+ scrollback rows
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE\r\nFFFFF"u8);
        // Scrollback should have A, B, C
        Assert.True(term.ScrollbackLen >= 3);

        term.ScrollViewport(-3);
        Assert.Equal(3, term.ViewportOffset);
        Assert.True(term.IsScrolledBack);
    }

    [Fact]
    public void ScrollViewportDown()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE\r\nFFFFF"u8);

        term.ScrollViewport(-3);
        Assert.Equal(3, term.ViewportOffset);

        term.ScrollViewport(2);
        Assert.Equal(1, term.ViewportOffset);
    }

    [Fact]
    public void ScrollViewportClampedToScrollbackLen()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);
        int sbLen = term.ScrollbackLen;

        // Try to scroll way past available scrollback
        term.ScrollViewport(-10000);
        Assert.Equal(sbLen, term.ViewportOffset);
    }

    [Fact]
    public void ScrollViewportClampedToZero()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);

        term.ScrollViewport(-2);
        // Try scrolling past bottom
        term.ScrollViewport(100);
        Assert.Equal(0, term.ViewportOffset);
        Assert.False(term.IsScrolledBack);
    }

    [Fact]
    public void ViewportRowCellsAtBottom()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);

        // At bottom, ViewportRowCells should return same as RowCells
        for (ushort row = 0; row < 3; row++)
        {
            var expected = term.RowCells(row);
            var actual = term.ViewportRowCells(row);
            for (int col = 0; col < 5; col++)
                Assert.Equal(expected[col].Codepoint, actual[col].Codepoint);
        }
    }

    [Fact]
    public void ViewportRowCellsScrolledBack()
    {
        // 5 cols x 3 rows
        var term = new Terminal(new GridSize(5, 3));
        // Write 5 lines: A, B, C, D, E
        // Grid shows: C, D, E. Scrollback has: A, B
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE"u8);
        Assert.Equal(2, term.ScrollbackLen);

        // Scroll back by 2 — viewport should show: A, B, C
        term.ScrollViewport(-2);
        Assert.Equal(2, term.ViewportOffset);

        Assert.Equal('A', term.ViewportRowCells(0)[0].Codepoint);
        Assert.Equal('B', term.ViewportRowCells(1)[0].Codepoint);
        Assert.Equal('C', term.ViewportRowCells(2)[0].Codepoint);
    }

    [Fact]
    public void ViewportAutoAdvancesOnNewContent()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);

        // Scroll back
        term.ScrollViewport(-1);
        Assert.Equal(1, term.ViewportOffset);

        // New content arrives that pushes to scrollback
        term.ProcessPtyOutput("\r\nEEEEE"u8);

        // Viewport offset should auto-advance to keep showing same content
        Assert.Equal(2, term.ViewportOffset);
    }

    [Fact]
    public void ResetViewportSnapsToBottom()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);

        term.ScrollViewport(-2);
        Assert.True(term.IsScrolledBack);

        term.ResetViewport();
        Assert.Equal(0, term.ViewportOffset);
        Assert.False(term.IsScrolledBack);
    }

    [Fact]
    public void ViewportResetOnAltScreen()
    {
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD"u8);

        term.ScrollViewport(-2);
        Assert.True(term.IsScrolledBack);

        // Enter alt screen
        term.ProcessPtyOutput("\x1b[?1049h"u8);
        Assert.Equal(0, term.ViewportOffset);
        Assert.False(term.IsScrolledBack);
    }

    // ── Bug-confirming: viewport offset not clamped after Resize() ──
    // Resize() clears and rebuilds scrollback but never adjusts _viewportOffset.
    // When making the terminal taller, scrollback rows get absorbed into the grid,
    // but the offset still points into now-empty scrollback.

    [Fact]
    public void ResizeWithViewportOffsetClampsToNewScrollback()
    {
        // 5×3 terminal — fill 5 lines (2 go to scrollback)
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE"u8);
        Assert.Equal(2, term.ScrollbackLen);

        // Scroll back to top of scrollback
        term.ScrollViewport(-2);
        Assert.Equal(2, term.ViewportOffset);

        // Resize taller: all content fits in grid, scrollback should be empty
        term.Resize(new GridSize(5, 6));
        Assert.Equal(0, term.ScrollbackLen);

        // ViewportOffset must be clamped to new scrollback length
        Assert.True(term.ViewportOffset <= term.ScrollbackLen,
            $"ViewportOffset {term.ViewportOffset} exceeds ScrollbackLen {term.ScrollbackLen} after resize");
    }

    [Fact]
    public void ViewportRowCellsValidAfterResizeWithScrollback()
    {
        // Same setup: 5×3, push 5 lines, scroll back, resize taller
        var term = new Terminal(new GridSize(5, 3));
        term.ProcessPtyOutput("AAAAA\r\nBBBBB\r\nCCCCC\r\nDDDDD\r\nEEEEE"u8);
        Assert.Equal(2, term.ScrollbackLen);

        term.ScrollViewport(-2);
        Assert.Equal(2, term.ViewportOffset);

        term.Resize(new GridSize(5, 6));

        // ViewportRowCells(0) should return actual content, not blank rows
        var firstRow = term.ViewportRowCells(0);
        Assert.NotEqual(' ', firstRow[0].Codepoint);
    }

    // ── In-place update patterns ──

    [Fact]
    public void CarriageReturnOverwritesSameLine()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("Timer: 0.1s"u8);
        Assert.Equal((ushort)0, term.CursorPos.Row);
        Assert.Equal('T', term.RowCells(0)[0].Codepoint);
        Assert.Equal('1', term.RowCells(0)[9].Codepoint);

        term.ProcessPtyOutput("\rTimer: 0.2s"u8);
        Assert.Equal((ushort)0, term.CursorPos.Row);
        Assert.Equal('2', term.RowCells(0)[9].Codepoint);

        term.ProcessPtyOutput("\rTimer: 0.3s"u8);
        Assert.Equal((ushort)0, term.CursorPos.Row);
        Assert.Equal('3', term.RowCells(0)[9].Codepoint);
    }

    [Fact]
    public void CursorUpAndOverwritePreviousLine()
    {
        var term = new Terminal(new GridSize(80, 24));
        // Write first line and move to next row
        term.ProcessPtyOutput("Building... 0.1s\n"u8);
        Assert.Equal((ushort)1, term.CursorPos.Row);

        // Cursor up, carriage return, overwrite
        term.ProcessPtyOutput("\x1b[A\rBuilding... 0.2s"u8);
        Assert.Equal((ushort)0, term.CursorPos.Row);
        // Row 0 should show "0.2s", not "0.1s"
        Assert.Equal('2', term.RowCells(0)[14].Codepoint);
    }

    [Fact]
    public void EraseLineAndRewrite()
    {
        var term = new Terminal(new GridSize(80, 24));
        term.ProcessPtyOutput("Old content"u8);
        Assert.Equal('O', term.RowCells(0)[0].Codepoint);

        // CR + erase entire line + new content
        term.ProcessPtyOutput("\r\x1b[2KNew content"u8);
        Assert.Equal((ushort)0, term.CursorPos.Row);
        Assert.Equal('N', term.RowCells(0)[0].Codepoint);
        Assert.Equal('e', term.RowCells(0)[1].Codepoint);
        Assert.Equal('w', term.RowCells(0)[2].Codepoint);
        // "Old content" is 11 chars, "New content" is 11 chars — no leftover
        Assert.Equal('t', term.RowCells(0)[10].Codepoint);
        // Beyond the new content should be blank
        Assert.Equal(' ', term.RowCells(0)[11].Codepoint);
    }

    // ── CSI E / CSI F — Cursor Next/Previous Line ──

    [Fact]
    public void CsiF_CursorPreviousLine()
    {
        var term = new Terminal(new GridSize(80, 24));
        // Position cursor at row 5, col 10
        term.ProcessPtyOutput("\x1b[6;11H"u8);
        Assert.Equal(new GridPosition(10, 5), term.CursorPos);

        // CSI 3F — move up 3 lines, column resets to 0
        term.ProcessPtyOutput("\x1b[3F"u8);
        Assert.Equal(new GridPosition(0, 2), term.CursorPos);
    }

    [Fact]
    public void CsiE_CursorNextLine()
    {
        var term = new Terminal(new GridSize(80, 24));
        // Position cursor at row 2, col 15
        term.ProcessPtyOutput("\x1b[3;16H"u8);
        Assert.Equal(new GridPosition(15, 2), term.CursorPos);

        // CSI 2E — move down 2 lines, column resets to 0
        term.ProcessPtyOutput("\x1b[2E"u8);
        Assert.Equal(new GridPosition(0, 4), term.CursorPos);
    }

    [Fact]
    public void CsiF_MsBuildOverwritePattern()
    {
        var term = new Terminal(new GridSize(40, 10));
        // Simulate MSBuild terminal logger: write 3 status lines
        term.ProcessPtyOutput("Project1         (0.1s)\r\n"u8);
        term.ProcessPtyOutput("Project2         (0.2s)\r\n"u8);
        term.ProcessPtyOutput("Project3         (0.3s)"u8);
        Assert.Equal((ushort)2, term.CursorPos.Row);

        // CSI 2F — go back up 2 lines with column reset to 0
        term.ProcessPtyOutput("\x1b[2F"u8);
        Assert.Equal(new GridPosition(0, 0), term.CursorPos);

        // Overwrite all 3 lines with updated content
        term.ProcessPtyOutput("Project1         (1.0s)\r\n"u8);
        term.ProcessPtyOutput("Project2         (1.1s)\r\n"u8);
        term.ProcessPtyOutput("Project3         (1.2s)"u8);

        // Verify overwritten content
        Assert.Equal('1', term.RowCells(0)[18].Codepoint);
        Assert.Equal('0', term.RowCells(0)[20].Codepoint);
        Assert.Equal('1', term.RowCells(1)[18].Codepoint);
        Assert.Equal('1', term.RowCells(1)[20].Codepoint);
        Assert.Equal('1', term.RowCells(2)[18].Codepoint);
        Assert.Equal('2', term.RowCells(2)[20].Codepoint);
    }

    [Fact]
    public void ResizeNarrowerCursorStaysInGrid()
    {
        var term = new Terminal(new GridSize(10, 5));
        // Write text that positions cursor past column 5
        term.ProcessPtyOutput("ABCDEFGH"u8);
        Assert.Equal((ushort)8, term.CursorPos.Col);

        // Resize narrower: cursor must be clamped within new grid
        term.Resize(new GridSize(5, 5));
        Assert.True(term.CursorPos.Col < 5,
            $"Cursor col {term.CursorPos.Col} is outside new grid width 5");
    }
}
