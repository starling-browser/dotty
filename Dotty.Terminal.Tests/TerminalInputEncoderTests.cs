using Dotty.Terminal.Input;
using Xunit;

namespace Dotty.Terminal.Tests;

public class TerminalInputEncoderTests
{
    [Fact]
    public void CtrlLetterProducesControlCode()
    {
        var result = TerminalInputEncoder.Encode(
            TerminalKey.C,
            TerminalKeyModifiers.Control,
            text: null,
            TerminalModes.None);

        Assert.Equal([0x03], result);
    }

    [Fact]
    public void ArrowUsesApplicationCursorMode()
    {
        var result = TerminalInputEncoder.Encode(
            TerminalKey.Up,
            TerminalKeyModifiers.None,
            text: null,
            TerminalModes.CursorKeys);

        Assert.Equal("\x1bOA"u8.ToArray(), result);
    }

    [Fact]
    public void BracketedPasteWrapsText()
    {
        var result = TerminalInputEncoder.EncodePaste("hello", bracketed: true);

        Assert.Equal("\x1b[200~hello\x1b[201~"u8.ToArray(), result);
    }
}
