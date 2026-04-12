using Dapper;
using Npgsql;

namespace ECOM.ProductApi.Data.Repositories;

public class CategoryRepository(NpgsqlDataSource dataSource) : ICategoryRepository
{
    public async Task<List<FlatCategory>> GetAllAsync(CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT "Id", "Name", "ParentCategoryId"
            FROM "ProductCategories"
            ORDER BY "Id"
            """;

        var cmd = new CommandDefinition(sql, cancellationToken: ct);
        var rows = await conn.QueryAsync<FlatCategory>(cmd);
        return rows.ToList();
    }
}
