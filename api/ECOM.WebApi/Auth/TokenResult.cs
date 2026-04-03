namespace ECOM.WebApi.Auth;

public record TokenResult(
    bool Success,
    string? AccessToken,
    int ExpiresIn,
    string? Error);
