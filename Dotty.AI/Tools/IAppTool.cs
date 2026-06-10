namespace Dotty.AI.Tools;

public record ToolResult(string ResultJson, bool IsError = false);

public interface IAppTool
{
    string Name { get; }
    string Description { get; }
    string ParametersSchemaJson { get; }
    Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct);
}
