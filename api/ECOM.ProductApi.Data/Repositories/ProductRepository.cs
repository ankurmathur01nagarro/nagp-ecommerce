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

    public async Task<(List<Product> Items, int TotalCount, ProductSearchFacets Facets)> SearchAsync(ProductFilter filter, CancellationToken ct = default)
    {
        await using var conn = await dataSource.OpenConnectionAsync(ct);

        var builder = new SqlBuilder();

        var countTemplate = builder.AddTemplate("""
            SELECT COUNT(*)
            FROM "Products" p
            INNER JOIN "ProductCategories" c ON c."Id" = p."CategoryId"
            INNER JOIN "Brands" b ON b."Id" = p."BrandId"
            /**where**/
            """);

        var orderBy = ResolveOrderBy(filter.SortBy, filter.SortDir);

        var dataTemplate = builder.AddTemplate($"""
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
            ORDER BY {orderBy}
            LIMIT @PageSize OFFSET @Offset
            """, new { filter.PageSize, Offset = (filter.Page - 1) * filter.PageSize });

        // Single facet query: CTE materialises the filtered (pre-pagination) product set once,
        // then UNION ALL branches aggregate by category / brand / color / size / tag.
        // Using a CTE + single QueryAsync keeps everything in the extended query protocol so
        // named parameters are bound correctly — multi-statement QueryMultipleAsync would fall
        // back to simple protocol and skip parameter binding.
        var facetTemplate = builder.AddTemplate("""
            WITH "Filtered" AS (
                SELECT p."Id", p."CategoryId", p."BrandId", p."Metadata"::jsonb AS "Meta"
                FROM "Products" p
                INNER JOIN "ProductCategories" c ON c."Id" = p."CategoryId"
                INNER JOIN "Brands" b ON b."Id" = p."BrandId"
                /**where**/
            )
            SELECT 'category' AS "FacetType", c."Id"::text AS "FacetId", c."Name" AS "FacetValue", COUNT(*)::int AS "Count", c."ParentCategoryId" AS "ParentFacetId", NULL::text AS "FacetExtra"
            FROM "Filtered" f
            INNER JOIN "ProductCategories" c ON c."Id" = f."CategoryId"
            GROUP BY c."Id", c."Name", c."ParentCategoryId"
            UNION ALL
            SELECT 'brand', '', b."Name", COUNT(*)::int, NULL::int, NULL::text
            FROM "Filtered" f
            INNER JOIN "Brands" b ON b."Id" = f."BrandId"
            GROUP BY b."Name"
            UNION ALL
            SELECT 'color', '', col->>'name', COUNT(*)::int, NULL::int, MAX(col->>'hexCode')
            FROM "Filtered" f,
            LATERAL jsonb_array_elements(COALESCE(f."Meta"->'colors', '[]'::jsonb)) AS col
            WHERE col->>'name' IS NOT NULL AND col->>'name' <> ''
            GROUP BY col->>'name'
            UNION ALL
            SELECT 'size', '', sz, COUNT(*)::int, NULL::int, NULL::text
            FROM "Filtered" f,
            LATERAL jsonb_array_elements_text(COALESCE(f."Meta"->'sizes', '[]'::jsonb)) AS sz
            WHERE sz IS NOT NULL AND sz <> ''
            GROUP BY sz
            UNION ALL
            SELECT 'tag', '', tag, COUNT(*)::int, NULL::int, NULL::text
            FROM "Filtered" f,
            LATERAL jsonb_array_elements_text(COALESCE(f."Meta"->'tags', '[]'::jsonb)) AS tag
            WHERE tag IS NOT NULL AND tag <> ''
            GROUP BY tag
            """);

        if (filter.Colors is { Count: > 0 })
            builder.Where("""
                EXISTS (
                    SELECT 1 FROM jsonb_array_elements(p."Metadata"::jsonb -> 'colors') AS col
                    WHERE col->>'name' = ANY(@Colors)
                )
                """, new { Colors = filter.Colors.ToArray() });

        if (filter.Sizes is { Count: > 0 })
            builder.Where("""
                EXISTS (
                    SELECT 1 FROM jsonb_array_elements_text(p."Metadata"::jsonb -> 'sizes') AS sz
                    WHERE sz = ANY(@Sizes)
                )
                """, new { Sizes = filter.Sizes.ToArray() });

        if (filter.PriceMin.HasValue)
            builder.Where(@"p.""Price"" >= @PriceMin", new { filter.PriceMin });

        if (filter.PriceMax.HasValue)
            builder.Where(@"p.""Price"" <= @PriceMax", new { filter.PriceMax });

        if (filter.Brands is { Count: > 0 })
            builder.Where(@"b.""Name"" = ANY(@Brands)", new { Brands = filter.Brands.ToArray() });

        if (filter.Tags is { Count: > 0 })
            builder.Where("""
                EXISTS (
                    SELECT 1 FROM jsonb_array_elements_text(p."Metadata"::jsonb -> 'tags') AS tag
                    WHERE tag = ANY(@Tags)
                )
                """, new { Tags = filter.Tags.ToArray() });

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            // Resolve the category (by name or numeric ID) and all its descendants in one
            // recursive CTE query, then filter products by the resulting ID set.
            const string categorySql = """
                WITH RECURSIVE "CategoryTree" AS (
                    SELECT "Id" FROM "ProductCategories"
                    WHERE "Name" = @Category OR "Id"::text = @Category
                    UNION ALL
                    SELECT pc."Id" FROM "ProductCategories" pc
                    INNER JOIN "CategoryTree" ct ON pc."ParentCategoryId" = ct."Id"
                )
                SELECT "Id" FROM "CategoryTree"
                """;
            var catCmd = new CommandDefinition(categorySql, new { filter.Category }, cancellationToken: ct);
            var categoryIds = (await conn.QueryAsync<int>(catCmd)).ToArray();

            builder.Where(
                categoryIds.Length > 0
                    ? @"p.""CategoryId"" = ANY(@CategoryIds)"
                    : "1 = 0",  // category not found → return no results
                new { CategoryIds = categoryIds });
        }

        if (!string.IsNullOrWhiteSpace(filter.Gender))
            builder.Where(@"p.""Gender"" = @Gender", new { filter.Gender });

        if (filter.RatingMin.HasValue)
            builder.Where(
                @"p.""Metadata""->>'rating' IS NOT NULL AND (p.""Metadata""->>'rating')::int >= @RatingMin",
                new { filter.RatingMin });

        if (filter.RatingMax.HasValue)
            builder.Where(
                @"p.""Metadata""->>'rating' IS NOT NULL AND (p.""Metadata""->>'rating')::int <= @RatingMax",
                new { filter.RatingMax });

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

        var facetCmd = new CommandDefinition(facetTemplate.RawSql, facetTemplate.Parameters, cancellationToken: ct);
        var allFacets = (await conn.QueryAsync<FacetRow>(facetCmd)).ToList();

        var categories = allFacets
            .Where(r => r.FacetType == "category")
            .Select(r => new CategoryFacet(int.Parse(r.FacetId), r.FacetValue, r.ParentFacetId, r.Count))
            .ToList();
        var brands  = allFacets.Where(r => r.FacetType == "brand") .Select(r => new FacetCount(r.FacetValue, r.Count)).ToList();
        var colors  = allFacets.Where(r => r.FacetType == "color") .Select(r => new ColorFacet(r.FacetValue, r.FacetExtra, r.Count)).ToList();
        var sizes   = allFacets.Where(r => r.FacetType == "size")  .Select(r => new FacetCount(r.FacetValue, r.Count)).ToList();
        var tags    = allFacets.Where(r => r.FacetType == "tag")   .Select(r => new FacetCount(r.FacetValue, r.Count)).ToList();

        var facets = new ProductSearchFacets(categories, colors, sizes, brands, tags);
        return (items, totalCount, facets);
    }

    private record FacetRow(string FacetType, string FacetId, string FacetValue, int Count, int? ParentFacetId, string? FacetExtra);

    private static string ResolveOrderBy(string sortBy, string sortDir)
    {
        var dir = string.Equals(sortDir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
        return sortBy.ToLowerInvariant() switch
        {
            "price"  => $@"p.""Price"" {dir}",
            "rating" => $@"(p.""Metadata""->>'rating')::int {dir} NULLS LAST",
            _        => $@"p.""Name"" {dir}"   // name (default), popularity — fall back to name
        };
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
