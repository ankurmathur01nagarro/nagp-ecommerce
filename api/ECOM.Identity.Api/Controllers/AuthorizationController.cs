using System.Security.Claims;
using ECOM.Identity.Api.DataAccess;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ECOM.Identity.Api.Controllers;

[ApiController]
public partial class AuthorizationController(
    IdentityDbContext dbContext,
    IPasswordHasher<Users> passwordHasher,
    ILogger<AuthorizationController> logger) : ControllerBase
{
    [HttpPost("~/connect/token")]
    [Consumes("application/x-www-form-urlencoded")]
    [Produces("application/json")]
    public async Task<IActionResult> Exchange(CancellationToken ct)
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict request not found.");

        if (request.IsClientCredentialsGrantType())
        {
            return SignIn(BuildClientPrincipal(request), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrantAsync(request, ct);
        }

        return Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.UnsupportedGrantType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "The grant type is not supported."
            }),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static ClaimsPrincipal BuildClientPrincipal(OpenIddictRequest request)
    {
        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(Claims.Subject, request.ClientId!);
        identity.AddClaim(Claims.Name, request.ClientId!);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());
        return principal;
    }

    private async Task<IActionResult> HandlePasswordGrantAsync(OpenIddictRequest request, CancellationToken ct)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username!.ToLower(), ct);

        if (user is null)
        {
            LogUserNotFound(request.Username!);
            return InvalidCredentialsForbid();
        }

        var verificationResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            LogInvalidPassword(request.Username!);
            return InvalidCredentialsForbid();
        }

        if (verificationResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(ct);
            LogPasswordRehashed(request.Username!);
        }

        var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        identity.AddClaim(Claims.Subject, user.Id.ToString());
        identity.AddClaim(Claims.Name, user.Username);
        identity.AddClaim(Claims.Email, user.Email);
        identity.AddClaim(Claims.Role, user.Role);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(request.GetScopes());

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private ForbidResult InvalidCredentialsForbid() =>
        Forbid(
            new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] = "Invalid username or password."
            }),
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password grant failed: user '{Username}' not found.")]
    private partial void LogUserNotFound(string username);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Password grant failed: invalid password for user '{Username}'.")]
    private partial void LogInvalidPassword(string username);

    [LoggerMessage(Level = LogLevel.Information, Message = "Rehashed password for user '{Username}'.")]
    private partial void LogPasswordRehashed(string username);
}
