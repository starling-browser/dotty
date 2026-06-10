using Dotty.Terminal.Pty;

namespace Dotty.Terminal.Hosting;

public sealed class TerminalSessionOptions
{
    public GridSize Size { get; init; } = GridSize.Default;
    public string? Shell { get; init; }
    public string? WorkingDirectory { get; init; }
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

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
