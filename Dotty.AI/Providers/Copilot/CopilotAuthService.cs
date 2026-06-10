using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Dotty.AI.Serialization;

namespace Dotty.AI.Providers.Copilot;

/// <summary>
/// GitHub Copilot OAuth device flow + Copilot session token management.
/// Adapted from akt-sh CopilotAuthService.
/// </summary>
public class CopilotAuthService
{
    private const string ClientId = "Iv1.b507a08c87ecfe98";
    private const string DeviceCodeUrl = "https://github.com/login/device/code";
    private const string TokenPollUrl = "https://github.com/login/oauth/access_token";
    private const string CopilotTokenUrl = "https://api.github.com/copilot_internal/v2/token";

    private readonly HttpClient _httpClient;
    private readonly CopilotTokenStore _tokenStore;

    public string? UserCode { get; private set; }
    public string? VerificationUri { get; private set; }
    public AuthState State { get; private set; } = AuthState.NotConfigured;

    public event Action<AuthState>? AuthStateChanged;

    public CopilotAuthService(HttpClient httpClient, CopilotTokenStore tokenStore)
    {
        _httpClient = httpClient;
        _tokenStore = tokenStore;
    }

    public async Task InitializeAsync()
    {
        var token = await _tokenStore.GetOAuthTokenAsync();
        if (token != null)
        {
            SetState(AuthState.Authenticated);
        }
    }

    /// <summary>
    /// Runs the full device code flow: request code → user authorizes → poll for token → get Copilot token.
    /// </summary>
    public async Task<bool> StartDeviceCodeFlowAsync(CancellationToken cancellationToken = default)
    {
        SetState(AuthState.Authenticating);

        // Step 1: Request device code
        var deviceCodeRequest = new FormUrlEncodedContent([
            new KeyValuePair<string, string>("client_id", ClientId),
            new KeyValuePair<string, string>("scope", "read:user"),
        ]);

        var dcResponse = await _httpClient.PostAsync(DeviceCodeUrl, deviceCodeRequest, cancellationToken);
        if (!dcResponse.IsSuccessStatusCode)
        {
            SetState(AuthState.Failed);
            return false;
        }

        var dcJson = await dcResponse.Content.ReadAsStringAsync(cancellationToken);
        var deviceCode = JsonSerializer.Deserialize(dcJson, AiJsonContext.Default.DeviceCodeResponse);
        if (deviceCode is null)
        {
            SetState(AuthState.Failed);
            return false;
        }

        UserCode = deviceCode.UserCode;
        VerificationUri = deviceCode.VerificationUri;

        // Step 2: Poll for authorization
        int interval = Math.Max(deviceCode.Interval, 5);
        var deadline = DateTimeOffset.UtcNow.AddSeconds(deviceCode.ExpiresIn);

        while (DateTimeOffset.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken);

            var pollRequest = new FormUrlEncodedContent([
                new KeyValuePair<string, string>("client_id", ClientId),
                new KeyValuePair<string, string>("device_code", deviceCode.DeviceCode),
                new KeyValuePair<string, string>("grant_type", "urn:ietf:params:oauth:grant-type:device_code"),
            ]);

            var pollResponse = await _httpClient.PostAsync(TokenPollUrl, pollRequest, cancellationToken);
            var pollJson = await pollResponse.Content.ReadAsStringAsync(cancellationToken);
            var pollResult = JsonSerializer.Deserialize(pollJson, AiJsonContext.Default.TokenPollResponse);

            if (pollResult?.AccessToken != null)
            {
                await _tokenStore.SetOAuthTokenAsync(pollResult.AccessToken);
                SetState(AuthState.Authenticated);
                return true;
            }

            if (pollResult?.Error == "slow_down")
            {
                interval = pollResult.Interval ?? interval + 5;
            }
            else if (pollResult?.Error != "authorization_pending")
            {
                // Unexpected error (expired_token, access_denied, etc.)
                SetState(AuthState.Failed);
                return false;
            }
        }

        SetState(AuthState.Failed);
        return false;
    }

    /// <summary>
    /// Gets a Copilot session token, using cache or fetching fresh.
    /// </summary>
    public async Task<string?> GetCopilotTokenAsync(CancellationToken cancellationToken = default)
    {
        var cached = _tokenStore.GetCopilotToken();
        if (cached != null)
            return cached;

        var oauthToken = await _tokenStore.GetOAuthTokenAsync();
        if (oauthToken is null)
            return null;

        var request = new HttpRequestMessage(HttpMethod.Get, CopilotTokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", oauthToken);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Dotty", "1.0"));

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // OAuth token expired — clear and require re-auth
            await _tokenStore.ClearOAuthTokenAsync();
            SetState(AuthState.NotConfigured);
            return null;
        }

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonSerializer.Deserialize(json, AiJsonContext.Default.CopilotTokenResponse);
        if (tokenResponse is null)
            return null;

        _tokenStore.SetCopilotToken(tokenResponse.Token, tokenResponse.ExpiresAt);
        return tokenResponse.Token;
    }

    public async Task SignOutAsync()
    {
        await _tokenStore.ClearOAuthTokenAsync();
        UserCode = null;
        VerificationUri = null;
        SetState(AuthState.NotConfigured);
    }

    private void SetState(AuthState state)
    {
        State = state;
        AuthStateChanged?.Invoke(state);
    }
}
