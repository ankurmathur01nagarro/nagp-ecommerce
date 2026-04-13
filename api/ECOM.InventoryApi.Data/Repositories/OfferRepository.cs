using System.Text.Json;
using Dapper;
using ECOM.InventoryApi.Data.DataModels;
using Npgsql;

namespace ECOM.InventoryApi.Data.Repositories;

public class OfferRepository(NpgsqlDataSource dataSource) : IOfferRepository
{
    public async Task<Offer?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT "Id", "Name", "Description", "ProductId", "DiscountType", "DiscountValue",
                   "StartsAt", "EndsAt", "IsActive", "Rules", "CreatedAt", "UpdatedAt"
            FROM "Offers"
            WHERE "Id" = @Id
            """;
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        return await conn.QuerySingleOrDefaultAsync<Offer>(cmd);
    }

    public async Task<(List<Offer> Items, int TotalCount)> GetListAsync(
        int page, int pageSize, int? productId, bool? activeOnly, string? couponCode, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var builder = new SqlBuilder();

        var countTemplate = builder.AddTemplate("""
            SELECT COUNT(*) FROM "Offers" o
            /**where**/
            """);

        var dataTemplate = builder.AddTemplate("""
            SELECT o."Id", o."Name", o."Description", o."ProductId", o."DiscountType", o."DiscountValue",
                   o."StartsAt", o."EndsAt", o."IsActive", o."Rules", o."CreatedAt", o."UpdatedAt"
            FROM "Offers" o
            /**where**/
            ORDER BY o."StartsAt" DESC
            LIMIT @PageSize OFFSET @Offset
            """, new { PageSize = pageSize, Offset = (page - 1) * pageSize });

        if (productId.HasValue)
            builder.Where(@"o.""ProductId"" = @ProductId", new { ProductId = productId.Value });

        if (activeOnly == true)
            builder.Where(@"o.""IsActive"" = TRUE AND o.""StartsAt"" <= NOW() AND o.""EndsAt"" >= NOW()");

        if (!string.IsNullOrWhiteSpace(couponCode))
            builder.Where(
                @"o.""Rules""::jsonb -> 'couponCodes' @> @CouponJson::jsonb",
                new { CouponJson = JsonSerializer.Serialize(new[] { couponCode }, JsonDefaults.CamelCase) });

        var countCmd = new CommandDefinition(countTemplate.RawSql, countTemplate.Parameters, cancellationToken: ct);
        var totalCount = await conn.ExecuteScalarAsync<int>(countCmd);

        var dataCmd = new CommandDefinition(dataTemplate.RawSql, dataTemplate.Parameters, cancellationToken: ct);
        var items = (await conn.QueryAsync<Offer>(dataCmd)).ToList();

        return (items, totalCount);
    }

    public async Task<List<Offer>> GetActiveForProductAsync(int productId, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT "Id", "Name", "Description", "ProductId", "DiscountType", "DiscountValue",
                   "StartsAt", "EndsAt", "IsActive", "Rules", "CreatedAt", "UpdatedAt"
            FROM "Offers"
            WHERE "IsActive" = TRUE
              AND "StartsAt" <= NOW()
              AND "EndsAt" >= NOW()
              AND ("ProductId" = @ProductId OR "ProductId" IS NULL)
            ORDER BY "DiscountValue" DESC
            """;
        var cmd = new CommandDefinition(sql, new { ProductId = productId }, cancellationToken: ct);
        return (await conn.QueryAsync<Offer>(cmd)).ToList();
    }

    public async Task<List<Offer>> GetActiveForProductsAsync(int[] productIds, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            SELECT "Id", "Name", "Description", "ProductId", "DiscountType", "DiscountValue",
                   "StartsAt", "EndsAt", "IsActive", "Rules", "CreatedAt", "UpdatedAt"
            FROM "Offers"
            WHERE "IsActive" = TRUE
              AND "StartsAt" <= NOW()
              AND "EndsAt" >= NOW()
              AND ("ProductId" = ANY(@ProductIds) OR "ProductId" IS NULL)
            ORDER BY "ProductId" NULLS LAST, "DiscountValue" DESC
            """;
        var cmd = new CommandDefinition(sql, new { ProductIds = productIds }, cancellationToken: ct);
        return (await conn.QueryAsync<Offer>(cmd)).ToList();
    }

    public async Task<int> CreateAsync(Offer offer, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            INSERT INTO "Offers"
                ("Name", "Description", "ProductId", "DiscountType", "DiscountValue",
                 "StartsAt", "EndsAt", "IsActive", "Rules", "CreatedAt", "UpdatedAt")
            VALUES
                (@Name, @Description, @ProductId, @DiscountType, @DiscountValue,
                 @StartsAt, @EndsAt, @IsActive, @Rules::jsonb, @CreatedAt, @UpdatedAt)
            RETURNING "Id"
            """;
        var cmd = new CommandDefinition(sql, new
        {
            offer.Name,
            offer.Description,
            offer.ProductId,
            offer.DiscountType,
            offer.DiscountValue,
            offer.StartsAt,
            offer.EndsAt,
            offer.IsActive,
            offer.Rules,
            offer.CreatedAt,
            offer.UpdatedAt
        }, cancellationToken: ct);
        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    public async Task<bool> UpdateAsync(Offer offer, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """
            UPDATE "Offers"
            SET "Name" = @Name,
                "Description" = @Description,
                "ProductId" = @ProductId,
                "DiscountType" = @DiscountType,
                "DiscountValue" = @DiscountValue,
                "StartsAt" = @StartsAt,
                "EndsAt" = @EndsAt,
                "IsActive" = @IsActive,
                "Rules" = @Rules::jsonb,
                "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id
            """;
        var cmd = new CommandDefinition(sql, new
        {
            offer.Id,
            offer.Name,
            offer.Description,
            offer.ProductId,
            offer.DiscountType,
            offer.DiscountValue,
            offer.StartsAt,
            offer.EndsAt,
            offer.IsActive,
            offer.Rules,
            offer.UpdatedAt
        }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);
        const string sql = """DELETE FROM "Offers" WHERE "Id" = @Id""";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        return await conn.ExecuteAsync(cmd) > 0;
    }
}
