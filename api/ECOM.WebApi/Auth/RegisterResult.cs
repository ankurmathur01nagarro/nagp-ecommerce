namespace ECOM.WebApi.Auth;

public record RegisterResult(bool Success, string? Error, bool IsConflict = false);
