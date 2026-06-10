using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Dotty.AI.Serialization;
using Dotty.AI.Tools;

namespace Dotty.Tools;

/// <summary>
/// IAppTool wrapper for HTTP requests. Reuses the same DTOs as HttpToolHandler.
/// </summary>
public class ExecuteHttpRequestTool : IAppTool
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };

    private const int MaxBodyLength = 2000;

    public string Name => "execute_http_request";

    public string Description =>
        "Execute an HTTP request and return the response. Use this to test APIs, fetch data, or interact with web services.";

    public string ParametersSchemaJson => """
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
        """;

    public async Task<ToolResult> ExecuteAsync(string argumentsJson, CancellationToken ct)
    {
        ExecuteHttpRequestArgs? args;
        try
        {
            args = JsonSerializer.Deserialize(argumentsJson, AiJsonContext.Default.ExecuteHttpRequestArgs);
        }
        catch (Exception ex)
        {
            return new ToolResult(
                JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = $"Invalid arguments: {ex.Message}" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError: true);
        }

        if (args is null || string.IsNullOrWhiteSpace(args.Url))
        {
            return new ToolResult(
                JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = "URL is required" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError: true);
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

            var response = await HttpClient.SendAsync(request, ct);
            sw.Stop();

            var body = await response.Content.ReadAsStringAsync(ct);
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

            return new ToolResult(
                JsonSerializer.Serialize(result, AiJsonContext.Default.HttpToolResponse));
        }
        catch (TaskCanceledException)
        {
            return new ToolResult(
                JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = "Request timed out (30s)" },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError: true);
        }
        catch (Exception ex)
        {
            return new ToolResult(
                JsonSerializer.Serialize(
                    new ToolErrorResponse { Error = ex.Message },
                    AiJsonContext.Default.ToolErrorResponse),
                IsError: true);
        }
    }
}
