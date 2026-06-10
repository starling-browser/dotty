using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Dotty.AI.Tools;
using Dotty.Commands;
using Dotty.Controls;
using Dotty.Mcp;
using Dotty.Settings;
using Dotty.Theme;
using Dotty.Tools;

namespace Dotty;

public partial class MainWindow : Window
{
    private CommandRegistry? _registry;
    private ThemeManager? _themeManager;
    private McpHttpServer? _mcpServer;
    private bool _allTerminalsClosed;

    private TerminalControl? ActiveTerminal => TerminalTabManager.ActiveTerminal;

    public MainWindow()
    {
        InitializeComponent();

        this.Opened += OnOpened;
        CommandPalette.Dismissed += (_, _) => ActiveTerminal?.Focus();
        CommandPalette.CommandExecuted += (_, _) => ActiveTerminal?.Focus();
        SettingsPanel.Dismissed += (_, _) => ActiveTerminal?.Focus();
        SettingsPanel.ScanLineToggled += OnScanLineToggled;

        // Tunnel so we intercept before TerminalControl sends to PTY
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);

        TerminalTabManager.AllSessionsClosed += () =>
        {
            _allTerminalsClosed = true;
        };
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        var settings = SettingsService.Load();
        _themeManager = new ThemeManager(settings);
        _themeManager.ThemeChanged += OnThemeChanged;

        // Apply initial theme
        ApplyTheme(_themeManager.Theme, _themeManager.CurrentDefinition);
        ScanLineOverlay.IsVisible = settings.ScanLineEnabled;

        // Create the first terminal tab
        TerminalTabManager.CreateSession(settings.FontSize);

        _registry = new CommandRegistry();
        foreach (var cmd in BuiltInCommands.Create(TerminalTabManager, this, _themeManager))
            _registry.Register(cmd);

        // Tool registry backing the embedded MCP server
        var toolRegistry = new AppToolRegistry();
        toolRegistry.Register(new ReadTerminalScreenTool(
            () => ActiveTerminal?.GetVisibleText() ?? ""));
        toolRegistry.Register(new WriteToTerminalTool(async bytes =>
        {
            await Dispatcher.UIThread.InvokeAsync(() => ActiveTerminal?.WriteToPty(bytes));
        }));
        toolRegistry.Register(new ExecuteHttpRequestTool());
        toolRegistry.Register(new ExecuteCommandTool(_registry, async commandId =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var cmd = _registry!.Commands.FirstOrDefault(c => c.Id == commandId);
                cmd?.Execute();
            });
        }));
        toolRegistry.Register(new ListCommandsTool(_registry));

        // Tab tools
        toolRegistry.Register(new GetTabsTool(async () =>
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var sb = new StringBuilder();
                sb.Append("{\"tabs\": [");
                var sessions = TerminalTabManager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    var s = sessions[i];
                    var isActive = s.Control == ActiveTerminal;
                    sb.Append($"{{\"id\": {s.Id}, \"title\": \"{s.Title}\", \"active\": {(isActive ? "true" : "false")}}}");
                }
                sb.Append("]}");
                return sb.ToString();
            });
        }));
        toolRegistry.Register(new NewTabTool(async () =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                TerminalTabManager.CreateSession(ActiveTerminal?.FontSize ?? 16));
        }));
        toolRegistry.Register(new CloseTabTool(async () =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
                TerminalTabManager.CloseActiveSession());
        }));
        toolRegistry.Register(new SwitchTabTool(async direction =>
        {
            return await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (direction == "next") TerminalTabManager.CycleNext();
                else TerminalTabManager.CyclePrevious();
                return true;
            });
        }));

        toolRegistry.Register(new ResizeWindowTool(async (width, height) =>
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Width = width;
                Height = height;
            });
        }));

        // Start embedded MCP server
        if (settings.McpEnabled)
        {
            _mcpServer = new McpHttpServer(toolRegistry, settings.McpPort);
            _mcpServer.Log += msg => System.Diagnostics.Debug.WriteLine(msg);
            _mcpServer.Start();
            StatusBar.BindMcpServer(_mcpServer);
        }
        else
        {
            StatusBar.SetMcpDisabled();
        }

        ActiveTerminal?.Focus();
    }

    private void OnThemeChanged(TerminalTheme theme, ThemeDefinition def)
    {
        ApplyTheme(theme, def);
    }

    private void ApplyTheme(TerminalTheme theme, ThemeDefinition def)
    {
        TerminalTabManager.ApplyTheme(theme);
        CommandPalette.ApplyTheme(theme);
        SettingsPanel.OnThemeChanged();
        StatusBar.ApplyTheme(theme);

        // Window chrome uses Base (canvas-deep #141414 for HMI) while
        // the terminal renderer uses palette.Background (canvas #1E1E1E for HMI)
        var chromeColor = Avalonia.Media.Color.FromRgb(
            theme.Palette.Base.R,
            theme.Palette.Base.G,
            theme.Palette.Base.B);
        Background = new SolidColorBrush(chromeColor);

        Application.Current!.RequestedThemeVariant =
            def.IsDark ? ThemeVariant.Dark : ThemeVariant.Light;
    }

    public void OpenSettings()
    {
        if (_themeManager == null) return;
        if (SettingsPanel.IsVisible)
            SettingsPanel.Close();
        else
            SettingsPanel.Open(_themeManager);
    }

    private void OnScanLineToggled(object? sender, bool enabled)
    {
        if (_themeManager == null) return;
        _themeManager.Settings.ScanLineEnabled = enabled;
        SettingsService.Save(_themeManager.Settings);
        ScanLineOverlay.IsVisible = enabled;
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        // Cmd on macOS, Ctrl elsewhere
        bool isMeta = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? e.KeyModifiers.HasFlag(KeyModifiers.Meta)
            : e.KeyModifiers.HasFlag(KeyModifiers.Control);

        bool isShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool isCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);

        // After all terminals closed, Ctrl+C closes the window
        if (_allTerminalsClosed && isCtrl && e.Key == Key.C)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (isMeta)
        {
            switch (e.Key)
            {
                case Key.K:
                    if (CommandPalette.IsVisible)
                        CommandPalette.Close();
                    else if (_registry != null)
                        CommandPalette.Open(_registry);
                    e.Handled = true;
                    return;

                case Key.OemComma:
                    OpenSettings();
                    e.Handled = true;
                    return;

                case Key.T:
                    TerminalTabManager.CreateSession(ActiveTerminal?.FontSize ?? 16);
                    e.Handled = true;
                    return;

                case Key.W:
                    TerminalTabManager.CloseActiveSession();
                    e.Handled = true;
                    return;

                case Key.C:
                    ActiveTerminal?.CopySelection(this);
                    e.Handled = true;
                    return;

                case Key.X:
                    ActiveTerminal?.CopySelection(this);
                    e.Handled = true;
                    return;

                case Key.V:
                    ActiveTerminal?.PasteFromClipboard(this);
                    e.Handled = true;
                    return;
            }

            // Cmd+Shift+[ / ] — cycle terminal tabs
            if (isShift)
            {
                if (e.Key == Key.OemOpenBrackets)
                {
                    TerminalTabManager.CyclePrevious();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.OemCloseBrackets)
                {
                    TerminalTabManager.CycleNext();
                    e.Handled = true;
                    return;
                }
            }
        }

        // Escape outside the terminal returns focus to the terminal
        if (e.Key == Key.Escape)
        {
            var focused = FocusManager?.GetFocusedElement();
            if (focused != null && focused != ActiveTerminal)
            {
                // Don't steal Escape from settings/command palette
                if (!SettingsPanel.IsVisible && !CommandPalette.IsVisible)
                {
                    ActiveTerminal?.Focus();
                    e.Handled = true;
                }
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _mcpServer?.Dispose();
        base.OnClosed(e);
    }
}
