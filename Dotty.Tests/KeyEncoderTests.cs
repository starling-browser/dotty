using Avalonia.Input;
using Dotty.Input;
using Xunit;
using TerminalModes = Dotty.Terminal.TerminalModes;

namespace Dotty.Tests;

public class KeyEncoderTests
{
    private static readonly TerminalModes NoModes = TerminalModes.None;
    private static readonly TerminalModes AppCursor = TerminalModes.CursorKeys;

    private static byte[]? Encode(Key key, KeyModifiers mods = KeyModifiers.None, string? text = null, TerminalModes modes = TerminalModes.None)
        => KeyEncoder.Encode(key, mods, text, modes);

    // ── Ctrl+letter → control codes 1–26 ──

    [Theory]
    [InlineData(Key.A, 1)]
    [InlineData(Key.B, 2)]
    [InlineData(Key.C, 3)]
    [InlineData(Key.D, 4)]
    [InlineData(Key.E, 5)]
    [InlineData(Key.F, 6)]
    [InlineData(Key.G, 7)]
    [InlineData(Key.H, 8)]
    [InlineData(Key.I, 9)]
    [InlineData(Key.J, 10)]
    [InlineData(Key.K, 11)]
    [InlineData(Key.L, 12)]
    [InlineData(Key.M, 13)]
    [InlineData(Key.N, 14)]
    [InlineData(Key.O, 15)]
    [InlineData(Key.P, 16)]
    [InlineData(Key.Q, 17)]
    [InlineData(Key.R, 18)]
    [InlineData(Key.S, 19)]
    [InlineData(Key.T, 20)]
    [InlineData(Key.U, 21)]
    [InlineData(Key.V, 22)]
    [InlineData(Key.W, 23)]
    [InlineData(Key.X, 24)]
    [InlineData(Key.Y, 25)]
    [InlineData(Key.Z, 26)]
    public void CtrlLetterProducesControlCode(Key key, byte expected)
    {
        var result = Encode(key, KeyModifiers.Control);
        Assert.Equal(new[] { expected }, result);
    }

    [Fact]
    public void CtrlSpaceProducesNul()
    {
        var result = Encode(Key.Space, KeyModifiers.Control);
        Assert.Equal(new byte[] { 0x00 }, result);
    }

    // ── Named keys ──

    [Fact]
    public void EnterProducesCr()
    {
        Assert.Equal(new byte[] { 0x0d }, Encode(Key.Enter));
    }

    [Fact]
    public void BackspaceProducesDel()
    {
        Assert.Equal(new byte[] { 0x7f }, Encode(Key.Back));
    }

    [Fact]
    public void TabProducesHt()
    {
        Assert.Equal(new byte[] { 0x09 }, Encode(Key.Tab));
    }

    [Fact]
    public void ShiftTabProducesCsiZ()
    {
        Assert.Equal("\x1b[Z"u8.ToArray(), Encode(Key.Tab, KeyModifiers.Shift));
    }

    [Fact]
    public void EscapeProducesEsc()
    {
        Assert.Equal(new byte[] { 0x1b }, Encode(Key.Escape));
    }

    // ── Arrow keys: normal mode (CSI) ──

    [Theory]
    [InlineData(Key.Up, 'A')]
    [InlineData(Key.Down, 'B')]
    [InlineData(Key.Right, 'C')]
    [InlineData(Key.Left, 'D')]
    public void ArrowKeysNormalMode(Key key, char suffix)
    {
        var expected = new byte[] { 0x1b, (byte)'[', (byte)suffix };
        Assert.Equal(expected, Encode(key, modes: NoModes));
    }

    // ── Arrow keys: application cursor mode (SS3) ──

    [Theory]
    [InlineData(Key.Up, 'A')]
    [InlineData(Key.Down, 'B')]
    [InlineData(Key.Right, 'C')]
    [InlineData(Key.Left, 'D')]
    public void ArrowKeysAppCursorMode(Key key, char suffix)
    {
        var expected = new byte[] { 0x1b, (byte)'O', (byte)suffix };
        Assert.Equal(expected, Encode(key, modes: AppCursor));
    }

    // ── Function keys ──

    [Theory]
    [InlineData(Key.F1, "\x1bOP")]
    [InlineData(Key.F2, "\x1bOQ")]
    [InlineData(Key.F3, "\x1bOR")]
    [InlineData(Key.F4, "\x1bOS")]
    [InlineData(Key.F5, "\x1b[15~")]
    [InlineData(Key.F6, "\x1b[17~")]
    [InlineData(Key.F7, "\x1b[18~")]
    [InlineData(Key.F8, "\x1b[19~")]
    [InlineData(Key.F9, "\x1b[20~")]
    [InlineData(Key.F10, "\x1b[21~")]
    [InlineData(Key.F11, "\x1b[23~")]
    [InlineData(Key.F12, "\x1b[24~")]
    public void FunctionKeys(Key key, string expectedStr)
    {
        var expected = System.Text.Encoding.UTF8.GetBytes(expectedStr);
        Assert.Equal(expected, Encode(key));
    }

    // ── Navigation keys ──

    [Fact]
    public void HomeKey() => Assert.Equal("\x1b[H"u8.ToArray(), Encode(Key.Home));

    [Fact]
    public void EndKey() => Assert.Equal("\x1b[F"u8.ToArray(), Encode(Key.End));

    [Fact]
    public void PageUpKey() => Assert.Equal("\x1b[5~"u8.ToArray(), Encode(Key.PageUp));

    [Fact]
    public void PageDownKey() => Assert.Equal("\x1b[6~"u8.ToArray(), Encode(Key.PageDown));

    [Fact]
    public void InsertKey() => Assert.Equal("\x1b[2~"u8.ToArray(), Encode(Key.Insert));

    [Fact]
    public void DeleteKey() => Assert.Equal("\x1b[3~"u8.ToArray(), Encode(Key.Delete));

    // ── Alt+key → ESC prefix ──

    [Fact]
    public void AltKeyProducesEscPrefix()
    {
        var result = Encode(Key.A, KeyModifiers.Alt, "a");
        Assert.Equal(new byte[] { 0x1b, (byte)'a' }, result);
    }

    [Fact]
    public void AltKeyWithMultiByteChar()
    {
        // Alt + Unicode char (e.g., "ñ")
        var result = Encode(Key.None, KeyModifiers.Alt, "ñ");
        var expected = new byte[] { 0x1b, 0xc3, 0xb1 }; // ESC + UTF-8 ñ
        Assert.Equal(expected, result);
    }

    // ── Printable text passthrough ──

    [Fact]
    public void PrintableTextPassthrough()
    {
        var result = Encode(Key.None, text: "hello");
        Assert.Equal(System.Text.Encoding.UTF8.GetBytes("hello"), result);
    }

    [Fact]
    public void PrintableTextSingleChar()
    {
        var result = Encode(Key.A, text: "a");
        Assert.Equal(new byte[] { (byte)'a' }, result);
    }

    // ── KeyToChar fallback: letters ──

    [Theory]
    [InlineData(Key.A, false, 'a')]
    [InlineData(Key.A, true, 'A')]
    [InlineData(Key.Z, false, 'z')]
    [InlineData(Key.Z, true, 'Z')]
    [InlineData(Key.M, false, 'm')]
    [InlineData(Key.M, true, 'M')]
    public void KeyToCharLetters(Key key, bool shift, char expected)
    {
        var mods = shift ? KeyModifiers.Shift : KeyModifiers.None;
        var result = Encode(key, mods, null);
        Assert.Equal(new[] { (byte)expected }, result);
    }

    // ── KeyToChar fallback: digits ──

    [Theory]
    [InlineData(Key.D0, false, '0')]
    [InlineData(Key.D1, false, '1')]
    [InlineData(Key.D9, false, '9')]
    public void KeyToCharDigits(Key key, bool shift, char expected)
    {
        var mods = shift ? KeyModifiers.Shift : KeyModifiers.None;
        var result = Encode(key, mods, null);
        Assert.Equal(new[] { (byte)expected }, result);
    }

    // ── KeyToChar fallback: shift+digit symbols ──

    [Theory]
    [InlineData(Key.D0, ')')]
    [InlineData(Key.D1, '!')]
    [InlineData(Key.D2, '@')]
    [InlineData(Key.D3, '#')]
    [InlineData(Key.D4, '$')]
    [InlineData(Key.D5, '%')]
    [InlineData(Key.D6, '^')]
    [InlineData(Key.D7, '&')]
    [InlineData(Key.D8, '*')]
    [InlineData(Key.D9, '(')]
    public void KeyToCharShiftDigitSymbols(Key key, char expected)
    {
        var result = Encode(key, KeyModifiers.Shift, null);
        Assert.Equal(new[] { (byte)expected }, result);
    }

    // ── KeyToChar fallback: numpad ──

    [Theory]
    [InlineData(Key.NumPad0, '0')]
    [InlineData(Key.NumPad5, '5')]
    [InlineData(Key.NumPad9, '9')]
    public void KeyToCharNumpad(Key key, char expected)
    {
        var result = Encode(key, KeyModifiers.None, null);
        Assert.Equal(new[] { (byte)expected }, result);
    }

    // ── KeyToChar fallback: punctuation / OEM keys ──

    [Theory]
    [InlineData(Key.OemMinus, false, '-')]
    [InlineData(Key.OemMinus, true, '_')]
    [InlineData(Key.OemPlus, false, '=')]
    [InlineData(Key.OemPlus, true, '+')]
    [InlineData(Key.OemOpenBrackets, false, '[')]
    [InlineData(Key.OemOpenBrackets, true, '{')]
    [InlineData(Key.OemCloseBrackets, false, ']')]
    [InlineData(Key.OemCloseBrackets, true, '}')]
    [InlineData(Key.OemPipe, false, '\\')]
    [InlineData(Key.OemPipe, true, '|')]
    [InlineData(Key.OemSemicolon, false, ';')]
    [InlineData(Key.OemSemicolon, true, ':')]
    [InlineData(Key.OemQuotes, false, '\'')]
    [InlineData(Key.OemQuotes, true, '"')]
    [InlineData(Key.OemComma, false, ',')]
    [InlineData(Key.OemComma, true, '<')]
    [InlineData(Key.OemPeriod, false, '.')]
    [InlineData(Key.OemPeriod, true, '>')]
    [InlineData(Key.OemQuestion, false, '/')]
    [InlineData(Key.OemQuestion, true, '?')]
    [InlineData(Key.OemTilde, false, '`')]
    [InlineData(Key.OemTilde, true, '~')]
    public void KeyToCharPunctuation(Key key, bool shift, char expected)
    {
        var mods = shift ? KeyModifiers.Shift : KeyModifiers.None;
        var result = Encode(key, mods, null);
        Assert.Equal(new[] { (byte)expected }, result);
    }

    // ── KeyToChar fallback: math operator keys ──

    [Theory]
    [InlineData(Key.Multiply, '*')]
    [InlineData(Key.Add, '+')]
    [InlineData(Key.Subtract, '-')]
    [InlineData(Key.Decimal, '.')]
    [InlineData(Key.Divide, '/')]
    public void KeyToCharMathKeys(Key key, char expected)
    {
        var result = Encode(key, KeyModifiers.None, null);
        Assert.Equal(new[] { (byte)expected }, result);
    }

    // ── Unknown key → null ──

    [Fact]
    public void UnknownKeyReturnsNull()
    {
        var result = Encode(Key.LeftAlt, KeyModifiers.None, null);
        Assert.Null(result);
    }

    [Fact]
    public void SpaceKeyToChar()
    {
        var result = Encode(Key.Space, KeyModifiers.None, null);
        Assert.Equal(new[] { (byte)' ' }, result);
    }
}
