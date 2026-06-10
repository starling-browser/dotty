using Dotty.AI.Models;
using Dotty.AI.Providers;
using Dotty.AI.Providers.Copilot;
using Dotty.AI.Tools;

namespace Dotty.AI;

/// <summary>
/// Coordinates provider + tools in an agentic loop.
/// Handles streaming, tool call accumulation, execution, and follow-up rounds.
/// </summary>
public class ChatOrchestrator
{
    private const int MaxToolRounds = 5;

    private readonly IChatProvider _provider;
    private readonly ChatSession _session = new();
    private readonly List<IChatToolHandler> _toolHandlers = [];
    private string _model = "gpt-4o";
    private string? _systemPrompt;
    private bool _isBusy;

    public AuthState AuthState => _provider.AuthState;
    public bool IsBusy => _isBusy;

    public event Action<string>? StreamingTextReceived;
    public event Action? StreamingCompleted;
    public event Action<string, string>? ToolCallStarted; // name, args
    public event Action<string, string>? ToolCallCompleted; // name, result summary
    public event Action<string>? ErrorOccurred;
    public event Action<AuthState>? AuthStateChanged;

    public CopilotChatProvider? CopilotProvider => _provider as CopilotChatProvider;

    public ChatOrchestrator(IChatProvider provider)
    {
        _provider = provider;
        _provider.AuthStateChanged += state => AuthStateChanged?.Invoke(state);
    }

    public void RegisterToolHandler(IChatToolHandler handler)
    {
        _toolHandlers.Add(handler);
    }

    public void SetSystemPrompt(string prompt)
    {
        _systemPrompt = prompt;
    }

    public void SetModel(string model)
    {
        _model = model;
    }

    public Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        return _provider.AuthenticateAsync(cancellationToken);
    }

    public void ClearSession()
    {
        _session.Clear();
    }

    public async Task SendMessageAsync(string userMessage, CancellationToken cancellationToken = default)
    {
        if (_isBusy) return;
        _isBusy = true;

        try
        {
            // Ensure system prompt is first message
            if (_systemPrompt != null && _session.Messages.Count == 0)
                _session.AddSystemMessage(_systemPrompt);

            _session.AddUserMessage(userMessage);

            // Gather all tool definitions
            var toolDefs = new List<ToolDefinition>();
            foreach (var handler in _toolHandlers)
                toolDefs.AddRange(handler.GetToolDefinitions());

            for (int round = 0; round < MaxToolRounds; round++)
            {
                var contentBuilder = new System.Text.StringBuilder();
                var toolCallAccumulator = new Dictionary<int, AccumulatedToolCall>();
                bool hasToolCalls = false;

                await foreach (var chunk in _provider.StreamCompletionAsync(
                    _session.Messages, _model, toolDefs.Count > 0 ? toolDefs : null, cancellationToken))
                {
                    if (chunk.ContentDelta != null)
                    {
                        contentBuilder.Append(chunk.ContentDelta);
                        StreamingTextReceived?.Invoke(chunk.ContentDelta);
                    }

                    if (chunk.ToolCalls is { Count: > 0 })
                    {
                        hasToolCalls = true;
                        foreach (var tc in chunk.ToolCalls)
                        {
                            if (!toolCallAccumulator.TryGetValue(tc.Index, out var acc))
                            {
                                acc = new AccumulatedToolCall();
                                toolCallAccumulator[tc.Index] = acc;
                            }

                            if (tc.Id != null) acc.Id = tc.Id;
                            if (tc.FunctionName != null) acc.FunctionName = tc.FunctionName;
                            if (tc.ArgumentsDelta != null) acc.ArgumentsBuilder.Append(tc.ArgumentsDelta);
                        }
                    }
                }

                if (!hasToolCalls || toolCallAccumulator.Count == 0)
                {
                    // No tool calls — final response
                    _session.AddAssistantMessage(contentBuilder.ToString());
                    StreamingCompleted?.Invoke();
                    return;
                }

                // Build tool calls for the session
                var chatToolCalls = new List<ChatToolCall>();
                foreach (var (_, acc) in toolCallAccumulator.OrderBy(kv => kv.Key))
                {
                    chatToolCalls.Add(new ChatToolCall
                    {
                        Id = acc.Id ?? $"call_{Guid.NewGuid():N}",
                        FunctionName = acc.FunctionName ?? "",
                        ArgumentsJson = acc.ArgumentsBuilder.ToString(),
                    });
                }

                _session.AddAssistantMessage(contentBuilder.ToString(), chatToolCalls);

                // Execute each tool call
                foreach (var toolCall in chatToolCalls)
                {
                    ToolCallStarted?.Invoke(toolCall.FunctionName, toolCall.ArgumentsJson);

                    var result = await ExecuteToolCallAsync(toolCall, cancellationToken);
                    _session.AddToolResult(toolCall.Id, result.ResultJson);

                    var summary = result.IsError
                        ? $"Error: {result.ResultJson}"
                        : TruncateForDisplay(result.ResultJson, 200);
                    ToolCallCompleted?.Invoke(toolCall.FunctionName, summary);
                }

                // Continue loop — send tool results back to LLM
            }

            // Max rounds exceeded
            StreamingCompleted?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Cancelled
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(ex.Message);
        }
        finally
        {
            _isBusy = false;
        }
    }

    private async Task<ChatToolResult> ExecuteToolCallAsync(ChatToolCall toolCall, CancellationToken cancellationToken)
    {
        foreach (var handler in _toolHandlers)
        {
            var definitions = handler.GetToolDefinitions();
            if (definitions.Any(d => d.Name == toolCall.FunctionName))
            {
                return await handler.ExecuteToolAsync(
                    toolCall.Id, toolCall.FunctionName, toolCall.ArgumentsJson, cancellationToken);
            }
        }

        return new ChatToolResult
        {
            ToolCallId = toolCall.Id,
            ResultJson = $"{{\"error\": \"No handler for tool: {toolCall.FunctionName}\"}}",
            IsError = true
        };
    }

    private static string TruncateForDisplay(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..maxLength] + "...";
    }

    private class AccumulatedToolCall
    {
        public string? Id;
        public string? FunctionName;
        public readonly System.Text.StringBuilder ArgumentsBuilder = new();
    }
}
