namespace ECOM.WebApi.Auth;

public record UserInfoResult(bool Success, UserInfo? UserInfo, string? Error);
