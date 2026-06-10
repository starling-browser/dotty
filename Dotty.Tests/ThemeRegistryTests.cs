using Dotty.Theme;
using Xunit;

namespace Dotty.Tests;

public class ThemeRegistryTests
{
    [Fact]
    public void All_ContainsExpectedThemeCount()
    {
        Assert.Equal(1, ThemeRegistry.All.Count);
    }

    [Fact]
    public void All_HasUniqueIds()
    {
        var ids = ThemeRegistry.All.Select(t => t.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void GetById_ReturnsCorrectTheme()
    {
        var theme = ThemeRegistry.GetById("hmi-dark");
        Assert.Equal("HMI Cockpit", theme.DisplayName);
        Assert.True(theme.IsDark);
    }

    [Fact]
    public void GetById_UnknownId_ReturnsDefault()
    {
        var theme = ThemeRegistry.GetById("nonexistent");
        Assert.Equal("hmi-dark", theme.Id);
    }

    [Fact]
    public void All_CreatePalette_ReturnsValidPalettes()
    {
        foreach (var def in ThemeRegistry.All)
        {
            var palette = def.CreatePalette();
            Assert.Equal(256, palette.Colors.Length);
            Assert.Equal(def.IsDark, palette.IsDark);
        }
    }

    [Fact]
    public void ThemeDefinition_RecordEquality()
    {
        var a = ThemeRegistry.GetById("hmi-dark");
        var b = ThemeRegistry.GetById("hmi-dark");
        Assert.Equal(a, b);
    }

    [Fact]
    public void All_ExpectedIds()
    {
        var ids = ThemeRegistry.All.Select(t => t.Id).ToList();
        Assert.Contains("hmi-dark", ids);
    }
}
