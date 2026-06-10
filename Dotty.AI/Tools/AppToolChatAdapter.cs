using Dotty.AI.Providers;

namespace Dotty.AI.Tools;

/// <summary>
/// Adapts AppToolRegistry tools to the IChatToolHandler interface
/// so ChatOrchestrator works unchanged.
/// </summary>
public class AppToolChatAdapter : IChatToolHandler
{
    private readonly AppToolRegistry _registry;

    public AppToolChatAdapter(AppToolRegistry registry)
    {
        _registry = registry;
    }

    public List<ToolDefinition> GetToolDefinitions()
    {
        var defs = new List<ToolDefinition>();
        foreach (var tool in _registry.AllTools)
        {
            defs.Add(new ToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                ParametersSchemaJson = tool.ParametersSchemaJson,
            });
        }
        return defs;
    }

    public async Task<ChatToolResult> ExecuteToolAsync(
        string toolCallId,
        string functionName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        var tool = _registry.GetTool(functionName);
        if (tool == null)
        {
            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = $"{{\"error\": \"Unknown tool: {functionName}\"}}",
                IsError = true,
            };
        }

        var result = await tool.ExecuteAsync(argumentsJson, cancellationToken);
        return new ChatToolResult
        {
            ToolCallId = toolCallId,
            ResultJson = result.ResultJson,
            IsError = result.IsError,
        };
    }
}
