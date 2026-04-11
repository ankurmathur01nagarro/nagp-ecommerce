using System.Text.Json;
using Dapper;
using ECOM.InventoryApi.Data.DataModels;
using Npgsql;

namespace ECOM.InventoryApi.Data.Repositories;

public class InventoryRepository(NpgsqlDataSource dataSource) : IInventoryRepository
{
    public async Task<Inventory?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT "Id", "ProductId", "Sku", "Quantity", "Reserved", "LowStockThreshold",
                   "Metadata", "CreatedAt", "UpdatedAt"
            FROM "Inventories"
            WHERE "Id" = @Id
            """;
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<Inventory>(cmd);
    }

    public async Task<Inventory?> GetByProductIdAsync(int productId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT "Id", "ProductId", "Sku", "Quantity", "Reserved", "LowStockThreshold",
                   "Metadata", "CreatedAt", "UpdatedAt"
            FROM "Inventories"
            WHERE "ProductId" = @ProductId
            """;
        var cmd = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<Inventory>(cmd);
    }

    public async Task<(List<Inventory> Items, int TotalCount)> GetListAsync(
        int page, int pageSize, bool? lowStockOnly, string? warehouseCode, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var builder = new SqlBuilder();

        var countTemplate = builder.AddTemplate("""
            SELECT COUNT(*) FROM "Inventories" i
            /**where**/
            """);

        var dataTemplate = builder.AddTemplate("""
            SELECT i."Id", i."ProductId", i."Sku", i."Quantity", i."Reserved", i."LowStockThreshold",
                   i."Metadata", i."CreatedAt", i."UpdatedAt"
            FROM "Inventories" i
            /**where**/
            ORDER BY i."UpdatedAt" DESC
            LIMIT @PageSize OFFSET @Offset
            """, new { PageSize = pageSize, Offset = (page - 1) * pageSize });

        if (lowStockOnly == true)
            builder.Where(@"(i.""Quantity"" - i.""Reserved"") <= i.""LowStockThreshold""");

        if (!string.IsNullOrWhiteSpace(warehouseCode))
            builder.Where(
                @"i.""Metadata""::jsonb -> 'warehouses' @> @WarehouseJson::jsonb",
                new { WarehouseJson = JsonSerializer.Serialize(new[] { new { code = warehouseCode } }, JsonDefaults.CamelCase) });

        var countCmd = new CommandDefinition(countTemplate.RawSql, countTemplate.Parameters, cancellationToken: ct);
        var totalCount = await conn.ExecuteScalarAsync<int>(countCmd);

        var dataCmd = new CommandDefinition(dataTemplate.RawSql, dataTemplate.Parameters, cancellationToken: ct);
        var items = (await conn.QueryAsync<Inventory>(dataCmd)).ToList();

        return (items, totalCount);
    }

    public async Task<int> CreateAsync(Inventory inventory, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO "Inventories"
                ("ProductId", "Sku", "Quantity", "Reserved", "LowStockThreshold",
                 "Metadata", "CreatedAt", "UpdatedAt")
            VALUES
                (@ProductId, @Sku, @Quantity, @Reserved, @LowStockThreshold,
                 @Metadata::jsonb, @CreatedAt, @UpdatedAt)
            RETURNING "Id"
            """;
        var cmd = new CommandDefinition(sql, new
        {
            inventory.ProductId,
            inventory.Sku,
            inventory.Quantity,
            inventory.Reserved,
            inventory.LowStockThreshold,
            inventory.Metadata,
            inventory.CreatedAt,
            inventory.UpdatedAt
        }, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    public async Task<bool> UpdateAsync(Inventory inventory, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            UPDATE "Inventories"
            SET "Sku" = @Sku,
                "Quantity" = @Quantity,
                "Reserved" = @Reserved,
                "LowStockThreshold" = @LowStockThreshold,
                "Metadata" = @Metadata::jsonb,
                "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id
            """;
        var cmd = new CommandDefinition(sql, new
        {
            inventory.Id,
            inventory.Sku,
            inventory.Quantity,
            inventory.Reserved,
            inventory.LowStockThreshold,
            inventory.Metadata,
            inventory.UpdatedAt
        }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> AdjustQuantityAsync(int productId, int delta, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            UPDATE "Inventories"
            SET "Quantity" = "Quantity" + @Delta,
                "UpdatedAt" = NOW()
            WHERE "ProductId" = @ProductId
              AND ("Quantity" + @Delta) >= "Reserved"
            """;
        var cmd = new CommandDefinition(sql, new { ProductId = productId, Delta = delta }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> ReserveAsync(int productId, int quantity, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            UPDATE "Inventories"
            SET "Reserved" = "Reserved" + @Quantity,
                "UpdatedAt" = NOW()
            WHERE "ProductId" = @ProductId
              AND ("Quantity" - "Reserved") >= @Quantity
            """;
        var cmd = new CommandDefinition(sql, new { ProductId = productId, Quantity = quantity }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> ReleaseReservationAsync(int productId, int quantity, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            UPDATE "Inventories"
            SET "Reserved" = GREATEST("Reserved" - @Quantity, 0),
                "UpdatedAt" = NOW()
            WHERE "ProductId" = @ProductId
            """;
        var cmd = new CommandDefinition(sql, new { ProductId = productId, Quantity = quantity }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """DELETE FROM "Inventories" WHERE "Id" = @Id""";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }
}
