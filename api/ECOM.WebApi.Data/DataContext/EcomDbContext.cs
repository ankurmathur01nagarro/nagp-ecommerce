using Microsoft.EntityFrameworkCore;
using ECOM.WebApi.Data.DataModels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace ECOM.WebApi.Data;

public class EcomDbContext(DbContextOptions<EcomDbContext> options)
    : DbContext(options)
{
    public DbSet<Users> Users { get; set; }
    public DbSet<ProductCategory> ProductCategories { get; set; }
    public DbSet<Products> Products { get; set; }
    public DbSet<ProductImages> ProductImages { get; set; }
    public DbSet<ProductInventory> ProductInventories { get; set; }
    public DbSet<UserProductCart> UserProductCarts { get; set; }
}

public static class StartupExtensions
{
    public static void RegisterDatabaseServices(this IHostApplicationBuilder app)
    {
        var services = app.Services;
        var isDev = app.Environment.IsDevelopment();
        services.AddDbContextPool<EcomDbContext>(o =>
        {
            o.UseNpgsql(
                app.Configuration.GetConnectionString("PostgresConnString"),
                pg =>
                {
                    pg.SetPostgresVersion(17, 0);
                })
            .EnableDetailedErrors(isDev)
            .EnableSensitiveDataLogging(isDev);
        });
    }
}