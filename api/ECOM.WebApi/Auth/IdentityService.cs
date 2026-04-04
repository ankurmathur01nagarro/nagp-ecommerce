using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ECOM.WebApi.Auth;

public partial class IdentityService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<IdentityService> logger) : IIdentityService
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

    public async Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("identity");

        var payload = JsonSerializer.Serialize(new
        {
            request.Username,
            request.Email,
            request.Password,
            request.MobileNumber
        });

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync(
                "/api/register",
                new StringContent(payload, Encoding.UTF8, "application/json"),
                ct);
        }
        catch (HttpRequestException ex)
        {
            LogIdentityUnavailable(ex.Message);
            return new RegisterResult(false, "Authentication service unavailable.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.Conflict)
            return new RegisterResult(false, "A user with that username or email already exists.");

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var error = TryParseError(body);
            LogTokenRequestFailed((int)response.StatusCode, error);
            return new RegisterResult(false, error);
        }

        return new RegisterResult(true, null);
    }

    public async Task<UserInfoResult> GetUserInfoAsync(CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient("identity");

        HttpResponseMessage response;
        try
        {
            response = await client.GetAsync("/connect/userinfo", ct);
        }
        catch (HttpRequestException ex)
        {
            LogIdentityUnavailable(ex.Message);
            return new UserInfoResult(false, null, "Authentication service unavailable.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            var error = TryParseError(body);
            LogTokenRequestFailed((int)response.StatusCode, error);
            return new UserInfoResult(false, null, error);
        }

        var userInfoResponse = await response.Content.ReadFromJsonAsync<UserInfoResponse>(ct);
        if (userInfoResponse?.Sub is null)
        {
            LogMalformedResponse();
            return new UserInfoResult(false, null, "Unexpected response from authentication service.");
        }

        return new UserInfoResult(true, new UserInfo(
            userInfoResponse.Sub,
            userInfoResponse.Name ?? string.Empty,
            userInfoResponse.Email ?? string.Empty,
            userInfoResponse.EmailVerified,
            userInfoResponse.PhoneNumber,
            userInfoResponse.Role ?? string.Empty), null);
    }

    public async Task<ExternalLoginResult> ExchangeExternalCodeAsync(
        string code, string redirectUri, CancellationToken ct)
    {
        var clientId = configuration["IdentityApi:ClientId"]!;
        var clientSecret = configuration["IdentityApi:ClientSecret"]!;

        var client = httpClientFactory.CreateClient("identity");

        // Standard OAuth 2.0 authorization_code grant.
        // client_id + client_secret authenticate WebApi as a confidential client.
        // redirect_uri must match exactly what was sent to /connect/authorize.
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
        };

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(form), ct);
        }
        catch (HttpRequestException ex)
        {
            LogIdentityUnavailable(ex.Message);
            return new ExternalLoginResult(false, null, 0, null, "Authentication service unavailable.");
        }

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var error = TryParseError(body);
            LogTokenRequestFailed((int)response.StatusCode, error);
            return new ExternalLoginResult(false, null, 0, null, error);
        }

        var tokenResponse = JsonSerializer.Deserialize<TokenResponse>(body);
        if (tokenResponse?.AccessToken is null)
        {
            LogMalformedResponse();
            return new ExternalLoginResult(false, null, 0, null, "Unexpected response from authentication service.");
        }

        // Decode the username from the token's name claim without full validation —
        // we trust the token since we just received it directly from our own Identity API.
        var username = TryExtractNameClaim(tokenResponse.AccessToken);
        return new ExternalLoginResult(true, tokenResponse.AccessToken, tokenResponse.ExpiresIn, username, null);
    }

    private static string? TryExtractNameClaim(string accessToken)
    {
        try
        {
            // JWT is base64url: header.payload.signature — decode payload only
            var parts = accessToken.Split('.');
            if (parts.Length != 3) return null;
            var payload = parts[1];
            // Pad to a multiple of 4 for base64 decoding
            payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
            var doc = JsonDocument.Parse(json);
            // OpenIddict maps Claims.Name → "name" claim in the JWT
            if (doc.RootElement.TryGetProperty("name", out var nameProp))
                return nameProp.GetString();
        }
        catch { /* ignore — username is optional in the response */ }
        return null;
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

    private sealed class UserInfoResponse
    {
        [JsonPropertyName("sub")]
        public string? Sub { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("email")]
        public string? Email { get; init; }

        [JsonPropertyName("email_verified")]
        public bool EmailVerified { get; init; }

        [JsonPropertyName("phone_number")]
        public string? PhoneNumber { get; init; }

        [JsonPropertyName("role")]
        public string? Role { get; init; }
    }
}
