using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.ProductApi.Data.Migrations
{
    /// <summary>
    /// Data migration — adds a stable UUID "id" field to every element inside the
    /// Products.Images JSONB array.  No schema change, so Up/Down use raw SQL only.
    ///
    /// After this migration, every ProductImage object in the JSONB column has the shape:
    ///   { "id": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx", "url": "...", "alt": "...", "sortOrder": N }
    ///
    /// Down() strips the "id" field back out, restoring the previous shape.
    /// </summary>
    public partial class AddImageIds : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rebuild the Images array, adding gen_random_uuid() to each element that
            // does not already have an "id" key (idempotent if re-run).
            migrationBuilder.Sql("""
                UPDATE "Products"
                SET    "Images" = (
                    SELECT jsonb_agg(
                        CASE
                            WHEN elem ? 'id' THEN elem
                            ELSE elem || jsonb_build_object('id', gen_random_uuid()::text)
                        END
                        ORDER BY (elem->>'sortOrder')::int
                    )
                    FROM jsonb_array_elements("Images") AS elem
                )
                WHERE "Images" IS NOT NULL
                  AND jsonb_array_length("Images") > 0;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove the "id" key from every element in the array.
            migrationBuilder.Sql("""
                UPDATE "Products"
                SET    "Images" = (
                    SELECT jsonb_agg(elem - 'id' ORDER BY (elem->>'sortOrder')::int)
                    FROM jsonb_array_elements("Images") AS elem
                )
                WHERE "Images" IS NOT NULL
                  AND jsonb_array_length("Images") > 0;
                """);
        }
    }
}
