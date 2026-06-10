using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Dotty.Terminal.Mcp;

/// <summary>
/// Embedded MCP server using HttpListener with SSE transport.
/// GET /sse  → SSE stream with endpoint event
/// POST /message?sessionId=xxx → JSON-RPC dispatch
/// </summary>
public class McpHttpServer : IDisposable
{
    private readonly AppToolRegistry _registry;
    private readonly HttpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly ConcurrentDictionary<string, SseSession> _sessions = new();
    private readonly int _port;
    private Task? _listenTask;

    public int Port => _port;
    public bool IsRunning { get; private set; }
    public int SessionCount => _sessions.Count;
    public int TotalRequests => _totalRequests;
    public IReadOnlyList<McpLogEntry> RecentActivity => _recentActivity.ToArray();

    public event Action<string>? Log;
    public event Action? StateChanged;

    private int _totalRequests;
    private readonly ConcurrentQueue<McpLogEntry> _recentActivity = new();

    public McpHttpServer(AppToolRegistry registry, int port = 8257)
    {
        _registry = registry;
        _port = port;
        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
    }

    public void Start()
    {
        try
        {
            _listener.Start();
            IsRunning = true;
            Log?.Invoke($"MCP server listening on http://localhost:{_port}/");
            _listenTask = Task.Run(() => ListenLoop(_cts.Token));
        }
        catch (Exception ex)
        {
            Log?.Invoke($"MCP server failed to start: {ex.Message}");
        }
    }

    public void Stop()
    {
        if (!IsRunning) return;
        IsRunning = false;
        _cts.Cancel();

        // Close all SSE sessions
        foreach (var session in _sessions.Values)
            session.Cancel();

        try { _listener.Stop(); } catch { /* shutting down */ }
        Log?.Invoke("MCP server stopped");
    }

    public void Dispose()
    {
        Stop();
        _cts.Dispose();
        _listener.Close();
    }

    private async Task ListenLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var context = await _listener.GetContextAsync().WaitAsync(ct);
                _ = Task.Run(() => HandleRequest(context), ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log?.Invoke($"MCP listener error: {ex.Message}");
            }
        }
    }

    private async Task HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // CORS headers for browser-based MCP clients
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        try
        {
            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 204;
                response.Close();
                return;
            }

            var path = request.Url?.AbsolutePath ?? "";

            switch (path)
            {
                case "/sse" when request.HttpMethod == "GET":
                    await HandleSse(context);
                    break;

                case "/message" when request.HttpMethod == "POST":
                    await HandleMessage(context);
                    break;

                default:
                    response.StatusCode = 404;
                    var notFound = Encoding.UTF8.GetBytes("{\"error\": \"Not found\"}");
                    response.ContentType = "application/json";
                    await response.OutputStream.WriteAsync(notFound);
                    response.Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            Log?.Invoke($"MCP request error: {ex.Message}");
            try
            {
                response.StatusCode = 500;
                response.Close();
            }
            catch { /* response may already be closed */ }
        }
    }

    private async Task HandleSse(HttpListenerContext context)
    {
        var sessionId = Guid.NewGuid().ToString("N");
        var session = new SseSession(sessionId);
        _sessions[sessionId] = session;

        var response = context.Response;
        response.ContentType = "text/event-stream";
        response.Headers.Add("Cache-Control", "no-cache");
        response.Headers.Add("Connection", "keep-alive");

        var stream = response.OutputStream;

        Log?.Invoke($"MCP SSE session opened: {sessionId}");
            StateChanged?.Invoke();

        try
        {
            // Send the endpoint event per MCP SSE spec
            var endpointEvent = $"event: endpoint\ndata: /message?sessionId={sessionId}\n\n";
            var endpointBytes = Encoding.UTF8.GetBytes(endpointEvent);
            await stream.WriteAsync(endpointBytes);
            await stream.FlushAsync();

            // Keep the SSE connection alive until cancelled
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
                session.CancellationToken, _cts.Token);
            var token = linkedCts.Token;

            while (!token.IsCancellationRequested)
            {
                // Wait for a message or timeout for keepalive
                var message = await session.WaitForMessageAsync(TimeSpan.FromSeconds(30), token);

                if (message != null)
                {
                    var sseMessage = $"event: message\ndata: {message}\n\n";
                    var messageBytes = Encoding.UTF8.GetBytes(sseMessage);
                    await stream.WriteAsync(messageBytes, token);
                    await stream.FlushAsync(token);
                }
                else
                {
                    // Keepalive
                    var keepAlive = Encoding.UTF8.GetBytes(": keepalive\n\n");
                    await stream.WriteAsync(keepAlive, token);
                    await stream.FlushAsync(token);
                }
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Log?.Invoke($"MCP SSE session error: {ex.Message}");
        }
        finally
        {
            _sessions.TryRemove(sessionId, out _);
            Log?.Invoke($"MCP SSE session closed: {sessionId}");
            StateChanged?.Invoke();
            try { response.Close(); } catch { /* already closed */ }
        }
    }

    private async Task HandleMessage(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;

        var sessionId = request.QueryString["sessionId"];
        if (sessionId == null || !_sessions.TryGetValue(sessionId, out var session))
        {
            response.StatusCode = 400;
            var error = Encoding.UTF8.GetBytes("{\"error\": \"Invalid or missing sessionId\"}");
            response.ContentType = "application/json";
            await response.OutputStream.WriteAsync(error);
            response.Close();
            return;
        }

        // Read the JSON-RPC request body
        string body;
        using (var reader = new StreamReader(request.InputStream, Encoding.UTF8))
        {
            body = await reader.ReadToEndAsync();
        }

        JsonRpcRequest? rpcRequest;
        try
        {
            rpcRequest = JsonSerializer.Deserialize(body, McpJsonContext.Default.JsonRpcRequest);
        }
        catch
        {
            await SendJsonRpcError(response, null, -32700, "Parse error");
            return;
        }

        if (rpcRequest == null)
        {
            await SendJsonRpcError(response, null, -32600, "Invalid request");
            return;
        }

        Interlocked.Increment(ref _totalRequests);
        LogActivity(rpcRequest.Method, sessionId);
        Log?.Invoke($"MCP [{sessionId[..8]}] {rpcRequest.Method}");

        // Handle notifications (no id) — just acknowledge
        if (rpcRequest.Id == null)
        {
            // notifications/initialized is a common one — just acknowledge
            response.StatusCode = 202;
            response.Close();
            return;
        }

        // Dispatch based on method
        var result = rpcRequest.Method switch
        {
            "initialize" => HandleInitialize(rpcRequest),
            "tools/list" => HandleToolsList(rpcRequest),
            "tools/call" => await HandleToolsCall(rpcRequest),
            _ => CreateErrorResponse(rpcRequest.Id, -32601, $"Method not found: {rpcRequest.Method}")
        };

        // Send response back via SSE stream
        var resultJson = JsonSerializer.Serialize(result, McpJsonContext.Default.JsonRpcResponse);
        session.EnqueueMessage(resultJson);

        // Also send 202 Accepted on the POST response
        response.StatusCode = 202;
        response.Close();
    }

    private JsonRpcResponse HandleInitialize(JsonRpcRequest request)
    {
        var initResult = new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            Capabilities = new McpCapabilities
            {
                Tools = new McpToolsCapability { ListChanged = false }
            },
            ServerInfo = new McpServerInfo
            {
                Name = "dotty",
                Version = "1.0.0"
            }
        };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToElement(initResult, McpJsonContext.Default.InitializeResult),
        };
    }

    private JsonRpcResponse HandleToolsList(JsonRpcRequest request)
    {
        var tools = new List<McpToolDefinition>();

        foreach (var tool in _registry.AllTools)
        {
            var def = new McpToolDefinition
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = ParseInputSchema(tool.ParametersSchemaJson),
            };
            tools.Add(def);
        }

        var result = new ToolsListResult { Tools = tools };
        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToElement(result, McpJsonContext.Default.ToolsListResult),
        };
    }

    private async Task<JsonRpcResponse> HandleToolsCall(JsonRpcRequest request)
    {
        if (request.Params == null)
        {
            return CreateErrorResponse(request.Id, -32602, "Missing params");
        }

        ToolCallParams? callParams;
        try
        {
            callParams = JsonSerializer.Deserialize(
                request.Params.Value.GetRawText(),
                McpJsonContext.Default.ToolCallParams);
        }
        catch
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params");
        }

        if (callParams == null)
        {
            return CreateErrorResponse(request.Id, -32602, "Invalid params");
        }

        var tool = _registry.GetTool(callParams.Name);
        if (tool == null)
        {
            return CreateErrorResponse(request.Id, -32602, $"Unknown tool: {callParams.Name}");
        }

        var argumentsJson = callParams.Arguments?.GetRawText() ?? "{}";
        var toolResult = await tool.ExecuteAsync(argumentsJson, _cts.Token);

        var mcpResult = new ToolCallResult
        {
            Content = [new McpContentItem { Type = "text", Text = toolResult.ResultJson }],
            IsError = toolResult.IsError,
        };

        return new JsonRpcResponse
        {
            Id = request.Id,
            Result = JsonSerializer.SerializeToElement(mcpResult, McpJsonContext.Default.ToolCallResult),
        };
    }

    private static McpInputSchema ParseInputSchema(string schemaJson)
    {
        try
        {
            var doc = JsonDocument.Parse(schemaJson);
            var root = doc.RootElement;

            var schema = new McpInputSchema
            {
                Type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() ?? "object" : "object",
            };

            if (root.TryGetProperty("properties", out var props))
                schema.Properties = props.Clone();

            if (root.TryGetProperty("required", out var req))
            {
                schema.Required = [];
                foreach (var item in req.EnumerateArray())
                {
                    var val = item.GetString();
                    if (val != null) schema.Required.Add(val);
                }
            }

            return schema;
        }
        catch
        {
            return new McpInputSchema();
        }
    }

    private static JsonRpcResponse CreateErrorResponse(object? id, int code, string message)
    {
        return new JsonRpcResponse
        {
            Id = id,
            Error = new JsonRpcError { Code = code, Message = message },
        };
    }

    private static async Task SendJsonRpcError(HttpListenerResponse response, object? id, int code, string message)
    {
        var errorResponse = CreateErrorResponse(id, code, message);
        var json = JsonSerializer.Serialize(errorResponse, McpJsonContext.Default.JsonRpcResponse);
        var bytes = Encoding.UTF8.GetBytes(json);
        response.ContentType = "application/json";
        response.StatusCode = 200;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private void LogActivity(string method, string sessionId)
    {
        _recentActivity.Enqueue(new McpLogEntry(DateTime.UtcNow, method, sessionId[..8]));
        while (_recentActivity.Count > 20)
            _recentActivity.TryDequeue(out _);
        StateChanged?.Invoke();
    }

    private class SseSession
    {
        public string SessionId { get; }
        private readonly CancellationTokenSource _cts = new();
        private readonly ConcurrentQueue<string> _messageQueue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public CancellationToken CancellationToken => _cts.Token;

        public SseSession(string sessionId)
        {
            SessionId = sessionId;
        }

        public void EnqueueMessage(string message)
        {
            _messageQueue.Enqueue(message);
            _signal.Release();
        }

        /// <summary>
        /// Waits for a message with timeout. Returns null on timeout (keepalive needed).
        /// </summary>
        public async Task<string?> WaitForMessageAsync(TimeSpan timeout, CancellationToken ct)
        {
            var acquired = await _signal.WaitAsync(timeout, ct);
            if (acquired && _messageQueue.TryDequeue(out var message))
                return message;
            return null;
        }

        public void Cancel()
        {
            try { _cts.Cancel(); } catch { }
        }
    }
}

public record McpLogEntry(DateTime Timestamp, string Method, string SessionPrefix);
