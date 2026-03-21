using ECOM.WebApi.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace ECOM.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(ITokenService tokenService) : ControllerBase
{
    private const string CookieName = "ecom_auth";

    [HttpPost("login")]
    [EnableRateLimiting("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await tokenService.GetTokenAsync(request.Username, request.Password, ct);

        if (!result.Success)
            return Unauthorized(new { error = result.Error });

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(result.ExpiresIn);

        Response.Cookies.Append(CookieName, result.AccessToken!, new CookieOptions
        {
            HttpOnly = true,          // not accessible from JS
            Secure = true,            // HTTPS only
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt,
            Path = "/"
        });

        return Ok(new LoginResponse(request.Username, expiresAt));
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = "/"
        });

        return NoContent();
    }
}
