using Dotty.Settings;

namespace Dotty.Theme;

public class ThemeManager
{
    public TerminalTheme Theme { get; private set; }
    public ThemeDefinition CurrentDefinition { get; private set; }
    public AppSettings Settings { get; }

    public event Action<TerminalTheme, ThemeDefinition>? ThemeChanged;

    public ThemeManager(AppSettings settings)
    {
        Settings = settings;
        CurrentDefinition = ThemeRegistry.GetById(settings.ThemeId);
        Theme = new TerminalTheme(CurrentDefinition.CreatePalette());
    }

    public void ApplyTheme(ThemeDefinition definition)
    {
        CurrentDefinition = definition;
        Theme = new TerminalTheme(definition.CreatePalette());
        Settings.ThemeId = definition.Id;
        SettingsService.Save(Settings);
        ThemeChanged?.Invoke(Theme, definition);
    }
}
