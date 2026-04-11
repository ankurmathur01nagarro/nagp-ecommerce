using ECOM.InventoryApi.Data;
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
    services.AddDbContext<InventoryDbContext>(o =>
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
var logger = loggerFactory.CreateLogger("InventoryMigration");
logger.LogInformation("Starting InventoryApi database migration");

using var scope = app.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
await db.Database.MigrateAsync();

logger.LogInformation("InventoryApi database migration completed");
