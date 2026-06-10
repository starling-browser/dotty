using Avalonia.Media;
using Palette = Dotty.Terminal.Palette;

namespace Dotty.Theme;

public class TerminalTheme
{
    public Palette Palette { get; }

    private readonly ISolidColorBrush[] _brushCache = new ISolidColorBrush[256];

    public TerminalTheme(Palette palette)
    {
        Palette = palette;
        for (int i = 0; i < 256; i++)
        {
            var (r, g, b) = palette.Colors[i];
            _brushCache[i] = new SolidColorBrush(Avalonia.Media.Color.FromRgb(r, g, b));
        }
    }

    public Avalonia.Media.Color ToAvaloniaColor((byte R, byte G, byte B) rgb)
        => Avalonia.Media.Color.FromRgb(rgb.R, rgb.G, rgb.B);

    public ISolidColorBrush ForegroundBrush
        => new SolidColorBrush(ToAvaloniaColor(Palette.Foreground));

    public ISolidColorBrush BackgroundBrush
        => new SolidColorBrush(ToAvaloniaColor(Palette.Background));

    public ISolidColorBrush CursorBrush
        => new SolidColorBrush(ToAvaloniaColor(Palette.Cursor));

    public ISolidColorBrush GetBrush(int index)
        => index >= 0 && index < 256 ? _brushCache[index] : ForegroundBrush;

    /// <summary>Returns an Avalonia Color from a palette RGB tuple with specified alpha byte.</summary>
    public static Color WithAlpha((byte R, byte G, byte B) c, byte alpha)
        => Color.FromArgb(alpha, c.R, c.G, c.B);

    /// <summary>Linearly interpolates between two palette colors.</summary>
    public static Color Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double factor)
    {
        byte r = (byte)(a.R + (b.R - a.R) * factor);
        byte g = (byte)(a.G + (b.G - a.G) * factor);
        byte bl = (byte)(a.B + (b.B - a.B) * factor);
        return Color.FromRgb(r, g, bl);
    }
}
