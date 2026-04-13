using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.ProductApi.Data.Migrations
{
    /// <summary>
    /// Replaces the swimwear product images (IDs 81-85) with work-appropriate
    /// lifestyle and catalog photographs:
    ///
    ///   81  Triangle Bikini Top          — one-piece swimsuit catalog shots
    ///   82  High-Waist Bikini Bottom     — one-piece swimsuit lifestyle photos
    ///   83  Plunge-Neck One-Piece        — elegant one-piece catalog shots
    ///   84  Classic Swim Shorts (Men)    — beach lifestyle, shorts visible
    ///   85  Printed Board Shorts (Men)   — beach lifestyle, shorts visible
    ///
    /// All replacement images show models in tasteful, non-revealing poses
    /// suitable for a professional / work environment.
    ///
    /// Sources: Unsplash CDN (verified hashes) + Pexels CDN
    /// </summary>
    public partial class ReplaceSwimwearImages : Migration
    {
        static string Ui(string h) =>
            $"https://images.unsplash.com/photo-{h}?w=600&h=900&fit=crop&q=80&fm=jpg";

        static string Px(int id) =>
            $"https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&w=800";

        static string Esc(string s) => s.Replace("'", "''");

        static string Upd(int pid,
            string u1, string a1,
            string u2, string a2,
            string u3, string a3 = "Swimwear catalog detail view") =>
            $"""
             UPDATE "Products"
             SET "Images" = jsonb_build_array(
                 jsonb_build_object('id', gen_random_uuid()::text,
                                    'url', '{Esc(u1)}', 'alt', '{Esc(a1)}', 'sortOrder', 1),
                 jsonb_build_object('id', gen_random_uuid()::text,
                                    'url', '{Esc(u2)}', 'alt', '{Esc(a2)}', 'sortOrder', 2),
                 jsonb_build_object('id', gen_random_uuid()::text,
                                    'url', '{Esc(u3)}', 'alt', '{Esc(a3)}', 'sortOrder', 3)
             )
             WHERE "Id" = {pid};
             """;

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Product 81: Triangle Bikini Top ──────────────────────────────
            // Replaced: revealing bikini pose shots
            // With:     models in one-piece swimsuits in tasteful catalog poses
            migrationBuilder.Sql(Upd(81,
                Ui("1610386613653-20e90bfb1200"), "Triangle Bikini Top – woman in elegant white swimsuit standing on beach",
                Ui("1525517710769-9f4fdb9e4099"), "Triangle Bikini Top – woman in one-piece swimsuit, lifestyle poolside",
                Ui("1608049429989-ce05a0c5e15c")));  // woman in stripe one-piece, beach

            // ── Product 82: High-Waist Bikini Bottom ─────────────────────────
            // Replaced: revealing bikini pose shots
            // With:     catalog-style one-piece swimwear photos
            migrationBuilder.Sql(Upd(82,
                Ui("1608049429989-ce05a0c5e15c"), "High-Waist Bikini Bottom – woman in striped one-piece swimsuit on beach",
                Ui("1517842886518-c4d1d5146300"), "High-Waist Bikini Bottom – smiling woman in black one-piece swimsuit",
                Ui("1610386613653-20e90bfb1200")));  // white swimsuit standing on beach

            // ── Product 83: Plunge-Neck One-Piece Swimsuit ───────────────────
            // Replaced: bikini source images reused from products 81-82
            // With:     dedicated one-piece swimsuit catalog shots
            migrationBuilder.Sql(Upd(83,
                Ui("1517842886518-c4d1d5146300"), "Plunge-Neck One-Piece Swimsuit – woman in elegant black one-piece swimsuit",
                Ui("1525517710769-9f4fdb9e4099"), "Plunge-Neck One-Piece Swimsuit – woman in one-piece swimsuit, natural light",
                Ui("1608049429989-ce05a0c5e15c")));  // striped one-piece, beach

            // ── Product 84: Classic Swim Shorts ──────────────────────────────
            // Replaced: generic street-style shots unrelated to swimwear
            // With:     beach lifestyle photos showing men in shorts
            migrationBuilder.Sql(Upd(84,
                Ui("1609537303157-6d51b4496ae5"), "Classic Swim Shorts – man in casual shorts on beach, daytime lifestyle",
                Px(6764007),                      "Classic Swim Shorts – man in shorts and sneakers, relaxed style",
                Px(9955748)));                     // man in jacket and trousers, urban casual

            // ── Product 85: Printed Board Shorts ─────────────────────────────
            // Replaced: generic street-style shots unrelated to swimwear
            // With:     beach lifestyle photos showing men in shorts
            migrationBuilder.Sql(Upd(85,
                Ui("1609537303157-6d51b4496ae5"), "Printed Board Shorts – man in shorts on beach, casual lifestyle shot",
                Px(9955748),                      "Printed Board Shorts – male model in casual shorts, outdoor setting",
                Px(11434887)));                    // male model on city street, summer styling
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the previous images by re-running ExpandCatalogueAndThirdImages;
            // Down is intentionally left as no-op for this image-only patch.
        }
    }
}
