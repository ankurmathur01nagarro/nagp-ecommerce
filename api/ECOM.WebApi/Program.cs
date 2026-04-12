using System.Threading.RateLimiting;
using ECOM.WebApi.Auth;
using ECOM.WebApi.Data;
using ECOM.WebApi.Infrastructure;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Validation.AspNetCore;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;
using Yarp.ReverseProxy.Transforms.Builder;

var builder = WebApplication.CreateBuilder(args);

// OpenTelemetry — tracing, metrics, logging via OTLP
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "ecom-web-api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddSource("Yarp.ReverseProxy");
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
builder.RegisterDatabaseServices();
builder.Services.Configure<ExternalAuthOptions>(
    builder.Configuration.GetSection(ExternalAuthOptions.Section));

builder.Services.AddHeaderPropagation(options => options.Headers.Add("Authorization"));

// HttpClient for calling the Identity API token endpoint.
// Header propagation forwards the user's Authorization header to the identity server.
builder.Services.AddHttpClient("identity", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["IdentityApi:BaseUrl"]!);
}).AddHeaderPropagation();
builder.Services.AddScoped<IIdentityService, IdentityService>();

// OpenIddict token validation (reads Bearer token from Authorization header)
builder.Services.AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(builder.Configuration["OpenIddict:Issuer"]!);
        options.AddAudiences("ecom-api");
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
builder.Services.AddAuthorization();

// YARP — proxies the browser-facing Identity API paths and the image proxy route.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransformFactory<ImgproxyTransformFactory>();

// HybridCache — L1 in-process memory cache (no secondary store).
// Used by ImageLookupService to cache ProductApi image-record lookups.
builder.Services.AddHybridCache();

// HttpClient for ProductApi (image catalog lookup).
// No header propagation: called from HybridCache's background factory which has
// no active HTTP request context — HeaderPropagationMessageHandler would throw.
builder.Services.AddHttpClient("product-api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ProductApi:BaseUrl"]!);
});
builder.Services.AddSingleton<IImageLookupService, ImageLookupService>();

// Trust X-Forwarded-* headers from the ingress controller so Request.Host / Scheme
// reflect the public-facing hostname rather than the internal pod address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                               | ForwardedHeaders.XForwardedHost
                               | ForwardedHeaders.XForwardedProto;
    // Clear the default whitelist — all pod-to-pod traffic inside the cluster is trusted.
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

// Brute-force protection on the login endpoint: max 10 attempts per IP per minute
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("login", config =>
    {
        config.Window = TimeSpan.FromMinutes(1);
        config.PermitLimit = 10;
        config.QueueLimit = 0;
        config.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

var app = builder.Build();

// Must be first so every subsequent middleware sees the correct public scheme/host.
app.UseForwardedHeaders();

if (corsEnabled) app.UseCors();
app.MapOpenApi();
app.MapScalarApiReference();

app.UseRateLimiter();

// Must run after cookie promotion so the Authorization header is already set when captured.
app.UseHeaderPropagation();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapReverseProxy();

// Serve Angular static files (wwwroot populated by the unified Dockerfile).
// UseStaticFiles handles hashed asset files (JS/CSS) with far-future cache headers;
// the SPA fallback serves index.html for any path not matched by an API route above,
// enabling Angular's client-side router to handle deep-links.
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();
