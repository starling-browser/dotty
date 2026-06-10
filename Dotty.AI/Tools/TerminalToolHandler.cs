using System.Text.Json;
using Dotty.AI.Providers;
using Dotty.AI.Serialization;

namespace Dotty.AI.Tools;

/// <summary>
/// Tool handler for read_terminal_screen.
/// Uses a callback to extract visible text from the terminal grid.
/// </summary>
public class TerminalToolHandler : IChatToolHandler
{
    private readonly Func<string> _getVisibleText;

    public TerminalToolHandler(Func<string> getVisibleText)
    {
        _getVisibleText = getVisibleText;
    }

    public List<ToolDefinition> GetToolDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "read_terminal_screen",
            Description = "Read the current visible text on the terminal screen. Use this to see what the user is looking at, understand command output, or help debug errors visible in the terminal.",
            ParametersSchemaJson = """
            {
                "type": "object",
                "properties": {}
            }
            """
        }
    ];

    public Task<ChatToolResult> ExecuteToolAsync(
        string toolCallId,
        string functionName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (functionName != "read_terminal_screen")
        {
            return Task.FromResult(new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = $"Unknown function: {functionName}" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            });
        }

        try
        {
            var text = _getVisibleText();
            // Manually escape for AOT compatibility instead of using JsonSerializer.Serialize<string>
            var escaped = text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t");
            return Task.FromResult(new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = $"{{\"screen_content\": \"{escaped}\"}}",
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = ex.Message },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            });
        }
    }
}
