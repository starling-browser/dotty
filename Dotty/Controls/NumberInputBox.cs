using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Dotty.Theme;

namespace Dotty.Controls;

public class NumberInputBox : UserControl
{
    private readonly TextBlock _labelBlock;
    private readonly TextBlock _valueBlock;
    private readonly Border _container;
    private readonly Button _minusButton;
    private readonly Button _plusButton;

    private double _value;
    private double _min;
    private double _max;
    private double _step;
    private string _formatString;

    // Theme colors cached for hover/focus states
    private Color _borderDefault = Color.FromArgb(0x33, 0x6C, 0x70, 0x86);
    private Color _borderHover = Color.FromArgb(0x55, 0x6C, 0x70, 0x86);
    private Color _borderFocus = Color.FromRgb(0xB4, 0xBE, 0xFE);
    private Color _bgColor = Color.FromRgb(0x24, 0x24, 0x38);

    public event EventHandler<double>? ValueChanged;

    public string Label
    {
        get => _labelBlock.Text ?? "";
        set => _labelBlock.Text = value.ToUpperInvariant();
    }

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, _min, _max);
            UpdateDisplay();
            ValueChanged?.Invoke(this, _value);
        }
    }

    public double Min { get => _min; set { _min = value; Value = Math.Clamp(_value, _min, _max); } }
    public double Max { get => _max; set { _max = value; Value = Math.Clamp(_value, _min, _max); } }
    public double Step { get => _step; set => _step = value; }

    public string FormatString
    {
        get => _formatString;
        set { _formatString = value; UpdateDisplay(); }
    }

    public NumberInputBox(string label, double min, double max, double defaultValue, double step, string formatString)
    {
        _min = min;
        _max = max;
        _value = defaultValue;
        _step = step;
        _formatString = formatString;
        Focusable = true;

        _labelBlock = new TextBlock
        {
            Text = label.ToUpperInvariant(),
            FontSize = VisualConstants.FontHmiLabel,
            FontWeight = FontWeight.SemiBold,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            LetterSpacing = 1,
            Opacity = 0.6,
            Margin = new Thickness(0, 0, 0, 4),
        };

        _minusButton = CreateStepButton("\u2212"); // minus sign
        _plusButton = CreateStepButton("+");

        _minusButton.Click += (_, _) => Value = Math.Max(_value - _step, _min);
        _plusButton.Click += (_, _) => Value = Math.Min(_value + _step, _max);

        _valueBlock = new TextBlock
        {
            FontSize = VisualConstants.FontHmiData,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var innerGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(_minusButton, 0);
        Grid.SetColumn(_valueBlock, 1);
        Grid.SetColumn(_plusButton, 2);
        innerGrid.Children.Add(_minusButton);
        innerGrid.Children.Add(_valueBlock);
        innerGrid.Children.Add(_plusButton);

        _container = new Border
        {
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4, 4),
            MinHeight = 32,
            Child = innerGrid,
        };

        var stack = new StackPanel
        {
            Spacing = 0,
            Children = { _labelBlock, _container },
        };

        Content = stack;

        _container.PointerEntered += (_, _) =>
        {
            if (!IsFocused)
                _container.BorderBrush = new SolidColorBrush(_borderHover);
        };
        _container.PointerExited += (_, _) =>
        {
            if (!IsFocused)
                _container.BorderBrush = new SolidColorBrush(_borderDefault);
        };

        UpdateDisplay();
        ApplyDefaultColors();
    }

    private static Button CreateStepButton(string text)
    {
        return new Button
        {
            Content = text,
            FontSize = 13,
            FontFamily = new FontFamily(VisualConstants.HmiDataFont),
            FontWeight = FontWeight.SemiBold,
            Width = 28,
            Height = 24,
            Padding = new Thickness(0),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
            Focusable = true,
        };
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        _container.BorderBrush = new SolidColorBrush(_borderFocus);
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        _container.BorderBrush = new SolidColorBrush(_borderDefault);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up or Key.Right:
                Value = Math.Min(_value + _step, _max);
                e.Handled = true;
                break;
            case Key.Down or Key.Left:
                Value = Math.Max(_value - _step, _min);
                e.Handled = true;
                break;
        }
    }

    private void UpdateDisplay()
    {
        _valueBlock.Text = string.Format(_formatString, _value);
    }

    private void ApplyDefaultColors()
    {
        _container.Background = new SolidColorBrush(_bgColor);
        _container.BorderBrush = new SolidColorBrush(_borderDefault);
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        var p = theme.Palette;

        _bgColor = Color.FromRgb(p.Surface0.R, p.Surface0.G, p.Surface0.B);
        _borderDefault = Color.FromArgb(0x33, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B);
        _borderHover = Color.FromArgb(0x55, p.Overlay0.R, p.Overlay0.G, p.Overlay0.B);
        _borderFocus = Color.FromRgb(p.Lavender.R, p.Lavender.G, p.Lavender.B);

        var textBrush = new SolidColorBrush(Color.FromRgb(p.Text.R, p.Text.G, p.Text.B));
        _labelBlock.Foreground = textBrush;
        _valueBlock.Foreground = textBrush;
        _minusButton.Foreground = new SolidColorBrush(Color.FromRgb(p.Subtext0.R, p.Subtext0.G, p.Subtext0.B));
        _plusButton.Foreground = new SolidColorBrush(Color.FromRgb(p.Subtext0.R, p.Subtext0.G, p.Subtext0.B));

        _container.Background = new SolidColorBrush(_bgColor);
        _container.BorderBrush = new SolidColorBrush(IsFocused ? _borderFocus : _borderDefault);
    }
}
