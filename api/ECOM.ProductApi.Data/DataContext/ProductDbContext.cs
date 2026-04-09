using ECOM.ProductApi.Data.DataModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace ECOM.ProductApi.Data;

public class ProductDbContext(DbContextOptions<ProductDbContext> options)
    : DbContext(options)
{
    public DbSet<Product> Products { get; set; }
    public DbSet<ProductCategory> ProductCategories { get; set; }
}

public static class StartupExtensions
{
    public static void RegisterDatabaseServices(this IHostApplicationBuilder app)
    {
        var services = app.Services;
        var isDev = app.Environment.IsDevelopment();
        services.AddDbContextPool<ProductDbContext>(o =>
        {
            o.UseNpgsql(
                app.Configuration.GetConnectionString("Default"),
                pg =>
                {
                    pg.SetPostgresVersion(17, 0);
                })
            .EnableDetailedErrors(isDev)
            .EnableSensitiveDataLogging(isDev);
        });
    }
}
