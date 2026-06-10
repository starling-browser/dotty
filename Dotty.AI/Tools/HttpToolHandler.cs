using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dotty.AI.Providers;
using Dotty.AI.Serialization;

namespace Dotty.AI.Tools;

/// <summary>
/// Tool handler for execute_http_request.
/// Adapted from akt-sh HttpChatToolHandler.
/// </summary>
public class HttpToolHandler : IChatToolHandler
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private const int MaxBodyLength = 2000;

    public List<ToolDefinition> GetToolDefinitions() =>
    [
        new ToolDefinition
        {
            Name = "execute_http_request",
            Description = "Execute an HTTP request and return the response. Use this to test APIs, fetch data, or interact with web services.",
            ParametersSchemaJson = """
            {
                "type": "object",
                "properties": {
                    "method": {
                        "type": "string",
                        "description": "HTTP method",
                        "enum": ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"]
                    },
                    "url": {
                        "type": "string",
                        "description": "The full URL to request"
                    },
                    "headers": {
                        "type": "object",
                        "description": "Optional HTTP headers as key-value pairs"
                    },
                    "body_type": {
                        "type": "string",
                        "description": "Body content type: json, text, or form",
                        "enum": ["json", "text", "form"]
                    },
                    "body": {
                        "type": "string",
                        "description": "Request body content"
                    }
                },
                "required": ["method", "url"]
            }
            """
        }
    ];

    public async Task<ChatToolResult> ExecuteToolAsync(
        string toolCallId,
        string functionName,
        string argumentsJson,
        CancellationToken cancellationToken = default)
    {
        if (functionName != "execute_http_request")
        {
            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = $"Unknown function: {functionName}" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            };
        }

        ExecuteHttpRequestArgs? args;
        try
        {
            args = JsonSerializer.Deserialize(argumentsJson, AiJsonContext.Default.ExecuteHttpRequestArgs);
        }
        catch (Exception ex)
        {
            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = $"Invalid arguments: {ex.Message}" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            };
        }

        if (args is null || string.IsNullOrWhiteSpace(args.Url))
        {
            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = "URL is required" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            };
        }

        try
        {
            var sw = Stopwatch.StartNew();
            var method = new HttpMethod(args.Method.ToUpperInvariant());
            var request = new HttpRequestMessage(method, args.Url);

            if (args.Headers != null)
            {
                foreach (var (key, value) in args.Headers)
                    request.Headers.TryAddWithoutValidation(key, value);
            }

            if (args.Body != null && method != HttpMethod.Get && method != HttpMethod.Head)
            {
                var contentType = args.BodyType switch
                {
                    "json" => "application/json",
                    "form" => "application/x-www-form-urlencoded",
                    _ => "text/plain"
                };
                request.Content = new StringContent(args.Body, Encoding.UTF8, contentType);
            }

            var response = await HttpClient.SendAsync(request, cancellationToken);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (body.Length > MaxBodyLength)
                body = body[..MaxBodyLength] + $"... (truncated, {body.Length} total chars)";

            var responseHeaders = new Dictionary<string, string>();
            foreach (var header in response.Headers)
                responseHeaders[header.Key] = string.Join(", ", header.Value);
            foreach (var header in response.Content.Headers)
                responseHeaders[header.Key] = string.Join(", ", header.Value);

            var result = new HttpToolResponse
            {
                Status = (int)response.StatusCode,
                Reason = response.ReasonPhrase ?? "",
                DurationMs = sw.ElapsedMilliseconds,
                Body = body,
                Headers = responseHeaders,
            };

            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(result, AiJsonContext.Default.HttpToolResponse),
            };
        }
        catch (TaskCanceledException)
        {
            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = "Request timed out (30s)" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            };
        }
        catch (Exception ex)
        {
            return new ChatToolResult
            {
                ToolCallId = toolCallId,
                ResultJson = JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = ex.Message },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError = true
            };
        }
    }
}
