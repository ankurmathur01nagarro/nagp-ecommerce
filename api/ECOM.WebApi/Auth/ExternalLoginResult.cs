namespace ECOM.WebApi.Auth;

/// <summary>
/// Result of exchanging a Google OAuth authorization code for a local JWT.
/// Returned by <see cref="IIdentityService.ExchangeExternalCodeAsync"/>.
/// </summary>
public record ExternalLoginResult(
    bool Success,
    string? AccessToken,
    int ExpiresIn,
    string? Username,
    string? Error);
