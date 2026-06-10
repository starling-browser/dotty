using Avalonia.Controls;
using Dotty.Controls;
using Dotty.Settings;
using Dotty.Theme;

namespace Dotty.Commands;

public static class BuiltInCommands
{
    public static List<Command> Create(TerminalTabManager tabManager, Window window, ThemeManager themeManager)
    {
        void ChangeFontAndSave(double delta)
        {
            tabManager.ActiveTerminal?.ChangeFontSize(delta);
            var fontSize = tabManager.ActiveTerminal?.FontSize ?? themeManager.Settings.FontSize;
            themeManager.Settings.FontSize = fontSize;
            SettingsService.Save(themeManager.Settings);
        }

        void ResetFontAndSave()
        {
            tabManager.ActiveTerminal?.ResetFontSize();
            var fontSize = tabManager.ActiveTerminal?.FontSize ?? 16;
            themeManager.Settings.FontSize = fontSize;
            SettingsService.Save(themeManager.Settings);
        }

        return
        [
            // Font
            new("font.increase", "Increase Font Size", "Font", "Cmd++",
                () => ChangeFontAndSave(2)),
            new("font.decrease", "Decrease Font Size", "Font", "Cmd+-",
                () => ChangeFontAndSave(-2)),
            new("font.reset", "Reset Font Size", "Font", null,
                () => ResetFontAndSave()),

            // Clipboard
            new("clipboard.copy", "Copy Selection", "Clipboard", "Cmd+C",
                () => tabManager.ActiveTerminal?.CopySelection(window)),
            new("clipboard.paste", "Paste", "Clipboard", "Cmd+V",
                () => tabManager.ActiveTerminal?.PasteFromClipboard(window)),

            // Terminal
            new("terminal.reset", "Reset Terminal", "Terminal", null,
                () => tabManager.ActiveTerminal?.ResetTerminal()),
            new("terminal.clear", "Clear Screen + Scrollback", "Terminal", null,
                () => tabManager.ActiveTerminal?.ClearTerminal()),

            // Terminal tabs
            new("terminal.new-tab", "New Terminal Tab", "Terminal", "Cmd+T",
                () => tabManager.CreateSession(tabManager.ActiveTerminal?.FontSize ?? 16)),
            new("terminal.close-tab", "Close Terminal Tab", "Terminal", "Cmd+W",
                () => tabManager.CloseActiveSession()),
            new("terminal.next-tab", "Next Terminal Tab", "Terminal", "Cmd+Shift+]",
                () => tabManager.CycleNext()),
            new("terminal.prev-tab", "Previous Terminal Tab", "Terminal", "Cmd+Shift+[",
                () => tabManager.CyclePrevious()),

            // Settings
            new("settings.open", "Open Settings", "Settings", "Cmd+,",
                () => (window as MainWindow)?.OpenSettings()),
        ];
    }
}
