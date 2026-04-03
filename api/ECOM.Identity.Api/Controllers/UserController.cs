using ECOM.Identity.Api.DataAccess;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ECOM.Identity.Api.Controllers;

[ApiController]
public class UserController(UserManager<ApplicationUser> userManager) : ControllerBase
{
    // OIDC Core §5.3 — UserInfo endpoint (GET and POST both required by spec)
    [HttpGet("~/connect/userinfo")]
    [HttpPost("~/connect/userinfo")]
    [Produces("application/json")]
    public async Task<IActionResult> Userinfo()
    {
        var result = await HttpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        if (result?.Principal is null)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var subject = result.Principal.GetClaim(Claims.Subject);

        if (subject is null)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var user = await userManager.FindByIdAsync(subject);

        if (user is null)
            return Challenge(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var claims = new Dictionary<string, object>
        {
            [Claims.Subject] = user.Id.ToString(),
            [Claims.Name] = user.UserName!,
            [Claims.Email] = user.Email!,
            [Claims.EmailVerified] = user.EmailConfirmed,
            [Claims.Role] = user.Role,
        };

        if (user.MobileNumber is not null)
            claims[Claims.PhoneNumber] = user.MobileNumber;

        if (user.UpdatedAt is not null)
            claims[Claims.UpdatedAt] = user.UpdatedAt.Value.ToUnixTimeSeconds();

        return Ok(claims);
    }
}
