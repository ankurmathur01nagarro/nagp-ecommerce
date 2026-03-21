namespace ECOM.WebApi.Auth;

public interface ITokenService
{
    Task<TokenResult> GetTokenAsync(string username, string password, CancellationToken ct);
}
