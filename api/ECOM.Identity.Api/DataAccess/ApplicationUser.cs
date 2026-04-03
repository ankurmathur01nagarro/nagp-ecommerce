using Microsoft.AspNetCore.Identity;

namespace ECOM.Identity.Api.DataAccess;

public class ApplicationUser : IdentityUser<int>
{
    // IdentityUser<int> provides: Id, UserName, NormalizedUserName, Email,
    // NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp,
    // ConcurrencyStamp, PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled,
    // LockoutEnd, LockoutEnabled, AccessFailedCount

    public string? MobileNumber { get; set; }
    public string Role { get; set; } = "user";
    public DateTimeOffset? CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
