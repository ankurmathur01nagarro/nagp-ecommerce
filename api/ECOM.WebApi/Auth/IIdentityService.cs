namespace ECOM.WebApi.Auth;

public interface IIdentityService
{
    Task<TokenResult> GetTokenAsync(string username, string password, CancellationToken ct);
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<UserInfoResult> GetUserInfoAsync(CancellationToken ct);

    /// <summary>
    /// Exchanges a Google OAuth authorization code for a local JWT.
    /// Performs a server-to-server grant_type=authorization_code POST to the Identity API.
    /// </summary>
    /// <param name="code">The authorization code received from Identity API at the /complete redirect.</param>
    /// <param name="redirectUri">Must exactly match the redirect_uri used when starting the flow.</param>
    Task<ExternalLoginResult> ExchangeExternalCodeAsync(string code, string redirectUri, CancellationToken ct);
}
