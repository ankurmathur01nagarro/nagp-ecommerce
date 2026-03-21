namespace ECOM.WebApi.Auth;

public record LoginResponse(string Username, DateTimeOffset ExpiresAt);
