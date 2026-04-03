using ECOM.Identity.Api.DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ECOM.Identity.Api.Controllers;

public record RegisterRequest(
    string Username,
    string Email,
    string Password,
    string? MobileNumber);

[ApiController]
[Route("api/register")]
public partial class RegisterController(
    UserManager<ApplicationUser> userManager,
    ILogger<RegisterController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Username, email, and password are required." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            MobileNumber = request.MobileNumber,
            Role = "user",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            if (result.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail"))
                return Conflict(new { error = "A user with that username or email already exists." });

            return BadRequest(new { error = result.Errors.First().Description });
        }

        LogUserRegistered(request.Username);
        return StatusCode(StatusCodes.Status201Created, new { user.UserName, user.Email });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "New user registered: '{Username}'.")]
    private partial void LogUserRegistered(string username);
}
