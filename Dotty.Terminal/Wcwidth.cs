using System.Globalization;

namespace Dotty.Terminal;

/// <summary>
/// Character cell widths (the classic wcwidth contract, for the BMP): 0 for
/// combining and zero-width characters, 2 for East Asian Wide/Fullwidth (and
/// the wide BMP emoji), 1 for everything else. Cells hold a single
/// <see cref="char"/>, so astral-plane codepoints (surrogate pairs) are out of
/// scope here — each surrogate half flows through as width 1.
/// </summary>
public static class Wcwidth
{
    public static int Width(char c)
    {
        if (IsZeroWidth(c))
            return 0;
        return IsWide(c) ? 2 : 1;
    }

    /// <summary>Combining marks and format characters occupy no cell of their
    /// own. The soft hyphen (U+00AD) is the classic wcwidth exception: width 1.</summary>
    public static bool IsZeroWidth(char c)
    {
        if (c == '­')
            return false;

        return CharUnicodeInfo.GetUnicodeCategory(c) is
            UnicodeCategory.NonSpacingMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format;
    }

    public static bool IsWide(char c)
    {
        if (c < 0x1100)
            return false;

        // Binary search over inclusive (start, end) ranges.
        int lo = 0, hi = WideRanges.Length - 1;
        while (lo <= hi)
        {
            var mid = (lo + hi) / 2;
            var (start, end) = WideRanges[mid];
            if (c < start)
                hi = mid - 1;
            else if (c > end)
                lo = mid + 1;
            else
                return true;
        }

        return false;
    }

    // East Asian Wide + Fullwidth BMP ranges (Unicode EastAsianWidth.txt),
    // plus the BMP emoji that render wide, in the tradition of musl's wcwidth.
    private static readonly (ushort Start, ushort End)[] WideRanges =
    [
        (0x1100, 0x115F), // Hangul Jamo
        (0x231A, 0x231B), (0x2329, 0x232A), (0x23E9, 0x23EC), (0x23F0, 0x23F0),
        (0x23F3, 0x23F3), (0x25FD, 0x25FE), (0x2614, 0x2615), (0x2648, 0x2653),
        (0x267F, 0x267F), (0x2693, 0x2693), (0x26A1, 0x26A1), (0x26AA, 0x26AB),
        (0x26BD, 0x26BE), (0x26C4, 0x26C5), (0x26CE, 0x26CE), (0x26D4, 0x26D4),
        (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5), (0x26FA, 0x26FA),
        (0x26FD, 0x26FD), (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728),
        (0x274C, 0x274C), (0x274E, 0x274E), (0x2753, 0x2755), (0x2757, 0x2757),
        (0x2795, 0x2797), (0x27B0, 0x27B0), (0x27BF, 0x27BF), (0x2B1B, 0x2B1C),
        (0x2B50, 0x2B50), (0x2B55, 0x2B55),
        (0x2E80, 0x303E), // CJK Radicals … CJK Symbols and Punctuation
        (0x3041, 0x33FF), // Hiragana … CJK Compatibility
        (0x3400, 0x4DBF), // CJK Extension A
        (0x4E00, 0x9FFF), // CJK Unified Ideographs
        (0xA000, 0xA4CF), // Yi
        (0xA960, 0xA97F), // Hangul Jamo Extended-A
        (0xAC00, 0xD7A3), // Hangul Syllables
        (0xF900, 0xFAFF), // CJK Compatibility Ideographs
        (0xFE10, 0xFE19), // Vertical forms
        (0xFE30, 0xFE52), (0xFE54, 0xFE66), (0xFE68, 0xFE6B), // CJK Compatibility Forms
        (0xFF00, 0xFF60), // Fullwidth Forms
        (0xFFE0, 0xFFE6),
    ];
}
