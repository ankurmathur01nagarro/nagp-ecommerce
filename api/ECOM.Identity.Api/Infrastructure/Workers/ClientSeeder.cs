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
        await SeedClientAsync(
            scope.ServiceProvider,
            "EcomApi",
            "ECOM Web API",
            ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    private static async Task SeedClientAsync(
        IServiceProvider services,
        string clientName,
        string clientDisplayName,
        CancellationToken ct)
    {
        var manager = services.GetRequiredService<IOpenIddictApplicationManager>();
        var config = services.GetRequiredService<IConfiguration>();

        var clientId = config[$"OpenIddict:Clients:{clientName}:ClientId"]
            ?? throw new InvalidOperationException($"OpenIddict:Clients:{clientName}:ClientId is not configured.");
        var clientSecret = config[$"OpenIddict:Clients:{clientName}:ClientSecret"]
            ?? throw new InvalidOperationException($"OpenIddict:Clients:{clientName}:ClientSecret is not configured.");

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientType = ClientTypes.Confidential,
            ClientSecret = clientSecret,
            DisplayName = clientDisplayName,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials,
                Permissions.GrantTypes.Password,
                Permissions.Prefixes.Scope + "api"
            }
        };

        var existing = await manager.FindByClientIdAsync(descriptor.ClientId, ct);
        if (existing is null)
            await manager.CreateAsync(descriptor, ct);
        else
            await manager.UpdateAsync(existing, descriptor, ct);
    }

    private static async Task SeedUsersAsync(IServiceProvider services, CancellationToken _)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (await userManager.FindByNameAsync("admin") is null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin",
                Email = "admin@ecom.local",
                Role = "Admin",
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            var result = await userManager.CreateAsync(admin, "Admin@123");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to seed admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
