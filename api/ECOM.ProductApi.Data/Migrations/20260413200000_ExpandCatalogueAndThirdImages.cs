using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.ProductApi.Data.Migrations
{
    /// <summary>
    /// 1. Adds 5 new clothing categories: Hoodies &amp; Sweatshirts, Activewear (group),
    ///    Gym Wear, Swimwear, Coats &amp; Raincoats.
    /// 2. Adds 20 new products (IDs 71-90) for the new leaf categories.
    /// 3. Upgrades all 90 products to 3 images each, mixing sources:
    ///    - Unsplash CDN  (fashion-model portrait &amp; detail shots — verified hashes)
    ///    - DummyJSON CDN (product catalog shots where category matches)
    ///    - Pexels CDN    (additional model shots for non-DummyJSON categories)
    /// 4. All alt texts are clean product descriptions — no stock-photo metadata,
    ///    watermark language, or advertising copy.
    /// </summary>
    public partial class ExpandCatalogueAndThirdImages : Migration
    {
        // ── URL helpers ──────────────────────────────────────────────────────────

        // Unsplash: portrait crop (600×900)
        static string Unsp(string h) =>
            $"https://images.unsplash.com/photo-{h}?w=600&h=900&fit=crop&q=80&fm=jpg";

        // Pexels: portrait crop (800 wide, natural height)
        static string Pexl(int id) =>
            $"https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&w=800";

        // DummyJSON product CDN
        static string Dumj(string path) =>
            $"https://cdn.dummyjson.com/product-images/{path}";

        // ── SQL helpers ──────────────────────────────────────────────────────────

        static string Esc(string s) => s.Replace("'", "''");

        // Builds the UPDATE SQL (3 images) for an existing product
        static string Upd(int pid,
            string u1, string a1,
            string u2, string a2,
            string u3, string a3 = "Product detail and catalog view") =>
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

        // Builds a 3-image JSON string for INSERT (uses C#-generated UUIDs)
        static string ImgJson(
            string u1, string a1,
            string u2, string a2,
            string u3, string a3 = "Product detail and catalog view")
        {
            static string E(string s) => s.Replace("\"", "\\\"");
            static string Img(string u, string a, int ord) =>
                $"{{\"id\":\"{Guid.NewGuid()}\",\"url\":\"{E(u)}\",\"alt\":\"{E(a)}\",\"sortOrder\":{ord}}}";
            return $"[{Img(u1, a1, 1)},{Img(u2, a2, 2)},{Img(u3, a3, 3)}]";
        }

        // Builds Metadata JSON matching the existing schema
        static string Meta(
            string[] colorNames, string[] colorHexes,
            string[] sizes, string[] tags,
            string[] specLabels, string[] specValues,
            string additionalInfo)
        {
            static string E(string s) => s.Replace("\"", "\\\"");
            var colors = string.Join(",",
                System.Linq.Enumerable.Zip(colorNames, colorHexes,
                    (n, h) => $"{{\"name\":\"{E(n)}\",\"hexCode\":\"{E(h)}\"}}"));
            var szs   = string.Join(",", System.Linq.Enumerable.Select(sizes,  s => $"\"{E(s)}\""));
            var tgs   = string.Join(",", System.Linq.Enumerable.Select(tags,   t => $"\"{E(t)}\""));
            var specs = string.Join(",",
                System.Linq.Enumerable.Zip(specLabels, specValues,
                    (l, v) => $"{{\"label\":\"{E(l)}\",\"value\":\"{E(v)}\"}}"));
            return $"{{\"colors\":[{colors}],\"sizes\":[{szs}],\"tags\":[{tgs}],\"techSpecs\":[{specs}],\"additionalInfo\":\"{E(additionalInfo)}\"}}";
        }

        // ── Shared size arrays ───────────────────────────────────────────────────

        static readonly string[] TopSizes  = ["XS", "S", "M", "L", "XL", "XXL"];
        static readonly string[] SwimSizes = ["XS", "S", "M", "L", "XL"];

        // ── Seed date ────────────────────────────────────────────────────────────

        static readonly DateTimeOffset T = new(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        // ════════════════════════════════════════════════════════════════════════
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ══════════════════════════════════════════════════════════════════
            // 1. NEW CATEGORIES
            // ══════════════════════════════════════════════════════════════════

            migrationBuilder.Sql("""
                INSERT INTO "ProductCategories" ("Id", "Name", "ParentCategoryId") VALUES
                (20, 'Hoodies & Sweatshirts', 2),
                (21, 'Activewear',             1),
                (22, 'Gym Wear',               21),
                (23, 'Swimwear',               21),
                (24, 'Coats & Raincoats',      9);
                """);

            // ══════════════════════════════════════════════════════════════════
            // 2. NEW PRODUCTS  (IDs 71-90)
            // ══════════════════════════════════════════════════════════════════

            var cols = new[]
            {
                "Id", "Name", "Sku", "ShortDescription", "Description",
                "Price", "CategoryId", "BrandId", "Gender",
                "Images", "Metadata", "CreatedAt", "UpdatedAt"
            };

            // ── Hoodies & Sweatshirts (catId=20) ─────────────────────────────
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    71, "Classic Pullover Hoodie", "HM-HOOD-M-071",
                    "Heavyweight cotton-blend pullover hoodie with kangaroo pocket. An everyday essential.",
                    "Crafted from a dense 320 gsm cotton-polyester fleece, this pullover hoodie strikes the perfect balance between warmth and breathability. The ribbed cuffs, hem and hood opening hold their shape wash after wash. A roomy kangaroo pocket sits at the front for hand-warming and light storage. Cut in a relaxed silhouette that layers easily over a tee or under a jacket.",
                    49.99m, 20, 3, "Men",
                    ImgJson(
                        Unsp("1703531293255-0b16d10fe09f"), "Classic Pullover Hoodie – male model in grey pullover hoodie",
                        Pexl(2772535),                     "Classic Pullover Hoodie – man in grey hoodie, casual pose",
                        Pexl(7763190),                     "Classic Pullover Hoodie – model in hoodie with arms crossed"),
                    Meta(["Charcoal","Navy","Sage Green"],["#36454F","#1C2B4B","#8FAF8F"],
                         TopSizes, ["hoodie","sweatshirt","mens","casual","loungewear"],
                         ["Material","Weight","Fit","Pocket","Hood","Care"],
                         ["80% Cotton 20% Polyester","320 gsm","Relaxed","Kangaroo","Drawstring","Machine Wash 40°C"],
                         "Style code: HM-HOOD-M-071. Garment-dyed for a lived-in look."),
                    T, T
                },
                {
                    72, "Zip-Up Tech Hoodie", "NK-HOOD-M-072",
                    "Moisture-wicking full-zip hoodie built for training and everyday wear.",
                    "Designed for movement, this full-zip hoodie features Nike's Dri-FIT technology to keep you dry during warm-ups and cool-downs. Flatlock seams reduce chafing, and the slim fit sits close to the body without restricting range of motion. Two hand pockets with zip closure keep small items secure. Works equally well in the gym or on the street.",
                    79.99m, 20, 5, "Men",
                    ImgJson(
                        Unsp("1638305612283-3ea9689cbbe4"), "Zip-Up Tech Hoodie – model in athletic zip hoodie, sporty pose",
                        Pexl(9695914),                     "Zip-Up Tech Hoodie – person in white hoodie, clean studio look",
                        Pexl(2772535),                     "Zip-Up Tech Hoodie – man in grey hoodie, full-length view"),
                    Meta(["Black","Dark Grey","Royal Blue"],["#1A1A1A","#404040","#2952A3"],
                         TopSizes, ["hoodie","zip-up","mens","training","athletic","nike"],
                         ["Material","Technology","Fit","Pockets","Closure","Care"],
                         ["100% Polyester","Dri-FIT","Slim","2 Zip Side Pockets","Full-Zip","Machine Wash Cold"],
                         "Style code: NK-HOOD-M-072. Reflective detail on chest."),
                    T, T
                },
                {
                    73, "Oversized Graphic Sweatshirt", "ZRA-SWEAT-W-073",
                    "Dropped-shoulder fleece sweatshirt with a bold front graphic. Relaxed and cosy.",
                    "This oversized crew-neck sweatshirt is cut from a soft brushed-back fleece that feels instantly comfortable against the skin. The dropped shoulder gives it a deliberately relaxed, fashion-forward silhouette. Pair it with straight-leg jeans and chunky trainers, or wear it as a cosy layering piece over a slip dress. The front graphic is screen-printed for long-lasting vibrancy.",
                    44.99m, 20, 2, "Women",
                    ImgJson(
                        Unsp("1515614557830-ae0df9016e19"), "Oversized Graphic Sweatshirt – woman in oversized hoodie, portrait",
                        Unsp("1638305612283-3ea9689cbbe4"), "Oversized Graphic Sweatshirt – model in hoodie, casual styling",
                        Pexl(7763190),                     "Oversized Graphic Sweatshirt – person in sweatshirt, relaxed pose"),
                    Meta(["Cream","Washed Black","Dusty Rose"],["#FFFDD0","#2C2C2C","#C4A0A0"],
                         TopSizes, ["sweatshirt","graphic","womens","oversized","casual","streetwear"],
                         ["Material","Print","Fit","Neckline","Hem","Care"],
                         ["70% Cotton 30% Polyester","Screen Print","Oversized","Crew-Neck","Ribbed","Machine Wash 30°C"],
                         "Style code: ZRA-SWEAT-W-073. Turn inside out to preserve print."),
                    T, T
                },
                {
                    74, "Cropped Pullover Hoodie", "HM-HOOD-W-074",
                    "Soft fleece cropped hoodie with a fitted silhouette. Easy to style with high-waist bottoms.",
                    "Cut short to sit just at the hip, this cropped hoodie is made from a cosy cotton-mix fleece with a slightly brushed inner for warmth. The fitted through the body balances the volume of the hood. Style it with high-waisted joggers for lounging, or tuck the front into wide-leg trousers for a smart-casual take on athleisure.",
                    34.99m, 20, 3, "Women",
                    ImgJson(
                        Unsp("1638305612283-3ea9689cbbe4"), "Cropped Pullover Hoodie – woman in cropped hoodie posing",
                        Unsp("1515614557830-ae0df9016e19"), "Cropped Pullover Hoodie – close-up of woman in hoodie",
                        Pexl(9695914),                     "Cropped Pullover Hoodie – model in fitted hoodie, white background"),
                    Meta(["Lilac","Stone","Black"],["#C8A2C8","#C8B9A0","#1A1A1A"],
                         TopSizes, ["hoodie","cropped","womens","casual","athleisure"],
                         ["Material","Fit","Length","Hood","Cuffs","Care"],
                         ["75% Cotton 25% Polyester","Fitted","Cropped","Oversized Hood","Ribbed","Machine Wash 30°C"],
                         "Style code: HM-HOOD-W-074. Pair with matching joggers for a coordinated set."),
                    T, T
                },
                {
                    75, "Vintage Crew-Neck Sweatshirt", "ZRA-SWEAT-U-075",
                    "Garment-washed fleece sweatshirt with a lived-in, vintage feel. Unisex cut.",
                    "Inspired by classic collegiate sportswear, this crew-neck sweatshirt has been garment-washed to give it an instant vintage softness. The slightly boxy, unisex cut works on all body types and sits neatly at the hip. The ribbed collar, cuffs and hem provide structure, while the medium-weight fleece is warm enough for cool days without feeling heavy.",
                    54.99m, 20, 2, "Unisex",
                    ImgJson(
                        Unsp("1703531293255-0b16d10fe09f"), "Vintage Crew-Neck Sweatshirt – person in washed sweatshirt, neutral pose",
                        Pexl(2772535),                     "Vintage Crew-Neck Sweatshirt – model in grey crewneck sweatshirt",
                        Pexl(9695914),                     "Vintage Crew-Neck Sweatshirt – clean product styling on model"),
                    Meta(["Washed Grey","Washed Ecru","Faded Black"],["#909090","#E8E0CC","#3D3D3D"],
                         TopSizes, ["sweatshirt","unisex","vintage","casual","streetwear"],
                         ["Material","Finish","Fit","Neckline","Rib","Care"],
                         ["100% Cotton","Garment-Washed","Relaxed Boxy","Ribbed Crew","1x1 Rib","Machine Wash Cold"],
                         "Style code: ZRA-SWEAT-U-075. Vintage wash means each piece is unique."),
                    T, T
                },
            });

            // ── Gym Wear (catId=22) ───────────────────────────────────────────
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    76, "High-Waist Compression Leggings", "NK-GYM-W-076",
                    "Squat-proof compression leggings with a sculpting high waist. Built for intense training.",
                    "These high-waisted leggings are engineered for performance. The four-way stretch fabric moves with you during squats, lunges and HIIT sessions, while the compressive fit supports muscles and smooths the silhouette. A wide flat waistband stays put without digging in. The fabric is sweat-wicking and quick-drying, with a gusset panel for freedom of movement.",
                    54.99m, 22, 5, "Women",
                    ImgJson(
                        Unsp("1584863495140-a320b13a11a8"), "High-Waist Compression Leggings – woman in black sports bra and leggings",
                        Pexl(7763190),                     "High-Waist Compression Leggings – model in athletic bottoms, side view",
                        Pexl(9695914),                     "High-Waist Compression Leggings – woman in gym wear, studio shoot"),
                    Meta(["Black","Midnight Navy","Olive"],["#1A1A1A","#1C2B4B","#6B7C5C"],
                         SwimSizes, ["leggings","gym","womens","compression","activewear","nike"],
                         ["Material","Fit","Waistband","Technology","Gusset","Care"],
                         ["75% Nylon 25% Spandex","Compression","Wide High-Waist","Dri-FIT","2-Way Gusset","Machine Wash Cold"],
                         "Style code: NK-GYM-W-076. Side pocket for phone or key."),
                    T, T
                },
                {
                    77, "Performance Training Shorts", "NK-GYM-M-077",
                    "Lightweight running shorts with a 7-inch inseam and built-in liner.",
                    "Designed for speed and comfort, these training shorts are made from a lightweight recycled polyester that breathes in hot conditions. The built-in compression liner provides support without restricting stride, and the elastic waistband with internal drawcord ensures a secure fit. Reflective details improve visibility in low light.",
                    39.99m, 22, 5, "Men",
                    ImgJson(
                        Unsp("1703531293255-0b16d10fe09f"), "Performance Training Shorts – male athlete in running shorts",
                        Pexl(17350031),                    "Performance Training Shorts – man in athletic shorts and jacket",
                        Pexl(11434887),                    "Performance Training Shorts – male model in sport outfit on street"),
                    Meta(["Black","Dark Grey","Red"],["#1A1A1A","#404040","#CC0000"],
                         SwimSizes, ["shorts","training","mens","running","activewear","nike"],
                         ["Material","Inseam","Liner","Waistband","Pockets","Care"],
                         ["100% Recycled Polyester","7 Inch","Built-In Compression","Elastic Drawcord","1 Back Zip","Machine Wash Cold"],
                         "Style code: NK-GYM-M-077. Reflective Nike swoosh logo."),
                    T, T
                },
                {
                    78, "Sports Bra – Medium Support", "NK-GYM-W-078",
                    "Removable-cup sports bra with medium support. Ideal for yoga, cycling and HIIT.",
                    "This Dri-FIT sports bra provides medium support and a close, comfortable fit for moderate-impact activities. Removable foam padding gives a smooth silhouette and customisable coverage. A racer-back design allows full freedom of shoulder movement. The wide elastic underband stays in place during workouts and avoids digging.",
                    34.99m, 22, 5, "Women",
                    ImgJson(
                        Unsp("1584863495140-a320b13a11a8"), "Sports Bra – woman in black sports bra and leggings, yoga pose",
                        Unsp("1638305612283-3ea9689cbbe4"), "Sports Bra – model in athletic top, confident pose",
                        Pexl(7763190),                     "Sports Bra – woman in gym wear, studio shot"),
                    Meta(["Black","Dusty Pink","Forest Green"],["#1A1A1A","#C4A0A0","#4A6741"],
                         SwimSizes, ["sports-bra","gym","womens","yoga","medium-support","nike"],
                         ["Material","Support","Padding","Back","Underband","Care"],
                         ["78% Polyester 22% Spandex","Medium Impact","Removable Foam","Racer-Back","Wide Elastic","Hand Wash Cold"],
                         "Style code: NK-GYM-W-078. Do not tumble dry."),
                    T, T
                },
                {
                    79, "Slim-Fit Training Tee", "NK-GYM-M-079",
                    "Breathable slim-fit training tee. Lightweight and quick-drying.",
                    "Cut slim through the chest and shoulders, this training tee is made from a soft Dri-FIT knit that pulls sweat away from the skin and dries rapidly. Flatlock seams sit flat against the body to eliminate chafing during repetitive movements. An understated design works in the gym and straight on to the street.",
                    24.99m, 22, 5, "Men",
                    ImgJson(
                        Unsp("1703531293255-0b16d10fe09f"), "Slim-Fit Training Tee – male model in athletic tee",
                        Pexl(18516993),                    "Slim-Fit Training Tee – man in fitted black shirt, gym-ready look",
                        Pexl(26447865),                    "Slim-Fit Training Tee – man in T-shirt, standing pose"),
                    Meta(["Black","White","Electric Blue"],["#1A1A1A","#F5F5F5","#0052CC"],
                         TopSizes, ["tee","training","mens","gym","activewear","nike"],
                         ["Material","Fit","Seams","Technology","Hem","Care"],
                         ["100% Polyester","Slim","Flatlock","Dri-FIT","Straight","Machine Wash Cold"],
                         "Style code: NK-GYM-M-079. Recycled fabric."),
                    T, T
                },
                {
                    80, "Yoga Flow Leggings", "HM-GYM-W-080",
                    "Ultra-soft high-waist leggings designed for yoga and studio workouts.",
                    "Made from a buttery-soft four-way stretch fabric with a matte finish, these yoga leggings feel like a second skin. The wide waistband folds down to a mid-rise for supported seated poses, or stays high for standing flows. The fabric is squat-proof, odour-resistant and quick-drying. An internal waistband pocket holds a key or card.",
                    44.99m, 22, 3, "Women",
                    ImgJson(
                        Unsp("1584863495140-a320b13a11a8"), "Yoga Flow Leggings – woman in black leggings in yoga pose",
                        Unsp("1515614557830-ae0df9016e19"), "Yoga Flow Leggings – woman in athletic wear, relaxed pose",
                        Pexl(9695914),                     "Yoga Flow Leggings – model in studio activewear look"),
                    Meta(["Midnight Black","Slate Blue","Berry"],["#1A1A1A","#6A7F9C","#7D3354"],
                         SwimSizes, ["leggings","yoga","womens","studio","activewear"],
                         ["Material","Waistband","Finish","Pocket","Odour Control","Care"],
                         ["80% Nylon 20% Lycra","Wide Fold-Down","Matte","Internal Key Pocket","Yes","Machine Wash 30°C"],
                         "Style code: HM-GYM-W-080. Do not iron."),
                    T, T
                },
            });

            // ── Swimwear (catId=23) ───────────────────────────────────────────
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    81, "Triangle Bikini Top", "ZRA-SWIM-W-081",
                    "Adjustable tie-front triangle bikini top. Mix and match with any of our bottoms.",
                    "Crafted from a chlorine-resistant fabric with UV50+ protection, this triangle bikini top is built for sun-filled days at the beach or pool. Adjustable tie straps at the neck and back allow a personalised fit, and removable padding provides optional shaping. The fabric retains its shape and colour after repeated exposure to salt water, chlorine and sunscreen.",
                    34.99m, 23, 2, "Women",
                    ImgJson(
                        Unsp("1623039497026-00af61471107"), "Triangle Bikini Top – woman in bikini posing confidently",
                        Unsp("1495890238575-bc6ea0503834"), "Triangle Bikini Top – woman in swimwear on sandy beach",
                        Pexl(15461326),                    "Triangle Bikini Top – model in swimwear, studio product shot"),
                    Meta(["Coral","Cobalt Blue","Leopard Print"],["#FF6B6B","#0047AB","#C19A6B"],
                         SwimSizes, ["bikini","swimwear","womens","beach","holiday"],
                         ["Material","Protection","Padding","Closure","Resistance","Care"],
                         ["82% Polyamide 18% Elastane","UPF 50+","Removable","Adjustable Tie","Chlorine-Resistant","Hand Wash Cold"],
                         "Style code: ZRA-SWIM-W-081. Sold separately from bottoms."),
                    T, T
                },
                {
                    82, "High-Waist Bikini Bottom", "ZRA-SWIM-W-082",
                    "Flattering high-waist bikini brief. Full coverage with a retro-modern silhouette.",
                    "These high-waist bikini bottoms offer full seat coverage with a vintage-inspired cut that flatters all body types. The wide waistband creates a smooth line without digging in. Made from the same chlorine-resistant, quick-drying fabric as our tops for complete mix-and-match freedom.",
                    29.99m, 23, 2, "Women",
                    ImgJson(
                        Unsp("1495890238575-bc6ea0503834"), "High-Waist Bikini Bottom – woman in high-waist swimwear on beach",
                        Unsp("1623039497026-00af61471107"), "High-Waist Bikini Bottom – model in bikini bottoms, beach shoot",
                        Pexl(15759423),                    "High-Waist Bikini Bottom – woman in swimwear in sunlight"),
                    Meta(["Coral","Cobalt Blue","Forest Green"],["#FF6B6B","#0047AB","#355E3B"],
                         SwimSizes, ["bikini","swimwear","womens","high-waist","beach"],
                         ["Material","Rise","Coverage","Resistance","Waistband","Care"],
                         ["82% Polyamide 18% Elastane","High","Full Seat","Chlorine-Resistant","Wide Flat","Hand Wash Cold"],
                         "Style code: ZRA-SWIM-W-082. Mix with any Zara swimwear top."),
                    T, T
                },
                {
                    83, "Plunge-Neck One-Piece Swimsuit", "ZRA-SWIM-W-083",
                    "Elegant plunge-neck one-piece with ruched side detailing. For pool and beach.",
                    "A sophisticated one-piece swimsuit with a deep V plunge neckline and ruched side panels that create a flattering gather at the waist. Built-in underwiring and removable padding provide support and shape without a separate bra. The adjustable shoulder straps ensure a secure fit for swimming as well as poolside lounging.",
                    69.99m, 23, 2, "Women",
                    ImgJson(
                        Unsp("1623039497026-00af61471107"), "Plunge-Neck One-Piece Swimsuit – woman in one-piece swimsuit posing",
                        Unsp("1495890238575-bc6ea0503834"), "Plunge-Neck One-Piece Swimsuit – model in elegant swimsuit, beach",
                        Pexl(3444499),                     "Plunge-Neck One-Piece Swimsuit – woman in sleek swimwear, product view"),
                    Meta(["Black","Ivory","Sage"],["#1A1A1A","#FFFFF0","#8FAF8F"],
                         SwimSizes, ["one-piece","swimsuit","womens","elegant","holiday"],
                         ["Material","Neckline","Padding","Straps","Lining","Care"],
                         ["80% Polyamide 20% Elastane","Deep Plunge V","Removable Underwired","Adjustable","Fully Lined","Hand Wash Cold"],
                         "Style code: ZRA-SWIM-W-083. UPF 30+ protection."),
                    T, T
                },
                {
                    84, "Classic Swim Shorts", "HM-SWIM-M-084",
                    "Quick-dry swim shorts with an elasticated waist and mesh lining.",
                    "These swim shorts are made from a lightweight technical fabric that dries rapidly after leaving the water. A mesh inner lining provides comfort and modesty, while the elasticated waistband with internal drawcord allows a personalised fit. Side pockets and a back Velcro pocket keep essentials secure. Versatile enough to double as casual shorts.",
                    29.99m, 23, 3, "Men",
                    ImgJson(
                        Unsp("1495890238575-bc6ea0503834"), "Classic Swim Shorts – man in swim shorts on beach",
                        Pexl(6764007),                     "Classic Swim Shorts – man in casual shorts, summer styling",
                        Pexl(11434887),                    "Classic Swim Shorts – male model in summer outfit, street style"),
                    Meta(["Navy","Khaki","Black"],["#1C2B4B","#C2A882","#1A1A1A"],
                         SwimSizes, ["swim-shorts","swimwear","mens","beach","holiday","quick-dry"],
                         ["Material","Lining","Waist","Pockets","Length","Care"],
                         ["100% Polyester","Mesh Inner","Elastic Drawcord","2 Side + 1 Back Velcro","Mid-Thigh","Machine Wash 30°C"],
                         "Style code: HM-SWIM-M-084. UPF 40+ sun protection."),
                    T, T
                },
                {
                    85, "Printed Board Shorts", "NK-SWIM-M-085",
                    "Bold graphic board shorts with Dri-FIT technology. Made for surf and pool.",
                    "These board shorts feature Nike's Dri-FIT technology in a lightweight woven fabric that moves freely in and out of the water. A bold graphic print adds personality, while the 20-inch length offers good coverage for active water sports. The elastic waistband with lace-up closure provides a surf-ready, adjustable fit.",
                    44.99m, 23, 5, "Men",
                    ImgJson(
                        Unsp("1623039497026-00af61471107"), "Printed Board Shorts – man in graphic board shorts, beach pose",
                        Pexl(6764007),                     "Classic Board Shorts – man in blue denim shorts and sneakers",
                        Pexl(9955748),                     "Printed Board Shorts – male model in casual shorts, outdoor shoot"),
                    Meta(["Ocean Blue","Tropical White","Black"],["#006994","#F5F5F0","#1A1A1A"],
                         SwimSizes, ["board-shorts","swimwear","mens","surf","beach","nike"],
                         ["Material","Technology","Length","Waist","Pockets","Care"],
                         ["100% Polyester","Dri-FIT","20 Inch","Elastic + Lace-Up","2 Side Pockets","Machine Wash Cold"],
                         "Style code: NK-SWIM-M-085. Do not bleach."),
                    T, T
                },
            });

            // ── Coats & Raincoats (catId=24) ──────────────────────────────────
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    86, "Classic Belted Trench Coat", "ZRA-COAT-W-086",
                    "Timeless double-breasted trench coat in water-repellent cotton gabardine.",
                    "A wardrobe investment crafted from a tightly woven cotton gabardine treated with a durable water-repellent finish. The double-breasted front with tortoiseshell buttons, storm flap and epaulettes remain faithful to the iconic trench silhouette. A self-fabric belt cinches the waist for shape. The removable inner lining extends the wearable season into late autumn.",
                    199.99m, 24, 2, "Women",
                    ImgJson(
                        Unsp("1744112908363-e3014e0553c0"), "Classic Belted Trench Coat – woman in camel trench coat posing",
                        Unsp("1653660666869-2345adc51155"), "Classic Belted Trench Coat – woman in trench coat and sunglasses",
                        Dumj("womens-dresses/marni-red-&-black-suit/1.webp")),
                    Meta(["Camel","Black","British Khaki"],["#C19A6B","#1A1A1A","#B5A27A"],
                         TopSizes, ["trench-coat","coat","womens","classic","smart","water-repellent"],
                         ["Material","Finish","Closure","Belt","Lining","Care"],
                         ["100% Cotton Gabardine","DWR Water-Repellent","Double-Breasted","Self-Fabric Belt","Removable","Dry Clean Only"],
                         "Style code: ZRA-COAT-W-086. Storm flap and gun flap included."),
                    T, T
                },
                {
                    87, "Oversized Trench Coat", "ZRA-COAT-W-087",
                    "Contemporary oversized trench with clean lines and a relaxed modern silhouette.",
                    "This oversized take on the classic trench coat is designed to be worn open as a statement layer or belted for a more defined look. The clean, minimal construction removes traditional details in favour of a sharp, contemporary aesthetic. Hidden press-stud closure keeps the front smooth when worn open. Deep side pockets add practicality.",
                    169.99m, 24, 2, "Women",
                    ImgJson(
                        Unsp("1724709972210-4beb408de580"), "Oversized Trench Coat – woman in trench coat, relaxed pose",
                        Unsp("1713594863204-5763a3733eaa"), "Oversized Trench Coat – woman in long coat, street style",
                        Unsp("1632072602520-bf48499df391"), "Oversized Trench Coat – model in trench coat and hat, editorial look"),
                    Meta(["Cream","Caramel","Forest Green"],["#FFFDD0","#C19A6B","#355E3B"],
                         TopSizes, ["trench-coat","coat","womens","oversized","minimal"],
                         ["Material","Fit","Closure","Pockets","Length","Care"],
                         ["65% Polyester 35% Cotton","Oversized","Hidden Press-Stud","2 Deep Side","Midi","Machine Wash 30°C"],
                         "Style code: ZRA-COAT-W-087. Contrast tonal stitching."),
                    T, T
                },
                {
                    88, "Men's Hooded Raincoat", "HM-COAT-M-088",
                    "Waterproof hooded anorak with taped seams. Built for wet weather.",
                    "When the weather turns, this technical raincoat keeps you dry without sacrificing style. The 10,000 mm hydrostatic head fabric repels heavy rainfall, and fully taped seams prevent water ingress at every stitch. A packable hood with a stiff brim keeps rain off the face, and the hem can be cinched for a more fitted silhouette. Packs into its own front pocket.",
                    99.99m, 24, 3, "Men",
                    ImgJson(
                        Unsp("1487793433179-ce0b55eda342"), "Mens Hooded Raincoat – man in hooded coat, outdoors",
                        Unsp("1514564652994-565a3bf3a25b"), "Mens Hooded Raincoat – man in waterproof jacket in rain",
                        Pexl(9955748),                     "Mens Hooded Raincoat – male model in jacket, street style"),
                    Meta(["Olive","Black","Electric Blue"],["#6B7C5C","#1A1A1A","#0052CC"],
                         TopSizes, ["raincoat","waterproof","mens","outdoor","packable"],
                         ["Material","Waterproofing","Seams","Hood","Packable","Care"],
                         ["100% Nylon","10,000 mm HH","Fully Taped","Adjustable Wired Brim","Yes — Front Pocket","Machine Wash 30°C"],
                         "Style code: HM-COAT-M-088. Breathability rating: 8,000 g/m2/24h."),
                    T, T
                },
                {
                    89, "Waterproof Parka Coat", "HM-COAT-W-089",
                    "Long parka with faux-fur trim hood and waterproof shell. Winter-ready.",
                    "Combining serious weather protection with polished styling, this parka features a waterproof outer shell, a cosy faux-fur trimmed hood, and a removable padded inner for layering flexibility. The extended length provides extra coverage against wind and rain, and internal storm cuffs seal out drafts at the wrists. A versatile everyday coat for the coldest months.",
                    139.99m, 24, 3, "Women",
                    ImgJson(
                        Unsp("1461935793258-ac2ac2c930b2"), "Waterproof Parka Coat – woman in brown parka with backpack in snow",
                        Unsp("1675942154414-aaf2def8cabb"), "Waterproof Parka Coat – woman in grey coat posing outdoors",
                        Pexl(15461326),                    "Waterproof Parka Coat – female model in warm parka, studio shot"),
                    Meta(["Black","Olive","Navy"],["#1A1A1A","#6B7C5C","#1C2B4B"],
                         TopSizes, ["parka","coat","womens","waterproof","winter","faux-fur"],
                         ["Material","Fill","Hood","Inner","Length","Care"],
                         ["Outer: 100% Polyester","Padded Inner Removable","Faux-Fur Trim Detachable","Quilted Liner","Longline","Dry Clean Outer"],
                         "Style code: HM-COAT-W-089. Storm cuffs and draft excluder at hem."),
                    T, T
                },
                {
                    90, "Wool Blend Overcoat", "RL-COAT-W-090",
                    "Elegant single-breasted overcoat in a luxurious Italian wool blend.",
                    "Tailored in a rich wool-cashmere blend sourced from Italian mills, this longline overcoat drapes beautifully and offers natural warmth. The single-breasted construction with notch lapels creates a clean, versatile silhouette that moves effortlessly from the office to the weekend. A half-lining in satin allows the coat to be worn over both knitwear and suiting without snagging.",
                    299.00m, 24, 4, "Women",
                    ImgJson(
                        Unsp("1724709972210-4beb408de580"), "Wool Blend Overcoat – woman in elegant long coat posing",
                        Unsp("1744112908363-e3014e0553c0"), "Wool Blend Overcoat – woman in tailored overcoat, fashion editorial",
                        Dumj("womens-dresses/marni-red-&-black-suit/2.webp"), "Wool Blend Overcoat – product catalog detail shot"),
                    Meta(["Camel","Charcoal","Ivory"],["#C19A6B","#36454F","#FFFFF0"],
                         TopSizes, ["overcoat","coat","womens","wool","luxury","ralph-lauren"],
                         ["Material","Lining","Closure","Length","Warmth","Care"],
                         ["80% Wool 20% Cashmere","Half-Lined Satin","Single-Breasted","Longline","Natural Insulation","Dry Clean Only"],
                         "Style code: RL-COAT-W-090. Dry clean only to preserve wool blend."),
                    T, T
                },
            });

            // ══════════════════════════════════════════════════════════════════
            // 3. UPDATE EXISTING PRODUCTS 1-70 — 3 images each
            //    Strategy:
            //    - Image 1: Unsplash model portrait (verified CDN hash)
            //             OR Pexels verified model photo
            //    - Image 2: Unsplash/Pexels second model shot
            //    - Image 3: DummyJSON product CDN where category matches,
            //               otherwise Unsplash/Pexels third shot
            // ══════════════════════════════════════════════════════════════════

            // ── Shirts (1-5) ─────────────────────────────────────────────────
            migrationBuilder.Sql(Upd(1,
                Pexl(9558709),  "Classic Oxford Button-Down Shirt – man in crisp white shirt, front pose",
                Pexl(6109288),  "Classic Oxford Button-Down Shirt – man in white shirt and scarf, styled look",
                Dumj("mens-shirts/blue-&-black-check-shirt/1.webp")));

            migrationBuilder.Sql(Upd(2,
                Pexl(18297281), "Mens Slim Fit Linen Shirt – male model in blue checked linen shirt",
                Pexl(775771),   "Mens Slim Fit Linen Shirt – man in dress shirt with slim-fit jeans",
                Dumj("mens-shirts/man-plaid-shirt/1.webp")));

            migrationBuilder.Sql(Upd(3,
                Pexl(20080516), "Mens Poplin Dress Shirt – studio portrait, man in checkered dress shirt",
                Pexl(19366877), "Mens Poplin Dress Shirt – male model seated in smart shirt, studio",
                Dumj("mens-shirts/men-check-shirt/1.webp")));

            migrationBuilder.Sql(Upd(4,
                Pexl(1380595),  "Womens Silk Blouse – female model in elegant blouse and black jeans",
                Pexl(16375487), "Womens Silk Blouse – woman posing against white wall, refined styling",
                Dumj("tops/blue-frock/1.webp")));

            migrationBuilder.Sql(Upd(5,
                Pexl(14823052), "Womens Cotton Poplin Shirt – casual model in oversized shirt and jeans",
                Pexl(17265467), "Womens Cotton Poplin Shirt – model in tucked shirt, polished outfit",
                Dumj("tops/short-frock/1.webp")));

            // ── Jumpers & Cardigans (6-10) ────────────────────────────────────
            migrationBuilder.Sql(Upd(6,
                Unsp("1576110598658-096ae24cdb97"), "Mens Lambswool Crew-Neck Jumper – man in brown knit crew-neck, portrait",
                Unsp("1642886512884-529c2fa16aa9"), "Mens Lambswool Crew-Neck Jumper – man in deep red knit sweater",
                Unsp("1574201635302-388dd92a4c3f")));   // grey knit — same hash, different crop applied by CDN

            migrationBuilder.Sql(Upd(7,
                Unsp("1642886512884-529c2fa16aa9"), "Mens Chunky Rib-Knit Sweater – man in bold-colour chunky knit",
                Unsp("1574201635302-388dd92a4c3f"), "Mens Chunky Rib-Knit Sweater – person in grey heavy-gauge knit",
                Pexl(4890733)));                        // man in blue knit sweater

            migrationBuilder.Sql(Upd(8,
                Unsp("1516550570643-7872251e295b"), "Womens Ribbed Turtleneck Sweater – woman in black ribbed knit cardigan",
                Unsp("1611232658526-33dec2927498"), "Womens Ribbed Turtleneck Sweater – woman in dark knit sweater, standing",
                Pexl(2132189)));                        // woman in mustard sweater

            migrationBuilder.Sql(Upd(9,
                Unsp("1628260848185-fcaff647ca9b"), "Womens Open-Front Merino Cardigan – woman in yellow open-front cardigan",
                Unsp("1712068944613-1ff36db16612"), "Womens Open-Front Merino Cardigan – model in black sweater and jeans",
                Pexl(3582500)));                        // woman in white crochet cardigan

            migrationBuilder.Sql(Upd(10,
                Unsp("1516550570643-7872251e295b"), "Womens Oversized Chunky Cardigan – woman in oversized dark cardigan",
                Unsp("1628260848185-fcaff647ca9b"), "Womens Oversized Chunky Cardigan – woman in chunky knit cardigan",
                Pexl(245388)));                         // woman in heather-grey cardigan by window

            // ── Jeans (11-15) ─────────────────────────────────────────────────
            migrationBuilder.Sql(Upd(11,
                Unsp("1614031679232-0dae776a72ee"), "Classic Original Fit Jeans – man in blue denim jeans street pose",
                Unsp("1614483573119-1e3b2be05565"), "Classic Original Fit Jeans – man in hoodie and slim denim jeans",
                Pexl(2815417)));                        // male model in double denim

            migrationBuilder.Sql(Upd(12,
                Unsp("1627379114594-7aff6664cd94"), "Mens Slim Fit Stretch Jeans – man in dark jeans and blazer",
                Unsp("1614031679232-0dae776a72ee"), "Mens Slim Fit Stretch Jeans – slim denim silhouette, street style",
                Pexl(2315311)));                        // person in white shirt and fitted blue jeans

            migrationBuilder.Sql(Upd(13,
                Unsp("1614483573119-1e3b2be05565"), "Mens Straight Leg Jeans – man in straight-cut denim, relaxed pose",
                Pexl(18662550),                    "Mens Straight Leg Jeans – model in jeans and tailored blazer",
                Pexl(3889627)));                        // man in plaid shirt and straight-leg jeans

            migrationBuilder.Sql(Upd(14,
                Unsp("1753395298691-eb93244d76c1"), "Womens High-Rise Skinny Jeans – woman in white shirt and skinny jeans",
                Unsp("1753877439268-6263fc86fdf2"), "Womens High-Rise Skinny Jeans – woman in denim jacket and jeans, Paris",
                Pexl(1380595)));                        // woman posing in sport shirt and black jeans

            migrationBuilder.Sql(Upd(15,
                Unsp("1753877439268-6263fc86fdf2"), "Womens Wide-Leg Mom Jeans – woman in wide-leg denim, street styling",
                Pexl(18168659),                    "Womens Wide-Leg Mom Jeans – woman in wide-cut jeans posing in park",
                Pexl(12610340)));                       // fashion model in statement jeans

            // ── Trousers (16-20) ──────────────────────────────────────────────
            migrationBuilder.Sql(Upd(16,
                Unsp("1700227047786-8835486ba7af"), "Mens Slim Fit Chino Trousers – man in slim tailored trousers and tie",
                Unsp("1638908219803-a142c5c1b5c2"), "Mens Slim Fit Chino Trousers – man in dark trousers front building",
                Pexl(2662794)));                        // male model wearing slim chinos

            migrationBuilder.Sql(Upd(17,
                Unsp("1748500192796-711d2b9384a5"), "Mens Classic Pleated Trousers – young man in formal suit and sunglasses",
                Unsp("1700227047786-8835486ba7af"), "Mens Classic Pleated Trousers – man in tailored trousers and tie",
                Pexl(19357654)));                       // man posing in suit in studio

            migrationBuilder.Sql(Upd(18,
                Unsp("1620122830785-a18b43585b44"), "Mens Relaxed Cargo Trousers – man in casual jacket and trousers, street",
                Pexl(11434887),                    "Mens Relaxed Cargo Trousers – male model in relaxed trousers on street",
                Pexl(3483102)));                        // man in jacket and brown cargo trousers

            migrationBuilder.Sql(Upd(19,
                Unsp("1609941367698-2648ef759ada"), "Womens Wide-Leg Tailored Trousers – woman in white blazer and trousers",
                Unsp("1545935490-7f84f2bf4c15"),   "Womens Wide-Leg Tailored Trousers – woman in tailored blazer pose",
                Pexl(19272278)));                       // model in elegant blazer and wide-leg trousers

            migrationBuilder.Sql(Upd(20,
                Unsp("1747814896398-bdf36d11f596"), "Womens High-Waist Crepe Trousers – woman in blazer suit, monochrome studio",
                Unsp("1609941367698-2648ef759ada"), "Womens High-Waist Crepe Trousers – model in tailored trousers and blazer",
                Pexl(14997427)));                       // woman in tailored high-waist suit

            // ── Shorts (21-25) ────────────────────────────────────────────────
            migrationBuilder.Sql(Upd(21,
                Unsp("1538091015952-7f04178b40f2"), "Mens Slim Fit Chino Shorts – man in bomber jacket and chino shorts",
                Pexl(3483102),                     "Mens Slim Fit Chino Shorts – man in jacket and casual shorts",
                Pexl(9955748)));                        // male model in jacket, shorts visible

            migrationBuilder.Sql(Upd(22,
                Unsp("1614031679232-0dae776a72ee"), "Mens Washed Denim Shorts – man in denim-on-denim street look",
                Pexl(2815417),                     "Mens Washed Denim Shorts – male model in double denim outfit",
                Pexl(3889627)));                        // man in plaid shirt and casual denim

            migrationBuilder.Sql(Upd(23,
                Unsp("1744838337050-608797e60e59"), "Mens Performance Running Shorts – person in bomber and athletic shorts",
                Pexl(11434887),                    "Mens Performance Running Shorts – male model in street sportswear",
                Pexl(17350031)));                       // model in sporty patterned bottoms

            migrationBuilder.Sql(Upd(24,
                Unsp("1753877439268-6263fc86fdf2"), "Womens High-Waisted Denim Shorts – woman in denim jacket and shorts",
                Pexl(8991032),                     "Womens High-Waisted Denim Shorts – woman in black jacket and denim",
                Pexl(13391056)));                       // woman in white crop top and denim shorts

            migrationBuilder.Sql(Upd(25,
                Unsp("1745142640164-74774600af1d"), "Womens Linen-Blend Shorts – woman in casual summer outfit walking",
                Pexl(25786705),                    "Womens Linen-Blend Shorts – female model in skirt and crop top, street",
                Pexl(19163488)));                       // woman in shorts and heels, full-length pose

            // ── Autumn Jackets (26-30) ────────────────────────────────────────
            migrationBuilder.Sql(Upd(26,
                Unsp("1538091015952-7f04178b40f2"), "Mens Water-Resistant Bomber Jacket – man in bomber jacket on car",
                Unsp("1744838337050-608797e60e59"), "Mens Water-Resistant Bomber Jacket – person in bomber and black trousers",
                Pexl(9955748)));                        // male model in jacket

            migrationBuilder.Sql(Upd(27,
                Unsp("1744838337050-608797e60e59"), "Mens Quilted Lightweight Jacket – person in quilted jacket and trousers",
                Unsp("1620122830785-a18b43585b44"), "Mens Quilted Lightweight Jacket – man in jacket, urban street style",
                Pexl(16751012)));                       // model in beige jacket and cargo trousers

            migrationBuilder.Sql(Upd(28,
                Unsp("1538091015952-7f04178b40f2"), "Mens Harrington Jacket – man in classic jacket, seated outdoors",
                Pexl(10274665),                    "Mens Harrington Jacket – man in brown jacket, confident studio pose",
                Pexl(13937357)));                       // man in black leather jacket, urban pose

            migrationBuilder.Sql(Upd(29,
                Unsp("1555991610-dc16d095c6f5"), "Womens Padded Utility Jacket – woman in black bomber jacket, daytime",
                Pexl(2896428),                  "Womens Padded Utility Jacket – woman in fashionable utility jacket",
                Pexl(14495270)));                   // portrait of woman in stylish coat

            migrationBuilder.Sql(Upd(30,
                Unsp("1555991610-dc16d095c6f5"), "Womens Cropped Windbreaker Jacket – woman in bomber jacket on street",
                Pexl(3398192),                  "Womens Cropped Windbreaker Jacket – woman in black coat, fashion pose",
                Pexl(7236497)));                    // woman in brown coat with hand in pocket

            // ── Winter Jackets (31-35) ────────────────────────────────────────
            migrationBuilder.Sql(Upd(31,
                Unsp("1487793433179-ce0b55eda342"), "Mens Down-Fill Parka – man in beige parka in forest setting",
                Unsp("1514564652994-565a3bf3a25b"), "Mens Down-Fill Parka – man in parka jacket in snowy landscape",
                Pexl(7037432)));                        // male models in down jackets on snow

            migrationBuilder.Sql(Upd(32,
                Unsp("1514564652994-565a3bf3a25b"), "Mens Wool Blend Peacoat – man in classic winter coat in snow",
                Unsp("1487793433179-ce0b55eda342"), "Mens Wool Blend Peacoat – man in structured winter coat outdoors",
                Pexl(16168570)));                       // man in tailored coat posing indoors

            migrationBuilder.Sql(Upd(33,
                Unsp("1487793433179-ce0b55eda342"), "Mens Padded Duvet Coat – man in oversized padded coat outdoors",
                Pexl(21858851),                    "Mens Padded Duvet Coat – man in warm hat and padded jacket in winter",
                Pexl(7037432)));                        // models in heavy-duty down jackets on snow

            migrationBuilder.Sql(Upd(34,
                Unsp("1675942154414-aaf2def8cabb"), "Womens Oversized Puffer Jacket – woman in grey puffer coat posing",
                Unsp("1461935793258-ac2ac2c930b2"), "Womens Oversized Puffer Jacket – woman in parka with backpack in snow",
                Pexl(15759423)));                       // woman in beige winter jacket in snowy landscape

            migrationBuilder.Sql(Upd(35,
                Unsp("1724709972210-4beb408de580"), "Womens Faux Fur Trim Parka – woman in long coat posing, fashion look",
                Unsp("1675942154414-aaf2def8cabb"), "Womens Faux Fur Trim Parka – woman in grey coat, outdoor portrait",
                Pexl(14495270)));                       // woman in stylish coat, portrait

            // ── Leather Jackets (36-40) ───────────────────────────────────────
            migrationBuilder.Sql(Upd(36,
                Unsp("1607149553615-e9d1f694c1ae"), "Mens Classic Biker Leather Jacket – man in black leather jacket",
                Unsp("1632958978877-69406b688b11"), "Mens Classic Biker Leather Jacket – man in leather jacket and tie",
                Pexl(15869797)));                       // male model in leather jacket, sunglasses

            migrationBuilder.Sql(Upd(37,
                Unsp("1761882376368-f65ef57181d5"), "Mens Slim Fit Leather Jacket – man in leather jacket and sunglasses",
                Unsp("1632958983989-49773325c326"), "Mens Slim Fit Leather Jacket – man in leather and tie, close-up",
                Pexl(10274665)));                       // man in brown leather jacket, studio pose

            migrationBuilder.Sql(Upd(38,
                Unsp("1632958978877-69406b688b11"), "Mens Distressed Brown Leather Jacket – man in leather jacket and tie",
                Unsp("1607149553615-e9d1f694c1ae"), "Mens Distressed Brown Leather Jacket – street style leather jacket",
                Pexl(17350031)));                       // model in patterned pants and jacket

            migrationBuilder.Sql(Upd(39,
                Unsp("1730727484993-ec77189e0a4a"), "Womens Cropped Black Leather Jacket – woman in leather jacket and hat",
                Unsp("1621871675550-433cf99706a8"), "Womens Cropped Black Leather Jacket – woman in leather and green skirt",
                Pexl(8441422)));                        // model in black turtleneck leather jacket

            migrationBuilder.Sql(Upd(40,
                Unsp("1621871675550-433cf99706a8"), "Womens Oversized Faux Leather Jacket – woman in leather skirt and jacket",
                Unsp("1730727484993-ec77189e0a4a"), "Womens Oversized Faux Leather Jacket – woman in leather jacket, portrait",
                Pexl(11555859)));                       // woman in black leather jacket and pants

            // ── Dresses & Skirts (41-45) ──────────────────────────────────────
            migrationBuilder.Sql(Upd(41,
                Pexl(9512043),  "Floral Wrap Midi Dress – woman in floral dress on catwalk",
                Pexl(2474256),  "Floral Wrap Midi Dress – model standing in elegant floral dress",
                Dumj("tops/girl-summer-dress/1.webp")));

            migrationBuilder.Sql(Upd(42,
                Pexl(17570989), "Classic Little Black Mini Dress – model in black dress posing in studio",
                Pexl(30736117), "Classic Little Black Mini Dress – elegant fashion model in studio shoot",
                Dumj("womens-dresses/black-women''s-gown/1.webp")));

            migrationBuilder.Sql(Upd(43,
                Pexl(8751237),  "Maxi Ruffle Hem Dress – young woman in white maxi dress, arms raised",
                Pexl(30736118), "Maxi Ruffle Hem Dress – high-fashion model in elegant dress, studio",
                Dumj("womens-dresses/dress-pea/1.webp")));

            migrationBuilder.Sql(Upd(44,
                Pexl(20016340), "Womens A-Line Mini Skirt – woman in mini skirt and crop top",
                Pexl(25786705), "Womens A-Line Mini Skirt – female model in skirt on street",
                Dumj("womens-dresses/corset-with-black-skirt/1.webp")));

            migrationBuilder.Sql(Upd(45,
                Pexl(19163488), "Womens Satin Midi Pencil Skirt – woman in skirt and heels, full pose",
                Pexl(14997427), "Womens Satin Midi Pencil Skirt – woman in tailored skirt and blazer",
                Dumj("womens-dresses/corset-leather-with-skirt/1.webp")));

            // ── Suits & Blazers (46-50) ───────────────────────────────────────
            migrationBuilder.Sql(Upd(46,
                Unsp("1695857596080-a15b7d35c35b"), "Mens Classic Slim Fit Suit – man in black tuxedo suit, posed",
                Unsp("1748500192796-711d2b9384a5"), "Mens Classic Slim Fit Suit – man in suit and sunglasses",
                Pexl(15092611)));                       // man in dark slim-fit suit, portrait

            migrationBuilder.Sql(Upd(47,
                Unsp("1700227047786-8835486ba7af"), "Mens Double-Breasted Wool Blazer – man in suit and tie posing",
                Unsp("1638908219803-a142c5c1b5c2"), "Mens Double-Breasted Wool Blazer – man in black suit in front of building",
                Pexl(18348433)));                       // man in vest and suit

            migrationBuilder.Sql(Upd(48,
                Unsp("1695857596080-a15b7d35c35b"), "Mens Slim Fit Black Tuxedo Suit – man in tuxedo, formal pose",
                Unsp("1645305783409-afea2f9ee251"), "Mens Slim Fit Black Tuxedo Suit – man in grey suit and white trainers",
                Pexl(3217111)));                        // man in black suit jacket with teal bowtie

            migrationBuilder.Sql(Upd(49,
                Unsp("1609941367698-2648ef759ada"), "Womens Tailored Single-Breasted Blazer – woman in white blazer and dress",
                Unsp("1747814896398-bdf36d11f596"), "Womens Tailored Single-Breasted Blazer – model in blazer, studio shoot",
                Pexl(19272278)));                       // model in elegant blazer and trousers

            migrationBuilder.Sql(Upd(50,
                Unsp("1545935490-7f84f2bf4c15"),   "Womens Power Suit Set – woman in blazer, confident full-length pose",
                Unsp("1609941367698-2648ef759ada"), "Womens Power Suit Set – woman in white blazer, polished look",
                Pexl(17397914)));                       // woman in sharp suit with bag

            // ── Shoes (51-55) ─────────────────────────────────────────────────
            migrationBuilder.Sql(Upd(51,
                Pexl(17427589), "Mens Classic White Leather Sneakers – male model in black jacket and white shoes",
                Pexl(11434887), "Mens Classic White Leather Sneakers – male model on city street, shoes on show",
                Dumj("mens-shoes/puma-future-rider-trainers/1.webp")));

            migrationBuilder.Sql(Upd(52,
                Pexl(19357654), "Mens Brogue Oxford Shoes – man in full suit, formal shoe styling",
                Pexl(18348433), "Mens Brogue Oxford Shoes – man in suit and vest, dress shoe look",
                Dumj("mens-shoes/sports-sneakers-off-white-&-red/1.webp")));

            migrationBuilder.Sql(Upd(53,
                Pexl(9955748),  "Mens React Running Trainers – male model in jacket, trainer styling",
                Pexl(17350031), "Mens React Running Trainers – model in sporty outfit with trainers",
                Dumj("mens-shoes/nike-air-jordan-1-red-and-black/1.webp")));

            migrationBuilder.Sql(Upd(54,
                Pexl(19163488), "Womens Classic Ballet Flats – woman in skirt and flats, elegant pose",
                Pexl(25786705), "Womens Classic Ballet Flats – female model in skirt, shoes in frame",
                Dumj("womens-shoes/calvin-klein-heel-shoes/1.webp")));

            migrationBuilder.Sql(Upd(55,
                Pexl(14997427), "Womens Leather Ankle Boots – woman in tailored skirt and ankle boots",
                Pexl(20016340), "Womens Leather Ankle Boots – model in mini skirt, boot detail",
                Dumj("womens-shoes/golden-shoes-woman/1.webp")));

            // ── Bags (56-60) ──────────────────────────────────────────────────
            migrationBuilder.Sql(Upd(56,
                Pexl(1936848),  "Womens Structured Leather Tote – woman wearing brown leather tote bag",
                Pexl(1653222),  "Womens Structured Leather Tote – model holding structured leather bag",
                Dumj("womens-bags/heshe-women''s-leather-bag/1.webp")));

            migrationBuilder.Sql(Upd(57,
                Pexl(23023550), "Womens Chain Shoulder Bag – brunette posing with quilted chain handbag",
                Pexl(27151080), "Womens Chain Shoulder Bag – woman holding designer shoulder bag",
                Dumj("womens-bags/prada-women-bag/1.webp")));

            migrationBuilder.Sql(Upd(58,
                Pexl(5745781),  "Womens Mini Crossbody Bag – stylish woman with small bag in golden sunlight",
                Pexl(12002801), "Womens Mini Crossbody Bag – woman in coat carrying compact crossbody",
                Dumj("womens-bags/blue-women''s-handbag/1.webp")));

            migrationBuilder.Sql(Upd(59,
                Pexl(19711183), "Mens Canvas Shopper Bag – models with leather and canvas bags",
                Pexl(11124945), "Mens Canvas Shopper Bag – person carrying casual canvas tote",
                Dumj("womens-bags/white-faux-leather-backpack/1.webp")));

            migrationBuilder.Sql(Upd(60,
                Pexl(17397914), "Womens Quilted Clutch Bag – woman in sharp suit holding elegant clutch",
                Pexl(5745781),  "Womens Quilted Clutch Bag – stylish woman with handbag, warm light",
                Dumj("womens-bags/women-handbag-black/1.webp")));

            // ── Underwear & Basics (61-65) ────────────────────────────────────
            migrationBuilder.Sql(Upd(61,
                Pexl(18516993), "Mens 3-Pack Stretch Cotton Briefs – man in fitted black shirt and pants",
                Pexl(26447865), "Mens 3-Pack Stretch Cotton Briefs – male model in T-shirt and cap",
                Pexl(19099186)));                       // man in black T-shirt, clean studio shot

            migrationBuilder.Sql(Upd(62,
                Pexl(19099186), "Mens Classic Stretch Boxers – man in black T-shirt, minimal studio",
                Pexl(17718201), "Mens Classic Stretch Boxers – portrait of man in clean black shirt",
                Pexl(26447865)));                       // man standing in T-shirt and cap

            migrationBuilder.Sql(Upd(63,
                Pexl(2908870),  "Womens 5-Pack Cotton Hipster Briefs – woman in casual knitwear, soft styling",
                Pexl(4620610),  "Womens 5-Pack Cotton Hipster Briefs – model in white sweater, studio light",
                Pexl(2132189)));                        // woman in mustard fitted top

            migrationBuilder.Sql(Upd(64,
                Pexl(2132189),  "Womens Underwired T-Shirt Bra – close-up of woman in mustard fitted top",
                Pexl(3582500),  "Womens Underwired T-Shirt Bra – woman in fitted white crochet top",
                Pexl(15915189)));                       // woman in fitted pink hooded top

            migrationBuilder.Sql(Upd(65,
                Pexl(245388),   "Womens Seamless Soft-Cup Bralette – woman in soft grey cardigan by window",
                Pexl(15915189), "Womens Seamless Soft-Cup Bralette – woman in fitted pink hooded top",
                Pexl(4620610)));                        // model in white sweater, soft studio

            // ── Glasses (66-70) ───────────────────────────────────────────────
            migrationBuilder.Sql(Upd(66,
                Pexl(17140041), "Mens Classic Aviator Sunglasses – male model wearing aviator sunglasses",
                Pexl(20080516), "Mens Classic Aviator Sunglasses – man in dress shirt and sunglasses, studio",
                Dumj("sunglasses/black-sun-glasses/1.webp")));

            migrationBuilder.Sql(Upd(67,
                Unsp("1659805853744-d495c20ba093"), "Mens Square Frame Reading Glasses – person in glasses and scarf",
                Pexl(9558709),                     "Mens Square Frame Reading Glasses – man in white shirt minimal look",
                Dumj("sunglasses/classic-sun-glasses/1.webp")));

            migrationBuilder.Sql(Upd(68,
                Pexl(16375487), "Womens Cat-Eye Sunglasses – female model posing in front of white wall",
                Pexl(1380595),  "Womens Cat-Eye Sunglasses – woman in sunglasses posing confidently",
                Dumj("sunglasses/green-and-black-glasses/1.webp")));

            migrationBuilder.Sql(Upd(69,
                Pexl(17265467), "Womens Oversized Round Sunglasses – model in silver top and jeans with sunglasses",
                Pexl(14823052), "Womens Oversized Round Sunglasses – casual model in T-shirt, sunglasses styling",
                Dumj("sunglasses/sunglasses/1.webp")));

            migrationBuilder.Sql(Upd(70,
                Pexl(2908870),  "Womens Tortoiseshell Frame Glasses – woman in soft knitwear, scholarly style",
                Pexl(15915189), "Womens Tortoiseshell Frame Glasses – woman in pink top wearing glasses",
                Dumj("sunglasses/classic-sun-glasses/2.webp")));

            // ── Reset identity sequences so future EF inserts do not conflict ──
            migrationBuilder.Sql("""
                SELECT setval(pg_get_serial_sequence('"ProductCategories"', 'Id'), (SELECT MAX("Id") FROM "ProductCategories"));
                SELECT setval(pg_get_serial_sequence('"Products"', 'Id'), (SELECT MAX("Id") FROM "Products"));
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove new products and categories
            migrationBuilder.Sql("""
                DELETE FROM "Products" WHERE "Id" BETWEEN 71 AND 90;
                DELETE FROM "ProductCategories" WHERE "Id" IN (20, 21, 22, 23, 24);
                """);
            // Image restoration for existing products is non-destructive — re-run
            // the previous UpdateProductImages migration to restore 2-image state.
        }
    }
}
