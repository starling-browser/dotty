using Avalonia.Media;
using Palette = Dotty.Terminal.Palette;

namespace Dotty.Theme;

public class TerminalTheme
{
    public Palette Palette { get; }

    private readonly ISolidColorBrush[] _brushCache = new ISolidColorBrush[Palette.ColorCount];

    public TerminalTheme(Palette palette)
    {
        Palette = palette;
        for (int i = 0; i < Palette.ColorCount; i++)
        {
            var (r, g, b) = palette.GetColor(i);
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
        => (uint)index < Palette.ColorCount ? _brushCache[index] : ForegroundBrush;

    /// <summary>Returns an Avalonia Color from a palette RGB tuple with specified alpha byte.</summary>
    public static Color WithAlpha((byte R, byte G, byte B) c, byte alpha)
        => Color.FromArgb(alpha, c.R, c.G, c.B);

    /// <summary>Linearly interpolates between two palette colors.</summary>
    public static Color Mix((byte R, byte G, byte B) a, (byte R, byte G, byte B) b, double factor)
    {
        var mixed = Palette.Mix(a, b, factor);
        return Color.FromRgb(mixed.R, mixed.G, mixed.B);
    }
}
