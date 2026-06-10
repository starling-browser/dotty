using System;

namespace Dotty.Controls;

public class TerminalSession
{
    private static int _nextId;

    public int Id { get; }
    public string Title { get; private set; }
    public TerminalControl Control { get; }

    public event Action? TitleChanged;

    public TerminalSession(double fontSize)
    {
        Id = Interlocked.Increment(ref _nextId);
        Title = $"Terminal {Id}";
        Control = new TerminalControl { FontSize = fontSize };
    }

    public void SetTitle(string title)
    {
        Title = title;
        TitleChanged?.Invoke();
    }
}
