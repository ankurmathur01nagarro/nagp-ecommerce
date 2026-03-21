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
            options.UseOpenIddict(); // tells EF to use OpenIddict entity config
        })
        .AddOpenIddict()
        .AddCore(options =>
        {
            options.UseEntityFrameworkCore()
                .UseDbContext<IdentityDbContext>();
        })
        .AddServer(options =>
        {
            options.SetTokenEndpointUris("/connect/token");

            options.RegisterScopes("api");

            options.AllowClientCredentialsFlow();
            options.AllowPasswordFlow();

            // In dev, use ephemeral keys (don't persist between restarts)
            options.AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();

            // In prod, use real certs:
            // options.AddEncryptionCertificate(...)
            // options.AddSigningCertificate(...)

            options.UseAspNetCore()
                .EnableTokenEndpointPassthrough(); // routes /connect/token to your controller
        })
        .AddValidation(options =>
        {
            options.UseLocalServer(); // validate tokens issued by this same server
            options.UseAspNetCore();
        });

        builder.Services.AddSingleton<IPasswordHasher<Users>, PasswordHasher<Users>>();
        builder.Services.AddHostedService<ClientSeeder>();
    }
}