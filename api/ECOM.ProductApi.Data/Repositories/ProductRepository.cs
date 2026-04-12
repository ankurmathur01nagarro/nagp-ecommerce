using System.Text.Json;
using Dapper;
using ECOM.ProductApi.Data.DataModels;
using Npgsql;

namespace ECOM.ProductApi.Data.Repositories;

public class ProductRepository(NpgsqlDataSource dataSource) : IProductRepository
{
    public async Task<Product?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """
            SELECT
                p."Id", p."Name", p."Sku", p."ShortDescription", p."Description",
                p."Price", p."CategoryId", p."BrandId", p."Gender",
                p."Images", p."Metadata",
                p."CreatedAt", p."UpdatedAt",
                c."Id", c."Name",
                b."Id", b."Name", b."LogoUrl"
            FROM "Products" p
            INNER JOIN "ProductCategories" c ON c."Id" = p."CategoryId"
            INNER JOIN "Brands" b ON b."Id" = p."BrandId"
            WHERE p."Id" = @Id
            """;

        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        var result = await conn.QueryAsync<Product, ProductCategory, Brand, Product>(
            cmd,
            (product, category, brand) =>
            {
                product.Category = category;
                product.Brand = brand;
                return product;
            },
            splitOn: "Id,Id");

        return result.FirstOrDefault();
    }

    public async Task<(List<Product> Items, int TotalCount)> GetListAsync(
        int page, int pageSize, string? category, string? brand, string? tag, string? gender,
        CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var builder = new SqlBuilder();

        // Count query
        var countTemplate = builder.AddTemplate("""
            SELECT COUNT(*)
            FROM "Products" p
            INNER JOIN "ProductCategories" c ON c."Id" = p."CategoryId"
            INNER JOIN "Brands" b ON b."Id" = p."BrandId"
            /**where**/
            """);

        // Data query
        var dataTemplate = builder.AddTemplate("""
            SELECT
                p."Id", p."Name", p."Sku", p."ShortDescription", p."Description",
                p."Price", p."CategoryId", p."BrandId", p."Gender",
                p."Images", p."Metadata",
                p."CreatedAt", p."UpdatedAt",
                c."Id", c."Name",
                b."Id", b."Name", b."LogoUrl"
            FROM "Products" p
            INNER JOIN "ProductCategories" c ON c."Id" = p."CategoryId"
            INNER JOIN "Brands" b ON b."Id" = p."BrandId"
            /**where**/
            ORDER BY p."CreatedAt" DESC
            LIMIT @PageSize OFFSET @Offset
            """, new { PageSize = pageSize, Offset = (page - 1) * pageSize });

        if (!string.IsNullOrWhiteSpace(category))
            builder.Where(@"c.""Name"" = @Category", new { Category = category });

        if (!string.IsNullOrWhiteSpace(brand))
            builder.Where(@"b.""Name"" = @Brand", new { Brand = brand });

        if (!string.IsNullOrWhiteSpace(tag))
            builder.Where(@"p.""Metadata""::jsonb -> 'tags' @> @TagJson::jsonb", new { TagJson = JsonSerializer.Serialize(new[] { tag }, JsonDefaults.CamelCase) });

        if (!string.IsNullOrWhiteSpace(gender))
            builder.Where(@"p.""Gender"" = @Gender", new { Gender = gender });

        var countCmd = new CommandDefinition(countTemplate.RawSql, countTemplate.Parameters, cancellationToken: ct);
        var totalCount = await conn.ExecuteScalarAsync<int>(countCmd);

        var dataCmd = new CommandDefinition(dataTemplate.RawSql, dataTemplate.Parameters, cancellationToken: ct);
        var items = (await conn.QueryAsync<Product, ProductCategory, Brand, Product>(
            dataCmd,
            (product, cat, br) =>
            {
                product.Category = cat;
                product.Brand = br;
                return product;
            },
            splitOn: "Id,Id")).ToList();

        return (items, totalCount);
    }

    public async Task<int> CreateAsync(Product product, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """
            INSERT INTO "Products"
                ("Name", "Sku", "ShortDescription", "Description", "Price",
                 "CategoryId", "BrandId", "Gender", "Images", "Metadata", "CreatedAt", "UpdatedAt")
            VALUES
                (@Name, @Sku, @ShortDescription, @Description, @Price,
                 @CategoryId, @BrandId, @Gender, @Images::jsonb, @Metadata::jsonb, @CreatedAt, @UpdatedAt)
            RETURNING "Id"
            """;

        var cmd = new CommandDefinition(sql, new
        {
            product.Name,
            product.Sku,
            product.ShortDescription,
            product.Description,
            product.Price,
            product.CategoryId,
            product.BrandId,
            product.Gender,
            product.Images,
            product.Metadata,
            product.CreatedAt,
            product.UpdatedAt
        }, cancellationToken: ct);

        return await conn.ExecuteScalarAsync<int>(cmd);
    }

    public async Task<bool> UpdateAsync(Product product, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """
            UPDATE "Products"
            SET "Name" = @Name,
                "Sku" = @Sku,
                "ShortDescription" = @ShortDescription,
                "Description" = @Description,
                "Price" = @Price,
                "CategoryId" = @CategoryId,
                "BrandId" = @BrandId,
                "Gender" = @Gender,
                "Images" = @Images::jsonb,
                "Metadata" = @Metadata::jsonb,
                "UpdatedAt" = @UpdatedAt
            WHERE "Id" = @Id
            """;

        var cmd = new CommandDefinition(sql, new
        {
            product.Id,
            product.Name,
            product.Sku,
            product.ShortDescription,
            product.Description,
            product.Price,
            product.CategoryId,
            product.BrandId,
            product.Gender,
            product.Images,
            product.Metadata,
            product.UpdatedAt
        }, cancellationToken: ct);

        var rows = await conn.ExecuteAsync(cmd);
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        const string sql = """DELETE FROM "Products" WHERE "Id" = @Id""";
        var cmd = new CommandDefinition(sql, new { Id = id }, cancellationToken: ct);
        var rows = await conn.ExecuteAsync(cmd);
        return rows > 0;
    }
}
