using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ECOM.Identity.Api.DataAccess;

public class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder); // must come first — sets up all Identity table mappings

        // Keep existing table name; Identity defaults to "AspNetUsers"
        builder.Entity<ApplicationUser>().ToTable("Users");

        // EF migration will rename the existing "Username" column to "UserName"
        // to match the standard Identity convention

        builder.Entity<IdentityRole<int>>().ToTable("UserRoles");
        builder.Entity<IdentityUserRole<int>>().ToTable("UserRoleMappings");
        builder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("AspNetUserLogins");
        builder.Entity<IdentityUserToken<int>>().ToTable("AspNetUserTokens");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaimMappings");

        builder.UseOpenIddict();
    }
}
