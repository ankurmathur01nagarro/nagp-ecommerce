using ECOM.Identity.Api.DataAccess;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ECOM.Identity.Api.Infrastructure;

public static class OpenIddictExtensions
{
    public static void SetupOpenIddict(this IHostApplicationBuilder builder)
    {
        builder.Services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseNpgsql(builder.Configuration.GetConnectionString("Default"));
            options.UseOpenIddict();
        });

        builder.Services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole<int>>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddDefaultTokenProviders();

        builder.Services
            .AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>();
            })
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("/connect/token");
                options.SetUserInfoEndpointUris("/connect/userinfo");

                options.RegisterScopes("api");

                options.AllowClientCredentialsFlow();
                options.AllowPasswordFlow();

                // In dev, use ephemeral keys (don't persist between restarts)
                options.AddEphemeralEncryptionKey()
                    .AddEphemeralSigningKey();

                // Issue standard signed JWTs (not encrypted) so the WebApi resource server
                // can validate tokens remotely using the JWKS endpoint.
                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .DisableTransportSecurityRequirement();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        builder.Services.AddHostedService<ClientSeeder>();
    }
}
