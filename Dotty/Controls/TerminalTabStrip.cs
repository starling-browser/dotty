using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Dotty.Theme;

namespace Dotty.Controls;

public class TerminalTabStrip : UserControl
{
    private readonly StackPanel _tabStack;
    private readonly Button _newTabButton;
    private readonly Border _container;
    private readonly List<TerminalTabItem> _tabs = [];

    private int _focusedIndex = -1;
    private Color _foregroundColor = Color.FromRgb(0xcd, 0xd6, 0xf4);

    public event Action<TerminalSession>? TabActivated;
    public event Action<TerminalSession>? TabCloseRequested;
    public event Action? NewTabRequested;

    public TerminalTabStrip()
    {
        Focusable = true;
        Height = VisualConstants.PanelTabStripHeight;

        _tabStack = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Margin = new Thickness(4, 0),
        };

        _newTabButton = new Button
        {
            Content = "+",
            FontSize = 16,
            FontWeight = FontWeight.Light,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 2),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
            Focusable = true,
        };
        _newTabButton.Click += (_, _) => NewTabRequested?.Invoke();

        var strip = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Children = { _tabStack, _newTabButton },
        };

        _container = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = strip,
            },
        };

        Content = _container;
    }

    public void AddTab(TerminalSession session, bool isActive)
    {
        var tab = new TerminalTabItem(session, showClose: true)
        {
            IsActive = isActive,
        };
        tab.Activated += OnTabActivated;
        tab.CloseRequested += OnTabCloseRequested;
        _tabs.Add(tab);
        _tabStack.Children.Add(tab);

        UpdateVisibility();
    }

    public void RemoveTab(TerminalSession session)
    {
        var tab = _tabs.Find(t => t.Session == session);
        if (tab == null) return;

        _tabs.Remove(tab);
        _tabStack.Children.Remove(tab);

        UpdateVisibility();
    }

    public void UpdateActiveTab(TerminalSession? active)
    {
        foreach (var tab in _tabs)
            tab.IsActive = tab.Session == active;
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        var p = theme.Palette;
        _foregroundColor = Color.FromRgb(p.Text.R, p.Text.G, p.Text.B);
        _container.BorderBrush = new SolidColorBrush(
            Color.FromRgb(VisualConstants.HmiBorder.R, VisualConstants.HmiBorder.G, VisualConstants.HmiBorder.B));
        _newTabButton.Foreground = new SolidColorBrush(_foregroundColor);

        foreach (var tab in _tabs)
            tab.ApplyTheme(theme);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Left:
                MoveFocus(-1);
                e.Handled = true;
                break;
            case Key.Right:
                MoveFocus(1);
                e.Handled = true;
                break;
            case Key.Enter or Key.Space:
                if (_focusedIndex >= 0 && _focusedIndex < _tabs.Count)
                {
                    TabActivated?.Invoke(_tabs[_focusedIndex].Session);
                    e.Handled = true;
                }
                break;
        }
    }

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        if (_focusedIndex < 0 && _tabs.Count > 0)
        {
            _focusedIndex = _tabs.FindIndex(t => t.IsActive);
            if (_focusedIndex < 0) _focusedIndex = 0;
            _tabs[_focusedIndex].Focus();
        }
    }

    private void UpdateVisibility()
    {
        // Hide strip when only 1 tab
        IsVisible = _tabs.Count > 1;
    }

    private void MoveFocus(int delta)
    {
        if (_tabs.Count == 0) return;
        _focusedIndex = Math.Clamp(_focusedIndex + delta, 0, _tabs.Count - 1);
        _tabs[_focusedIndex].Focus();
    }

    private void OnTabActivated(TerminalTabItem tab)
    {
        _focusedIndex = _tabs.IndexOf(tab);
        TabActivated?.Invoke(tab.Session);
    }

    private void OnTabCloseRequested(TerminalTabItem tab)
    {
        TabCloseRequested?.Invoke(tab.Session);
    }
}
