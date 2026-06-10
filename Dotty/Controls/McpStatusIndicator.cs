using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Platform.Storage;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Dotty.Terminal.Mcp;
using Dotty.Theme;

namespace Dotty.Controls;

/// <summary>
/// Compact MCP server status indicator for the StatusBar.
/// Clicking opens a Popup with port, health, and recent activity.
/// </summary>
public class McpStatusIndicator : UserControl
{
    private readonly Border _led;
    private readonly TextBlock _label;
    private readonly Border _pill;

    private readonly Popup _popup;
    private readonly Border _popupCard;
    private readonly TextBlock _portText;
    private readonly TextBlock _healthText;
    private readonly TextBlock _sessionsText;
    private readonly TextBlock _requestsText;
    private readonly StackPanel _activityList;

    private McpHttpServer? _server;
    private int _port = 8257;
    private DispatcherTimer? _autoHideTimer;
    private TextBlock? _globalStatus;
    private TextBlock? _repoStatus;

    public McpStatusIndicator()
    {
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);

        _led = new Border
        {
            Width = 5,
            Height = 5,
            CornerRadius = new CornerRadius(2.5),
            Background = new SolidColorBrush(VisualConstants.StatusInactiveColor),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(2, 0, 4, 0),
        };

        _label = new TextBlock
        {
            Text = "MCP",
            FontSize = 9,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            VerticalAlignment = VerticalAlignment.Center,
            LetterSpacing = 0.5,
        };

        _pill = new Border
        {
            Padding = new Thickness(5, 2),
            CornerRadius = new CornerRadius(VisualConstants.RadiusSm),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { _led, _label },
            },
        };

        Content = _pill;

        // ── Popup card content ──
        var dimBrush = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55));

        _portText = CreateValueText();
        _healthText = CreateValueText();
        _sessionsText = CreateValueText();
        _requestsText = CreateValueText();
        _activityList = new StackPanel { Spacing = 2 };

        var activityHeader = new TextBlock
        {
            Text = "RECENT ACTIVITY",
            FontSize = 9,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            Foreground = dimBrush,
            LetterSpacing = 1,
            Margin = new Thickness(0, 8, 0, 4),
        };

        var setupSection = CreateSetupSection();

        _popupCard = new Border
        {
            Width = 260,
            Padding = new Thickness(12, 10),
            CornerRadius = new CornerRadius(VisualConstants.RadiusLg),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse(VisualConstants.ShadowElevation3),
            Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x1A)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)),
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(VisualConstants.DurationNormal),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
            Child = new StackPanel
            {
                Children =
                {
                    CreateRow("Port", _portText),
                    CreateRow("Health", _healthText),
                    CreateRow("Sessions", _sessionsText),
                    CreateRow("Requests", _requestsText),
                    activityHeader,
                    _activityList,
                    setupSection,
                },
            },
        };

        // ── Popup ──
        _popup = new Popup
        {
            PlacementTarget = this,
            Placement = PlacementMode.Top,
            VerticalOffset = -4,
            IsLightDismissEnabled = true,
            Child = _popupCard,
        };

        _popup.Closed += (_, _) => _autoHideTimer?.Stop();

        PointerPressed += OnPillClicked;
        PointerEntered += (_, _) =>
        {
            _autoHideTimer?.Stop();
            _pill.Background = new SolidColorBrush(Color.FromArgb(0x20, 0xFF, 0xFF, 0xFF));
        };
        PointerExited += (_, _) =>
        {
            _pill.Background = null;
        };

        KeyDown += OnKeyDown;
    }

    public void Bind(McpHttpServer server)
    {
        _server = server;
        _port = server.Port;
        server.StateChanged += () => Dispatcher.UIThread.Post(RefreshIndicator);
        RefreshIndicator();
    }

    public void SetDisabled()
    {
        _led.Background = new SolidColorBrush(VisualConstants.StatusInactiveColor);
        _label.Text = "MCP";
        _label.Opacity = 0.35;
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        var p = theme.Palette;
        var fg = new SolidColorBrush(Color.FromRgb(p.Text.R, p.Text.G, p.Text.B));
        _label.Foreground = fg;
        _portText.Foreground = fg;
        _healthText.Foreground = fg;
        _sessionsText.Foreground = fg;
        _requestsText.Foreground = fg;

        var surface = VisualConstants.GlassBackground(
            (p.Surface0.R, p.Surface0.G, p.Surface0.B), p.IsDark);
        _popupCard.Background = new SolidColorBrush(surface);
        _popupCard.BorderBrush = new SolidColorBrush(
            Color.FromArgb(0x44, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B));
    }

    private void RefreshIndicator()
    {
        if (_server == null) return;
        var sessions = _server.SessionCount;

        _led.Background = new SolidColorBrush(
            _server.IsRunning
                ? (sessions > 0 ? VisualConstants.StatusNormalColor : VisualConstants.StatusAdvisoryColor)
                : VisualConstants.StatusCriticalColor);

        _label.Text = sessions > 0 ? $"MCP ({sessions})" : "MCP";

        if (_popup.IsOpen)
            RefreshPopupContent();
    }

    private void RefreshPopupContent()
    {
        if (_server == null) return;

        _portText.Text = _server.Port.ToString();
        _healthText.Text = _server.IsRunning ? "Running" : "Stopped";
        _sessionsText.Text = _server.SessionCount.ToString();
        _requestsText.Text = _server.TotalRequests.ToString();

        _activityList.Children.Clear();
        var activity = _server.RecentActivity;

        if (activity.Count == 0)
        {
            _activityList.Children.Add(new TextBlock
            {
                Text = "No activity yet",
                FontSize = 9,
                FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
            });
        }
        else
        {
            for (var i = Math.Max(0, activity.Count - 10); i < activity.Count; i++)
            {
                var entry = activity[i];
                var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };

                row.Children.Add(new TextBlock
                {
                    Text = entry.Timestamp.ToString("HH:mm:ss"),
                    FontSize = 9,
                    FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                    Width = 52,
                });
                row.Children.Add(new TextBlock
                {
                    Text = entry.Method,
                    FontSize = 9,
                    FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                    Foreground = new SolidColorBrush(MethodColor(entry.Method)),
                });
                row.Children.Add(new TextBlock
                {
                    Text = entry.SessionPrefix,
                    FontSize = 9,
                    FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)),
                });
                _activityList.Children.Add(row);
            }
        }
    }

    private void OpenPopup()
    {
        RefreshPopupContent();
        _popupCard.Opacity = 0;
        _popup.IsOpen = true;
        Dispatcher.UIThread.Post(() => _popupCard.Opacity = 1, DispatcherPriority.Input);
    }

    private void OnPillClicked(object? sender, PointerPressedEventArgs e)
    {
        if (_popup.IsOpen)
            _popup.IsOpen = false;
        else
            OpenPopup();
        e.Handled = true;
    }

    private new void OnKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter or Key.Space:
                if (_popup.IsOpen) _popup.IsOpen = false;
                else OpenPopup();
                e.Handled = true;
                break;
            case Key.Escape when _popup.IsOpen:
                _popup.IsOpen = false;
                e.Handled = true;
                break;
        }
    }

    private static Color MethodColor(string method) => method switch
    {
        "tools/call" => VisualConstants.StatusAdvisoryColor,
        "tools/list" => Color.FromRgb(0x88, 0x88, 0x88),
        "initialize" => VisualConstants.StatusNormalColor,
        _ => Color.FromRgb(0x55, 0x55, 0x55),
    };

    private Border CreateSetupSection()
    {
        var header = new TextBlock
        {
            Text = "SETUP — CLAUDE CODE",
            FontSize = 9,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)),
            LetterSpacing = 1,
            Margin = new Thickness(0, 10, 0, 6),
        };

        _globalStatus = CreateStatusText();
        _repoStatus = CreateStatusText();

        var globalBtn = CreateSetupButton("Set Up Globally");
        globalBtn.Click += async (_, _) =>
        {
            globalBtn.IsEnabled = false;
            try
            {
                await McpConfigWriter.WriteGlobalAsync(_port);
                var path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".claude", "settings.json");
                ShowStatus(_globalStatus, $"Written to {path}", success: true);
            }
            catch (Exception ex)
            {
                ShowStatus(_globalStatus, ex.Message, success: false);
            }
            finally
            {
                globalBtn.IsEnabled = true;
            }
        };

        var repoBtn = CreateSetupButton("Set Up for Repo");
        repoBtn.Click += async (_, _) =>
        {
            repoBtn.IsEnabled = false;
            try
            {
                var topLevel = TopLevel.GetTopLevel(this);
                var folders = await topLevel!.StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions { Title = "Select project root", AllowMultiple = false });
                if (folders.Count == 0) return;
                var folderPath = folders[0].TryGetLocalPath();
                if (folderPath == null) return;

                await McpConfigWriter.WriteProjectAsync(folderPath, _port);
                ShowStatus(_repoStatus, $"Written to {Path.Combine(folderPath, ".mcp.json")}", success: true);
            }
            catch (Exception ex)
            {
                ShowStatus(_repoStatus, ex.Message, success: false);
            }
            finally
            {
                repoBtn.IsEnabled = true;
            }
        };

        return new Border
        {
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(0, 2, 0, 0),
            Child = new StackPanel
            {
                Children = { header, globalBtn, _globalStatus, repoBtn, _repoStatus },
            },
        };
    }

    private static Button CreateSetupButton(string text) => new()
    {
        Content = text,
        FontSize = 9,
        FontFamily = new FontFamily(VisualConstants.HmiDataFont),
        Foreground = new SolidColorBrush(VisualConstants.BtnForeground),
        Background = new SolidColorBrush(VisualConstants.BtnBackground),
        BorderBrush = new SolidColorBrush(VisualConstants.BtnBorder),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(VisualConstants.RadiusSm),
        Padding = new Thickness(8, 3),
        HorizontalAlignment = HorizontalAlignment.Left,
        Margin = new Thickness(0, 2, 0, 0),
        Cursor = new Cursor(StandardCursorType.Hand),
    };

    private static TextBlock CreateStatusText() => new()
    {
        FontSize = 9,
        FontFamily = new FontFamily(VisualConstants.HmiDataFont),
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 2, 0, 4),
        IsVisible = false,
    };

    private static void ShowStatus(TextBlock tb, string message, bool success)
    {
        tb.Text = success ? $"✓ {message}" : $"✗ {message}";
        tb.Foreground = new SolidColorBrush(success
            ? Color.FromRgb(0x4E, 0xC9, 0x7E)
            : Color.FromRgb(0xF4, 0x73, 0x67));
        tb.IsVisible = true;
    }

    private static TextBlock CreateValueText() => new()
    {
        FontSize = VisualConstants.FontHmiData,
        FontFamily = new FontFamily(VisualConstants.HmiDataFont),
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static DockPanel CreateRow(string label, TextBlock value)
    {
        var lbl = new TextBlock
        {
            Text = label,
            FontSize = 9,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
            VerticalAlignment = VerticalAlignment.Center,
            Width = 62,
        };
        var row = new DockPanel { Margin = new Thickness(0, 1) };
        DockPanel.SetDock(lbl, Dock.Left);
        row.Children.Add(lbl);
        row.Children.Add(value);
        return row;
    }
}
