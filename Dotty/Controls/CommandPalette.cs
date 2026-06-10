using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Dotty.Commands;
using Dotty.Theme;

namespace Dotty.Controls;

public class CommandPalette : UserControl
{
    private Color _surface0 = Color.FromRgb(0x31, 0x31, 0x44);
    private Color _surface1 = Color.FromRgb(0x3b, 0x3b, 0x50);
    private Color _textColor = Color.FromRgb(0xcd, 0xd6, 0xf4);
    private Color _subtext1 = Color.FromRgb(0xba, 0xc2, 0xde);
    private Color _lavenderColor = Color.FromRgb(0xb4, 0xbe, 0xfe);
    private Color _overlay0 = Color.FromRgb(0x6c, 0x70, 0x86);
    private bool _isDark = true;

    private CommandRegistry? _registry;
    private List<Command> _filtered = new();
    private int _selectedIndex;

    private readonly Border _backdrop;
    private readonly Border _container;
    private readonly TextBox _searchBox;
    private readonly StackPanel _itemsPanel;
    private readonly Avalonia.Controls.Shapes.Path _searchIcon;
    private readonly TextBlock _footerCount;

    public event EventHandler? Dismissed;
    public event EventHandler<Command>? CommandExecuted;

    // Magnifying glass (16x16 viewbox)
    private static readonly Geometry SearchIcon =
        Geometry.Parse("M 10.6 10.6 L 14.5 14.5 M 1.5 6.5 A 5 5 0 1 0 11.5 6.5 A 5 5 0 1 0 1.5 6.5");

    public CommandPalette()
    {
        IsVisible = false;
        Focusable = false;

        // Search magnifying glass icon
        _searchIcon = new Avalonia.Controls.Shapes.Path
        {
            Data = SearchIcon,
            Stroke = new SolidColorBrush(_overlay0),
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
            Fill = Brushes.Transparent,
            Width = 16,
            Height = 16,
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 0, 0),
            IsHitTestVisible = false,
        };

        _searchBox = new TextBox
        {
            PlaceholderText = "Type a command...",
            FontSize = VisualConstants.FontBase,
            FontFamily = new FontFamily(VisualConstants.HmiUiFont),
            Foreground = new SolidColorBrush(_textColor),
            Background = new SolidColorBrush(_surface0),
            BorderBrush = new SolidColorBrush(_overlay0),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(VisualConstants.RadiusMd),
            Padding = new Thickness(32, 10, 14, 10),
            CaretBrush = new SolidColorBrush(_lavenderColor),
        };
        _searchBox.TextChanged += OnSearchTextChanged;
        _searchBox.KeyDown += OnSearchKeyDown;

        var searchContainer = new Grid
        {
            Children = { _searchBox, _searchIcon },
        };

        // Separator between search and results
        var separator = new Border
        {
            Height = 1,
            Background = new SolidColorBrush(Color.FromArgb(0x26, _overlay0.R, _overlay0.G, _overlay0.B)),
            Margin = new Thickness(4, 4),
        };

        _itemsPanel = new StackPanel
        {
            Spacing = 2,
        };

        var scrollViewer = new ScrollViewer
        {
            MaxHeight = 300,
            Content = _itemsPanel,
        };

        // Styled kbd footer
        _footerCount = new TextBlock
        {
            FontSize = VisualConstants.FontHmiLabel,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            Opacity = 0.4,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var footer = CreateKbdFooter();

        _container = new Border
        {
            Background = new SolidColorBrush(VisualConstants.GlassBackground(
                (_surface0.R, _surface0.G, _surface0.B), _isDark)),
            CornerRadius = new CornerRadius(VisualConstants.RadiusLg),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x4D, _overlay0.R, _overlay0.G, _overlay0.B)),
            BorderThickness = new Thickness(1),
            Width = VisualConstants.PaletteWidth,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, VisualConstants.PaletteTop, 0, 0),
            Padding = new Thickness(12),
            BoxShadow = BoxShadows.Parse(VisualConstants.ShadowElevation3),
            RenderTransform = new ScaleTransform(1, 1),
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(VisualConstants.DurationNormal),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(VisualConstants.DurationNormal),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
            Child = new StackPanel
            {
                Spacing = 8,
                Children = { searchContainer, separator, scrollViewer, footer },
            },
        };

        _backdrop = new Border
        {
            Background = new SolidColorBrush(VisualConstants.BackdropColor(_isDark)),
            Child = _container,
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(VisualConstants.DurationNormal),
                    Easing = new Avalonia.Animation.Easings.CubicEaseOut(),
                },
            },
        };
        _backdrop.PointerPressed += OnBackdropPressed;

        Content = _backdrop;
    }

    public void Open(CommandRegistry registry)
    {
        _registry = registry;
        _searchBox.Text = "";
        _selectedIndex = 0;
        RebuildItems("");

        // Set initial animation state
        _container.Opacity = 0;
        _container.RenderTransform = new ScaleTransform(0.97, 0.97);
        _backdrop.Opacity = 0;

        IsVisible = true;

        // Animate in on next frame
        Dispatcher.UIThread.Post(() =>
        {
            _container.Opacity = 1;
            _container.RenderTransform = new ScaleTransform(1, 1);
            _backdrop.Opacity = 1;
            _searchBox.Focus();
        }, DispatcherPriority.Input);
    }

    public void Close()
    {
        IsVisible = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        _selectedIndex = 0;
        RebuildItems(_searchBox.Text ?? "");
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
            case Key.Down:
                _selectedIndex = Math.Min(_selectedIndex + 1, _filtered.Count - 1);
                UpdateSelection();
                e.Handled = true;
                break;
            case Key.Up:
                _selectedIndex = Math.Max(_selectedIndex - 1, 0);
                UpdateSelection();
                e.Handled = true;
                break;
            case Key.Enter:
                if (_selectedIndex >= 0 && _selectedIndex < _filtered.Count)
                    ExecuteCommand(_filtered[_selectedIndex]);
                e.Handled = true;
                break;
        }
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        // Only dismiss if clicking the backdrop itself, not the container
        if (e.Source == _backdrop)
        {
            Close();
            e.Handled = true;
        }
    }

    private void RebuildItems(string query)
    {
        _itemsPanel.Children.Clear();
        _filtered = _registry?.Filter(query) ?? new();

        // Group by category with section headers
        string? lastCategory = null;
        int itemIndex = 0;

        for (int i = 0; i < _filtered.Count; i++)
        {
            var command = _filtered[i];

            if (command.Category != lastCategory)
            {
                lastCategory = command.Category;
                var header = new TextBlock
                {
                    Text = command.Category.ToUpperInvariant(),
                    FontSize = 9,
                    FontWeight = FontWeight.SemiBold,
                    FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                    Foreground = new SolidColorBrush(_overlay0),
                    LetterSpacing = 1.5,
                    Margin = new Thickness(12, i > 0 ? 8 : 2, 0, 4),
                };
                _itemsPanel.Children.Add(header);
            }

            var item = CreateCommandItem(command, i);
            _itemsPanel.Children.Add(item);
            itemIndex++;
        }

        _footerCount.Text = $"{_filtered.Count} commands";

        UpdateSelection();
    }

    private Border CreateCommandItem(Command command, int index)
    {
        var label = new TextBlock
        {
            Text = command.Label,
            FontSize = VisualConstants.FontBase,
            FontWeight = FontWeight.Medium,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            Foreground = new SolidColorBrush(_textColor),
        };

        var row = new DockPanel
        {
            Children = { label },
        };

        if (command.ShortcutHint != null)
        {
            // Shortcut chip — keyboard-key badge look
            var shortcutText = new TextBlock
            {
                Text = command.ShortcutHint,
                FontSize = VisualConstants.FontHmiLabel,
                FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                Foreground = new SolidColorBrush(_overlay0),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var shortcutChip = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(0x80, _surface1.R, _surface1.G, _surface1.B)),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2),
                VerticalAlignment = VerticalAlignment.Center,
                Child = shortcutText,
            };

            DockPanel.SetDock(shortcutChip, Dock.Right);
            row.Children.Insert(0, shortcutChip);
        }

        var border = new Border
        {
            Padding = new Thickness(12, 8),
            CornerRadius = new CornerRadius(VisualConstants.RadiusMd),
            Child = row,
            Tag = index,
        };

        border.PointerEntered += (_, _) =>
        {
            _selectedIndex = (int)border.Tag!;
            UpdateSelection();
        };

        border.PointerPressed += (_, e) =>
        {
            if ((int)border.Tag! < _filtered.Count)
            {
                ExecuteCommand(_filtered[(int)border.Tag!]);
                e.Handled = true;
            }
        };

        return border;
    }

    private void UpdateSelection()
    {
        int commandIndex = 0;
        for (int i = 0; i < _itemsPanel.Children.Count; i++)
        {
            if (_itemsPanel.Children[i] is Border border && border.Tag is int)
            {
                border.Background = commandIndex == _selectedIndex
                    ? new SolidColorBrush(_surface1)
                    : Brushes.Transparent;
                commandIndex++;
            }
        }
    }

    private void ExecuteCommand(Command command)
    {
        IsVisible = false;
        command.Execute();
        CommandExecuted?.Invoke(this, command);
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        var p = theme.Palette;
        _surface0 = ToColor(p.Surface0);
        _surface1 = ToColor(p.Surface1);
        _textColor = ToColor(p.Text);
        _subtext1 = ToColor(p.Subtext1);
        _lavenderColor = ToColor(p.Lavender);
        _overlay0 = ToColor(p.Overlay0);
        _isDark = p.IsDark;

        _searchBox.Foreground = new SolidColorBrush(_textColor);
        _searchBox.Background = new SolidColorBrush(_surface0);
        _searchBox.BorderBrush = new SolidColorBrush(_overlay0);
        _searchBox.CaretBrush = new SolidColorBrush(_lavenderColor);

        _searchIcon.Stroke = new SolidColorBrush(_overlay0);

        _container.Background = new SolidColorBrush(
            VisualConstants.GlassBackground((p.Surface0.R, p.Surface0.G, p.Surface0.B), _isDark));
        _container.BorderBrush = new SolidColorBrush(
            Color.FromArgb(0x4D, _overlay0.R, _overlay0.G, _overlay0.B));

        _backdrop.Background = new SolidColorBrush(VisualConstants.BackdropColor(_isDark));

        // Update separator color
        if (_container.Child is StackPanel sp2
            && sp2.Children.Count > 1
            && sp2.Children[1] is Border sep)
        {
            sep.Background = new SolidColorBrush(
                Color.FromArgb(0x26, _overlay0.R, _overlay0.G, _overlay0.B));
        }

        // Rebuild items so they pick up new colors
        if (_registry != null && IsVisible)
            RebuildItems(_searchBox.Text ?? "");
    }

    private DockPanel CreateKbdFooter()
    {
        var hintsPanel = new StackPanel
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
                    FontSize = 8,
                    FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                    Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)),
                    VerticalAlignment = VerticalAlignment.Center,
                },
            };
            var lbl = new TextBlock
            {
                Text = label,
                FontSize = 8,
                FontFamily = new FontFamily(VisualConstants.HmiDataFont),
                Foreground = new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x66)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
            };
            hintsPanel.Children.Add(kbd);
            hintsPanel.Children.Add(lbl);
        }

        AddHint("ESC", "Close");
        AddHint("\u2191\u2193", "Navigate");
        AddHint("\u23CE", "Select");

        var footer = new DockPanel
        {
            Margin = new Thickness(0, 4, 0, 0),
        };
        DockPanel.SetDock(_footerCount, Dock.Right);
        footer.Children.Add(_footerCount);
        footer.Children.Add(hintsPanel);

        return footer;
    }

    private static Color ToColor((byte R, byte G, byte B) c) => Color.FromRgb(c.R, c.G, c.B);
}
