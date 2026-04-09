using ECOM.ProductApi.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateDefaultBuilder()
    .ConfigureLogging(o =>
    {
        o.AddConsole();
    });

builder.ConfigureServices((app, services) =>
{
    services.AddDbContext<ProductDbContext>(o =>
    {
        o.UseNpgsql(
            app.Configuration.GetConnectionString("Default"),
            pg =>
            {
                pg.SetPostgresVersion(17, 0);
            });
    });
});

var app = builder.Build();

var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger("ProductMigration");
logger.LogInformation("Starting ProductApi database migration");

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<ProductDbContext>();
await db.Database.MigrateAsync();

logger.LogInformation("ProductApi database migration completed");
