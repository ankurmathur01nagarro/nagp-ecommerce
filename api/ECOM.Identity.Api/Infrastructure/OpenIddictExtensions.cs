using System.Security.Cryptography.X509Certificates;
using ECOM.Identity.Api.DataAccess;
using Microsoft.AspNetCore.Authentication.Cookies;
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

        // A transient cookie is required to bridge the Google callback to /connect/authorize.
        // It carries the external identity for one browser round-trip only — it is not the app's auth mechanism.
        builder.Services
            .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie();

        builder.Services
            .AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<IdentityDbContext>();
            })
            // CLIENT role: manages the OAuth dance with Google on our behalf.
            // OpenIddict tracks PKCE, state, and nonce in its own DB token store.
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow();

                options.AddDevelopmentEncryptionCertificate()
                       .AddDevelopmentSigningCertificate();

                options.UseAspNetCore()
                       .DisableTransportSecurityRequirement()
                       .EnableRedirectionEndpointPassthrough(); // we handle the callback ourselves

                options.UseSystemNetHttp();
                
                options.UseWebProviders()
                       .AddGoogle(google =>
                       {
                           // The callback URI must point to the YARP proxy route on the WebApi host
                           // (/api/auth/google/callback) so the browser never needs to reach the
                           // Identity API directly. Configure per environment in appsettings.
                           var callbackUri = builder.Configuration["ExternalAuth:Google:CallbackUri"]
                               ?? "callback/login/google";

                           google
                               .SetClientId(builder.Configuration["ExternalAuth:Google:ClientId"]!)
                               .SetClientSecret(builder.Configuration["ExternalAuth:Google:ClientSecret"]!)
                               .SetRedirectUri(callbackUri)
                               .AddScopes("email", "profile");
                       });
            })
            // SERVER role: issues our own JWT to WebApi clients.
            .AddServer(options =>
            {
                // Pin the issuer to the Identity API's own base URL so that tokens always carry
                // iss=<IdentityApi base>, regardless of X-Forwarded-Host set by the YARP proxy.
                // If this is not set, OpenIddict derives the issuer from Request.Host which
                // would be the proxy host (e.g. localhost:5001) and break token validation in WebApi.
                var issuer = builder.Configuration["OpenIddict:Issuer"];
                if (!string.IsNullOrEmpty(issuer))
                    options.SetIssuer(new Uri(issuer));

                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                options.SetUserInfoEndpointUris("/connect/userinfo");

                options.RegisterScopes("api");

                options.AllowClientCredentialsFlow();
                options.AllowPasswordFlow();
                options.AllowAuthorizationCodeFlow();

                // Persist signing/encryption keys across restarts so existing tokens remain valid.
                // When running via Aspire (dev) or Kubernetes, certs are injected as base64 PFX env vars.
                // Fallback to ASP.NET development certificates only when running standalone without Aspire.
                var signingPfxBase64 = builder.Configuration["OpenIddict:SigningCertificate:PfxBase64"];
                if (!string.IsNullOrEmpty(signingPfxBase64))
                {
                    var signingCert = X509CertificateLoader.LoadPkcs12(
                        Convert.FromBase64String(signingPfxBase64),
                        builder.Configuration["OpenIddict:SigningCertificate:Password"]);

                    var encryptionCert = X509CertificateLoader.LoadPkcs12(
                        Convert.FromBase64String(builder.Configuration["OpenIddict:EncryptionCertificate:PfxBase64"]!),
                        builder.Configuration["OpenIddict:EncryptionCertificate:Password"]);

                    options.AddSigningCertificate(signingCert)
                           .AddEncryptionCertificate(encryptionCert);
                }
                else
                {
                    // Standalone dev without Aspire: use ASP.NET dev certs (persisted in user profile)
                    options.AddDevelopmentEncryptionCertificate()
                           .AddDevelopmentSigningCertificate();
                }

                // Issue standard signed JWTs (not encrypted) so the WebApi resource server
                // can validate tokens remotely using the JWKS endpoint.
                options.DisableAccessTokenEncryption();

                options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough() // we build the principal in /connect/authorize
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
