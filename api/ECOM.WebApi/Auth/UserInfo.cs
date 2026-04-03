namespace ECOM.WebApi.Auth;

public record UserInfo(
    string Sub,
    string Name,
    string Email,
    bool EmailVerified,
    string? PhoneNumber,
    string Role);
