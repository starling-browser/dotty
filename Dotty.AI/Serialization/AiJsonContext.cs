using System.Text.Json.Serialization;

namespace Dotty.AI.Serialization;

// ── Copilot Chat Completion Request ──

public class CopilotCompletionRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "";

    [JsonPropertyName("messages")]
    public List<CopilotRequestMessage> Messages { get; set; } = [];

    [JsonPropertyName("stream")]
    public bool Stream { get; set; } = true;

    [JsonPropertyName("tools")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CopilotTool>? Tools { get; set; }
}

public class CopilotRequestMessage
{
    [JsonPropertyName("role")]
    public string Role { get; set; } = "";

    [JsonPropertyName("content")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<CopilotToolCallMessage>? ToolCalls { get; set; }

    [JsonPropertyName("tool_call_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ToolCallId { get; set; }
}

public class CopilotToolCallMessage
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public CopilotFunctionCallMessage Function { get; set; } = new();
}

public class CopilotFunctionCallMessage
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = "";
}

public class CopilotTool
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "function";

    [JsonPropertyName("function")]
    public CopilotFunction Function { get; set; } = new();
}

public class CopilotFunction
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("description")]
    public string Description { get; set; } = "";

    [JsonPropertyName("parameters")]
    public CopilotFunctionParameters Parameters { get; set; } = new();
}

public class CopilotFunctionParameters
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "object";

    [JsonPropertyName("properties")]
    public Dictionary<string, CopilotParameterProperty> Properties { get; set; } = [];

    [JsonPropertyName("required")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Required { get; set; }
}

public class CopilotParameterProperty
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "string";

    [JsonPropertyName("description")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Description { get; set; }

    [JsonPropertyName("enum")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Enum { get; set; }
}

// ── SSE Chunk Response ──

public class SseChunkResponse
{
    [JsonPropertyName("choices")]
    public List<SseChoice>? Choices { get; set; }
}

public class SseChoice
{
    [JsonPropertyName("delta")]
    public SseDelta? Delta { get; set; }

    [JsonPropertyName("finish_reason")]
    public string? FinishReason { get; set; }
}

public class SseDelta
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("tool_calls")]
    public List<SseToolCall>? ToolCalls { get; set; }
}

public class SseToolCall
{
    [JsonPropertyName("index")]
    public int Index { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("function")]
    public SseToolCallFunction? Function { get; set; }
}

public class SseToolCallFunction
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("arguments")]
    public string? Arguments { get; set; }
}

// ── OAuth / Copilot Auth ──

public class DeviceCodeResponse
{
    [JsonPropertyName("device_code")]
    public string DeviceCode { get; set; } = "";

    [JsonPropertyName("user_code")]
    public string UserCode { get; set; } = "";

    [JsonPropertyName("verification_uri")]
    public string VerificationUri { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("interval")]
    public int Interval { get; set; } = 5;
}

public class TokenPollResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }

    [JsonPropertyName("interval")]
    public int? Interval { get; set; }
}

public class CopilotTokenResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("expires_at")]
    public long ExpiresAt { get; set; }
}

// ── Tool Argument/Result DTOs ──

public class ExecuteHttpRequestArgs
{
    [JsonPropertyName("method")]
    public string Method { get; set; } = "GET";

    [JsonPropertyName("url")]
    public string Url { get; set; } = "";

    [JsonPropertyName("headers")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("body_type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? BodyType { get; set; }

    [JsonPropertyName("body")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Body { get; set; }
}

public class HttpToolResponse
{
    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = "";

    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("headers")]
    public Dictionary<string, string> Headers { get; set; } = [];
}

public class ToolErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = "";
}

// ── Source-generated JSON context ──

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CopilotCompletionRequest))]
[JsonSerializable(typeof(CopilotRequestMessage))]
[JsonSerializable(typeof(CopilotToolCallMessage))]
[JsonSerializable(typeof(CopilotFunctionCallMessage))]
[JsonSerializable(typeof(CopilotTool))]
[JsonSerializable(typeof(CopilotFunction))]
[JsonSerializable(typeof(CopilotFunctionParameters))]
[JsonSerializable(typeof(CopilotParameterProperty))]
[JsonSerializable(typeof(SseChunkResponse))]
[JsonSerializable(typeof(SseChoice))]
[JsonSerializable(typeof(SseDelta))]
[JsonSerializable(typeof(SseToolCall))]
[JsonSerializable(typeof(SseToolCallFunction))]
[JsonSerializable(typeof(DeviceCodeResponse))]
[JsonSerializable(typeof(TokenPollResponse))]
[JsonSerializable(typeof(CopilotTokenResponse))]
[JsonSerializable(typeof(ExecuteHttpRequestArgs))]
[JsonSerializable(typeof(HttpToolResponse))]
[JsonSerializable(typeof(ToolErrorResponse))]
[JsonSerializable(typeof(List<CopilotRequestMessage>))]
[JsonSerializable(typeof(List<CopilotTool>))]
[JsonSerializable(typeof(List<CopilotToolCallMessage>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, CopilotParameterProperty>))]
public partial class AiJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
