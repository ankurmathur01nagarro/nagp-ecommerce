namespace ECOM.WebApi.Auth;

/// <summary>
/// Strongly-typed options for the "ExternalAuth" configuration section.
/// </summary>
public sealed class ExternalAuthOptions
{
    public const string Section = "ExternalAuth";

    /// <summary>SPA path to redirect to on successful external login. Token, username, expiresAt, and returnPath are appended as query parameters.</summary>
    public string SuccessCallbackPath { get; init; } = "/auth/callback";

    /// <summary>SPA path to redirect to on failed external login. An error query parameter is appended with a human-readable reason.</summary>
    public string ErrorCallbackPath { get; init; } = "/notloggedin";
}
