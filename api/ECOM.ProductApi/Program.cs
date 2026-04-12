using ECOM.ProductApi.Data;
using ECOM.ProductApi.Data.Repositories;
using ECOM.ProductApi.Infrastructure;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// CORS — everything allowed by default
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// OpenTelemetry — tracing, metrics, logging via OTLP
builder.Logging.AddOpenTelemetry(logging =>
{
    logging.IncludeFormattedMessage = true;
    logging.IncludeScopes = true;
});

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService(serviceName: "ecom-product-api"))
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

builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.RegisterDatabaseServices();

// Image storage — writes to PVC mount; same LocalImageStorage works in both local and cloud clusters.
// On cloud, back the PVC with a managed RWX volume (AWS EFS, GCP Filestore, Azure Files).
builder.Services.Configure<StorageOptions>(builder.Configuration.GetSection(StorageOptions.Section));
builder.Services.AddScoped<IImageStorage, LocalImageStorage>();

// NpgsqlDataSource for Dapper raw queries (JSONB)
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("Default")!));
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IImageCatalogRepository, ImageCatalogRepository>();
builder.Services.AddScoped<ICategoryRepository, CategoryRepository>();

var app = builder.Build();

app.UseCors();
app.MapOpenApi();
app.MapScalarApiReference();

app.MapControllers();

app.Run();
