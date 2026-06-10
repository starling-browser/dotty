using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dotty.Terminal.Mcp;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(InitializeResult))]
[JsonSerializable(typeof(McpCapabilities))]
[JsonSerializable(typeof(McpToolsCapability))]
[JsonSerializable(typeof(McpServerInfo))]
[JsonSerializable(typeof(ToolsListResult))]
[JsonSerializable(typeof(McpToolDefinition))]
[JsonSerializable(typeof(McpInputSchema))]
[JsonSerializable(typeof(ToolCallParams))]
[JsonSerializable(typeof(ToolCallResult))]
[JsonSerializable(typeof(McpContentItem))]
[JsonSerializable(typeof(List<McpToolDefinition>))]
[JsonSerializable(typeof(List<McpContentItem>))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(JsonElement))]
internal partial class McpJsonContext : JsonSerializerContext;
