using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Dotty.Commands;
using Dotty.Controls;
using Xunit;

namespace Dotty.Avalonia.Tests;

public class CommandPaletteTests
{
    [AvaloniaFact]
    public void CommandPalette_IsHiddenByDefault()
    {
        var palette = new CommandPalette();
        var window = new Window { Content = palette };

        window.Show();

        Assert.False(palette.IsVisible);
    }

    [AvaloniaFact]
    public void CommandPalette_OpensWithRegistry()
    {
        var palette = new CommandPalette();
        var window = new Window { Content = palette };
        var registry = new CommandRegistry();
        registry.Register(new Command("test", "Test", "Testing", null, () => { }));

        window.Show();
        palette.Open(registry);

        Assert.True(palette.IsVisible);
    }

    [AvaloniaFact]
    public void CommandPalette_CloseHidesAndFiresDismissed()
    {
        var palette = new CommandPalette();
        var window = new Window { Content = palette };
        var registry = new CommandRegistry();
        registry.Register(new Command("test", "Test", "Testing", null, () => { }));
        bool dismissed = false;
        palette.Dismissed += (_, _) => dismissed = true;

        window.Show();
        palette.Open(registry);
        palette.Close();

        Assert.False(palette.IsVisible);
        Assert.True(dismissed);
    }

    [AvaloniaFact]
    public void CommandPalette_EscapeCloses()
    {
        var palette = new CommandPalette();
        var window = new Window { Content = palette };
        var registry = new CommandRegistry();
        registry.Register(new Command("test", "Test", "Testing", null, () => { }));
        bool dismissed = false;
        palette.Dismissed += (_, _) => dismissed = true;

        window.Show();
        palette.Open(registry);
        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(palette.IsVisible);
        Assert.True(dismissed);
    }
}
