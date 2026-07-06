using Dotty.Terminal.Input;
using Xunit;

namespace Dotty.Terminal.Tests;

public class TerminalKeyMappingTests
{
    [Theory]
    [InlineData(0x41, TerminalKey.A)]
    [InlineData(0x5a, TerminalKey.Z)]
    [InlineData(0x30, TerminalKey.D0)]
    [InlineData(0x39, TerminalKey.D9)]
    [InlineData(0x60, TerminalKey.NumPad0)]
    [InlineData(0x69, TerminalKey.NumPad9)]
    [InlineData(0x0d, TerminalKey.Enter)]
    [InlineData(0x08, TerminalKey.Backspace)]
    [InlineData(0xba, TerminalKey.OemSemicolon)]
    [InlineData(0xde, TerminalKey.OemQuotes)]
    public void MapWindowsVirtualKey_MapsKnownKeys(int virtualKey, TerminalKey expected)
    {
        Assert.Equal(expected, TerminalKeyMapping.MapWindowsVirtualKey(virtualKey));
    }

    [Fact]
    public void MapWindowsVirtualKey_ReturnsNoneForUnknownKey()
    {
        Assert.Equal(TerminalKey.None, TerminalKeyMapping.MapWindowsVirtualKey(0));
    }

    [Theory]
    [InlineData(TerminalKey.A, true)]
    [InlineData(TerminalKey.D9, true)]
    [InlineData(TerminalKey.NumPad5, true)]
    [InlineData(TerminalKey.OemQuestion, true)]
    [InlineData(TerminalKey.Enter, false)]
    [InlineData(TerminalKey.Up, false)]
    [InlineData(TerminalKey.None, false)]
    public void IsPrintable_IdentifiesTextKeys(TerminalKey key, bool expected)
    {
        Assert.Equal(expected, TerminalKeyMapping.IsPrintable(key));
    }

    [Fact]
    public void MapContiguousKey_RejectsNegativeCount()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            TerminalKeyMapping.MapContiguousKey(1, 1, TerminalKey.A, -1));
    }
}
