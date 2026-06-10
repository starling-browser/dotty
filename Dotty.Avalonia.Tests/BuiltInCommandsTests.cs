using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Dotty.Commands;
using Dotty.Controls;
using Dotty.Settings;
using Dotty.Theme;
using Xunit;

namespace Dotty.Avalonia.Tests;

public class BuiltInCommandsTests
{
    private static (List<Command> commands, TerminalTabManager tabManager, ThemeManager manager) CreateCommands()
    {
        var settings = new AppSettings();
        var tabManager = new TerminalTabManager();
        var window = new Window { Content = tabManager };
        window.Show();
        tabManager.CreateSession(16);
        var manager = new ThemeManager(settings);
        var commands = BuiltInCommands.Create(tabManager, window, manager);
        return (commands, tabManager, manager);
    }

    private static Command Find(List<Command> commands, string id) =>
        commands.First(c => c.Id == id);

    [AvaloniaFact]
    public void FontIncrease_UpdatesSettingsFontSize()
    {
        var (commands, tabManager, manager) = CreateCommands();

        Find(commands, "font.increase").Execute();

        Assert.Equal(18.0, tabManager.ActiveTerminal!.FontSize);
        Assert.Equal(18.0, manager.Settings.FontSize);
    }

    [AvaloniaFact]
    public void FontDecrease_UpdatesSettingsFontSize()
    {
        var (commands, tabManager, manager) = CreateCommands();

        Find(commands, "font.decrease").Execute();

        Assert.Equal(14.0, tabManager.ActiveTerminal!.FontSize);
        Assert.Equal(14.0, manager.Settings.FontSize);
    }

    [AvaloniaFact]
    public void FontReset_UpdatesSettingsFontSize()
    {
        var (commands, tabManager, manager) = CreateCommands();

        // Change first, then reset
        Find(commands, "font.increase").Execute();
        Find(commands, "font.increase").Execute();
        Find(commands, "font.reset").Execute();

        Assert.Equal(16.0, tabManager.ActiveTerminal!.FontSize);
        Assert.Equal(16.0, manager.Settings.FontSize);
    }

    [AvaloniaFact]
    public void FontIncrease_PersistsViaSettingsService()
    {
        var original = SettingsService.Load();
        try
        {
            var (commands, _, _) = CreateCommands();

            Find(commands, "font.increase").Execute();

            var loaded = SettingsService.Load();
            Assert.Equal(18.0, loaded.FontSize);
        }
        finally
        {
            SettingsService.Save(original);
        }
    }

    [AvaloniaFact]
    public void FontDecrease_PersistsViaSettingsService()
    {
        var original = SettingsService.Load();
        try
        {
            var (commands, _, _) = CreateCommands();

            Find(commands, "font.decrease").Execute();

            var loaded = SettingsService.Load();
            Assert.Equal(14.0, loaded.FontSize);
        }
        finally
        {
            SettingsService.Save(original);
        }
    }

    [AvaloniaFact]
    public void FontReset_PersistsViaSettingsService()
    {
        var original = SettingsService.Load();
        try
        {
            var (commands, _, _) = CreateCommands();

            Find(commands, "font.increase").Execute();
            Find(commands, "font.reset").Execute();

            var loaded = SettingsService.Load();
            Assert.Equal(16.0, loaded.FontSize);
        }
        finally
        {
            SettingsService.Save(original);
        }
    }

    [AvaloniaFact]
    public void FontSize_ClampsAtMaximum()
    {
        var (commands, tabManager, manager) = CreateCommands();
        var increase = Find(commands, "font.increase");

        // 16 -> 48 requires 16 increments of 2
        for (int i = 0; i < 20; i++)
            increase.Execute();

        Assert.Equal(48.0, tabManager.ActiveTerminal!.FontSize);
        Assert.Equal(48.0, manager.Settings.FontSize);
    }

    [AvaloniaFact]
    public void FontSize_ClampsAtMinimum()
    {
        var (commands, tabManager, manager) = CreateCommands();
        var decrease = Find(commands, "font.decrease");

        // 16 -> 8 requires 4 decrements of 2, keep going past
        for (int i = 0; i < 10; i++)
            decrease.Execute();

        Assert.Equal(8.0, tabManager.ActiveTerminal!.FontSize);
        Assert.Equal(8.0, manager.Settings.FontSize);
    }

    [AvaloniaFact]
    public void FontSize_SettingsAndTerminalStayInSync()
    {
        var (commands, tabManager, manager) = CreateCommands();

        Find(commands, "font.increase").Execute();
        Assert.Equal(tabManager.ActiveTerminal!.FontSize, manager.Settings.FontSize);

        Find(commands, "font.decrease").Execute();
        Assert.Equal(tabManager.ActiveTerminal!.FontSize, manager.Settings.FontSize);

        Find(commands, "font.reset").Execute();
        Assert.Equal(tabManager.ActiveTerminal!.FontSize, manager.Settings.FontSize);
    }
}
