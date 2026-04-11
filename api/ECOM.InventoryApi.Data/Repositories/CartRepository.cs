using Dapper;
using ECOM.InventoryApi.Data.DataModels;
using Npgsql;

namespace ECOM.InventoryApi.Data.Repositories;

public class CartRepository(NpgsqlDataSource dataSource) : ICartRepository
{
    public async Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT "Id", "UserId", "Items", "CreatedAt", "UpdatedAt"
            FROM "Carts"
            WHERE "UserId" = @UserId
            """;
        var cmd = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<Cart>(cmd);
    }

    public async Task<Cart> GetOrCreateByUserIdAsync(Guid userId, CancellationToken ct = default)
    {
        var existing = await GetByUserIdAsync(userId, ct);
        if (existing is not null)
            return existing;

        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO "Carts" ("UserId", "Items", "CreatedAt", "UpdatedAt")
            VALUES (@UserId, '[]'::jsonb, @Now, @Now)
            ON CONFLICT ("UserId") DO UPDATE SET "UpdatedAt" = EXCLUDED."UpdatedAt"
            RETURNING "Id", "UserId", "Items", "CreatedAt", "UpdatedAt"
            """;
        var cmd = new CommandDefinition(sql, new { UserId = userId, Now = DateTimeOffset.UtcNow }, cancellationToken: ct);
        return await conn.QuerySingleAsync<Cart>(cmd);
    }

    public async Task<bool> SetItemsAsync(Guid userId, string itemsJson, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO "Carts" ("UserId", "Items", "CreatedAt", "UpdatedAt")
            VALUES (@UserId, @Items::jsonb, @Now, @Now)
            ON CONFLICT ("UserId") DO UPDATE
                SET "Items" = EXCLUDED."Items",
                    "UpdatedAt" = EXCLUDED."UpdatedAt"
            """;
        var cmd = new CommandDefinition(sql, new { UserId = userId, Items = itemsJson, Now = DateTimeOffset.UtcNow }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> ClearAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            UPDATE "Carts"
            SET "Items" = '[]'::jsonb,
                "UpdatedAt" = NOW()
            WHERE "UserId" = @UserId
            """;
        var cmd = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> DeleteAsync(Guid userId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """DELETE FROM "Carts" WHERE "UserId" = @UserId""";
        var cmd = new CommandDefinition(sql, new { UserId = userId }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }
}
