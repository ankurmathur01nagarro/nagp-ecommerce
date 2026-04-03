using ECOM.WebApi.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Validation.AspNetCore;

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
            return Conflict(new { error = result.Error });

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
}
