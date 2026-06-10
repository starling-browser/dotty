using Avalonia.Headless.XUnit;
using Dotty.Settings;
using Dotty.Theme;
using Xunit;

namespace Dotty.Avalonia.Tests;

public class ThemeManagerTests
{
    [AvaloniaFact]
    public void Constructor_LoadsDefaultTheme()
    {
        var settings = new AppSettings();
        var manager = new ThemeManager(settings);

        Assert.Equal("hmi-dark", manager.CurrentDefinition.Id);
        Assert.NotNull(manager.Theme);
        Assert.True(manager.Theme.Palette.IsDark);
    }

    [AvaloniaFact]
    public void Constructor_LoadsThemeFromSettings()
    {
        var settings = new AppSettings { ThemeId = "hmi-dark" };
        var manager = new ThemeManager(settings);

        Assert.Equal("hmi-dark", manager.CurrentDefinition.Id);
        Assert.Equal("HMI Cockpit", manager.CurrentDefinition.DisplayName);
    }

    [AvaloniaFact]
    public void Constructor_UnknownThemeId_FallsBackToDefault()
    {
        var settings = new AppSettings { ThemeId = "nonexistent" };
        var manager = new ThemeManager(settings);

        Assert.Equal("hmi-dark", manager.CurrentDefinition.Id);
    }

    [AvaloniaFact]
    public void ApplyTheme_UpdatesCurrentDefinition()
    {
        var settings = new AppSettings();
        var manager = new ThemeManager(settings);

        var hmi = ThemeRegistry.GetById("hmi-dark");
        manager.ApplyTheme(hmi);

        Assert.Equal("hmi-dark", manager.CurrentDefinition.Id);
    }

    [AvaloniaFact]
    public void ApplyTheme_UpdatesSettingsThemeId()
    {
        var settings = new AppSettings();
        var manager = new ThemeManager(settings);

        var hmi = ThemeRegistry.GetById("hmi-dark");
        manager.ApplyTheme(hmi);

        Assert.Equal("hmi-dark", settings.ThemeId);
    }

    [AvaloniaFact]
    public void ApplyTheme_FiresThemeChangedEvent()
    {
        var settings = new AppSettings();
        var manager = new ThemeManager(settings);

        TerminalTheme? receivedTheme = null;
        ThemeDefinition? receivedDef = null;
        manager.ThemeChanged += (theme, def) =>
        {
            receivedTheme = theme;
            receivedDef = def;
        };

        var hmi = ThemeRegistry.GetById("hmi-dark");
        manager.ApplyTheme(hmi);

        Assert.NotNull(receivedTheme);
        Assert.NotNull(receivedDef);
        Assert.Equal("hmi-dark", receivedDef!.Id);
    }

    [AvaloniaFact]
    public void ApplyTheme_PaletteMatchesDefinition()
    {
        var settings = new AppSettings();
        var manager = new ThemeManager(settings);

        foreach (var def in ThemeRegistry.All)
        {
            manager.ApplyTheme(def);
            Assert.Equal(def.IsDark, manager.Theme.Palette.IsDark);
            Assert.Equal(def.Id, manager.CurrentDefinition.Id);
        }
    }
}
