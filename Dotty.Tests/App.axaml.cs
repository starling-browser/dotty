using Avalonia;
using Avalonia.Markup.Xaml;

namespace Dotty.Tests;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
