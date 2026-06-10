namespace Dotty.AI.Providers.Copilot;

/// <summary>
/// Manages both the persistent OAuth access token (keychain) and
/// the ephemeral Copilot session token (in-memory, ~30 min TTL).
/// </summary>
public class CopilotTokenStore
{
    private string? _cachedOAuthToken;
    private string? _copilotSessionToken;
    private DateTimeOffset _copilotTokenExpiry;

    public async Task<string?> GetOAuthTokenAsync()
    {
        if (_cachedOAuthToken != null)
            return _cachedOAuthToken;

        _cachedOAuthToken = await KeychainService.GetTokenAsync();
        return _cachedOAuthToken;
    }

    public async Task SetOAuthTokenAsync(string token)
    {
        _cachedOAuthToken = token;
        await KeychainService.SetTokenAsync(token);
    }

    public async Task ClearOAuthTokenAsync()
    {
        _cachedOAuthToken = null;
        _copilotSessionToken = null;
        await KeychainService.DeleteTokenAsync();
    }

    public string? GetCopilotToken()
    {
        if (_copilotSessionToken != null && DateTimeOffset.UtcNow < _copilotTokenExpiry)
            return _copilotSessionToken;

        return null;
    }

    public void SetCopilotToken(string token, long expiresAtUnix)
    {
        _copilotSessionToken = token;
        // Refresh 2 minutes before actual expiry
        _copilotTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(expiresAtUnix)
            .AddMinutes(-2);
    }

    public bool HasOAuthToken => _cachedOAuthToken != null;
}
