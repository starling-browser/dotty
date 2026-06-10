using Avalonia;
using Avalonia.Media.Fonts;

namespace Dotty.Controls;

public static class AppBuilderExtensions
{
    /// <summary>
    /// Registers the Overpass Mono font collection bundled with the terminal control
    /// under the <c>fonts:DottyTerminal</c> key. Call this on the host AppBuilder so
    /// TerminalControl renders with its default font without the host shipping fonts.
    /// </summary>
    public static AppBuilder WithDottyTerminalFonts(this AppBuilder builder)
        => builder.ConfigureFonts(fontManager =>
        {
            fontManager.AddFontCollection(new EmbeddedFontCollection(
                new Uri("fonts:DottyTerminal", UriKind.Absolute),
                new Uri("avares://Dotty.Terminal.Avalonia/Assets/Fonts", UriKind.Absolute)));
        });
}
