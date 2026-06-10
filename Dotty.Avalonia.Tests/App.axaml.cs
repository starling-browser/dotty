using Avalonia;
using Avalonia.Markup.Xaml;

namespace Dotty.Avalonia.Tests;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
