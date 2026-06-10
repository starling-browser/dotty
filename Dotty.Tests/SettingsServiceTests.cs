using Dotty.Settings;
using Xunit;

namespace Dotty.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_ThemeId()
    {
        var settings = new AppSettings();
        Assert.Equal("hmi-dark", settings.ThemeId);
    }

    [Fact]
    public void Defaults_FontSize()
    {
        var settings = new AppSettings();
        Assert.Equal(16.0, settings.FontSize);
    }

    [Fact]
    public void Defaults_ScanLineDisabled()
    {
        var settings = new AppSettings();
        Assert.False(settings.ScanLineEnabled);
    }

    [Fact]
    public void Properties_AreSettable()
    {
        var settings = new AppSettings
        {
            ThemeId = "hmi-dark",
            FontSize = 20.0,
            ScanLineEnabled = true,
        };

        Assert.Equal("hmi-dark", settings.ThemeId);
        Assert.Equal(20.0, settings.FontSize);
        Assert.True(settings.ScanLineEnabled);
    }
}

public class SettingsServiceTests
{
    [Fact]
    public void Load_ReturnsValidSettings()
    {
        // Load always returns a valid AppSettings object
        var settings = SettingsService.Load();
        Assert.NotNull(settings);
        Assert.NotNull(settings.ThemeId);
        Assert.True(settings.FontSize > 0);
    }

    [Fact]
    public void SaveAndLoad_RoundTrips()
    {
        // Save the current state so we can restore it
        var original = SettingsService.Load();

        try
        {
            var settings = new AppSettings
            {
                ThemeId = "hmi-dark",
                FontSize = 22.0,
            };

            SettingsService.Save(settings);

            var loaded = SettingsService.Load();
            Assert.Equal("hmi-dark", loaded.ThemeId);
            Assert.Equal(22.0, loaded.FontSize);
        }
        finally
        {
            // Restore original settings
            SettingsService.Save(original);
        }
    }

    [Fact]
    public void SaveAndLoad_PreservesAllProperties()
    {
        var original = SettingsService.Load();

        try
        {
            var settings = new AppSettings
            {
                ThemeId = "hmi-dark",
                FontSize = 14.0,
                ScanLineEnabled = true,
            };

            SettingsService.Save(settings);
            var loaded = SettingsService.Load();

            Assert.Equal(settings.ThemeId, loaded.ThemeId);
            Assert.Equal(settings.FontSize, loaded.FontSize);
            Assert.Equal(settings.ScanLineEnabled, loaded.ScanLineEnabled);
        }
        finally
        {
            SettingsService.Save(original);
        }
    }
}
