using Microsoft.EntityFrameworkCore;
using ECOM.WebApi.Data;

public class Migrator
{
    private readonly EcomDbContext movieDbContext;

    public Migrator(EcomDbContext movieDbContext)
    {
        this.movieDbContext = movieDbContext;
    }

    public async Task Migrate()
    {
        await this.movieDbContext.Database.MigrateAsync();
    }
}