using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Dotty.Terminal.Mcp;
using Dotty.Theme;

namespace Dotty.Controls;

/// <summary>
/// Bottom status bar. Shows session info, MCP server status,
/// keyboard shortcut hints, and a clock.
/// </summary>
public class StatusBar : UserControl
{
    private readonly Border _container;
    private readonly TextBlock _sessionInfo;
    private readonly TextBlock _clock;
    private readonly McpStatusIndicator _mcpIndicator;

    private DispatcherTimer? _clockTimer;

    public StatusBar()
    {
        Focusable = false;
        Height = VisualConstants.StatusBarHeight;

        // Session info
        _sessionInfo = new TextBlock
        {
            FontSize = VisualConstants.FontHmiData,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.6,
            Margin = new Thickness(8, 0, 0, 0),
        };
        UpdateSessionInfo();

        // Clock — no "UTC" suffix
        _clock = new TextBlock
        {
            FontSize = VisualConstants.FontHmiData,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 10, 0),
        };

        // MCP status indicator
        _mcpIndicator = new McpStatusIndicator();

        // Styled kbd shortcut hints
        var shortcutHints = CreateKbdHints();

        // Layout: [Session] | ...stretches... | [MCP] | [Shortcuts] | [Clock]
        var rightPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                CreateDivider(),
                _mcpIndicator,
                CreateDivider(),
                shortcutHints,
                CreateDivider(),
                _clock,
            },
        };

        var layout = new DockPanel
        {
            LastChildFill = true,
        };
        DockPanel.SetDock(_sessionInfo, Dock.Left);
        DockPanel.SetDock(rightPanel, Dock.Right);
        layout.Children.Add(_sessionInfo);
        layout.Children.Add(rightPanel);
        layout.Children.Add(new Panel());

        _container = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = layout,
        };

        Content = _container;

        // Start clock
        StartClock();
    }

    public void BindMcpServer(McpHttpServer server)
    {
        _mcpIndicator.Bind(server);
    }

    public void SetMcpDisabled()
    {
        _mcpIndicator.SetDisabled();
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        var p = theme.Palette;
        var foregroundColor = Color.FromRgb(p.Text.R, p.Text.G, p.Text.B);
        var borderColor = Color.FromArgb(0x1A, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B);

        bool isDark = p.IsDark;
        _container.Background = new SolidColorBrush(
            VisualConstants.GlassBackground((p.Surface0.R, p.Surface0.G, p.Surface0.B), isDark));
        _container.BorderBrush = new SolidColorBrush(borderColor);

        var fg = new SolidColorBrush(foregroundColor);
        _sessionInfo.Foreground = fg;
        _clock.Foreground = fg;
        _mcpIndicator.ApplyTheme(theme);
    }

    private void UpdateSessionInfo()
    {
        var shell = Environment.GetEnvironmentVariable("SHELL") ?? "sh";
        shell = System.IO.Path.GetFileName(shell);
        _sessionInfo.Text = $"Session: {shell}";
    }

    private void StartClock()
    {
        UpdateClockText();
        _clockTimer = new DispatcherTimer(
            TimeSpan.FromSeconds(1),
            DispatcherPriority.Background,
            (_, _) => UpdateClockText());
        _clockTimer.Start();
    }

    private void UpdateClockText()
    {
        _clock.Text = DateTime.UtcNow.ToString("HH:mm:ss");
    }

    private static Border CreateDivider()
    {
        return new Border
        {
            Width = 1,
            Height = 14,
            Background = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0),
        };
    }

    private static StackPanel CreateKbdHints()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 6,
            VerticalAlignment = VerticalAlignment.Center,
        };

        void AddHint(string key, string label)
        {
            var kbd = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x22, 0x22, 0x22)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(4, 0),
                CornerRadius = new CornerRadius(2),
                Child = new TextBlock
                {
                    Text = key,
                    FontSize = 10,
                    FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 10,
                FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            panel.Children.Add(kbd);
            panel.Children.Add(lbl);
        }

        AddHint("⌘K", "Palette");
        AddHint("⌘,", "Settings");

        return panel;
    }
}
