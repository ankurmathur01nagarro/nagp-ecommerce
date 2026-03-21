using Microsoft.EntityFrameworkCore;

namespace ECOM.Identity.Api.DataAccess;

/// <summary>
/// OpenIddict needs its own tables (OpenIddictApplications,
/// OpenIddictAuthorizations, OpenIddictScopes, OpenIddictTokens).
/// You wire this into a DbContext
/// </summary>
/// <param name="options"></param>
public class IdentityDbContext(DbContextOptions<IdentityDbContext> options)
    : DbContext(options)
{
    public DbSet<Users> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict(); // registers the 4 OpenIddict entity sets
    }
}