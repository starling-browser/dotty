using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Dotty.Theme;

namespace Dotty.Controls;

public class TerminalTabItem : UserControl
{
    private readonly Border _container;
    private readonly TextBlock _titleBlock;
    private readonly Button _closeButton;
    private readonly Border _activeAccent;

    private bool _isActive;
    private Color _accentColor = VisualConstants.StatusAdvisoryColor;
    private Color _surfaceColor = Color.FromRgb(0x24, 0x24, 0x38);
    private Color _foregroundColor = Color.FromRgb(0xcd, 0xd6, 0xf4);
    private bool _showClose;

    public TerminalSession Session { get; }
    public event Action<TerminalTabItem>? Activated;
    public event Action<TerminalTabItem>? CloseRequested;

    public bool IsActive
    {
        get => _isActive;
        set
        {
            _isActive = value;
            UpdateVisuals();
        }
    }

    public TerminalTabItem(TerminalSession session, bool showClose)
    {
        Session = session;
        _showClose = showClose;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);

        _titleBlock = new TextBlock
        {
            Text = session.Title.ToUpperInvariant(),
            FontSize = VisualConstants.FontHmiLabel,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(10, 0, 4, 0),
            MaxWidth = 120,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        _closeButton = new Button
        {
            Content = "\u00D7", // multiplication sign x
            FontSize = 14,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0,
            IsVisible = showClose,
            Focusable = true,
            Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(VisualConstants.DurationFast),
                },
            },
        };
        _closeButton.Click += (_, e) =>
        {
            CloseRequested?.Invoke(this);
            e.Handled = true;
        };

        _closeButton.PointerEntered += (_, _) =>
        {
            if (_showClose) _closeButton.Opacity = 1.0;
        };
        _closeButton.PointerExited += (_, _) =>
        {
            if (_showClose) _closeButton.Opacity = _isActive ? 0.5 : 0;
        };

        _activeAccent = new Border
        {
            Height = 2,
            VerticalAlignment = VerticalAlignment.Bottom,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };

        var tabContent = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _titleBlock, _closeButton },
        };

        _container = new Border
        {
            CornerRadius = new CornerRadius(2, 2, 0, 0),
            Padding = new Thickness(2, 4),
            Child = new Panel
            {
                Children = { tabContent, _activeAccent },
            },
        };

        Content = _container;

        session.TitleChanged += () => _titleBlock.Text = session.Title.ToUpperInvariant();

        PointerPressed += (_, e) =>
        {
            Activated?.Invoke(this);
            e.Handled = true;
        };

        PointerEntered += (_, _) =>
        {
            if (_showClose) _closeButton.Opacity = 0.5;
        };

        PointerExited += (_, _) =>
        {
            if (!_isActive && _showClose) _closeButton.Opacity = 0;
        };

        UpdateVisuals();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter or Key.Space:
                Activated?.Invoke(this);
                e.Handled = true;
                break;
            case Key.Delete:
                CloseRequested?.Invoke(this);
                e.Handled = true;
                break;
        }
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        var p = theme.Palette;
        _accentColor = VisualConstants.StatusAdvisoryColor;
        _surfaceColor = Color.FromRgb(p.Surface0.R, p.Surface0.G, p.Surface0.B);
        _foregroundColor = Color.FromRgb(p.Text.R, p.Text.G, p.Text.B);
        _closeButton.Foreground = new SolidColorBrush(_foregroundColor);
        UpdateVisuals();
    }

    private void UpdateVisuals()
    {
        if (_isActive)
        {
            _container.Background = new SolidColorBrush(_surfaceColor);
            _activeAccent.Background = new SolidColorBrush(_accentColor);
            _titleBlock.Foreground = new SolidColorBrush(_foregroundColor);
            _titleBlock.Opacity = 1.0;
        }
        else
        {
            _container.Background = Brushes.Transparent;
            _activeAccent.Background = Brushes.Transparent;
            _titleBlock.Foreground = new SolidColorBrush(_foregroundColor);
            _titleBlock.Opacity = 0.6;
        }
    }
}
