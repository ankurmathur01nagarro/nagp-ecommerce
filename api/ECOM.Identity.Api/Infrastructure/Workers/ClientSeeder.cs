using ECOM.Identity.Api.DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace ECOM.Identity.Api.Infrastructure;

/// <summary>
/// OpenIddict stores client apps in the database. Upserts clients on startup from configuration.
/// </summary>
public class ClientSeeder(IServiceProvider serviceProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = serviceProvider.CreateScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync(ct);

        await SeedUsersAsync(scope.ServiceProvider, ct);

        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        var clientSecret = config["OpenIddict:Clients:EcomApi:ClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:Clients:EcomApi:ClientSecret is not configured.");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = "ecom-api",
            ClientType = ClientTypes.Public,
            ClientSecret = clientSecret,
            DisplayName = "ECOM Web API",
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.GrantTypes.Password,
                Permissions.Prefixes.Scope + "api"
            }
        };

        var existing = await manager.FindByClientIdAsync("ecom-api", ct);
        if (existing is null)
            await manager.CreateAsync(descriptor, ct);
        else
            await manager.UpdateAsync(existing, descriptor, ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task SeedUsersAsync(IServiceProvider services, CancellationToken ct)
    {
        var dbContext = services.GetRequiredService<IdentityDbContext>();
        var passwordHasher = services.GetRequiredService<IPasswordHasher<Users>>();

        if (!await dbContext.Users.AnyAsync(ct))
        {
            var admin = new Users
            {
                Username = "admin",
                Email = "admin@ecom.local",
                Role = "Admin",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                PasswordHash = string.Empty
            };
            admin.PasswordHash = passwordHasher.HashPassword(admin, "Admin@123");

            dbContext.Users.Add(admin);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}
