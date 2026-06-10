using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Dotty.Controls;
using Dotty.Settings;
using Dotty.Theme;
using Xunit;

namespace Dotty.Avalonia.Tests;

public class SettingsPanelTests
{
    [AvaloniaFact]
    public void SettingsPanel_IsHiddenByDefault()
    {
        var panel = new SettingsPanel();
        var window = new Window { Content = panel };

        window.Show();

        Assert.False(panel.IsVisible);
    }

    [AvaloniaFact]
    public void SettingsPanel_OpensWithThemeManager()
    {
        var panel = new SettingsPanel();
        var window = new Window { Content = panel };
        var manager = new ThemeManager(new AppSettings());

        window.Show();
        panel.Open(manager);

        Assert.True(panel.IsVisible);
    }

    [AvaloniaFact]
    public void SettingsPanel_CloseHidesAndFiresDismissed()
    {
        var panel = new SettingsPanel();
        var window = new Window { Content = panel };
        var manager = new ThemeManager(new AppSettings());
        bool dismissed = false;
        panel.Dismissed += (_, _) => dismissed = true;

        window.Show();
        panel.Open(manager);
        panel.Close();

        Assert.False(panel.IsVisible);
        Assert.True(dismissed);
    }

    [AvaloniaFact]
    public void SettingsPanel_EscapeCloses()
    {
        var panel = new SettingsPanel();
        var window = new Window { Content = panel };
        var manager = new ThemeManager(new AppSettings());
        bool dismissed = false;
        panel.Dismissed += (_, _) => dismissed = true;

        window.Show();
        panel.Open(manager);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(panel.IsVisible);
        Assert.True(dismissed);
    }

    [AvaloniaFact]
    public void SettingsPanel_OnThemeChanged_UpdatesWhenVisible()
    {
        var panel = new SettingsPanel();
        var window = new Window { Content = panel };
        var manager = new ThemeManager(new AppSettings());

        window.Show();
        panel.Open(manager);

        // Switching theme should not throw when panel is visible
        var hmi = ThemeRegistry.GetById("hmi-dark");
        manager.ApplyTheme(hmi);
        panel.OnThemeChanged();

        Assert.True(panel.IsVisible);
    }
}
