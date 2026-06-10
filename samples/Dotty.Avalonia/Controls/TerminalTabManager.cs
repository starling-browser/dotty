using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Layout;
using Dotty.Theme;

namespace Dotty.Controls;

public class TerminalTabManager : UserControl
{
    private readonly TerminalTabStrip _tabStrip;
    private readonly Panel _contentPanel;
    private readonly List<TerminalSession> _sessions = [];

    private TerminalSession? _activeSession;
    private TerminalTheme? _theme;

    public TerminalControl? ActiveTerminal => _activeSession?.Control;
    public int SessionCount => _sessions.Count;
    public IReadOnlyList<TerminalSession> Sessions => _sessions;

    public event Action? ActiveSessionChanged;
    public event Action<TerminalSession>? SessionClosed;
    public event Action? AllSessionsClosed;

    public TerminalTabManager()
    {
        Focusable = false;

        _tabStrip = new TerminalTabStrip();
        _tabStrip.TabActivated += session => Activate(session);
        _tabStrip.TabCloseRequested += session => CloseSession(session);
        _tabStrip.NewTabRequested += () => CreateSession(_activeSession?.Control.FontSize ?? 16);

        _contentPanel = new Panel();

        var dock = new DockPanel();
        DockPanel.SetDock(_tabStrip, Dock.Top);
        dock.Children.Add(_tabStrip);
        dock.Children.Add(_contentPanel);

        Content = dock;
    }

    public TerminalSession CreateSession(double fontSize)
    {
        var session = new TerminalSession(fontSize);

        if (_theme != null)
            session.Control.ApplyTheme(_theme);

        session.Control.CloseRequested += () => CloseSession(session);

        session.Control.IsVisible = false;
        _contentPanel.Children.Add(session.Control);
        _sessions.Add(session);

        _tabStrip.AddTab(session, isActive: true);
        Activate(session);

        return session;
    }

    public void CloseSession(TerminalSession session)
    {
        int index = _sessions.IndexOf(session);
        if (index < 0) return;

        _sessions.RemoveAt(index);
        _contentPanel.Children.Remove(session.Control);
        _tabStrip.RemoveTab(session);

        SessionClosed?.Invoke(session);

        if (_sessions.Count == 0)
        {
            _activeSession = null;
            AllSessionsClosed?.Invoke();
        }
        else
        {
            // Activate neighbor
            int newIndex = Math.Min(index, _sessions.Count - 1);
            Activate(_sessions[newIndex]);
        }
    }

    public void Activate(TerminalSession session)
    {
        if (_activeSession == session && session.Control.IsVisible) return;

        // Hide previous
        if (_activeSession != null)
            _activeSession.Control.IsVisible = false;

        _activeSession = session;
        session.Control.IsVisible = true;
        _tabStrip.UpdateActiveTab(session);
        session.Control.Focus();

        ActiveSessionChanged?.Invoke();
    }

    public void CloseActiveSession()
    {
        if (_activeSession != null)
            CloseSession(_activeSession);
    }

    public void CycleNext()
    {
        if (_sessions.Count <= 1 || _activeSession == null) return;
        int idx = _sessions.IndexOf(_activeSession);
        int next = (idx + 1) % _sessions.Count;
        Activate(_sessions[next]);
    }

    public void CyclePrevious()
    {
        if (_sessions.Count <= 1 || _activeSession == null) return;
        int idx = _sessions.IndexOf(_activeSession);
        int prev = (idx - 1 + _sessions.Count) % _sessions.Count;
        Activate(_sessions[prev]);
    }

    public void ApplyTheme(TerminalTheme theme)
    {
        _theme = theme;
        _tabStrip.ApplyTheme(theme);
        foreach (var session in _sessions)
            session.Control.ApplyTheme(theme);
    }

    public void SetFontSize(double size)
    {
        foreach (var session in _sessions)
        {
            session.Control.FontSize = size;
            session.Control.InvalidateVisual();
        }
    }
}
