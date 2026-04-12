using Dapper;
using ECOM.ProductApi.Data.DataModels;
using Npgsql;

namespace ECOM.ProductApi.Data.Repositories;

public sealed class ImageCatalogRepository(NpgsqlDataSource dataSource) : IImageCatalogRepository
{
    public async Task<ProductImage?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        // jsonb_array_elements unnests the Images array; we search by the "id" text field.
        // The query is a full-table scan — acceptable because the result is always
        // served from HybridCache in WebApi; the DB is only hit on a cold cache miss.
        const string sql = """
            SELECT elem->>'id' AS "Id",
                   elem->>'url' AS "Url",
                   elem->>'alt' AS "Alt"
            FROM   "Products",
                   jsonb_array_elements("Images") AS elem
            WHERE  elem->>'id' = @Id
              AND  "Images" IS NOT NULL
            LIMIT  1
            """;

        return await conn.QueryFirstOrDefaultAsync<ProductImage>(
            new CommandDefinition(sql, new { Id = id.ToString() }, cancellationToken: ct));
    }
}
