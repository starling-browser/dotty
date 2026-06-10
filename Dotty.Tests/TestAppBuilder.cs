using Avalonia;
using Avalonia.Headless;
using Avalonia.Harfbuzz;
using Dotty.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Dotty.Tests;

public class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions())
            .WithInterFont()
            .UseHarfBuzz();
}
