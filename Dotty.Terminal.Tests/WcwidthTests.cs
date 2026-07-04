using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class WcwidthTests
{
    [Theory]
    [InlineData('a', 1)]
    [InlineData(' ', 1)]
    [InlineData('❯', 1)]        // U+276F, narrow punctuation
    [InlineData('─', 1)]        // U+2500, box drawing is narrow
    [InlineData('­', 1)]   // soft hyphen: the classic wcwidth exception
    [InlineData('漢', 2)]       // CJK unified ideograph
    [InlineData('あ', 2)]       // Hiragana
    [InlineData('한', 2)]       // Hangul syllable
    [InlineData('Ａ', 2)]       // fullwidth Latin
    [InlineData('⌚', 2)]       // U+231A, wide BMP emoji
    [InlineData('́', 0)]   // combining acute accent
    [InlineData('​', 0)]   // zero-width space (format)
    [InlineData('‍', 0)]   // zero-width joiner
    public void Width_MatchesWcwidthContract(char c, int expected)
    {
        Assert.Equal(expected, Wcwidth.Width(c));
    }
}
