using ECOM.InventoryApi.Data.DataModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ECOM.InventoryApi.Data;

public class InventoryDbContext(DbContextOptions<InventoryDbContext> options)
    : DbContext(options)
{
    public DbSet<Inventory> Inventories { get; set; }
    public DbSet<Offer> Offers { get; set; }
    public DbSet<Cart> Carts { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Inventory>(entity =>
        {
            entity.HasIndex(i => i.ProductId).IsUnique();
            entity.HasIndex(i => i.Sku);
        });

        modelBuilder.Entity<Offer>(entity =>
        {
            entity.HasIndex(o => o.ProductId);
            entity.HasIndex(o => new { o.IsActive, o.StartsAt, o.EndsAt });
            entity.Property(o => o.DiscountValue).HasColumnType("numeric(10,2)");
        });

        modelBuilder.Entity<Cart>(entity =>
        {
            entity.HasIndex(c => c.UserId).IsUnique();
        });
    }
}

public static class StartupExtensions
{
    public static void RegisterDatabaseServices(this IHostApplicationBuilder app)
    {
        var services = app.Services;
        var isDev = app.Environment.IsDevelopment();
        services.AddDbContextPool<InventoryDbContext>(o =>
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
