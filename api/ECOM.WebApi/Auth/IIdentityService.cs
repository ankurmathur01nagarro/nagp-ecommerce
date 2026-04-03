namespace ECOM.WebApi.Auth;

public interface IIdentityService
{
    Task<TokenResult> GetTokenAsync(string username, string password, CancellationToken ct);
    Task<RegisterResult> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<UserInfoResult> GetUserInfoAsync(CancellationToken ct);
}
