namespace Dotty.Terminal.Pty;

public class PtyConfig
{
    public string? Shell { get; set; }
    public string? WorkingDirectory { get; set; }
    public Dictionary<string, string> Env { get; set; } = new();
    public GridSize Size { get; set; } = GridSize.Default;
}
