using System.Text.Json.Serialization;

namespace Dotty.Tools;

// DTOs and source-generated JSON context for the host-provided HTTP tool.

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

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ExecuteHttpRequestArgs))]
[JsonSerializable(typeof(HttpToolResponse))]
[JsonSerializable(typeof(ToolErrorResponse))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class HttpToolJsonContext : JsonSerializerContext;
