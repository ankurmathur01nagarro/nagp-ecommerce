using System.Threading.RateLimiting;
using ECOM.WebApi.Auth;
using ECOM.WebApi.Data;
using Microsoft.AspNetCore.RateLimiting;
using OpenIddict.Validation.AspNetCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.RegisterDatabaseServices();

// HttpClient for calling the Identity API token endpoint
builder.Services.AddHttpClient("identity", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["IdentityApi:BaseUrl"]!);
});
builder.Services.AddScoped<ITokenService, TokenService>();

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

app.MapOpenApi();
app.MapScalarApiReference();

app.UseHttpsRedirection();
app.UseRateLimiter();

// Promote the ecom_auth cookie to an Authorization header so OpenIddict
// validation middleware can verify the token without any changes to that pipeline.
app.Use(async (context, next) =>
{
    if (!context.Request.Headers.ContainsKey("Authorization")
        && context.Request.Cookies.TryGetValue("ecom_auth", out var token))
    {
        context.Request.Headers.Authorization = $"Bearer {token}";
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
