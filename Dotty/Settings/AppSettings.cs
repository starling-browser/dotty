namespace Dotty.Settings;

public class AppSettings
{
    public string ThemeId { get; set; } = "hmi-dark";
    public double FontSize { get; set; } = 16.0;
    public bool ScanLineEnabled { get; set; }
    public int TerminalOpacity { get; set; } = 100;
    public int ScrollbackLines { get; set; } = 5000;
    public int McpPort { get; set; } = 8257;
    public bool McpEnabled { get; set; } = true;
}
