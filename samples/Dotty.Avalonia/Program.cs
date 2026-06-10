using Avalonia;
using Avalonia.Media.Fonts;
using Dotty.Controls;
using System;

namespace Dotty;

class Program
{
    public static void Main(string[] args)
    {
        try
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .WithDottyTerminalFonts()
            .ConfigureFonts(fontManager =>
            {
                fontManager.AddFontCollection(new EmbeddedFontCollection(
                    new Uri("fonts:Dotty", UriKind.Absolute),
                    new Uri("avares://Dotty/Assets/Fonts", UriKind.Absolute)));
            })
            .LogToTrace();
}
