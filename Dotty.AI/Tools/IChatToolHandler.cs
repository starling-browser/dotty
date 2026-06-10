using Dotty.AI.Providers;

namespace Dotty.AI.Tools;

public class ChatToolResult
{
    public string ToolCallId { get; set; } = "";
    public string ResultJson { get; set; } = "";
    public bool IsError { get; set; }
}

public interface IChatToolHandler
{
    List<ToolDefinition> GetToolDefinitions();

    Task<ChatToolResult> ExecuteToolAsync(
        string toolCallId,
        string functionName,
        string argumentsJson,
        CancellationToken cancellationToken = default);
}
