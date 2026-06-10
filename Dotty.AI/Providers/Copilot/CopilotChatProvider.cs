using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Dotty.AI.Models;
using Dotty.AI.Serialization;
using Dotty.AI.Streaming;

namespace Dotty.AI.Providers.Copilot;

/// <summary>
/// IChatProvider implementation for GitHub Copilot.
/// Streams chat completions from api.githubcopilot.com.
/// Adapted from akt-sh CopilotChatService.
/// </summary>
public class CopilotChatProvider : IChatProvider
{
    private const string CompletionUrl = "https://api.githubcopilot.com/chat/completions";

    private readonly HttpClient _httpClient;
    private readonly CopilotAuthService _authService;
    private readonly CopilotTokenStore _tokenStore;

    private static readonly string[] DefaultModels = ["gpt-4o", "claude-sonnet-4", "o3-mini"];

    public string ProviderId => "github-copilot";
    public string DisplayName => "GitHub Copilot";
    public AuthState AuthState => _authService.State;
    public IReadOnlyList<string> AvailableModels => DefaultModels;

    public string? UserCode => _authService.UserCode;
    public string? VerificationUri => _authService.VerificationUri;

    public event Action<AuthState>? AuthStateChanged;

    public CopilotChatProvider()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _tokenStore = new CopilotTokenStore();
        _authService = new CopilotAuthService(_httpClient, _tokenStore);
        _authService.AuthStateChanged += state => AuthStateChanged?.Invoke(state);
    }

    public async Task InitializeAsync()
    {
        await _authService.InitializeAsync();
    }

    public Task AuthenticateAsync(CancellationToken cancellationToken = default)
    {
        return _authService.StartDeviceCodeFlowAsync(cancellationToken);
    }

    public async IAsyncEnumerable<ChatCompletionChunk> StreamCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        IReadOnlyList<ToolDefinition>? tools = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var token = await _authService.GetCopilotTokenAsync(cancellationToken);
        if (token is null)
        {
            yield return new ChatCompletionChunk { ContentDelta = "Error: Not authenticated. Please connect to GitHub Copilot.", IsFinished = true };
            yield break;
        }

        var requestBody = BuildRequest(messages, model, tools);
        var json = JsonSerializer.Serialize(requestBody, AiJsonContext.Default.CopilotCompletionRequest);

        var request = new HttpRequestMessage(HttpMethod.Post, CompletionUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Add("copilot-integration-id", "vscode-chat");
        request.Headers.Add("editor-version", "Neovim/0.6.1");
        request.Headers.Add("openai-intent", "conversation-agent");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Dotty", "1.0"));

        HttpResponseMessage? response = null;
        string? sendError = null;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            sendError = ex.Message;
        }

        if (sendError != null)
        {
            yield return new ChatCompletionChunk { ContentDelta = $"Error: {sendError}", IsFinished = true };
            yield break;
        }

        if (!response!.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            yield return new ChatCompletionChunk
            {
                ContentDelta = $"Error {(int)response.StatusCode}: {errorBody}",
                IsFinished = true
            };
            yield break;
        }

        var stream = await response.Content.ReadAsStreamAsync(cancellationToken);

        await foreach (var payload in SseParser.ParseAsync(stream, cancellationToken))
        {
            SseChunkResponse? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize(payload, AiJsonContext.Default.SseChunkResponse);
            }
            catch
            {
                continue;
            }

            if (chunk?.Choices is not { Count: > 0 })
                continue;

            var choice = chunk.Choices[0];
            var delta = choice.Delta;

            var result = new ChatCompletionChunk
            {
                ContentDelta = delta?.Content,
                IsFinished = choice.FinishReason != null,
            };

            if (delta?.ToolCalls is { Count: > 0 })
            {
                result.ToolCalls = [];
                foreach (var tc in delta.ToolCalls)
                {
                    result.ToolCalls.Add(new ToolCallDelta
                    {
                        Index = tc.Index,
                        Id = tc.Id,
                        FunctionName = tc.Function?.Name,
                        ArgumentsDelta = tc.Function?.Arguments,
                    });
                }
            }

            yield return result;
        }
    }

    private static CopilotCompletionRequest BuildRequest(
        IReadOnlyList<ChatMessage> messages,
        string model,
        IReadOnlyList<ToolDefinition>? tools)
    {
        var requestMessages = new List<CopilotRequestMessage>(messages.Count);
        foreach (var msg in messages)
        {
            var reqMsg = new CopilotRequestMessage
            {
                Role = msg.Role switch
                {
                    ChatRole.System => "system",
                    ChatRole.User => "user",
                    ChatRole.Assistant => "assistant",
                    ChatRole.Tool => "tool",
                    _ => "user"
                },
                Content = msg.Content,
                ToolCallId = msg.ToolCallId,
            };

            if (msg.ToolCalls is { Count: > 0 })
            {
                reqMsg.ToolCalls = [];
                foreach (var tc in msg.ToolCalls)
                {
                    reqMsg.ToolCalls.Add(new CopilotToolCallMessage
                    {
                        Id = tc.Id,
                        Type = "function",
                        Function = new CopilotFunctionCallMessage
                        {
                            Name = tc.FunctionName,
                            Arguments = tc.ArgumentsJson,
                        }
                    });
                }
            }

            requestMessages.Add(reqMsg);
        }

        var request = new CopilotCompletionRequest
        {
            Model = model,
            Messages = requestMessages,
            Stream = true,
        };

        if (tools is { Count: > 0 })
        {
            request.Tools = [];
            foreach (var tool in tools)
            {
                var parameters = JsonSerializer.Deserialize(
                    tool.ParametersSchemaJson,
                    AiJsonContext.Default.CopilotFunctionParameters) ?? new CopilotFunctionParameters();

                request.Tools.Add(new CopilotTool
                {
                    Type = "function",
                    Function = new CopilotFunction
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = parameters,
                    }
                });
            }
        }

        return request;
    }
}
