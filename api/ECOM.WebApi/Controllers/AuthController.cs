using ECOM.WebApi.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
// QueryHelpers — builds query strings for the Identity API authorize redirect
using Microsoft.AspNetCore.WebUtilities;

namespace ECOM.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IIdentityService tokenService) : ControllerBase
{
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await tokenService.GetTokenAsync(request.Username, request.Password, ct);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);

        return Ok(new LoginResponse(request.Username, expiresAt, result.AccessToken!));
    }

    [HttpPost("register")]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request, CancellationToken ct)
    {
        var result = await tokenService.RegisterAsync(request, ct);

        if (!result.Success)
            return result.IsConflict
                ? Conflict(new { error = result.Error })
                : BadRequest(new { error = result.Error });

        return StatusCode(StatusCodes.Status201Created, new { request.Username, request.Email });
    }

    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var result = await tokenService.GetUserInfoAsync(ct);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        return Ok(result.UserInfo);
    }

    // ─── External (Google) login ─────────────────────────────────────────────

    /// <summary>
    /// SPA navigates to this endpoint to start Google login.
    /// Builds the /connect/authorize URL (served by YARP proxy on this same host)
    /// with all required OAuth params and issues a 302 redirect.
    /// </summary>
    [HttpGet("external/challenge")]
    public IActionResult ExternalChallenge(
        [FromServices] IConfiguration configuration,
        [FromQuery] string? postLoginPath = "/")
    {
        var clientId = configuration["IdentityApi:ClientId"]!;

        // redirect_uri must exactly match the URI registered in ClientSeeder (Identity API) and
        // the value sent during the code exchange in /complete. We derive it from Request.Scheme
        // and Request.Host, which UseForwardedHeaders() has already promoted from the ingress.
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/external/complete";

        // The authorize endpoint is proxied by YARP at /api/connect/authorize on this same host.
        // Request.Host is authoritative here because UseForwardedHeaders() has already promoted
        // X-Forwarded-Host from the ingress controller.
        var authorizeUrl = QueryHelpers.AddQueryString(
            $"{Request.Scheme}://{Request.Host}/api/connect/authorize",
            new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["response_type"] = "code",
                ["scope"] = "api",
                ["redirect_uri"] = redirectUri,
                ["state"] = postLoginPath
            });

        return Redirect(authorizeUrl);
    }

    /// <summary>
    /// Identity API redirects here with the authorization code after Google consent.
    /// Exchanges the code server-to-server for a JWT and returns it to the SPA as JSON.
    /// </summary>
    [HttpGet("external/complete")]
    public async Task<IActionResult> ExternalComplete(
        [FromServices] IOptions<ExternalAuthOptions> externalAuth,
        [FromQuery] string? code,
        [FromQuery] string? error,
        [FromQuery] string? state,
        CancellationToken ct)
    {
        var successPath = externalAuth.Value.SuccessCallbackPath;
        var errorPath = externalAuth.Value.ErrorCallbackPath;
        var postLoginPath = string.IsNullOrWhiteSpace(state) ? "/" : state;

        if (error is not null)
            return Redirect(QueryHelpers.AddQueryString(
                errorPath,
                new Dictionary<string, string?> { ["error"] = error }));

        if (string.IsNullOrEmpty(code))
            return Redirect(QueryHelpers.AddQueryString(
                errorPath,
                new Dictionary<string, string?> { ["error"] = "Missing authorization code." }));

        // redirect_uri must exactly match what was sent in /challenge.
        var redirectUri = $"{Request.Scheme}://{Request.Host}/api/auth/external/complete";

        var result = await tokenService.ExchangeExternalCodeAsync(code, redirectUri, ct);

        if (!result.Success)
            return Redirect(QueryHelpers.AddQueryString(
                errorPath,
                new Dictionary<string, string?> { ["error"] = result.Error ?? "Login failed." }));

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);

        // Redirect the browser back to the SPA callback route, passing the JWT as query params.
        // The SPA's AuthorizationHandlerComponent picks these up and stores them in AuthStore.
        return Redirect(QueryHelpers.AddQueryString(
            successPath,
            new Dictionary<string, string?>
            {
                ["token"] = result.AccessToken,
                ["username"] = result.Username ?? string.Empty,
                ["expiresAt"] = expiresAt.ToString("o"),
                ["returnPath"] = postLoginPath
            }));
    }
}
