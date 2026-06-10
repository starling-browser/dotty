using Dotty.Terminal;
using Xunit;

namespace Dotty.Terminal.Tests;

public class PaletteTests
{
    [Fact]
    public void CatppuccinMocha_IsDark()
    {
        var palette = Palette.CatppuccinMocha();
        Assert.True(palette.IsDark);
    }

    [Fact]
    public void CatppuccinLatte_IsLight()
    {
        var palette = Palette.CatppuccinLatte();
        Assert.False(palette.IsDark);
    }

    [Fact]
    public void Dracula_IsDark()
    {
        var palette = Palette.Dracula();
        Assert.True(palette.IsDark);
    }

    [Fact]
    public void Nord_IsDark()
    {
        var palette = Palette.Nord();
        Assert.True(palette.IsDark);
    }

    [Fact]
    public void SolarizedDark_IsDark()
    {
        var palette = Palette.SolarizedDark();
        Assert.True(palette.IsDark);
    }

    [Fact]
    public void SolarizedLight_IsLight()
    {
        var palette = Palette.SolarizedLight();
        Assert.False(palette.IsDark);
    }

    [Fact]
    public void AllPalettes_Have256Colors()
    {
        Func<Palette>[] factories =
        [
            Palette.CatppuccinMocha,
            Palette.CatppuccinLatte,
            Palette.Dracula,
            Palette.Nord,
            Palette.SolarizedDark,
            Palette.SolarizedLight,
        ];

        foreach (var factory in factories)
        {
            var palette = factory();
            Assert.Equal(256, palette.Colors.Length);
        }
    }

    [Fact]
    public void AllPalettes_HaveNonDefaultForegroundAndBackground()
    {
        Func<Palette>[] factories =
        [
            Palette.CatppuccinMocha,
            Palette.CatppuccinLatte,
            Palette.Dracula,
            Palette.Nord,
            Palette.SolarizedDark,
            Palette.SolarizedLight,
        ];

        foreach (var factory in factories)
        {
            var palette = factory();
            // Foreground and background should not both be (0,0,0)
            Assert.False(
                palette.Foreground == (0, 0, 0) && palette.Background == (0, 0, 0),
                "Both foreground and background are black");
        }
    }

    [Fact]
    public void AllPalettes_HaveDistinctForegroundAndBackground()
    {
        Func<Palette>[] factories =
        [
            Palette.CatppuccinMocha,
            Palette.CatppuccinLatte,
            Palette.Dracula,
            Palette.Nord,
            Palette.SolarizedDark,
            Palette.SolarizedLight,
        ];

        foreach (var factory in factories)
        {
            var palette = factory();
            Assert.NotEqual(palette.Foreground, palette.Background);
        }
    }

    [Fact]
    public void AllPalettes_Have16AnsiColors()
    {
        Func<Palette>[] factories =
        [
            Palette.CatppuccinMocha,
            Palette.CatppuccinLatte,
            Palette.Dracula,
            Palette.Nord,
            Palette.SolarizedDark,
            Palette.SolarizedLight,
        ];

        foreach (var factory in factories)
        {
            var palette = factory();
            // First 16 colors should be set (not all zero)
            bool anyNonZero = false;
            for (int i = 0; i < 16; i++)
            {
                var (r, g, b) = palette.Colors[i];
                if (r != 0 || g != 0 || b != 0)
                    anyNonZero = true;
            }
            Assert.True(anyNonZero, "All 16 ANSI colors are (0,0,0)");
        }
    }

    [Fact]
    public void ColorCube_ComputedCorrectly()
    {
        var palette = Palette.CatppuccinMocha();

        // Index 16 = (0,0,0) — all zero channels
        Assert.Equal((0, 0, 0), palette.Colors[16]);

        // Index 21 = r=0, g=0, b=5 => (0, 0, 255)
        Assert.Equal((byte)0, palette.Colors[21].R);
        Assert.Equal((byte)0, palette.Colors[21].G);
        Assert.Equal((byte)255, palette.Colors[21].B);

        // Index 196 = r=5, g=0, b=0 => (255, 0, 0)
        Assert.Equal((byte)255, palette.Colors[196].R);
        Assert.Equal((byte)0, palette.Colors[196].G);
        Assert.Equal((byte)0, palette.Colors[196].B);

        // Index 231 = r=5, g=5, b=5 => (255, 255, 255)
        Assert.Equal((byte)255, palette.Colors[231].R);
        Assert.Equal((byte)255, palette.Colors[231].G);
        Assert.Equal((byte)255, palette.Colors[231].B);
    }

    [Fact]
    public void GrayscaleRamp_ComputedCorrectly()
    {
        var palette = Palette.CatppuccinMocha();

        // Index 232 = first gray = 8
        Assert.Equal((byte)8, palette.Colors[232].R);
        Assert.Equal(palette.Colors[232].R, palette.Colors[232].G);
        Assert.Equal(palette.Colors[232].G, palette.Colors[232].B);

        // Index 255 = last gray = 23 * 10 + 8 = 238
        Assert.Equal((byte)238, palette.Colors[255].R);
        Assert.Equal(palette.Colors[255].R, palette.Colors[255].G);
        Assert.Equal(palette.Colors[255].G, palette.Colors[255].B);
    }

    [Fact]
    public void CatppuccinMocha_SpecificColors()
    {
        var palette = Palette.CatppuccinMocha();
        Assert.Equal((byte)0x1e, palette.Background.R);
        Assert.Equal((byte)0x1e, palette.Background.G);
        Assert.Equal((byte)0x2e, palette.Background.B);
        Assert.Equal((byte)0xcd, palette.Text.R);
    }

    [Fact]
    public void CatppuccinLatte_SpecificColors()
    {
        var palette = Palette.CatppuccinLatte();
        Assert.Equal((byte)0xef, palette.Background.R);
        Assert.Equal((byte)0xf1, palette.Background.G);
        Assert.Equal((byte)0xf5, palette.Background.B);
        Assert.Equal((byte)0x4c, palette.Text.R);
    }

    [Fact]
    public void AllPalettes_SemanticSlotsPopulated()
    {
        Func<Palette>[] factories =
        [
            Palette.CatppuccinMocha,
            Palette.CatppuccinLatte,
            Palette.Dracula,
            Palette.Nord,
            Palette.SolarizedDark,
            Palette.SolarizedLight,
        ];

        foreach (var factory in factories)
        {
            var p = factory();
            // Semantic slots should all be set (Text should differ from default (0,0,0))
            Assert.NotEqual((byte)0, p.Text.R + p.Text.G + p.Text.B);
            Assert.NotEqual((byte)0, p.Surface0.R + p.Surface0.G + p.Surface0.B);
            Assert.NotEqual((byte)0, p.Lavender.R + p.Lavender.G + p.Lavender.B);
            Assert.NotEqual((byte)0, p.Green.R + p.Green.G + p.Green.B);
        }
    }
}
