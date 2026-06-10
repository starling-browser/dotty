using Dotty.AI.Models;

namespace Dotty.AI.Providers;

public enum AuthState
{
    NotConfigured,
    Authenticating,
    Authenticated,
    Failed
}

public interface IChatProvider
{
    string ProviderId { get; }
    string DisplayName { get; }
    AuthState AuthState { get; }
    IReadOnlyList<string> AvailableModels { get; }

    IAsyncEnumerable<ChatCompletionChunk> StreamCompletionAsync(
        IReadOnlyList<ChatMessage> messages,
        string model,
        IReadOnlyList<ToolDefinition>? tools = null,
        CancellationToken cancellationToken = default);

    Task AuthenticateAsync(CancellationToken cancellationToken = default);

    event Action<AuthState>? AuthStateChanged;
}
