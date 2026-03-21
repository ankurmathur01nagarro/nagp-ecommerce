using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECOM.WebApi.Auth;

public record TokenResult(
    bool Success,
    string? AccessToken,
    int ExpiresIn,
    string? Error);

public partial class TokenService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TokenService> logger) : ITokenService
{
    public async Task<TokenResult> GetTokenAsync(string username, string password, CancellationToken ct)
    {
        var clientId = configuration["IdentityApi:ClientId"]!;
        var clientSecret = configuration["IdentityApi:ClientSecret"]!;

        var client = httpClientFactory.CreateClient("identity");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = username,
            ["password"] = password,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["scope"] = "api"
        };

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), ct);
        }
        catch (HttpRequestException ex)
        {
            LogIdentityUnavailable(ex.Message);
            return new TokenResult(false, null, 0, "Authentication service unavailable.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(body);
            LogTokenRequestFailed((int)response.StatusCode, error);
            return new TokenResult(false, null, 0, error);
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body);
        if (tokenResponse?.AccessToken is null)
        {
            LogMalformedResponse();
            return new TokenResult(false, null, 0, "Unexpected response from authentication service.");
        }

        return new TokenResult(true, tokenResponse.AccessToken, tokenResponse.ExpiresIn, null);
    }

    private static string TryParseError(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error_description", out var desc))
                return desc.GetString() ?? "Authentication failed.";
        }
        catch { /* unparseable body — fall through */ }

        return "Authentication failed.";
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity API unreachable: {Message}")]
    private partial void LogIdentityUnavailable(string message);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Token request failed with status {StatusCode}: {Error}")]
    private partial void LogTokenRequestFailed(int statusCode, string error);

    [LoggerMessage(Level = LogLevel.Error, Message = "Identity API returned a malformed token response.")]
    private partial void LogMalformedResponse();

    private sealed class TokenResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; init; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; init; }
    }
}
