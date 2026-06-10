using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Dotty.Theme;

namespace Dotty.Controls;

public class SettingsPanel : UserControl
{
    private ThemeManager? _themeManager;

    private readonly Border _backdrop;
    private readonly Border _container;
    private readonly TextBlock _title;
    private readonly Button _closeButton;
    private readonly ToggleSwitch _modeToggle;
    private readonly ToggleSwitch _scanLineToggle;
    private readonly Border _sectionDivider;
    private readonly TextBlock _themeLabel;
    private readonly Grid _cardGrid;

    private readonly NumberInputBox _fontSizeKnob;
    private readonly NumberInputBox _opacityKnob;
    private readonly NumberInputBox _scrollbackKnob;

    private readonly List<Border> _cards = new();

    private int _focusedCardIndex = -1;
    private bool _cardGridFocused;
    private const int CardColumns = 2;

    public event EventHandler? Dismissed;
    public event EventHandler<bool>? ScanLineToggled;

    public SettingsPanel()
    {
        IsVisible = false;
        Focusable = true;

        // Header bar with title and close button
        _title = new TextBlock
        {
            Text = "SETTINGS",
            FontSize = 11,
            FontWeight = FontWeight.SemiBold,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            LetterSpacing = 2,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _closeButton = new Button
        {
            Width = 28,
            Height = 28,
            Content = "\u00D7",
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
        };
        _closeButton.Click += (_, _) => Close();

        var headerBar = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 0),
        };
        DockPanel.SetDock(_closeButton, Dock.Right);
        headerBar.Children.Add(_closeButton);
        headerBar.Children.Add(_title);

        var headerBorder = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(VisualConstants.SpaceXxl, VisualConstants.SpaceLg),
            Child = headerBar,
        };

        _modeToggle = new ToggleSwitch
        {
            OnContent = "Dark Mode",
            OffContent = "Light Mode",
            Margin = new Thickness(0, 0, 0, VisualConstants.SpaceLg),
        };
        _modeToggle.IsCheckedChanged += OnModeToggleChanged;

        _scanLineToggle = new ToggleSwitch
        {
            OnContent = "Scan-line Effect",
            OffContent = "Scan-line Effect",
            Margin = new Thickness(0, 0, 0, VisualConstants.SpaceLg),
        };
        _scanLineToggle.IsCheckedChanged += OnScanLineToggleChanged;

        // Section divider between toggles and encoder knobs
        _sectionDivider = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 8, 0, 16),
        };

        // Encoder knobs section
        var knobsLabel = new TextBlock
        {
            Text = "TERMINAL CONTROLS",
            FontSize = VisualConstants.FontHmiLabel,
            FontWeight = FontWeight.SemiBold,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            LetterSpacing = 1,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 8),
        };

        _fontSizeKnob = new NumberInputBox("Font Size", 8, 24, 13, 1, "{0}px");
        _opacityKnob = new NumberInputBox("Opacity", 40, 100, 100, 5, "{0}%");
        _scrollbackKnob = new NumberInputBox("Scrollback", 1000, 50000, 5000, 1000, "{0}");

        var knobsRow = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*,*"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 0, 0, 16),
        };
        Grid.SetColumn(_fontSizeKnob, 0);
        Grid.SetColumn(_opacityKnob, 1);
        Grid.SetColumn(_scrollbackKnob, 2);
        _fontSizeKnob.Margin = new Thickness(0, 0, 4, 0);
        _opacityKnob.Margin = new Thickness(4, 0);
        _scrollbackKnob.Margin = new Thickness(4, 0, 0, 0);
        knobsRow.Children.Add(_fontSizeKnob);
        knobsRow.Children.Add(_opacityKnob);
        knobsRow.Children.Add(_scrollbackKnob);

        // Section divider between knobs and themes
        var sectionDivider2 = new Border
        {
            Height = 1,
            Margin = new Thickness(0, 8, 0, 16),
        };

        _themeLabel = new TextBlock
        {
            Text = "COLOR SCHEME",
            FontSize = VisualConstants.FontHmiLabel,
            FontWeight = FontWeight.SemiBold,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            LetterSpacing = 1,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 8),
        };

        // 2-column grid for theme cards
        _cardGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,*"),
            RowDefinitions = new RowDefinitions(),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var scrollViewer = new ScrollViewer
        {
            Content = new StackPanel
            {
                Spacing = 0,
                Margin = new Thickness(VisualConstants.SpaceXxl),
                Children =
                {
                    _modeToggle, _scanLineToggle,
                    _sectionDivider, knobsLabel, knobsRow,
                    sectionDivider2, _themeLabel, _cardGrid,
                },
            },
        };

        var mainLayout = new DockPanel();
        DockPanel.SetDock(headerBorder, Dock.Top);
        mainLayout.Children.Add(headerBorder);
        mainLayout.Children.Add(scrollViewer);

        _container = new Border
        {
            CornerRadius = new CornerRadius(VisualConstants.RadiusLg),
            BorderThickness = new Thickness(1, 0, 0, 0),
            Width = VisualConstants.SettingsWidth,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Stretch,
            BoxShadow = BoxShadows.Parse(VisualConstants.ShadowElevation3),
            RenderTransform = new TranslateTransform(VisualConstants.SettingsWidth, 0),
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
            Child = mainLayout,
        };

        _backdrop = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00)),
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

        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public void Open(ThemeManager themeManager)
    {
        _themeManager = themeManager;
        _modeToggle.IsChecked = themeManager.CurrentDefinition.IsDark;
        _scanLineToggle.IsChecked = themeManager.Settings.ScanLineEnabled;
        _fontSizeKnob.Value = themeManager.Settings.FontSize;
        _opacityKnob.Value = themeManager.Settings.TerminalOpacity;
        _scrollbackKnob.Value = themeManager.Settings.ScrollbackLines;
        BuildCards();
        ApplyPanelTheme();
        _cardGridFocused = false;
        _focusedCardIndex = -1;

        // Set initial animation state
        _container.Opacity = 0;
        _container.RenderTransform = new TranslateTransform(VisualConstants.SettingsWidth, 0);
        _backdrop.Opacity = 0;

        IsVisible = true;

        // Animate in on next frame
        Dispatcher.UIThread.Post(() =>
        {
            _container.Opacity = 1;
            _container.RenderTransform = new TranslateTransform(0, 0);
            _backdrop.Opacity = 1;
            _modeToggle.Focus();
        }, DispatcherPriority.Input);
    }

    public void Close()
    {
        _cardGridFocused = false;
        _focusedCardIndex = -1;
        IsVisible = false;
        Dismissed?.Invoke(this, EventArgs.Empty);
    }

    public void OnThemeChanged()
    {
        if (!IsVisible || _themeManager == null) return;
        _modeToggle.IsChecked = _themeManager.CurrentDefinition.IsDark;
        ApplyPanelTheme();
        UpdateCardSelection();
        UpdateCardFocus();
    }

    private void ApplyPanelTheme()
    {
        if (_themeManager == null) return;
        var p = _themeManager.Theme.Palette;
        bool isDark = p.IsDark;

        _container.Background = new SolidColorBrush(
            VisualConstants.GlassBackground((p.Base.R, p.Base.G, p.Base.B), isDark));
        _container.BorderBrush = new SolidColorBrush(
            Color.FromArgb(0x4D, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B));
        _title.Foreground = new SolidColorBrush(ToColor(p.Text));
        _themeLabel.Foreground = new SolidColorBrush(ToColor(p.Text));
        _closeButton.Foreground = new SolidColorBrush(ToColor(p.Text));

        // Header border
        if (_container.Child is DockPanel dp && dp.Children.Count > 0 && dp.Children[0] is Border headerBorder)
        {
            headerBorder.BorderBrush = new SolidColorBrush(
                Color.FromArgb(0x1F, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B));
        }

        // Section dividers
        var divBrush = new SolidColorBrush(
            Color.FromArgb(0x1F, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B));
        _sectionDivider.Background = divBrush;

        _backdrop.Background = new SolidColorBrush(VisualConstants.BackdropColor(isDark));

        // Theme knobs
        _fontSizeKnob.ApplyTheme(_themeManager.Theme);
        _opacityKnob.ApplyTheme(_themeManager.Theme);
        _scrollbackKnob.ApplyTheme(_themeManager.Theme);
    }

    private void BuildCards()
    {
        _cardGrid.Children.Clear();
        _cardGrid.RowDefinitions.Clear();
        _cards.Clear();

        if (_themeManager == null) return;

        bool wantDark = _modeToggle.IsChecked == true;

        var themes = new List<ThemeDefinition>();
        foreach (var def in ThemeRegistry.All)
        {
            if (def.IsDark != wantDark) continue;
            themes.Add(def);
        }

        int rowCount = (themes.Count + CardColumns - 1) / CardColumns;
        for (int r = 0; r < rowCount; r++)
            _cardGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int i = 0; i < themes.Count; i++)
        {
            var palette = themes[i].CreatePalette();
            var card = CreateThemeCard(themes[i], palette);
            _cards.Add(card);

            Grid.SetRow(card, i / CardColumns);
            Grid.SetColumn(card, i % CardColumns);
            _cardGrid.Children.Add(card);
        }

        UpdateCardSelection();

        if (_cardGridFocused && _cards.Count > 0)
            _focusedCardIndex = Math.Clamp(_focusedCardIndex, 0, _cards.Count - 1);
        UpdateCardFocus();
    }

    private Border CreateThemeCard(ThemeDefinition def, Terminal.Palette palette)
    {
        // Name above swatches (HTML prototype order)
        var nameBlock = new TextBlock
        {
            Text = def.DisplayName,
            FontSize = VisualConstants.FontHmiLabel,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            HorizontalAlignment = HorizontalAlignment.Left,
            Foreground = _themeManager != null
                ? new SolidColorBrush(ToColor(_themeManager.Theme.Palette.Text))
                : Brushes.White,
            Margin = new Thickness(0, 0, 0, 6),
        };

        // Rectangular swatch bars in 5-column grid
        (byte R, byte G, byte B)[] swatchColors =
        [
            palette.Background,
            palette.Surface0,
            palette.Text,
            palette.Lavender,
            palette.Green,
        ];

        var swatchGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,3,*,3,*,3,*,3,*"),
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        for (int i = 0; i < swatchColors.Length; i++)
        {
            var c = swatchColors[i];
            var bar = new Border
            {
                Height = 16,
                CornerRadius = new CornerRadius(1),
                Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B)),
            };
            Grid.SetColumn(bar, i * 2); // columns 0, 2, 4, 6, 8; gaps at 1, 3, 5, 7
            swatchGrid.Children.Add(bar);
        }

        var card = new Border
        {
            Padding = new Thickness(8, 6),
            Margin = new Thickness(4),
            CornerRadius = new CornerRadius(VisualConstants.RadiusMd),
            Background = _themeManager != null
                ? new SolidColorBrush(ToColor(_themeManager.Theme.Palette.Surface0))
                : Brushes.Transparent,
            BorderThickness = new Thickness(2),
            BorderBrush = Brushes.Transparent,
            BoxShadow = BoxShadows.Parse(VisualConstants.ShadowElevation1),
            Cursor = new Cursor(StandardCursorType.Hand),
            Tag = def.Id,
            Transitions = new Transitions
            {
                new BrushTransition
                {
                    Property = Border.BackgroundProperty,
                    Duration = TimeSpan.FromMilliseconds(VisualConstants.DurationFast),
                },
            },
            Child = new StackPanel
            {
                Spacing = 4,
                Children = { nameBlock, swatchGrid },
            },
        };

        card.PointerEntered += (_, _) =>
        {
            if (_themeManager == null) return;
            var p = _themeManager.Theme.Palette;
            card.Background = new SolidColorBrush(
                TerminalTheme.Mix(p.Surface0, p.Surface1, 0.5));
        };

        card.PointerExited += (_, _) =>
        {
            if (_themeManager == null) return;
            card.Background = new SolidColorBrush(
                ToColor(_themeManager.Theme.Palette.Surface0));
        };

        card.PointerPressed += (_, e) =>
        {
            if (_themeManager == null) return;
            var clickedDef = ThemeRegistry.GetById((string)card.Tag!);
            _themeManager.ApplyTheme(clickedDef);
            e.Handled = true;
        };

        return card;
    }

    private void UpdateCardSelection()
    {
        if (_themeManager == null) return;

        var accentColor = VisualConstants.StatusAdvisoryColor;

        foreach (var card in _cards)
        {
            bool selected = (string)card.Tag! == _themeManager.CurrentDefinition.Id;
            card.BorderBrush = selected
                ? new SolidColorBrush(accentColor)
                : Brushes.Transparent;

            card.BoxShadow = selected
                ? new BoxShadows(new BoxShadow
                {
                    Color = Color.FromArgb(0x33, accentColor.R, accentColor.G, accentColor.B),
                    Blur = 8,
                })
                : BoxShadows.Parse(VisualConstants.ShadowElevation1);

            card.Background = new SolidColorBrush(
                ToColor(_themeManager.Theme.Palette.Surface0));

            // Update the name TextBlock color
            if (card.Child is StackPanel sp && sp.Children.Count > 0
                && sp.Children[0] is TextBlock tb)
            {
                tb.Foreground = new SolidColorBrush(
                    ToColor(_themeManager.Theme.Palette.Text));
            }
        }
    }

    private void OnModeToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_themeManager == null) return;

        bool wantDark = _modeToggle.IsChecked == true;

        // If current theme already matches mode, just rebuild cards
        if (_themeManager.CurrentDefinition.IsDark == wantDark)
        {
            BuildCards();
            return;
        }

        // Switch to first theme of the target mode
        foreach (var def in ThemeRegistry.All)
        {
            if (def.IsDark == wantDark)
            {
                _themeManager.ApplyTheme(def);
                BuildCards();
                return;
            }
        }
    }

    private void OnScanLineToggleChanged(object? sender, RoutedEventArgs e)
    {
        if (_themeManager != null)
            _themeManager.Settings.ScanLineEnabled = _scanLineToggle.IsChecked == true;
        ScanLineToggled?.Invoke(this, _scanLineToggle.IsChecked == true);
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source == _backdrop)
        {
            Close();
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Close();
            e.Handled = true;
            return;
        }

        if (!_cardGridFocused || _cards.Count == 0) return;

        switch (e.Key)
        {
            case Key.Left:
                if (_focusedCardIndex > 0)
                    _focusedCardIndex--;
                e.Handled = true;
                break;
            case Key.Right:
                if (_focusedCardIndex < _cards.Count - 1)
                    _focusedCardIndex++;
                e.Handled = true;
                break;
            case Key.Up:
                if (_focusedCardIndex >= CardColumns)
                    _focusedCardIndex -= CardColumns;
                e.Handled = true;
                break;
            case Key.Down:
                if (_focusedCardIndex + CardColumns < _cards.Count)
                    _focusedCardIndex += CardColumns;
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
                SelectFocusedCard();
                e.Handled = true;
                break;
        }

        UpdateCardFocus();
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (!IsVisible || e.Key != Key.Tab) return;

        if (_cardGridFocused)
        {
            _cardGridFocused = false;
            _focusedCardIndex = -1;
            UpdateCardFocus();
            _modeToggle.Focus();
        }
        else
        {
            _cardGridFocused = true;
            _focusedCardIndex = GetSelectedCardIndex();
            UpdateCardFocus();
            Focus();
        }

        e.Handled = true;
    }

    private int GetSelectedCardIndex()
    {
        if (_themeManager == null || _cards.Count == 0) return 0;
        for (int i = 0; i < _cards.Count; i++)
        {
            if ((string)_cards[i].Tag! == _themeManager.CurrentDefinition.Id)
                return i;
        }
        return 0;
    }

    private void SelectFocusedCard()
    {
        if (_themeManager == null || _focusedCardIndex < 0 || _focusedCardIndex >= _cards.Count)
            return;
        var def = ThemeRegistry.GetById((string)_cards[_focusedCardIndex].Tag!);
        _themeManager.ApplyTheme(def);
    }

    private void UpdateCardFocus()
    {
        if (_themeManager == null) return;
        var lavender = _themeManager.Theme.Palette.Lavender;

        for (int i = 0; i < _cards.Count; i++)
        {
            bool focused = _cardGridFocused && i == _focusedCardIndex;
            _cards[i].BoxShadow = focused
                ? new BoxShadows(new BoxShadow { Spread = 3, Color = ToColor(lavender) })
                : default;
        }
    }

    private static Color ToColor((byte R, byte G, byte B) c) => Color.FromRgb(c.R, c.G, c.B);
}
