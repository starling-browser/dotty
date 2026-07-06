using Dotty.Terminal.Pty;

namespace Dotty.Terminal.Hosting;

public sealed class TerminalSessionOptions
{
    public GridSize Size { get; init; } = GridSize.Default;
    public string? Shell { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// How the embedding application would like the shell prompt to look.
    /// See <see cref="PromptHint"/> for the available styles and
    /// which shells honor the hint.
    /// </summary>
    public PromptHint PromptHint { get; init; } = PromptHint.None;

    /// <summary>
    /// Whether OSC 52 clipboard-write sequences from the terminal are honored
    /// (raising <see cref="TerminalSession.ClipboardWriteRequested"/>). Off by
    /// default: any program whose output reaches the emulator — including
    /// piped-through command output, a remote host over SSH, or an HTTP
    /// response body printed into the pane — can otherwise silently overwrite
    /// the user's clipboard (e.g. with a command that auto-runs on paste).
    /// Enable only when the embedder trusts the terminal's output stream.
    /// </summary>
    public bool AllowClipboardWrite { get; init; }

    internal PtyConfig ToPtyConfig()
    {
        var config = new PtyConfig
        {
            Size = Size,
            Shell = Shell,
            WorkingDirectory = WorkingDirectory,
        };

        foreach (var (key, value) in Environment)
            config.Env[key] = value;

        return config;
    }
}
