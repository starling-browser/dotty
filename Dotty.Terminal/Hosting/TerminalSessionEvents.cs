namespace Dotty.Terminal.Hosting;

public sealed class TerminalBellEventArgs : EventArgs;

public sealed class TerminalClipboardEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}

public sealed class TerminalExitEventArgs(uint? exitCode) : EventArgs
{
    public uint? ExitCode { get; } = exitCode;
}

public sealed class TerminalFaultEventArgs(Exception exception) : EventArgs
{
    public Exception Exception { get; } = exception;
}

public sealed class TerminalTitleChangedEventArgs(string title) : EventArgs
{
    public string Title { get; } = title;
}

public sealed class TerminalWorkingDirectoryChangedEventArgs(string? workingDirectory) : EventArgs
{
    public string? WorkingDirectory { get; } = workingDirectory;
}
