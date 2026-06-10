using Dotty.Terminal;

namespace Dotty.Theme;

public record ThemeDefinition(string Id, string DisplayName, bool IsDark, Func<Palette> CreatePalette);

public static class ThemeRegistry
{
    public static IReadOnlyList<ThemeDefinition> All { get; } =
    [
        new("hmi-dark", "HMI Cockpit", true, Palette.HmiDark),
    ];

    public static ThemeDefinition GetById(string id)
        => All.FirstOrDefault(t => t.Id == id) ?? All[0];
}
