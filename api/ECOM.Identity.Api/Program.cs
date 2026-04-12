using System.Security.Claims;
using ECOM.Identity.Api.DataAccess;
using ECOM.Identity.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.Http.Extensions;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Client.WebIntegration;
using OpenIddict.Server.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry — tracing, metrics, logging via OTLP
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "ecom-identity-api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddRuntimeInstrumentation();
        metrics.AddMeter("Microsoft.AspNetCore.Hosting");
        metrics.AddMeter("Microsoft.AspNetCore.Server.Kestrel");
    })
    .UseOtlpExporter();

var corsConfig = builder.Configuration.GetSection("Cors");
var corsEnabled = corsConfig.GetValue<bool>("Enabled");
var allowedOrigins = corsConfig.GetSection("AllowedOrigins").Get<string[]>() ?? [];

if (corsEnabled)
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
    });
}

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.SetupOpenIddict();

// Trust X-Forwarded-* headers from YARP (running inside WebApi) so that
// GetEncodedUrl() in /connect/authorize returns the public hostname rather than
// the internal pod address. Pod-to-pod traffic is trusted, so all sources are allowed.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedHost
                               | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddHeaderPropagation(options => options.Headers.Add("Authorization"));
builder.Services.ConfigureHttpClientDefaults(http => http.AddHeaderPropagation());

var app = builder.Build();
if (corsEnabled) app.UseCors();

// Must run first so every subsequent middleware sees the correct public scheme/host.
app.UseForwardedHeaders();
app.UseHeaderPropagation();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// ─── External login endpoints ────────────────────────────────────────────────

// Called by Google after the user consents.
// OpenIddict's client middleware has already validated the authorization code
// and fetched userinfo from Google — the result is available via AuthenticateAsync.
// We provision/link the local user, then set a transient cookie so that the browser's
// next request to /connect/authorize can identify who just logged in.
app.MapMethods("api/auth/google/callback", [HttpMethods.Get, HttpMethods.Post],
    async (HttpContext context, UserManager<ApplicationUser> userManager) =>
    {
        // Read the Google identity that OpenIddict's client already validated.
        // Must use the OpenIddict client scheme — the provider name is not an auth scheme.
        var googleResult = await context.AuthenticateAsync(
            OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

        if (googleResult?.Principal is null)
            return Results.BadRequest("Google authentication failed.");

        var googleSub = googleResult.Principal.FindFirstValue(Claims.Subject)
            ?? googleResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = googleResult.Principal.FindFirstValue(Claims.Email)
            ?? googleResult.Principal.FindFirstValue(ClaimTypes.Email);

        if (googleSub is null || email is null)
            return Results.BadRequest("Required claims (sub, email) missing from Google response.");

        // Find an existing user linked to this Google account via AspNetUserLogins table
        var user = await userManager.FindByLoginAsync("Google", googleSub);

        if (user is null)
        {
            // First time — provision a new local user from the Google identity
            user = await userManager.FindByEmailAsync(email);

            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    Role = "Customer",
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                var createResult = await userManager.CreateAsync(user);
                if (!createResult.Succeeded)
                    return Results.Problem("Failed to create user account.");
            }

            // Link the Google login to the local user for future sign-ins
            var addLoginResult = await userManager.AddLoginAsync(
                user, new UserLoginInfo("Google", googleSub, "Google"));
            if (!addLoginResult.Succeeded)
                return Results.Problem("Failed to link Google account.");
        }

        // Set a transient cookie carrying only the local user ID.
        // This is consumed on the very next request to /connect/authorize — not a long-lived session.
        var identity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));

        var authProperties = new AuthenticationProperties
        {
            // Redirect back to the /connect/authorize URL that originally triggered the Google challenge
            RedirectUri = googleResult.Properties?.RedirectUri ?? "/connect/authorize"
        };

        return Results.SignIn(new ClaimsPrincipal(identity), authProperties,
            CookieAuthenticationDefaults.AuthenticationScheme);
    });

// The OpenIddict authorization endpoint — entry point for the authorization code flow.
// Called twice:
//   1. First call (no cookie) → challenge to Google, which eventually comes back here.
//   2. Second call (cookie set by callback above) → build principal, issue authorization code.
//
// ExternalAuth:PostCallbackBaseUrl is the public base URL of the YARP proxy (e.g. http://localhost:5001).
// We append /api/connect/authorize so the browser returns via the YARP route after Google consent.
var proxyBaseUrl = app.Configuration["ExternalAuth:PostCallbackBaseUrl"];
app.MapMethods("connect/authorize", [HttpMethods.Get, HttpMethods.Post],
    async (HttpContext context, UserManager<ApplicationUser> userManager) =>
    {
        // Check whether the browser already has a local session from the Google callback
        var cookieResult = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (cookieResult?.Principal is null || cookieResult.Principal.Identity?.IsAuthenticated != true)
        {
            // Build the redirect URI that points back through the YARP proxy (/api/connect/authorize)
            // so that after Google returns, the browser hits YARP rather than Identity directly.
            // ExternalAuth:PostCallbackBaseUrl already holds the public proxy host (e.g. http://localhost:5001).
            var encodedUrl = !string.IsNullOrEmpty(proxyBaseUrl)
                ? $"{proxyBaseUrl}/api/connect/authorize{context.Request.QueryString}"
                : context.Request.GetEncodedUrl();

            var properties = new AuthenticationProperties
            {
                RedirectUri = encodedUrl
            };
            return Results.Challenge(properties,
                [OpenIddictClientWebIntegrationConstants.Providers.Google]);
        }

        // Transient cookie present — load the local user and issue our authorization code
        var userIdValue = cookieResult.Principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = await userManager.FindByIdAsync(userIdValue!);
        if (user is null)
            return Results.Forbid();

        // Delete the transient cookie — it served its purpose
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        // Build the ClaimsPrincipal that OpenIddict's server will embed in the authorization code.
        // authenticationType must match the OpenIddict server scheme so it is treated as authenticated.
        var identity = new ClaimsIdentity(
            authenticationType: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity.AddClaim(new Claim(Claims.Subject, user.Id.ToString()));
        identity.AddClaim(new Claim(Claims.Name, user.UserName!)
            .SetDestinations(Destinations.AccessToken));
        identity.AddClaim(new Claim(Claims.Email, user.Email!)
            .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
        identity.AddClaim(new Claim(Claims.Role, user.Role)
            .SetDestinations(Destinations.AccessToken));

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes("api");
        principal.SetResources("ecom-api");

        // SignIn against the OpenIddict server scheme — this produces the authorization code
        // that WebApi will exchange for a JWT at /connect/token
        return Results.SignIn(principal, properties: null,
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    });

app.Run();
