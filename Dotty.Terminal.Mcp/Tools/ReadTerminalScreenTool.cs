namespace Dotty.Terminal.Mcp.Tools;

/// <summary>
/// MCP tool that reads the visible text on the terminal screen. Bound to the
/// embeddable terminal via <see cref="ITerminalTarget"/>, so it travels with the
/// terminal regardless of host UI framework.
/// </summary>
public sealed class ReadTerminalScreenTool : IAppTool
{
    private readonly ITerminalTarget _terminal;

    public ReadTerminalScreenTool(ITerminalTarget terminal)
    {
        _terminal = terminal;
    }

    public string Name => "read_terminal_screen";

    public string Description =>
        "Read the current visible text on the terminal screen. Use this to see what the user is looking at, understand command output, or help debug errors visible in the terminal.";

    public string ParametersSchemaJson => """
        {
            "type": "object",
            "properties": {}
        }
        """;

    public Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        try
        {
            var text = _terminal.GetVisibleText();
            var escaped = text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            return Task.FromResult(new ToolResult($"{{\"screen_content\": \"{escaped}\"}}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ToolResult(
                $"{{\"error\": \"{ex.Message.Replace("\"", "\\\"")}\"}}", IsError: true));
        }
    }
}
