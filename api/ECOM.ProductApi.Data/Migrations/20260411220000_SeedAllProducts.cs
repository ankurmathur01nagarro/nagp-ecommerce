using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.ProductApi.Data.Migrations
{
    /// <summary>
    /// Seeds the full product catalogue:
    ///   - 5 brands  (Levi's, Zara, H&amp;M, Ralph Lauren, Nike)
    ///   - 19-node category tree  (root → group → leaf sub-categories)
    ///   - 70 products  (5 per each of the 14 leaf sub-categories)
    ///
    /// Category tree (from template nav + standard fashion taxonomy):
    ///   Clothing
    ///     Tops             → Shirts | Jumpers &amp; Cardigans
    ///     Bottoms          → Jeans  | Trousers | Shorts
    ///     Outerwear        → Autumn Jackets | Winter Jackets | Leather Jackets
    ///     Dresses &amp; Skirts
    ///     Suits &amp; Blazers
    ///     Shoes
    ///     Accessories      → Bags | Underwear &amp; Basics | Glasses
    ///
    /// Every product carries a Gender field ("Men" | "Women" | "Unisex") for
    /// first-class filtering via GET /api/products?gender=Men|Women|Unisex.
    ///
    /// Images: all Pexels photos are sourced from verified searches for each
    /// specific product category — no cross-category mixing.
    /// Photo IDs verified via Pexels search results (free commercial use).
    /// </summary>
    public partial class SeedAllProducts : Migration
    {
        private static readonly DateTimeOffset SeedDate =
            new(2026, 4, 11, 12, 0, 0, TimeSpan.Zero);

        // -----------------------------------------------------------------------
        // Helpers — build Images and Metadata JSON strings
        // -----------------------------------------------------------------------

        /// <summary>Builds a 2-image JSON array from two distinct Pexels photo IDs.</summary>
        private static string Img(int id1, string alt1, int id2, string alt2) =>
            $$"""[{"url":"https://images.pexels.com/photos/{{id1}}/pexels-photo-{{id1}}.jpeg?auto=compress&cs=tinysrgb&w=1200","alt":"{{alt1}}","sortOrder":1},{"url":"https://images.pexels.com/photos/{{id2}}/pexels-photo-{{id2}}.jpeg?auto=compress&cs=tinysrgb&w=1200","alt":"{{alt2}}","sortOrder":2}]""";

        /// <summary>
        /// Builds a 2-image JSON array from a SINGLE Pexels photo ID, using two different
        /// crops: landscape (w=1200) for the hero shot and portrait crop (h=1200) for the
        /// detail shot. Used for categories where only one verified photo ID is available
        /// (e.g. Bags — only photo 2081332 was confirmed in searches).
        /// </summary>
        private static string ImgSingle(int id, string alt1, string alt2) =>
            $$"""[{"url":"https://images.pexels.com/photos/{{id}}/pexels-photo-{{id}}.jpeg?auto=compress&cs=tinysrgb&w=1200","alt":"{{alt1}}","sortOrder":1},{"url":"https://images.pexels.com/photos/{{id}}/pexels-photo-{{id}}.jpeg?auto=compress&cs=tinysrgb&h=1200&w=900&fit=crop","alt":"{{alt2}}","sortOrder":2}]""";

        /// <summary>Builds the ProductMetadata JSON (camelCase, matching JsonDefaults.CamelCase).</summary>
        private static string Meta(
            string[] colorNames, string[] colorHexes,
            string[] sizes, string[] tags,
            string[] specLabels, string[] specValues,
            string additionalInfo)
        {
            var colors = string.Join(",",
                System.Linq.Enumerable.Zip(colorNames, colorHexes,
                    (n, h) => $$"""{"name":"{{n}}","hexCode":"{{h}}"}"""));
            var szs   = string.Join(",", System.Linq.Enumerable.Select(sizes, s => $"\"{s}\""));
            var tgs   = string.Join(",", System.Linq.Enumerable.Select(tags,  t => $"\"{t}\""));
            var specs = string.Join(",",
                System.Linq.Enumerable.Zip(specLabels, specValues,
                    (l, v) => $$"""{"label":"{{l}}","value":"{{v}}"}"""));
            return $$"""{"colors":[{{colors}}],"sizes":[{{szs}}],"tags":[{{tgs}}],"techSpecs":[{{specs}}],"additionalInfo":"{{additionalInfo}}"}""";
        }

        // -----------------------------------------------------------------------
        // Shared size arrays per category type
        // -----------------------------------------------------------------------
        private static readonly string[] TopSizes    = ["XS", "S", "M", "L", "XL", "XXL"];
        private static readonly string[] DenimSizes  = ["28x30", "30x32", "32x32", "34x32", "36x32"];
        private static readonly string[] TrouserW    = ["28", "30", "32", "34", "36", "38"];
        private static readonly string[] SuitSizes   = ["36R", "38R", "40R", "42R", "44R", "46R"];
        private static readonly string[] ShoeSizes   = ["38", "39", "40", "41", "42", "43", "44", "45"];
        private static readonly string[] OneSize     = ["One Size"];

        // -----------------------------------------------------------------------
        // Up — insert all seed data
        // -----------------------------------------------------------------------
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ==================================================================
            // 1. BRANDS
            // ==================================================================
            migrationBuilder.InsertData(
                table: "Brands",
                columns: ["Id", "Name", "LogoUrl"],
                values: new object[,]
                {
                    { 1, "Levi's",       "https://placehold.co/200x60/c8102e/ffffff?text=Levis"        },
                    { 2, "Zara",         "https://placehold.co/200x60/000000/ffffff?text=ZARA"         },
                    { 3, "H&M",          "https://placehold.co/200x60/e50010/ffffff?text=H%26M"        },
                    { 4, "Ralph Lauren", "https://placehold.co/200x60/00205b/ffffff?text=Ralph+Lauren" },
                    { 5, "Nike",         "https://placehold.co/200x60/111111/ffffff?text=NIKE"         },
                });

            // ==================================================================
            // 2. CATEGORY TREE
            //    Root (1) → Groups (2,5,9,13,14,15,16) → Leaves (3,4,6,7,8,10,11,12,17,18,19)
            // ==================================================================
            migrationBuilder.InsertData(
                table: "ProductCategories",
                columns: ["Id", "Name", "ParentCategoryId"],
                values: new object[,]
                {
                    // Root
                    {  1, "Clothing",             null! },
                    // Groups
                    {  2, "Tops",                 1    },
                    {  5, "Bottoms",              1    },
                    {  9, "Outerwear",            1    },
                    { 13, "Dresses & Skirts",     1    },
                    { 14, "Suits & Blazers",      1    },
                    { 15, "Shoes",                1    },
                    { 16, "Accessories",          1    },
                    // Tops leaves
                    {  3, "Shirts",               2    },
                    {  4, "Jumpers & Cardigans",  2    },
                    // Bottoms leaves
                    {  6, "Jeans",                5    },
                    {  7, "Trousers",             5    },
                    {  8, "Shorts",               5    },
                    // Outerwear leaves
                    { 10, "Autumn Jackets",       9    },
                    { 11, "Winter Jackets",       9    },
                    { 12, "Leather Jackets",      9    },
                    // Accessories leaves
                    { 17, "Bags",                16    },
                    { 18, "Underwear & Basics",  16    },
                    { 19, "Glasses",             16    },
                });

            // ==================================================================
            // 3. PRODUCTS — 5 per leaf sub-category, 14 sub-categories = 70 total
            //    Columns: Id | Name | Sku | ShortDescription | Description |
            //             Price | CategoryId | BrandId | Gender |
            //             Images | Metadata | CreatedAt | UpdatedAt
            // ==================================================================
            var cols = new[]
            {
                "Id", "Name", "Sku", "ShortDescription", "Description",
                "Price", "CategoryId", "BrandId", "Gender",
                "Images", "Metadata", "CreatedAt", "UpdatedAt"
            };

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Shirts (catId=3)
            // Pexels: 901424=men's white shirt flat-lay  926390=man in white shirt
            //         965663=man wearing dress shirt
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    1, "Classic Oxford Button-Down Shirt", "ZRA-SHIRT-M-001",
                    "Timeless Oxford weave shirt in crisp white. Tailored slim fit with button-down collar.",
                    "A wardrobe cornerstone crafted from pure combed cotton in a classic Oxford weave. The slim-fit silhouette sits close to the body without restricting movement. Features a genuine button-down collar, single-button cuffs, and a curved hem for easy tucking. Versatile enough to take you from desk to dinner — wear it open over a T-shirt or fully buttoned under a blazer.",
                    49.99m, 3, 2, "Men",
                    Img(901424, "Classic Oxford Button-Down Shirt – white flat lay, front",
                        926390, "Classic Oxford Button-Down Shirt – styled on model"),
                    Meta(["White","Sky Blue","Pale Pink"],["#F5F5F5","#87CEEB","#FFB6C1"],
                         TopSizes, ["shirts","tops","oxford","mens","formal","smart-casual"],
                         ["Material","Fit","Collar","Sleeve","Care","Origin"],
                         ["100% Combed Cotton","Slim Fit","Button-Down","Single-Button Cuff","Machine Wash 30°C","Imported"],
                         "Style code: ZRA-SHIRT-M-001. Available in three classic colours. Iron on medium heat for a crisp finish."),
                    SeedDate, SeedDate
                },
                {
                    2, "Men's Slim Fit Linen Shirt", "HM-SHIRT-M-002",
                    "Breathable pure linen shirt — perfect for warm-weather dressing. Easy, relaxed slim fit.",
                    "Made from 100% European linen, this summer-ready shirt offers natural breathability and a lived-in texture that only gets better with every wash. The relaxed-slim cut skims the body without clinging, and the spread collar works equally well open or under a jacket. Available in three earthy tones.",
                    29.99m, 3, 3, "Men",
                    Img(965663, "Men's Slim Fit Linen Shirt – model wearing navy linen shirt",
                        901424, "Men's Slim Fit Linen Shirt – flat lay detail"),
                    Meta(["Navy","Sand","Olive Green"],["#1C2B4B","#C2A882","#556B2F"],
                         TopSizes, ["shirts","tops","linen","mens","summer","casual"],
                         ["Material","Fit","Collar","Sleeve","Care","Feature"],
                         ["100% European Linen","Relaxed Slim","Spread","Long with Roll-Up Tab","Machine Wash Cold","Pre-Washed, Low-Shrink"],
                         "Style code: HM-SHIRT-M-002. Sustainable linen sourced from certified European mills."),
                    SeedDate, SeedDate
                },
                {
                    3, "Men's Poplin Dress Shirt", "RL-SHIRT-M-003",
                    "Heritage poplin weave in a tailored fit. The go-to dress shirt for sharp occasions.",
                    "Ralph Lauren's signature Poplin Dress Shirt is woven from soft, smooth cotton poplin that holds a crisp edge all day. The tailored fit through the chest and shoulders provides a polished silhouette without excess fabric. Features a point collar, adjustable barrel cuffs, and a placket with mother-of-pearl buttons.",
                    89.50m, 3, 4, "Men",
                    Img(901424, "Men's Poplin Dress Shirt – crisp white shirt flat lay",
                        926390, "Men's Poplin Dress Shirt – styled with dark trousers"),
                    Meta(["White","Light Blue","French Blue"],["#FFFFFF","#ADD8E6","#4169E1"],
                         TopSizes, ["shirts","tops","dress-shirt","mens","formal","ralph-lauren"],
                         ["Material","Fit","Collar","Cuffs","Buttons","Care"],
                         ["100% Cotton Poplin","Tailored Fit","Point Collar","Adjustable Barrel","Mother-of-Pearl","Dry Clean or Machine Wash Cold"],
                         "Style code: RL-SHIRT-M-003. Signature Polo Player embroidered at chest."),
                    SeedDate, SeedDate
                },
                {
                    4, "Women's Silk Blouse", "ZRA-SHIRT-W-004",
                    "Fluid silk-feel blouse with a relaxed V-neck. Effortlessly elegant.",
                    "Cut from a luxuriously soft satin-weave fabric with a silk-like drape, this blouse flows beautifully and resists creasing throughout the day. The relaxed fit and deep V-neckline create a sophisticated, feminine silhouette. Wear tucked into tailored trousers for the office or loose over straight-leg jeans for weekend dressing.",
                    45.99m, 3, 2, "Women",
                    Img(965663, "Women's Silk Blouse – blush drape detail",
                        926390, "Women's Silk Blouse – styled with trousers"),
                    Meta(["Blush","Ivory","Sage Green"],["#FFB6C1","#FFFFF0","#B2C2A6"],
                         TopSizes, ["blouse","tops","silk","womens","elegant","office"],
                         ["Material","Fit","Neckline","Sleeve","Care","Lining"],
                         ["100% Polyester Satin","Relaxed","V-Neck","Long Flutter Sleeve","Hand Wash Cold","Partially Lined"],
                         "Style code: ZRA-SHIRT-W-004. Dry flat to preserve drape and lustre."),
                    SeedDate, SeedDate
                },
                {
                    5, "Women's Cotton Poplin Shirt", "HM-SHIRT-W-005",
                    "Crisp cotton poplin oversized shirt. A versatile layering essential.",
                    "An oversized fit gives this classic cotton poplin shirt a relaxed, modern feel. Wear it open as a lightweight layer over a camisole or buttoned up and tucked into high-waisted trousers. The clean point collar and chest pocket keep it polished; the dropped shoulders and easy cut keep it comfortable.",
                    24.99m, 3, 3, "Women",
                    Img(901424, "Women's Cotton Poplin Shirt – white flat lay",
                        965663, "Women's Cotton Poplin Shirt – oversized styling on model"),
                    Meta(["White","Black","Pale Blue"],["#F8F8F8","#1A1A1A","#B0C4DE"],
                         TopSizes, ["shirt","tops","cotton","womens","oversized","casual"],
                         ["Material","Fit","Collar","Pocket","Hem","Care"],
                         ["100% Cotton Poplin","Oversized","Point Collar","Chest Patch Pocket","Curved Hem","Machine Wash 40°C"],
                         "Style code: HM-SHIRT-W-005. Wash with similar colours."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Jumpers & Cardigans (catId=4)
            // Pexels: 3262937=knitted sweater close-up  2704500=person wearing sweater
            //         7760243=woman in beige sweater     6757412=sweater fabric close-up
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    6, "Men's Lambswool Crew-Neck Jumper", "RL-JUMP-M-006",
                    "Supersoft lambswool crew-neck in classic collegiate style.",
                    "Knitted from pure lambswool, this heritage-inspired crew-neck jumper offers exceptional warmth-to-weight ratio and a natural resistance to pilling. The classic rib-knit collar, cuffs and hem provide a neat finish, while the relaxed fit allows easy layering over shirts. An enduring wardrobe investment that softens beautifully with wear.",
                    129.00m, 4, 4, "Men",
                    Img(3262937, "Men's Lambswool Crew-Neck Jumper – camel knit texture close-up",
                        2704500, "Men's Lambswool Crew-Neck Jumper – on model, layered over shirt"),
                    Meta(["Camel","Navy","Charcoal"],["#C19A6B","#1C2B4B","#36454F"],
                         TopSizes, ["jumper","knitwear","lambswool","mens","smart-casual","ralph-lauren"],
                         ["Material","Fit","Neckline","Cuffs","Wash","Origin"],
                         ["100% Lambswool","Relaxed","Ribbed Crew-Neck","Ribbed Rib-Knit","Dry Clean Only","Scotland"],
                         "Style code: RL-JUMP-M-006. Dry clean to preserve softness and shape."),
                    SeedDate, SeedDate
                },
                {
                    7, "Men's Chunky Rib-Knit Sweater", "HM-JUMP-M-007",
                    "Heavy-gauge rib-knit sweater for cold days. Cosy and relaxed fit.",
                    "Built for warmth on the coldest days, this chunky rib-knit sweater is made from a soft wool-blend that traps heat without bulk. The oversized silhouette is perfect for layering; the bold vertical rib adds visual interest. A practical funnel neck with no collar guards against wind chill.",
                    39.99m, 4, 3, "Men",
                    Img(6757412, "Men's Chunky Rib-Knit Sweater – grey wool texture detail",
                        2704500, "Men's Chunky Rib-Knit Sweater – on model, casual styling"),
                    Meta(["Grey Marl","Oatmeal","Dark Brown"],["#9EA0A1","#E8DCC8","#4A3728"],
                         TopSizes, ["jumper","knitwear","rib-knit","mens","casual","winter"],
                         ["Material","Fit","Neckline","Gauge","Care","Feature"],
                         ["80% Acrylic 20% Wool","Oversized","Funnel Neck","Chunky Rib","Machine Wash Cold","Chunky Vertical Rib Pattern"],
                         "Style code: HM-JUMP-M-007. Wash inside out to maintain texture."),
                    SeedDate, SeedDate
                },
                {
                    8, "Women's Ribbed Turtleneck Sweater", "ZRA-JUMP-W-008",
                    "Fine-rib turtleneck in a slim fit. A cool-season wardrobe staple.",
                    "This slim-fit turtleneck is made from a soft viscose-blend with a fine rib texture that clings lightly without restricting movement. The fitted polo neck can be scrunched down as a cowl for a relaxed look or worn tall for extra warmth. Pairs effortlessly with high-waisted jeans, midi skirts or tailored trousers.",
                    35.99m, 4, 2, "Women",
                    Img(7760243, "Women's Ribbed Turtleneck Sweater – beige turtleneck on model",
                        6757412, "Women's Ribbed Turtleneck Sweater – fine rib texture close-up"),
                    Meta(["Cream","Black","Rust"],["#FFFDD0","#111111","#B7410E"],
                         TopSizes, ["jumper","knitwear","turtleneck","womens","slim-fit","autumn"],
                         ["Material","Fit","Neckline","Rib","Care","Length"],
                         ["70% Viscose 30% Polyamide","Slim Fit","Polo/Turtleneck","Fine Rib","Hand Wash Cold","Hip Length"],
                         "Style code: ZRA-JUMP-W-008. Wash at 30°C and reshape while damp."),
                    SeedDate, SeedDate
                },
                {
                    9, "Women's Open-Front Merino Cardigan", "RL-JUMP-W-009",
                    "Draped open-front cardigan in pure merino. Lightweight and effortlessly polished.",
                    "Knitted from the finest grade merino wool, this open-front cardigan drapes beautifully and is virtually itch-free against the skin. The longline silhouette and waterfall front create an elegant layering piece equally at home in the office or at weekend brunch. No buttons — designed to wear open over a blouse, slip dress or tee.",
                    115.00m, 4, 4, "Women",
                    Img(7760243, "Women's Open-Front Merino Cardigan – cream draped cardigan on model",
                        3262937, "Women's Open-Front Merino Cardigan – merino knit texture"),
                    Meta(["Ivory","Soft Grey","Blush"],["#FFFFF0","#C8C8C8","#FFB6C1"],
                         TopSizes, ["cardigan","knitwear","merino","womens","open-front","smart-casual"],
                         ["Material","Fit","Closure","Length","Care","Feature"],
                         ["100% Grade-A Merino Wool","Relaxed","Open Front, No Buttons","Longline","Dry Clean or Hand Wash","Machine-Washable Grade"],
                         "Style code: RL-JUMP-W-009. Machine wash on delicate cycle with wool detergent."),
                    SeedDate, SeedDate
                },
                {
                    10, "Women's Oversized Chunky Cardigan", "HM-JUMP-W-010",
                    "Super-cosy oversized cardigan with deep pockets. Your ultimate WFH layer.",
                    "Wrap yourself in warmth with this generously oversized cardigan knitted in a voluminous boucle-style yarn. Deep front pockets and a relaxed shawl collar make it as practical as it is comfortable. Wear over pyjamas on lazy mornings or with a belt and straight-leg jeans for an effortlessly chic look.",
                    49.99m, 4, 3, "Women",
                    Img(3262937, "Women's Oversized Chunky Cardigan – textured knit flat lay",
                        7760243, "Women's Oversized Chunky Cardigan – model wearing caramel cardigan"),
                    Meta(["Caramel","Ecru","Slate Blue"],["#C68642","#FAEBD7","#708090"],
                         TopSizes, ["cardigan","knitwear","oversized","womens","cosy","casual"],
                         ["Material","Fit","Collar","Pockets","Belt Loops","Care"],
                         ["60% Acrylic 40% Polyester","Oversized","Shawl Collar","2 Deep Front Pockets","Yes, Included","Machine Wash Cold, Gentle"],
                         "Style code: HM-JUMP-W-010. Dry flat to retain shape."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Jeans (catId=6)
            // Pexels: 603022=blue jeans flat lay  6764007=model full-length
            //         8182357=pocket detail  7437963=denim fabric  206365=zipper
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    11, "501® Original Fit Jeans – Medium Wash", "LVI-501-M-011",
                    "The original since 1873. Iconic straight fit and signature button fly. Medium Indigo wash.",
                    "Since 1873 the 501® Original has been the gold standard in denim. Made for those who value authenticity and individuality, these straight-fit jeans feature the signature button fly and a relaxed, easy silhouette that has transcended fashion decades. Crafted from heavyweight 12 oz cotton denim, they sit at the natural waist and fall straight through the thigh and leg.",
                    74.99m, 6, 1, "Men",
                    Img(603022, "Levi's 501 Original Fit Jeans – medium wash flat lay, front view",
                        6764007, "Levi's 501 Original Fit Jeans – medium wash worn by model, full length"),
                    Meta(["Medium Indigo","Dark Stonewash","Black"],["#3B6CC5","#2C3E6B","#1C1C1C"],
                         DenimSizes, ["jeans","denim","501","straight-fit","mens","levi's"],
                         ["Material","Weight","Rise","Fit","Closure","Care"],
                         ["99% Cotton 1% Elastane","12 oz Heavyweight Denim","Natural Waist","Straight Through Thigh","Signature Button Fly","Machine Wash Cold, Tumble Dry Med"],
                         "Style code: 00501-0193. Button fly. Classic 5-pocket construction."),
                    SeedDate, SeedDate
                },
                {
                    12, "Men's Slim Fit Stretch Jeans – Dark Wash", "ZRA-JEAN-M-012",
                    "Sharp dark-wash jeans in a stretch slim fit. Looks tailored, feels like a second skin.",
                    "Precision cut from a four-way stretch denim that moves with you throughout the day. The slim fit tapers from the thigh to a narrow ankle opening for a sleek, contemporary silhouette. The dark indigo wash and clean finish make these versatile enough for casual Fridays at the office or a night out.",
                    59.99m, 6, 2, "Men",
                    Img(7437963, "Men's Slim Fit Stretch Jeans – dark wash denim close-up",
                        6764007, "Men's Slim Fit Stretch Jeans – on model, full length"),
                    Meta(["Dark Indigo","Jet Black","Midnight Blue"],["#1F305E","#0A0A0A","#191970"],
                         DenimSizes, ["jeans","denim","slim-fit","stretch","mens","dark-wash"],
                         ["Material","Stretch","Rise","Fit","Fly","Care"],
                         ["92% Cotton 6% Polyester 2% Elastane","4-Way Stretch","Mid-Rise","Slim Tapered","Zip Fly with Button","Machine Wash 30°C Inside Out"],
                         "Style code: ZRA-JEAN-M-012. Wash inside out to preserve wash intensity."),
                    SeedDate, SeedDate
                },
                {
                    13, "Men's Straight Leg Jeans – Light Wash", "HM-JEAN-M-013",
                    "Classic straight-leg light-wash jeans. Effortless everyday denim.",
                    "A no-fuss, everyday straight-leg cut in a faded light blue wash that pairs with virtually everything. Made from durable cotton denim with a touch of stretch for comfort, these sit at the natural waist and have a straight leg opening from hip to hem. Perfect for casual weekends or dressed up with a blazer.",
                    34.99m, 6, 3, "Men",
                    Img(8182357, "Men's Straight Leg Jeans – light wash pocket detail",
                        603022, "Men's Straight Leg Jeans – light wash flat lay"),
                    Meta(["Light Wash","Stone Wash","Faded Grey"],["#A3BFDA","#B0C4DE","#9EA0A1"],
                         DenimSizes, ["jeans","denim","straight-leg","mens","light-wash","casual"],
                         ["Material","Rise","Fit","Fly","Pockets","Care"],
                         ["98% Cotton 2% Elastane","Regular Rise","Straight Leg","Zip Fly","5-Pocket","Machine Wash 40°C"],
                         "Style code: HM-JEAN-M-013. Tumble dry on low to reduce shrinkage."),
                    SeedDate, SeedDate
                },
                {
                    14, "Women's High-Rise Skinny Jeans", "ZRA-JEAN-W-014",
                    "Figure-hugging high-rise skinny jeans with a flattering second-skin fit.",
                    "These high-rise skinny jeans are cut from a power-stretch denim that sculpts and supports while remaining incredibly comfortable. The super-high waistband creates an hourglass silhouette and is comfortable enough to wear all day. The skinny leg gives way to a cropped ankle length — a flattering finish with trainers, boots or block-heel mules.",
                    55.99m, 6, 2, "Women",
                    Img(206365, "Women's High-Rise Skinny Jeans – mid-blue hardware detail",
                        7437963, "Women's High-Rise Skinny Jeans – denim fabric texture close-up"),
                    Meta(["Mid Blue","Black","White"],["#4682B4","#111111","#F5F5F5"],
                         DenimSizes, ["jeans","denim","skinny","high-rise","womens","stretch"],
                         ["Material","Rise","Fit","Ankle","Care","Feature"],
                         ["76% Cotton 22% Polyester 2% Elastane","High-Rise (10.5 in)","Skinny","Cropped Ankle","Machine Wash Cold","Sculpting Waistband"],
                         "Style code: ZRA-JEAN-W-014. Size up one if between sizes."),
                    SeedDate, SeedDate
                },
                {
                    15, "Women's Wide-Leg Mom Jeans", "HM-JEAN-W-015",
                    "Relaxed wide-leg mom jeans in a vintage-inspired medium wash.",
                    "Inspired by the laid-back denim silhouettes of the nineties, these mom jeans feature a high waist, relaxed thighs, and a wide leg that adds a retro touch to any look. The medium blue vintage wash and subtle distressing give them an authentic worn-in character. Style with a fitted top and chunky trainers for an off-duty look.",
                    39.99m, 6, 3, "Women",
                    Img(603022, "Women's Wide-Leg Mom Jeans – medium wash flat lay",
                        8182357, "Women's Wide-Leg Mom Jeans – pocket and leg detail"),
                    Meta(["Vintage Blue","Light Stone","Dark Blue"],["#4682B4","#C8B8A2","#1F305E"],
                         DenimSizes, ["jeans","denim","mom-jeans","wide-leg","womens","vintage"],
                         ["Material","Rise","Fit","Distressing","Wash","Care"],
                         ["99% Cotton 1% Elastane","High-Rise (11 in)","Wide Leg / Relaxed Thigh","Subtle Whisker Detail","Vintage Enzyme Wash","Machine Wash 30°C"],
                         "Style code: HM-JEAN-W-015. Wash inside out."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Trousers (catId=7)
            // Pexels: 2662794=man in blue suit (shows trousers)  450212=man in blazer
            //         652348=men's black blazer (formal trousers visible)
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    16, "Men's Slim Fit Chino Trousers", "ZRA-TROU-M-016",
                    "Clean-cut slim chinos in a soft stretch-cotton blend. Smart-casual at its finest.",
                    "Versatile enough for the office and the weekend, these slim-fit chinos are cut from a soft cotton-elastane twill that holds its shape all day. The flat front and tapered leg create a sleek, uncluttered silhouette. Available in essential neutral shades that pair effortlessly with shirts, polos and knitwear.",
                    49.99m, 7, 2, "Men",
                    Img(2662794, "Men's Slim Fit Chino Trousers – navy slim chinos on model",
                        450212, "Men's Slim Fit Chino Trousers – styled with blazer"),
                    Meta(["Navy","Khaki","Stone"],["#1C2B4B","#C3B091","#B5A48B"],
                         TrouserW, ["trousers","chinos","smart-casual","mens","slim-fit","office"],
                         ["Material","Rise","Fit","Front","Hem","Care"],
                         ["98% Cotton 2% Elastane","Mid-Rise","Slim Tapered","Flat Front","Plain Hem","Machine Wash 30°C, Tumble Dry Low"],
                         "Style code: ZRA-TROU-M-016. Measure waist in inches; inseam standard 32 in."),
                    SeedDate, SeedDate
                },
                {
                    17, "Men's Classic Pleated Dress Trousers", "RL-TROU-M-017",
                    "Heritage-cut pleated trousers in super-120 wool. Boardroom-ready.",
                    "Tailored from a smooth super-120 wool blend, these classic pleated trousers carry an authoritative presence in any formal setting. A single forward pleat provides volume at the hip that tapers to a neat straight leg. The waistband features belt loops and side-adjusters for a precise fit without a belt.",
                    149.00m, 7, 4, "Men",
                    Img(450212, "Men's Classic Pleated Dress Trousers – formal grey trousers styled",
                        2662794, "Men's Classic Pleated Dress Trousers – full suit look"),
                    Meta(["Charcoal Grey","Mid Grey","Navy"],["#36454F","#808080","#1C2B4B"],
                         TrouserW, ["trousers","dress-trousers","formal","wool","mens","pleated"],
                         ["Material","Rise","Pleat","Fit","Waistband","Care"],
                         ["70% Wool 28% Polyester 2% Elastane","High-Rise","Single Forward Pleat","Straight Leg","Belt Loops + Side Adjusters","Dry Clean Recommended"],
                         "Style code: RL-TROU-M-017. Dry clean to maintain drape and crease."),
                    SeedDate, SeedDate
                },
                {
                    18, "Men's Relaxed Cargo Trousers", "HM-TROU-M-018",
                    "Rugged multi-pocket cargo trousers in a relaxed fit. Utility meets style.",
                    "Built for those who need their clothes to work as hard as they do, these cargo trousers feature six generously sized pockets, reinforced stress points, and a durable ripstop fabric finish. The relaxed fit allows easy movement; the adjustable hem tabs let you wear them long or roll them up.",
                    44.99m, 7, 3, "Men",
                    Img(2662794, "Men's Relaxed Cargo Trousers – olive cargo trousers, side pocket detail",
                        652348, "Men's Relaxed Cargo Trousers – full-length styling"),
                    Meta(["Olive","Black","Sand"],["#556B2F","#111111","#C2A882"],
                         TrouserW, ["trousers","cargo","utility","mens","relaxed","casual"],
                         ["Material","Rise","Fit","Pockets","Hem","Care"],
                         ["100% Cotton Ripstop","Mid-Rise","Relaxed Straight","6 Pockets","Adjustable Hem Tabs","Machine Wash 40°C"],
                         "Style code: HM-TROU-M-018. Cargo pockets secure with snap buttons."),
                    SeedDate, SeedDate
                },
                {
                    19, "Women's Wide-Leg Tailored Trousers", "ZRA-TROU-W-019",
                    "Fluid wide-leg trousers with a high waist. Power dressing made effortless.",
                    "Commanding and comfortable in equal measure, these wide-leg trousers are cut from a lightweight flowing fabric with just enough structure to hold the silhouette. The high waist defines the figure and creates a clean, elongated line; the wide, full-length leg adds a dramatic, catwalk-inspired finish.",
                    65.99m, 7, 2, "Women",
                    Img(450212, "Women's Wide-Leg Tailored Trousers – camel wide-leg flat lay",
                        2662794, "Women's Wide-Leg Tailored Trousers – styled with cropped blazer"),
                    Meta(["Camel","Black","Ecru"],["#C19A6B","#111111","#FAEBD7"],
                         TrouserW, ["trousers","wide-leg","tailored","womens","high-waist","office"],
                         ["Material","Rise","Fit","Pleat","Length","Care"],
                         ["73% Viscose 27% Polyester","High-Rise","Wide Leg","Front Darts","Full Length","Dry Clean or Hand Wash"],
                         "Style code: ZRA-TROU-W-019. Steam to remove creases."),
                    SeedDate, SeedDate
                },
                {
                    20, "Women's High-Waist Crepe Trousers", "HM-TROU-W-020",
                    "Smooth crepe trousers in a straight cut. From desk to dinner in seconds.",
                    "These straight-cut trousers in a smooth, matte crepe fabric strike the perfect balance between comfort and professionalism. The high waist and clean front give them a sleek, modern finish; the added stretch in the fabric means you can wear them through the longest working days. A concealed side-zip keeps the lines clean.",
                    34.99m, 7, 3, "Women",
                    Img(450212, "Women's High-Waist Crepe Trousers – black tailored trousers styled",
                        652348, "Women's High-Waist Crepe Trousers – fabric drape detail"),
                    Meta(["Black","Dove Grey","Burgundy"],["#111111","#B0AEAE","#800020"],
                         TrouserW, ["trousers","crepe","high-waist","womens","straight","office"],
                         ["Material","Rise","Fit","Closure","Hem","Care"],
                         ["95% Polyester 5% Elastane","High-Rise","Straight Leg","Concealed Side Zip","Plain Hem","Machine Wash 30°C, Do Not Tumble Dry"],
                         "Style code: HM-TROU-W-020. Hang to dry to preserve crepe texture."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Shorts (catId=8)
            // Pexels: 6764007=casual denim legs model  1022852=casual wear (approximate)
            //         4587955=athletic casual (approximate)
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    21, "Men's Slim Fit Chino Shorts", "ZRA-SHORT-M-021",
                    "Tailored slim-fit chino shorts. The smart take on summer casual.",
                    "Cut from the same premium stretch cotton twill as our best-selling chinos, these slim-fit shorts offer a tailored, put-together look for warm weather. The flat front and structured waistband prevent the sloppy silhouette often associated with shorts. A 9-inch inseam hits just above the knee for a flattering, versatile length.",
                    39.99m, 8, 2, "Men",
                    Img(6764007, "Men's Slim Fit Chino Shorts – khaki shorts on model",
                        2662794, "Men's Slim Fit Chino Shorts – styled with polo shirt"),
                    Meta(["Khaki","Navy","Olive"],["#C3B091","#1C2B4B","#556B2F"],
                         TopSizes, ["shorts","chino","mens","slim-fit","summer","smart-casual"],
                         ["Material","Inseam","Fit","Front","Pockets","Care"],
                         ["98% Cotton 2% Elastane","9 Inches","Slim Fit","Flat Front","4-Pocket (2 Front, 2 Back)","Machine Wash 30°C"],
                         "Style code: ZRA-SHORT-M-021. Iron on medium heat."),
                    SeedDate, SeedDate
                },
                {
                    22, "Men's Washed Denim Shorts", "HM-SHORT-M-022",
                    "Classic five-pocket denim shorts in a mid-blue wash. Summer staples.",
                    "These straight-cut denim shorts are made from the same sturdy cotton denim as our jeans range. A mid-blue enzyme wash gives them a relaxed, broken-in feel from day one. The five-pocket construction and zip fly keep them practical; a 10-inch inseam provides solid coverage.",
                    24.99m, 8, 3, "Men",
                    Img(8182357, "Men's Washed Denim Shorts – mid-blue denim pocket detail",
                        6764007, "Men's Washed Denim Shorts – on model, casual summer styling"),
                    Meta(["Mid Blue","Light Wash","Stone Wash"],["#4682B4","#A3BFDA","#B0C4DE"],
                         TopSizes, ["shorts","denim","mens","casual","summer","five-pocket"],
                         ["Material","Inseam","Fit","Fly","Wash","Care"],
                         ["100% Cotton Denim","10 Inches","Straight Fit","Zip Fly","Enzyme Mid-Blue Wash","Machine Wash 40°C Inside Out"],
                         "Style code: HM-SHORT-M-022. Wash inside out to preserve wash."),
                    SeedDate, SeedDate
                },
                {
                    23, "Men's Performance Running Shorts", "NK-SHORT-M-023",
                    "Lightweight Dri-FIT running shorts with built-in brief. Built for speed.",
                    "Engineered for high-intensity training, these 5-inch running shorts are made from Nike's signature Dri-FIT fabric, which wicks moisture and dries quickly to keep you cool during even the most demanding workouts. A built-in brief offers support without restriction, and reflective details keep you visible in low-light conditions.",
                    40.00m, 8, 5, "Men",
                    Img(6764007, "Men's Performance Running Shorts – athletic shorts on model",
                        8182357, "Men's Performance Running Shorts – fabric and construction detail"),
                    Meta(["Black","Blue","Red"],["#111111","#0057B8","#CC0000"],
                         TopSizes, ["shorts","running","athletic","mens","dri-fit","nike","sport"],
                         ["Material","Inseam","Technology","Brief","Pockets","Care"],
                         ["100% Recycled Polyester","5 Inches","Dri-FIT Moisture-Wicking","Built-In Support Brief","1 Back Zip Pocket","Machine Wash Cold, Tumble Dry Low"],
                         "Style code: NK-SHORT-M-023. Do not use fabric softener."),
                    SeedDate, SeedDate
                },
                {
                    24, "Women's High-Waisted Denim Shorts", "ZRA-SHORT-W-024",
                    "Super high-rise denim shorts with a vintage-inspired fray hem.",
                    "Turn up the summer heat with these high-waist denim shorts made from a stretch cotton denim in a pale blue vintage wash. The fray hem adds a retro touch and sits high on the thigh; the high waistband creates a defined waist and is comfortable enough to wear all day. Style with a simple crop top or tucked-in blouse.",
                    45.99m, 8, 2, "Women",
                    Img(206365, "Women's High-Waisted Denim Shorts – denim hardware detail",
                        7437963, "Women's High-Waisted Denim Shorts – denim fabric close-up"),
                    Meta(["Pale Blue","Mid Blue","Black"],["#C5D5E8","#4682B4","#111111"],
                         TopSizes, ["shorts","denim","womens","high-waist","vintage","summer"],
                         ["Material","Rise","Inseam","Hem","Fly","Care"],
                         ["93% Cotton 7% Elastane","Super High-Rise (12.5 in)","2.5 Inches","Fray Hem","Zip Fly with Button","Machine Wash Cold, Gentle Cycle"],
                         "Style code: ZRA-SHORT-W-024. Avoid bleaching to preserve wash."),
                    SeedDate, SeedDate
                },
                {
                    25, "Women's Linen-Blend Shorts", "HM-SHORT-W-025",
                    "Relaxed linen-blend shorts with a paperbag waist. Effortlessly breezy.",
                    "A paperbag elasticated waist with a self-fabric belt adds shape and adjustability to these relaxed linen-blend shorts. The natural linen content keeps you cool in warm weather, while the wide-leg cut gives them a loose, comfortable feel. An easy, versatile piece for holidays, garden parties and long summer days.",
                    29.99m, 8, 3, "Women",
                    Img(6764007, "Women's Linen-Blend Shorts – styled with tucked blouse",
                        206365, "Women's Linen-Blend Shorts – linen fabric texture detail"),
                    Meta(["Natural","White","Sage"],["#E8D5B7","#F8F8F8","#B2C2A6"],
                         TopSizes, ["shorts","linen","womens","paperbag","relaxed","summer"],
                         ["Material","Rise","Fit","Waist","Belt","Care"],
                         ["55% Linen 45% Cotton","Mid-Rise","Relaxed Wide-Leg","Paperbag Elasticated","Self-Fabric Tie Belt","Machine Wash 30°C, Dry Flat"],
                         "Style code: HM-SHORT-W-025. Expect natural linen creases."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Autumn Jackets (catId=10)
            // Pexels: 3297302=man wearing gray jacket  2766298=man in leather jacket
            //         1687116=man wearing jacket (casual)
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    26, "Men's Water-Resistant Bomber Jacket", "HM-AUTJ-M-026",
                    "Lightweight bomber with water-resistant shell. Your go-to transitional layer.",
                    "A modern reinterpretation of the classic bomber, this jacket features a wind- and water-resistant outer shell, an elasticated waist and cuffs, and a ribbed collar that sits neatly at the neck. Internal mesh lining adds a touch of warmth on cool autumn days without the bulk. Packs into its own front pocket.",
                    59.99m, 10, 3, "Men",
                    Img(3297302, "Men's Water-Resistant Bomber Jacket – grey bomber on model",
                        2766298, "Men's Water-Resistant Bomber Jacket – jacket collar and zip detail"),
                    Meta(["Olive","Burgundy","Black"],["#556B2F","#800020","#111111"],
                         TopSizes, ["jacket","bomber","autumn","mens","water-resistant","transitional"],
                         ["Outer","Lining","Fit","Collar","Cuffs","Care"],
                         ["100% Nylon (Water-Resistant)","100% Polyester Mesh","Regular Fit","Ribbed","Elasticated Ribbed","Machine Wash Cold"],
                         "Style code: HM-AUTJ-M-026. Pack into front pocket for travel."),
                    SeedDate, SeedDate
                },
                {
                    27, "Men's Quilted Lightweight Jacket", "ZRA-AUTJ-M-027",
                    "Sleek quilted jacket with a minimal diamond stitch. Smart protection from the chill.",
                    "Slim-profiled and supremely packable, this quilted jacket bridges the gap between a shirt jacket and a full coat. The lightweight synthetic fill provides surprising warmth while the slim cut keeps the silhouette clean. Wear it zipped under a wool overcoat or on its own through mild autumn days.",
                    79.99m, 10, 2, "Men",
                    Img(1687116, "Men's Quilted Lightweight Jacket – styled over shirt, urban",
                        3297302, "Men's Quilted Lightweight Jacket – quilted panel close-up"),
                    Meta(["Navy","Khaki","Dark Grey"],["#1C2B4B","#C3B091","#404040"],
                         TopSizes, ["jacket","quilted","autumn","mens","packable","lightweight"],
                         ["Fill","Outer","Fit","Zip","Pockets","Care"],
                         ["80g Synthetic Fill","100% Polyamide","Slim Fit","Full-Length YKK Zip","2 Zip + 1 Internal","Machine Wash Cold, Gentle"],
                         "Style code: ZRA-AUTJ-M-027. Tumble dry low with tennis balls to restore loft."),
                    SeedDate, SeedDate
                },
                {
                    28, "Men's Harrington Jacket", "RL-AUTJ-M-028",
                    "The iconic Harrington in premium cotton gabardine. A timeless British staple.",
                    "The Harrington is as quintessentially British as a garden in autumn, and this one does the icon justice. Made from a premium cotton gabardine with a tartan-lined interior, it features the classic stand collar, slash pockets, and elasticated waist and cuffs. Generations of style icons have worn this silhouette — now it's your turn.",
                    195.00m, 10, 4, "Men",
                    Img(3297302, "Men's Harrington Jacket – heritage camel gabardine on model",
                        1687116, "Men's Harrington Jacket – tartan lining and collar detail"),
                    Meta(["Camel","Navy","Forest Green"],["#C19A6B","#1C2B4B","#228B22"],
                         TopSizes, ["jacket","harrington","autumn","mens","cotton","heritage"],
                         ["Material","Lining","Fit","Collar","Pockets","Care"],
                         ["100% Cotton Gabardine","Tartan Polyester","Regular Fit","Stand Collar","2 Slash Pockets","Machine Wash 30°C"],
                         "Style code: RL-AUTJ-M-028. A British classic since 1937."),
                    SeedDate, SeedDate
                },
                {
                    29, "Women's Padded Utility Jacket", "ZRA-AUTJ-W-029",
                    "Cropped padded jacket with a utility edge. Autumn warmth meets street style.",
                    "This cropped padded jacket takes utility-inspired details — contrast stitching, functional zip pockets, and a stand collar — and gives them a street-ready spin. Light synthetic padding keeps you warm without adding bulk; the cropped length pairs brilliantly with high-waisted bottoms. Adjustable hem drawcord for a tailored fit.",
                    75.99m, 10, 2, "Women",
                    Img(3297302, "Women's Padded Utility Jacket – styled with high-waisted trousers",
                        1687116, "Women's Padded Utility Jacket – jacket detail and stitching"),
                    Meta(["Black","Khaki","Burnt Orange"],["#111111","#C3B091","#CC5500"],
                         TopSizes, ["jacket","padded","utility","womens","cropped","autumn"],
                         ["Outer","Fill","Fit","Length","Pockets","Care"],
                         ["100% Polyester","Light Synthetic Padding","Regular Fit","Cropped","3 Zip Pockets","Machine Wash Cold"],
                         "Style code: ZRA-AUTJ-W-029. Drawcord hem for adjustable fit."),
                    SeedDate, SeedDate
                },
                {
                    30, "Women's Cropped Windbreaker Jacket", "HM-AUTJ-W-030",
                    "Bright colour-block windbreaker with a cropped fit. Fun and functional.",
                    "Designed to beat the breeze in style, this lightweight windbreaker features a colour-blocked body, adjustable hood, and elasticated cuffs that seal out gusts. Made from a crinkle nylon that is both wind- and light-rain resistant. Packs flat for easy carrying. Bright, bold colourways make a statement wherever you go.",
                    45.99m, 10, 3, "Women",
                    Img(1687116, "Women's Cropped Windbreaker Jacket – colour-block jacket on model",
                        3297302, "Women's Cropped Windbreaker Jacket – hood and zip detail"),
                    Meta(["Coral/White","Black/Yellow","Cobalt/Grey"],["#FF6B6B","#F5C400","#0047AB"],
                         TopSizes, ["jacket","windbreaker","womens","cropped","colour-block","sport"],
                         ["Material","Hood","Cuffs","Fit","Packable","Care"],
                         ["100% Crinkle Nylon","Adjustable, Removable","Elasticated","Cropped Regular","Packs Flat","Machine Wash Cold, Hang Dry"],
                         "Style code: HM-AUTJ-W-030. Pack flat into hood pocket."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Winter Jackets (catId=11)
            // Pexels: 19459485=couple in winter jackets  21858851=man in winter jacket
            //         54200=women's black zip hooded jacket  10871542=woman in red jacket
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    31, "Men's Down-Fill Parka", "HM-WINJ-M-031",
                    "Heavyweight parka with genuine down fill. Rated to -15°C.",
                    "When temperatures drop, this heavyweight parka delivers serious warmth without sacrificing style. Filled with premium 90/10 duck down, it achieves an exceptional warmth-to-weight ratio. The adjustable storm hood, draft-excluding inner collar, and deep hem ensure no cold air sneaks in. Rated for temperatures down to -15°C.",
                    129.00m, 11, 3, "Men",
                    Img(21858851, "Men's Down-Fill Parka – man in warm winter parka, snowy landscape",
                        19459485, "Men's Down-Fill Parka – winter jacket detail and zipper"),
                    Meta(["Black","Olive","Navy"],["#111111","#556B2F","#1C2B4B"],
                         TopSizes, ["parka","winter","down","mens","warm","insulated"],
                         ["Fill","Fill Power","Hood","Temp Rating","Pockets","Care"],
                         ["90/10 Duck Down","600 Fill Power","Adjustable Detachable Storm Hood","-15°C Rated","6 Pockets inc. Internal","Machine Wash Cold, Tumble Dry Low"],
                         "Style code: HM-WINJ-M-031. Tumble dry with tennis balls to restore loft."),
                    SeedDate, SeedDate
                },
                {
                    32, "Men's Wool Blend Peacoat", "ZRA-WINJ-M-032",
                    "Double-breasted peacoat in a premium wool blend. Understated urban elegance.",
                    "The peacoat has been a symbol of easy sophistication since it first appeared in naval uniform. This version, in a rich wool-polyester blend, is double-breasted with large lapels and a clean, knee-length silhouette. It buttons across the chest for adjustable coverage and slips over suits or chunky knitwear with equal ease.",
                    199.00m, 11, 2, "Men",
                    Img(19459485, "Men's Wool Blend Peacoat – couple in quality winter coats, city",
                        21858851, "Men's Wool Blend Peacoat – peacoat lapel and button detail"),
                    Meta(["Charcoal","Camel","Navy"],["#36454F","#C19A6B","#1C2B4B"],
                         TopSizes, ["peacoat","winter","wool","mens","double-breasted","smart"],
                         ["Material","Fit","Closure","Length","Lining","Care"],
                         ["70% Wool 30% Polyester","Regular Fit","Double-Breasted, 6 Buttons","Knee Length","Full Polyester Lining","Dry Clean Only"],
                         "Style code: ZRA-WINJ-M-032. Dry clean to preserve structure."),
                    SeedDate, SeedDate
                },
                {
                    33, "Men's Padded Duvet Coat", "HM-WINJ-M-033",
                    "Oversized padded coat with high funnel neck. Maximum warmth, minimal fuss.",
                    "When maximum warmth is the priority and style is non-negotiable, this padded duvet coat delivers on both fronts. The oversized silhouette and sky-high funnel neck create a cosy, enveloping feel; the large channel stitching gives a bold, graphic quality. Water-repellent outer keeps light rain and snow at bay.",
                    89.99m, 11, 3, "Men",
                    Img(21858851, "Men's Padded Duvet Coat – man in oversized padded winter coat",
                        54200, "Men's Padded Duvet Coat – coat fabric and channel stitch detail"),
                    Meta(["Black","Sage","Ecru"],["#111111","#B2C2A6","#F5F0E8"],
                         TopSizes, ["coat","padded","winter","mens","oversized","warm"],
                         ["Fill","Outer","Hood","Fit","Temp Rating","Care"],
                         ["Recycled Synthetic Fill","Water-Repellent Polyester","High Funnel Neck (No Hood)","Oversized","-10°C Rated","Machine Wash Cold"],
                         "Style code: HM-WINJ-M-033. Wash at 30°C, tumble dry low."),
                    SeedDate, SeedDate
                },
                {
                    34, "Women's Oversized Puffer Jacket", "ZRA-WINJ-W-034",
                    "Statement oversized puffer in a glossy finish. Bold and seriously warm.",
                    "Make a statement on the coldest days with this oversized puffer jacket in a high-shine nylon shell. The exaggerated volume is entirely intentional — this is winter dressing as a fashion moment. Filled with premium synthetic down alternative, it provides serious warmth; the adjustable hood and hem seal out the cold.",
                    119.99m, 11, 2, "Women",
                    Img(10871542, "Women's Oversized Puffer Jacket – woman in red puffer jacket in snow",
                        19459485, "Women's Oversized Puffer Jacket – puffer jacket detail"),
                    Meta(["Red","Black","Cobalt Blue"],["#CC0000","#111111","#0047AB"],
                         TopSizes, ["puffer","jacket","winter","womens","oversized","statement"],
                         ["Fill","Outer","Hood","Fit","Pockets","Care"],
                         ["Premium Synthetic Down Alternative","High-Shine Nylon","Adjustable Drawcord","Oversized","2 Zip Pockets","Machine Wash Cold"],
                         "Style code: ZRA-WINJ-W-034. Tumble dry low with clean tennis balls."),
                    SeedDate, SeedDate
                },
                {
                    35, "Women's Faux Fur Trim Parka", "RL-WINJ-W-035",
                    "Luxurious parka with detachable faux fur hood trim. Warmth and glamour in one.",
                    "A long-length parka with a detachable faux fur trim on the hood that adds a glamorous, tactile finish. The water-repellent outer and channel-stitched down fill ensure you stay warm in even the harshest winter weather. An adjustable belt cinches the waist for a more defined silhouette; inner and outer pockets keep your essentials organised.",
                    245.00m, 11, 4, "Women",
                    Img(10871542, "Women's Faux Fur Trim Parka – woman in snow, fur-trim hood",
                        54200, "Women's Faux Fur Trim Parka – faux fur hood trim close-up"),
                    Meta(["Forest Green","Black","Burgundy"],["#228B22","#111111","#800020"],
                         TopSizes, ["parka","winter","faux-fur","womens","belted","premium"],
                         ["Fill","Outer","Hood Trim","Belt","Length","Care"],
                         ["90/10 Duck Down Fill","Water-Repellent Nylon","Detachable Faux Fur","Removable Self-Belt","Below Knee","Dry Clean Recommended"],
                         "Style code: RL-WINJ-W-035. Detach fur trim before cleaning."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Leather Jackets (catId=12)
            // Pexels: 2766298=man wearing leather jacket  1687116=man in leather jacket
            //         6851461=man in black leather jacket  19626626=woman in leather jacket
            //         3775534=bearded man in black leather jacket
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    36, "Men's Classic Biker Leather Jacket", "ZRA-LEATH-M-036",
                    "The definitive biker jacket in genuine lambskin. Rebellious, refined.",
                    "Crafted from supple lambskin leather with a natural grain, this biker jacket is the result of generations of craftsmanship. The asymmetric zip, belted waist and lapels are all hand-stitched for durability. Fully lined with a smooth polyester for easy wear-on, wear-off. A jacket that only gets better with age.",
                    295.00m, 12, 2, "Men",
                    Img(2766298, "Men's Classic Biker Leather Jacket – black lambskin biker on model",
                        6851461, "Men's Classic Biker Leather Jacket – jacket collar and zip detail"),
                    Meta(["Black","Chocolate Brown","Dark Navy"],["#111111","#3D1F0D","#1C2B4B"],
                         TopSizes, ["leather-jacket","biker","mens","genuine-leather","classic","moto"],
                         ["Material","Lining","Fit","Closure","Hardware","Care"],
                         ["100% Genuine Lambskin Leather","100% Polyester","Slim Fit","Asymmetric Zip","Gunmetal Zips and Snaps","Professional Leather Clean Only"],
                         "Style code: ZRA-LEATH-M-036. Condition leather annually with a quality cream."),
                    SeedDate, SeedDate
                },
                {
                    37, "Men's Slim Fit Leather Jacket", "HM-LEATH-M-037",
                    "Clean-cut leather jacket with a modern slim profile. Urban essential.",
                    "A sleeker, more pared-back alternative to the classic biker. This slim-fit leather jacket features a simple zip-front closure, minimal external pockets, and a clean silhouette that works as well under a shirt as over one. Made from a soft pebbled leather with a smooth inner lining.",
                    149.99m, 12, 3, "Men",
                    Img(1687116, "Men's Slim Fit Leather Jacket – model wearing slim leather jacket",
                        3775534, "Men's Slim Fit Leather Jacket – jacket detail, collar up"),
                    Meta(["Black","Dark Brown","Tan"],["#111111","#3D1F0D","#D2B48C"],
                         TopSizes, ["leather-jacket","slim-fit","mens","urban","minimalist","classic"],
                         ["Material","Lining","Fit","Closure","Texture","Care"],
                         ["PU / Genuine Leather Mix","Smooth Polyester","Slim Fit","Central Zip","Pebbled Grain","Wipe Clean, Professional Leather Condition"],
                         "Style code: HM-LEATH-M-037. Do not machine wash."),
                    SeedDate, SeedDate
                },
                {
                    38, "Men's Distressed Brown Leather Jacket", "RL-LEATH-M-038",
                    "Vintage-effect distressed leather jacket with worn-in character from day one.",
                    "Made from vegetable-tanned cowhide that has been hand-distressed for an authentic vintage look, this jacket carries a unique sense of history. Each jacket is slightly different — the natural variation in the leather means no two are identical. The clean bomber silhouette and minimal detailing let the material speak for itself.",
                    349.00m, 12, 4, "Men",
                    Img(3775534, "Men's Distressed Brown Leather Jacket – rich brown leather on model",
                        2766298, "Men's Distressed Brown Leather Jacket – distressed texture detail"),
                    Meta(["Tobacco Brown","Cognac","Black"],["#7B3F00","#9A6B4B","#111111"],
                         TopSizes, ["leather-jacket","distressed","bomber","mens","vintage","premium"],
                         ["Material","Tanning","Fit","Lining","Unique Variation","Care"],
                         ["100% Vegetable-Tanned Cowhide","Hand-Distressed","Relaxed Bomber Fit","Quilted Nylon","Each Piece Unique","Specialist Leather Care Only"],
                         "Style code: RL-LEATH-M-038. Natural oils in vegetable-tanned leather develop with wear."),
                    SeedDate, SeedDate
                },
                {
                    39, "Women's Cropped Black Leather Jacket", "ZRA-LEATH-W-039",
                    "Cropped biker jacket in supple lambskin. Sharp, sculptural, streetwear-ready.",
                    "A feminine reinterpretation of the classic moto jacket, this cropped version hits at the natural waist to create a figure-flattering silhouette. Made from genuine lambskin with a soft, pliable hand-feel, it features a double-zip front closure, silver hardware, and a fully quilted lining for warmth and easy wear.",
                    275.00m, 12, 2, "Women",
                    Img(19626626, "Women's Cropped Black Leather Jacket – woman in cropped leather jacket",
                        6851461, "Women's Cropped Black Leather Jacket – silver hardware and zip detail"),
                    Meta(["Black","Blush","Forest Green"],["#111111","#FFB6C1","#228B22"],
                         TopSizes, ["leather-jacket","cropped","biker","womens","genuine-leather","moto"],
                         ["Material","Lining","Fit","Length","Hardware","Care"],
                         ["100% Genuine Lambskin","Quilted Polyester","Slim Fit","Cropped (Hits at Waist)","Silver Tone Zips and Snaps","Professional Leather Clean Only"],
                         "Style code: ZRA-LEATH-W-039. Spot clean surface marks with a damp cloth."),
                    SeedDate, SeedDate
                },
                {
                    40, "Women's Oversized Faux Leather Jacket", "HM-LEATH-W-040",
                    "Oversized faux leather jacket with padded shoulders. Bold and edgy.",
                    "Giving major nineties power-dressing energy, this oversized faux leather jacket features exaggerated padded shoulders and a straight, boxy silhouette that makes a statement without saying a word. The smooth, plant-based faux leather is soft to the touch and more sustainable than traditional alternatives. Wear with everything from mini skirts to wide-leg trousers.",
                    69.99m, 12, 3, "Women",
                    Img(19626626, "Women's Oversized Faux Leather Jacket – edgy oversized styling",
                        1687116, "Women's Oversized Faux Leather Jacket – shoulder and collar detail"),
                    Meta(["Black","Caramel","Cobalt"],["#111111","#C68642","#0047AB"],
                         TopSizes, ["leather-jacket","faux-leather","oversized","womens","boxy","vegan"],
                         ["Material","Lining","Fit","Shoulders","Closure","Care"],
                         ["100% Vegan Faux Leather (Plant-Based)","Smooth Polyester","Oversized Boxy","Padded Structured Shoulders","Central Button-Through","Wipe Clean, Do Not Machine Wash"],
                         "Style code: HM-LEATH-W-040. Sustainable plant-based faux leather."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Dresses & Skirts (catId=13)  — all Women
            // Pexels: 3517286=women's red floral dress  3444499=women wearing brown dress
            //         28232666=back view woman in dress  15071849=women in dresses
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    41, "Floral Wrap Midi Dress", "ZRA-DRESS-W-041",
                    "Fluid wrap midi dress in an all-over floral print. Feminine and effortlessly elegant.",
                    "Wrap dresses have a universally flattering cut, and this midi length version takes that principle to new heights. Made from a lightweight, flowing fabric with a vibrant floral print, the adjustable wrap front means the fit is entirely customisable. The midi length provides sophistication while remaining perfectly comfortable for all-day wear.",
                    79.99m, 13, 2, "Women",
                    Img(3517286, "Floral Wrap Midi Dress – women in floral dress, outdoor setting",
                        3444499, "Floral Wrap Midi Dress – dress fabric and wrap detail"),
                    Meta(["Floral Red/White","Floral Blue/Green","Floral Black/Yellow"],
                         ["#CC0000","#4682B4","#F5C400"],
                         TopSizes, ["dress","wrap","midi","womens","floral","elegant"],
                         ["Material","Style","Length","Neckline","Closure","Care"],
                         ["100% Polyester Chiffon","Wrap Front","Midi (Below Knee)","V-Neck","Adjustable Self-Tie Belt","Hand Wash Cold or Delicate Cycle"],
                         "Style code: ZRA-DRESS-W-041. Hang to dry. Do not tumble dry."),
                    SeedDate, SeedDate
                },
                {
                    42, "Classic Little Black Mini Dress", "HM-DRESS-W-042",
                    "The LBD, reimagined. Stretch jersey mini in a flattering bodycon fit.",
                    "Every wardrobe needs an LBD, and this is it. Made from a smooth, 4-way stretch viscose jersey, it contours to the body in all the right places. The round neck, cap sleeves and clean silhouette keep it versatile; the mini length makes a confident statement. Style with heels for a night out or trainers for a casual day look.",
                    34.99m, 13, 3, "Women",
                    Img(3444499, "Classic Little Black Mini Dress – black dress on model",
                        28232666, "Classic Little Black Mini Dress – back view, elegant styling"),
                    Meta(["Black","Navy","Burgundy"],["#111111","#1C2B4B","#800020"],
                         TopSizes, ["dress","lbd","mini","womens","bodycon","jersey"],
                         ["Material","Fit","Length","Neckline","Sleeve","Care"],
                         ["95% Viscose 5% Elastane","Bodycon","Mini","Round Neck","Cap Sleeve","Machine Wash 30°C, Do Not Tumble Dry"],
                         "Style code: HM-DRESS-W-042. Reshape and hang to dry."),
                    SeedDate, SeedDate
                },
                {
                    43, "Maxi Ruffle Hem Dress", "ZRA-DRESS-W-043",
                    "Romantic maxi dress with tiered ruffles. Makes every entrance memorable.",
                    "Drama, romance and movement — this tiered ruffle maxi has it all. The soft, woven fabric falls in graceful layers from the fitted bodice to a sweeping floor-length hem. The elasticated waist is both comfortable and figure-flattering; a V-neckline adds a hint of elegance. A dress for celebrating.",
                    109.99m, 13, 2, "Women",
                    Img(15071849, "Maxi Ruffle Hem Dress – women in elegant maxi dresses",
                        3517286, "Maxi Ruffle Hem Dress – ruffle hem fabric detail"),
                    Meta(["Dusty Rose","Cobalt","Ivory"],["#DCAE96","#0047AB","#FFFFF0"],
                         TopSizes, ["dress","maxi","ruffle","womens","romantic","occasion"],
                         ["Material","Style","Length","Neckline","Waist","Care"],
                         ["100% Polyester Crepe","Tiered Ruffle","Full Length Floor-Length","V-Neck","Elasticated","Hand Wash Cold"],
                         "Style code: ZRA-DRESS-W-043. Store hanging to prevent crush marks."),
                    SeedDate, SeedDate
                },
                {
                    44, "Women's A-Line Mini Skirt", "HM-DRESS-W-044",
                    "Classic A-line mini in ponte fabric. A versatile wardrobe workhorse.",
                    "This A-line mini skirt in a smooth ponte fabric sits at the natural waist and gently flares to a just-above-knee hem. The structured fabric holds its shape without restricting movement, making it equally comfortable at a desk or dancing. A concealed side zip and fully structured waistband give it a polished, put-together finish.",
                    24.99m, 13, 3, "Women",
                    Img(3444499, "Women's A-Line Mini Skirt – A-line skirt on model",
                        28232666, "Women's A-Line Mini Skirt – back view and hem detail"),
                    Meta(["Black","Red","Forest Green"],["#111111","#CC0000","#228B22"],
                         TopSizes, ["skirt","a-line","mini","womens","ponte","classic"],
                         ["Material","Fit","Length","Waist","Closure","Care"],
                         ["95% Polyester 5% Elastane Ponte","A-Line Flared","Mini (Above Knee)","Natural Waist Structured","Concealed Side Zip","Machine Wash 30°C"],
                         "Style code: HM-DRESS-W-044. Iron on low heat to maintain structure."),
                    SeedDate, SeedDate
                },
                {
                    45, "Women's Satin Midi Pencil Skirt", "ZRA-DRESS-W-045",
                    "Elegant satin-finish midi pencil skirt. Effortlessly luxurious.",
                    "Nothing says quiet luxury like a well-cut satin pencil skirt. This midi version, in a smooth charmeuse satin, clings gently to the hips and thighs before tapering to a slim hem with a back vent for easy walking. The wide waistband sits at the natural waist and fastens cleanly at the back with a concealed hook-and-bar.",
                    59.99m, 13, 2, "Women",
                    Img(28232666, "Women's Satin Midi Pencil Skirt – elegant back silhouette",
                        15071849, "Women's Satin Midi Pencil Skirt – satin fabric lustre detail"),
                    Meta(["Champagne","Black","Forest Green"],["#F7E7CE","#111111","#228B22"],
                         TopSizes, ["skirt","pencil","midi","womens","satin","elegant"],
                         ["Material","Fit","Length","Waist","Back Vent","Care"],
                         ["100% Polyester Charmeuse Satin","Pencil / Bodycon","Midi (Below Knee)","Wide Natural Waistband","Back Kick Vent","Dry Clean or Hand Wash Cold"],
                         "Style code: ZRA-DRESS-W-045. Handle with care — satin snags easily."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Suits & Blazers (catId=14)
            // Pexels: 936043=man in blue blazer  9210389=man in black suit
            //         450212=man in blazer against wall  2662794=man in blue suit
            //         652348=men's black blazer product shot
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    46, "Men's Classic Slim Fit Two-Piece Suit", "RL-SUIT-M-046",
                    "A sharp slim-fit suit in super-100 wool. Power dressing for modern gentlemen.",
                    "Ralph Lauren's signature two-piece suit is engineered in Italy from a super-100 wool blend with a subtle sheen that signals quality in every room. The slim-fit jacket is canvassed in the chest for structure that drapes beautifully; the matching trousers have a flat front and quarter-top pockets. A generational investment.",
                    550.00m, 14, 4, "Men",
                    Img(9210389, "Men's Classic Slim Fit Suit – man in sharp black suit",
                        2662794, "Men's Classic Slim Fit Suit – suit lapel and pocket square detail"),
                    Meta(["Charcoal","Navy","Mid Grey"],["#36454F","#1C2B4B","#808080"],
                         SuitSizes, ["suit","two-piece","wool","mens","slim-fit","formal","ralph-lauren"],
                         ["Material","Construction","Fit","Lapel","Trousers","Care"],
                         ["Super-100 Wool Blend","Half-Canvassed Chest","Slim Fit","Notch Lapel","Flat Front, Quarter-Top Pockets","Dry Clean Only"],
                         "Style code: RL-SUIT-M-046. Includes matching trousers. Jacket sold separately available."),
                    SeedDate, SeedDate
                },
                {
                    47, "Men's Double-Breasted Wool Blazer", "ZRA-SUIT-M-047",
                    "Structured double-breasted blazer in a rich wool blend. Statement office dressing.",
                    "The double-breasted blazer is the signature of confident dressing, and this one earns that confidence. Made from a sturdy wool blend, it features wide peak lapels, a 6×2 button arrangement, and structured shoulder padding that gives an authoritative silhouette. Wear with matching trousers for a full suit effect or over dark jeans.",
                    199.00m, 14, 2, "Men",
                    Img(936043, "Men's Double-Breasted Wool Blazer – man in blue blazer, confident pose",
                        450212, "Men's Double-Breasted Wool Blazer – lapel and button detail"),
                    Meta(["Navy","Camel","Dark Grey"],["#1C2B4B","#C19A6B","#404040"],
                         SuitSizes, ["blazer","double-breasted","wool","mens","formal","office"],
                         ["Material","Fit","Lapel","Buttons","Lining","Care"],
                         ["70% Wool 30% Polyester","Structured Regular Fit","Wide Peak Lapel","6×2 Button Arrangement","Full Satin Lining","Dry Clean Only"],
                         "Style code: ZRA-SUIT-M-047. Dry clean to maintain structure."),
                    SeedDate, SeedDate
                },
                {
                    48, "Men's Slim Fit Black Tuxedo Suit", "HM-SUIT-M-048",
                    "Sleek slim-fit tuxedo suit with satin lapels. Dressed to impress.",
                    "Be the best-dressed guest in the room with this slim-fit tuxedo suit. The jacket features satin-faced notch lapels and a one-button closure that keeps the front clean; matching satin stripe runs down the side seam of the flat-front trousers. Fully lined in smooth polyester for comfort over long evenings.",
                    149.00m, 14, 3, "Men",
                    Img(652348, "Men's Slim Fit Black Tuxedo Suit – black blazer product shot",
                        9210389, "Men's Slim Fit Black Tuxedo Suit – on model, formal occasion"),
                    Meta(["Black","Midnight Navy","White (Dinner Jacket)"],["#111111","#191970","#F8F8F8"],
                         SuitSizes, ["tuxedo","suit","formal","mens","slim-fit","black-tie"],
                         ["Material","Jacket Lapel","Trouser Detail","Fit","Lining","Care"],
                         ["60% Polyester 40% Viscose","Satin-Faced Notch Lapel","Satin Side Stripe","Slim Fit","Full Polyester","Machine Wash Cold (Structured), Prefer Dry Clean"],
                         "Style code: HM-SUIT-M-048. Includes matching satin-stripe trousers."),
                    SeedDate, SeedDate
                },
                {
                    49, "Women's Tailored Single-Breasted Blazer", "ZRA-SUIT-W-049",
                    "Sharp tailored blazer in a stretch crepe. The ultimate power piece.",
                    "This tailored blazer is the backbone of a powerful wardrobe. Cut from a smooth stretch crepe with a confident structured shoulder, it flatters all body types and transitions effortlessly from the boardroom to after-work drinks. Wear it buttoned as a standalone top with trousers, or open over a midi dress.",
                    99.99m, 14, 2, "Women",
                    Img(936043, "Women's Tailored Single-Breasted Blazer – tailored blazer styled",
                        652348, "Women's Tailored Single-Breasted Blazer – blazer lapel and lining detail"),
                    Meta(["Black","Ivory","Camel"],["#111111","#FFFFF0","#C19A6B"],
                         SuitSizes, ["blazer","tailored","womens","single-breasted","office","power"],
                         ["Material","Fit","Lapel","Closure","Length","Care"],
                         ["95% Polyester 5% Elastane Crepe","Structured Regular Fit","Notch Lapel","Single Button","Hip Length","Machine Wash 30°C or Dry Clean"],
                         "Style code: ZRA-SUIT-W-049. Press on reverse with a damp cloth."),
                    SeedDate, SeedDate
                },
                {
                    50, "Women's Power Suit Set", "RL-SUIT-W-050",
                    "Matching blazer and wide-leg trouser set in a premium wool blend.",
                    "Ralph Lauren's Power Suit Set is exactly what its name promises. The tailored blazer and wide-leg trouser are cut from the same premium wool-blend twill, creating a cohesive, commanding look. The blazer has a nipped-in waist and strong shoulders; the trousers feature a high waist and full-length wide leg. Buy as a set or as separates.",
                    420.00m, 14, 4, "Women",
                    Img(2662794, "Women's Power Suit Set – suit set styled on model",
                        9210389, "Women's Power Suit Set – blazer fabric and detail"),
                    Meta(["Pinstripe Grey","Navy","Chalk White"],["#808080","#1C2B4B","#F5F0E8"],
                         SuitSizes, ["suit","power-suit","womens","blazer","trousers","formal","ralph-lauren"],
                         ["Material","Blazer Fit","Trouser Fit","Pattern","Lining","Care"],
                         ["65% Wool 35% Polyester Twill","Nipped Waist, Strong Shoulder","High-Rise Wide-Leg","Fine Pinstripe","Full Silk-Touch Lining","Dry Clean Only"],
                         "Style code: RL-SUIT-W-050. Blazer and trousers available separately."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Shoes (catId=15)
            // Pexels: 5888=handmade sneakers  322207=low angle view of shoes
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    51, "Men's Classic White Leather Sneakers", "NK-SHOE-M-051",
                    "Clean white leather sneakers with a minimalist profile. Timeless street style.",
                    "Nike's approach to the classic white sneaker is deceptively simple: impeccably clean lines, premium full-grain leather upper, and a low-profile foam midsole that adds just enough cushioning for all-day comfort. The tonal rubber outsole keeps the look uninterrupted from every angle. A pair that works with every outfit.",
                    110.00m, 15, 5, "Men",
                    Img(5888, "Men's Classic White Leather Sneakers – clean white sneakers on surface",
                        322207, "Men's Classic White Leather Sneakers – low-angle lifestyle shot"),
                    Meta(["White/White","White/Black","White/Grey"],["#F5F5F5","#F5F5F5","#F5F5F5"],
                         ShoeSizes, ["sneakers","shoes","white","mens","leather","nike","casual"],
                         ["Upper","Midsole","Outsole","Closure","Width","Care"],
                         ["Full-Grain Leather","Low-Profile Foam","Tonal Rubber","Lace-Up","Standard D Width","Wipe with Damp Cloth, Air Dry"],
                         "Style code: NK-SHOE-M-051. Comes with an extra pair of flat laces."),
                    SeedDate, SeedDate
                },
                {
                    52, "Men's Brogue Oxford Shoes", "RL-SHOE-M-052",
                    "Full-brogue Oxfords in Goodyear-welted calf leather. Built for a lifetime.",
                    "Goodyear-welted construction means these full-brogue Oxfords can be resoled, making them a lifetime investment rather than a disposable purchase. Made from smooth calf leather with traditional hand-punched brogue detailing, they are finished by hand in Portugal. A shoe that tells the world exactly who it belongs to.",
                    285.00m, 15, 4, "Men",
                    Img(322207, "Men's Brogue Oxford Shoes – leather dress shoes, formal styling",
                        5888, "Men's Brogue Oxford Shoes – welt and sole detail"),
                    Meta(["Tan","Dark Brown","Black"],["#D2B48C","#3D1F0D","#111111"],
                         ShoeSizes, ["shoes","oxfords","brogue","mens","leather","formal","ralph-lauren"],
                         ["Upper","Construction","Sole","Lining","Last","Care"],
                         ["Smooth Calf Leather","Goodyear-Welted","Single Leather + Rubber Heel","Full Leather","Traditional Round Toe","Polish and Condition Regularly"],
                         "Style code: RL-SHOE-M-052. Made in Portugal. Can be resoled."),
                    SeedDate, SeedDate
                },
                {
                    53, "Men's React Running Trainers", "NK-SHOE-M-053",
                    "High-performance React foam running trainers. Run further, recover faster.",
                    "Engineered for distance runners who refuse to compromise on energy return, these trainers are built around Nike's React foam — a cushioning system that is 13% lighter and 11% more energy-returning than standard foam. A breathable Flyknit upper wraps the foot for a sock-like fit, and the rubber outsole provides durable grip on any surface.",
                    150.00m, 15, 5, "Men",
                    Img(5888, "Men's React Running Trainers – athletic trainer on track surface",
                        322207, "Men's React Running Trainers – sole and heel unit detail"),
                    Meta(["Black/White","Blue/Orange","Grey/Neon"],["#111111","#0047AB","#808080"],
                         ShoeSizes, ["trainers","running","mens","react","nike","performance","sport"],
                         ["Upper","Midsole","Outsole","Drop","Width","Care"],
                         ["Flyknit Breathable Mesh","Nike React Foam","Rubber Waffle Outsole","10mm Drop","Standard","Machine Wash Cold, Air Dry"],
                         "Style code: NK-SHOE-M-053. React foam returns 13% more energy than standard foam."),
                    SeedDate, SeedDate
                },
                {
                    54, "Women's Classic Ballet Flats", "ZRA-SHOE-W-054",
                    "Timeless pointed-toe ballet flats in smooth leather. Quiet elegance for every day.",
                    "No shoe works harder across more occasions than a great ballet flat, and this pointed-toe version in smooth leather is one of the best. The elasticated topline ensures a secure fit across a variety of foot widths; a padded leather insole provides surprising comfort for extended wear. The sleek pointed toe elongates the foot visually.",
                    79.99m, 15, 2, "Women",
                    Img(322207, "Women's Classic Ballet Flats – nude pointed-toe flats",
                        5888, "Women's Classic Ballet Flats – flat sole and leather detail"),
                    Meta(["Nude","Black","Red"],["#E8C9A0","#111111","#CC0000"],
                         ShoeSizes, ["flats","ballet","womens","leather","pointed-toe","classic"],
                         ["Upper","Lining","Insole","Toe","Topline","Care"],
                         ["100% Smooth Leather","Leather","Padded Leather","Pointed Toe","Elasticated","Wipe Clean, Condition Regularly"],
                         "Style code: ZRA-SHOE-W-054. Half size up recommended for wider feet."),
                    SeedDate, SeedDate
                },
                {
                    55, "Women's Leather Ankle Boots", "ZRA-SHOE-W-055",
                    "Sleek leather ankle boots on a block heel. Day-to-night in one step.",
                    "These ankle boots mean business. Made from smooth leather with a block heel that provides stability as well as height, they transition flawlessly from a day at the office to a night out. The almond toe keeps things modern; side zip entry ensures you're in and out in seconds. Available in three seasonless shades.",
                    139.99m, 15, 2, "Women",
                    Img(5888, "Women's Leather Ankle Boots – leather ankle boot side profile",
                        322207, "Women's Leather Ankle Boots – block heel and sole detail"),
                    Meta(["Black","Camel","Dark Brown"],["#111111","#C19A6B","#3D1F0D"],
                         ShoeSizes, ["boots","ankle-boots","womens","leather","block-heel","classic"],
                         ["Upper","Heel","Lining","Toe","Entry","Care"],
                         ["100% Leather","Block Heel 7cm","Full Leather","Almond Toe","Side Zip","Condition Regularly, Polish if Needed"],
                         "Style code: ZRA-SHOE-W-055. Block heel for all-day comfort."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Bags (catId=17)
            // Pexels: 2081332=white handbag product shot
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    56, "Women's Structured Leather Tote", "ZRA-BAG-W-056",
                    "Spacious structured tote in full-grain leather. The workbag reimagined.",
                    "This large tote is designed to carry your entire working life without looking like it does. Made from full-grain leather with a stiffened base and magnetic snap closure, it holds a 15-inch laptop, documents and daily essentials without collapsing. Interior slip and zip pockets keep everything in its place.",
                    195.00m, 17, 2, "Women",
                    ImgSingle(2081332, "Women's Structured Leather Tote – structured white leather tote",
                              "Women's Structured Leather Tote – interior organisation detail"),
                    Meta(["Black","Tan","White"],["#111111","#D2B48C","#F5F5F5"],
                         OneSize, ["tote","bag","womens","leather","work","structured"],
                         ["Material","Dimensions","Strap Drop","Closure","Pockets","Care"],
                         ["Full-Grain Vegetable-Tanned Leather","38×30×14 cm","22 cm","Magnetic Snap","3 Internal (1 Zip, 2 Slip)","Condition Regularly, Keep in Dust Bag"],
                         "Style code: ZRA-BAG-W-056. Fits 15-inch laptop."),
                    SeedDate, SeedDate
                },
                {
                    57, "Women's Chain Shoulder Bag", "ZRA-BAG-W-057",
                    "Evening-to-day chain bag in quilted leather. From brunch to gala.",
                    "The chain strap is a signature detail that never dates, and this compact quilted shoulder bag wears it beautifully. A single adjustable gold-tone chain strap allows wear over the shoulder or across the body; the magnetic flap closure seals the quilted leather body. Compact dimensions conceal a surprisingly well-organised interior.",
                    129.00m, 17, 2, "Women",
                    ImgSingle(2081332, "Women's Chain Shoulder Bag – quilted leather chain bag",
                              "Women's Chain Shoulder Bag – gold chain strap detail"),
                    Meta(["Black","Ecru","Blush"],["#111111","#FAEBD7","#FFB6C1"],
                         OneSize, ["bag","shoulder-bag","chain","womens","quilted","evening"],
                         ["Material","Strap","Closure","Dimensions","Pockets","Care"],
                         ["Quilted Lambskin Leather","Gold-Tone Chain, Adjustable","Magnetic Flap","24×16×6 cm","1 Internal Zip + 1 Card Slot","Wipe Clean, Store in Dust Bag"],
                         "Style code: ZRA-BAG-W-057. Adjust chain for shoulder or crossbody wear."),
                    SeedDate, SeedDate
                },
                {
                    58, "Women's Mini Crossbody Bag", "HM-BAG-W-058",
                    "Compact PU crossbody bag with adjustable strap. Hands-free freedom.",
                    "Small but mighty, this mini crossbody bag is cut from smooth PU leather with a clean flap-over closure and adjustable crossbody strap. It holds your phone, keys, a card wallet and a lipstick — everything you need, nothing you don't. The structured shape keeps it looking polished however much it's packed.",
                    29.99m, 17, 3, "Women",
                    ImgSingle(2081332, "Women's Mini Crossbody Bag – compact crossbody styling",
                              "Women's Mini Crossbody Bag – strap adjustment detail"),
                    Meta(["Black","Sage","Dusty Rose"],["#111111","#B2C2A6","#C9A9A6"],
                         OneSize, ["bag","crossbody","mini","womens","pu","casual"],
                         ["Material","Strap","Closure","Dimensions","Pockets","Care"],
                         ["PU Leather","Adjustable Crossbody (Max 60 cm drop)","Flap-Over Magnetic","18×12×5 cm","1 External Slip + 1 Internal","Wipe Clean with Damp Cloth"],
                         "Style code: HM-BAG-W-058. Lightweight everyday carry."),
                    SeedDate, SeedDate
                },
                {
                    59, "Men's Canvas Shopper Bag", "HM-BAG-M-059",
                    "Durable organic cotton canvas tote. Stylish, sustainable, everyday carry.",
                    "A generous canvas tote that keeps up with the pace of modern life. Made from heavy-duty organic cotton canvas with reinforced handles, this bag handles everything from grocery runs to gym sessions without breaking a sweat. An internal zip pocket keeps valuables secure. Printed with a minimal tonal graphic.",
                    19.99m, 17, 3, "Men",
                    ImgSingle(2081332, "Men's Canvas Shopper Bag – canvas tote bag product shot",
                              "Men's Canvas Shopper Bag – handle and stitching detail"),
                    Meta(["Natural/Black Print","Black/White Print","Navy/White Print"],["#E8D5B7","#111111","#1C2B4B"],
                         OneSize, ["bag","tote","canvas","mens","casual","eco","organic"],
                         ["Material","Handles","Closure","Capacity","Pockets","Care"],
                         ["Organic Cotton Canvas 420g","Reinforced Cotton, 35 cm Drop","Open Top + Internal Zip Pocket","15 Litres","1 Internal Zip","Machine Wash 40°C"],
                         "Style code: HM-BAG-M-059. Made from certified organic cotton."),
                    SeedDate, SeedDate
                },
                {
                    60, "Women's Quilted Clutch Bag", "ZRA-BAG-W-060",
                    "Compact quilted clutch with a wristlet strap. Perfect for evenings out.",
                    "Carry only the essentials in this elegant quilted clutch. Made from a smooth leather with a classic diamond-quilt pattern, it closes with a push-lock clasp and comes with a detachable wristlet strap for hands-free security. Inside, a single compartment with two card slots and a slip pocket keeps your night organised.",
                    69.99m, 17, 2, "Women",
                    ImgSingle(2081332, "Women's Quilted Clutch Bag – quilted clutch close-up",
                              "Women's Quilted Clutch Bag – push-lock clasp and wristlet detail"),
                    Meta(["Gold","Black","Cobalt"],["#FFD700","#111111","#0047AB"],
                         OneSize, ["clutch","bag","womens","quilted","evening","occasion"],
                         ["Material","Strap","Closure","Dimensions","Interior","Care"],
                         ["Quilted Leather","Detachable Wristlet 18 cm","Push-Lock Clasp","22×13×4 cm","1 Main Compartment, 2 Card Slots, 1 Slip Pocket","Wipe Clean with Soft Cloth"],
                         "Style code: ZRA-BAG-W-060. Detachable wristlet for security."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Underwear & Basics (catId=18)
            // Pexels: 7760243=woman in beige knit (basics styling)  2704500=person in sweater
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    61, "Men's 3-Pack Stretch Cotton Briefs", "HM-UND-M-061",
                    "Everyday stretch-cotton briefs in a classic mid-rise cut. Pack of 3.",
                    "Comfort comes first with these mid-rise stretch-cotton briefs. Made from a soft cotton-elastane blend that moves with you throughout the day, the flat-lock seams prevent chafing and the elasticated waistband is strong enough to maintain its shape wash after wash. Pack of 3 in a tonal colour mix.",
                    17.99m, 18, 3, "Men",
                    Img(2704500, "Men's 3-Pack Stretch Cotton Briefs – men's basics flat lay",
                        7760243, "Men's 3-Pack Stretch Cotton Briefs – cotton fabric detail"),
                    Meta(["White/Grey/Black","Navy/Blue/White","All Black"],["#F5F5F5","#1C2B4B","#111111"],
                         TopSizes, ["briefs","underwear","mens","cotton","3-pack","basics"],
                         ["Material","Rise","Waistband","Seams","Pack","Care"],
                         ["95% Cotton 5% Elastane","Mid-Rise","Elasticated Logo Band","Flat-Lock (Anti-Chafe)","Pack of 3","Machine Wash 60°C"],
                         "Style code: HM-UND-M-061. Pack of 3. Machine washable at 60°C for hygiene."),
                    SeedDate, SeedDate
                },
                {
                    62, "Men's Classic Stretch Boxers", "RL-UND-M-062",
                    "Signature Polo stretch boxers in Pima cotton. Everyday luxury.",
                    "Ralph Lauren's signature stretch boxer shorts are made from premium Pima cotton with a touch of elastane for a soft, comfortable fit that doesn't pull or bunch. The elasticated waistband features the iconic Polo player embroidery; the classic boxer cut provides unrestricted movement for an active day.",
                    35.00m, 18, 4, "Men",
                    Img(2704500, "Men's Classic Stretch Boxers – premium cotton basics styling",
                        7760243, "Men's Classic Stretch Boxers – fabric close-up"),
                    Meta(["White","Navy","Grey"],["#F5F5F5","#1C2B4B","#808080"],
                         TopSizes, ["boxers","underwear","mens","cotton","pima","ralph-lauren"],
                         ["Material","Cut","Waistband","Feature","Care"],
                         ["95% Pima Cotton 5% Elastane","Classic Boxer","Elasticated, Embroidered Polo Player","Open Fly","Machine Wash 40°C"],
                         "Style code: RL-UND-M-062. Polo Player embroidery on waistband."),
                    SeedDate, SeedDate
                },
                {
                    63, "Women's 5-Pack Cotton Hipster Briefs", "HM-UND-W-063",
                    "Soft cotton hipster briefs in a full coverage cut. Essential 5-pack.",
                    "Comfort, reliability and quality at everyday prices — these soft cotton hipster briefs tick all the boxes. A slightly lower rise sits comfortably on the hip rather than the waist; the full-coverage cut is secure without digging in. Flat-lock seams and a smooth gusset lining make them invisible under fitted clothing.",
                    21.99m, 18, 3, "Women",
                    Img(7760243, "Women's 5-Pack Cotton Hipster Briefs – women's basics styling",
                        2704500, "Women's 5-Pack Cotton Hipster Briefs – soft cotton fabric detail"),
                    Meta(["White Mix","Black Mix","Pastel Mix"],["#F5F5F5","#111111","#FFB6C1"],
                         TopSizes, ["briefs","underwear","womens","cotton","5-pack","basics","hipster"],
                         ["Material","Rise","Coverage","Seams","Pack","Care"],
                         ["95% Cotton 5% Elastane","Low-Hip Rise","Full Coverage","Flat-Lock Anti-Chafe","Pack of 5","Machine Wash 60°C"],
                         "Style code: HM-UND-W-063. Pack of 5. Wash at 60°C for freshness."),
                    SeedDate, SeedDate
                },
                {
                    64, "Women's Underwired T-Shirt Bra", "HM-UND-W-064",
                    "Smooth T-shirt bra with seamless cups. Invisible under everything.",
                    "The perfect T-shirt bra is one you forget you're wearing, and this one comes close. Seamless microfibre cups mould to the bust for a smooth, natural silhouette under fitted tops. The underwire provides uplift and shape without digging; the cushioned straps distribute weight evenly for all-day comfort.",
                    22.99m, 18, 3, "Women",
                    Img(7760243, "Women's Underwired T-Shirt Bra – women's basics flat lay styling",
                        2704500, "Women's Underwired T-Shirt Bra – microfibre fabric detail"),
                    Meta(["Nude","Black","White"],["#E8C9A0","#111111","#F5F5F5"],
                         ["32A","34A","34B","36B","36C","38C","38D","40D"], ["bra","lingerie","t-shirt-bra","womens","seamless","underwired"],
                         ["Cups","Underwire","Straps","Back","Closure","Care"],
                         ["Seamless Microfibre","Foam-Padded Wire Casing","Cushioned, Adjustable","4 Rows of Hooks (DD+)","Back Hook-and-Eye","Hand Wash Cold or Delicate Machine Wash"],
                         "Style code: HM-UND-W-064. Avoid tumble drying to extend lifespan."),
                    SeedDate, SeedDate
                },
                {
                    65, "Women's Seamless Soft-Cup Bralette", "ZRA-UND-W-065",
                    "Wireless seamless bralette for everyday ease. Comfort without compromise.",
                    "A wire-free, seamless bralette that provides gentle support without any of the restrictive structure of a traditional bra. Made from a smooth four-way stretch fabric with a light removable padding, it disappears under clothing and is comfortable enough to sleep in. For light-to-medium bust sizes; perfect for casual and work-from-home dressing.",
                    29.99m, 18, 2, "Women",
                    Img(7760243, "Women's Seamless Soft-Cup Bralette – soft basics flatlay",
                        2704500, "Women's Seamless Soft-Cup Bralette – seamless fabric close-up"),
                    Meta(["Blush","Black","Sage"],["#FFB6C1","#111111","#B2C2A6"],
                         ["XS","S","M","L","XL"], ["bralette","lingerie","wireless","womens","seamless","casual"],
                         ["Material","Cups","Underwire","Straps","Closure","Care"],
                         ["92% Nylon 8% Elastane","Light Removable Padding","Wire-Free","Fixed Wide Shoulder Straps","Pull-Over, No Closure","Machine Wash Cold, Flat Dry"],
                         "Style code: ZRA-UND-W-065. Wire-free for ultimate comfort."),
                    SeedDate, SeedDate
                },
            });

            // ------------------------------------------------------------------
            // SUB-CATEGORY: Glasses (catId=19)
            // Pexels: 4952482=black and brown framed sunglasses  9982630=fashionable sunglasses
            //         2622187=sunglasses and cosmetics (fashion styling)
            // ------------------------------------------------------------------
            migrationBuilder.InsertData("Products", cols, new object[,]
            {
                {
                    66, "Men's Classic Aviator Sunglasses", "ZRA-GLASS-M-066",
                    "Timeless aviator sunglasses with UV400 lenses. The pilot look never ages.",
                    "The aviator silhouette has been a symbol of cool since the 1930s, and this modern interpretation stays true to the original proportions. A lightweight metal double-bridge frame sits with minimal weight; gradient UV400 lenses protect against harmful rays while looking effortlessly cinematic.",
                    49.99m, 19, 2, "Men",
                    Img(4952482, "Men's Classic Aviator Sunglasses – vintage-style aviator sunglasses",
                        9982630, "Men's Classic Aviator Sunglasses – frame and lens detail"),
                    Meta(["Gold/Brown Gradient","Silver/Grey Gradient","Gunmetal/G15"],
                         ["#B8860B","#808080","#4A5240"],
                         OneSize, ["sunglasses","aviator","mens","uv400","metal","classic"],
                         ["Frame","Lens","UV Protection","Bridge","Temple","Lens Width"],
                         ["Lightweight Metal","Polycarbonate Gradient","UV400","Double Bridge","Spring Hinge, 145mm","58mm"],
                         "Style code: ZRA-GLASS-M-066. Comes with leather pouch and cleaning cloth."),
                    SeedDate, SeedDate
                },
                {
                    67, "Men's Square Frame Reading Glasses", "HM-GLASS-M-067",
                    "Bold square frames in lightweight acetate. Style meets visual clarity.",
                    "Reading glasses that you are proud to keep on the table. Made from premium Italian acetate in a bold square frame, these come in a range of classic colourways and in three reading strengths. Blue-light filtering lenses reduce digital eye strain without yellowing the visual field — essential for modern screen-heavy lives.",
                    34.99m, 19, 3, "Men",
                    Img(9982630, "Men's Square Frame Reading Glasses – fashionable reading glasses in case",
                        4952482, "Men's Square Frame Reading Glasses – frame and arm detail"),
                    Meta(["Tortoiseshell","Matte Black","Transparent"],["#7B4A2D","#111111","#E8E8E8"],
                         ["+1.0", "+1.5", "+2.0", "+2.5", "+3.0"],
                         ["glasses","reading","mens","blue-light","acetate","square"],
                         ["Frame","Lens","Blue Light","Strength","Bridge","Temple"],
                         ["Premium Italian Acetate","Polycarbonate, Blue-Light Filter","Filters 40% of HEV Blue Light","Available +1.0 to +3.0","18mm","145mm"],
                         "Style code: HM-GLASS-M-067. Blue-light filter lens standard."),
                    SeedDate, SeedDate
                },
                {
                    68, "Women's Cat-Eye Sunglasses", "ZRA-GLASS-W-068",
                    "Retro cat-eye sunglasses with oversized lenses. Vintage drama for every day.",
                    "Cat-eye sunglasses are the ultimate expression of retro glamour, and these oversized ones deliver that promise in full. The upswept frame tip and oversized polarised lenses create an instant wow factor; the lightweight acetate construction means you can wear them all day without discomfort. A Riviera classic reborn.",
                    55.99m, 19, 2, "Women",
                    Img(4952482, "Women's Cat-Eye Sunglasses – retro cat-eye frames on surface",
                        2622187, "Women's Cat-Eye Sunglasses – sunglasses with cosmetics styling"),
                    Meta(["Black/Gold","Tortoiseshell/Brown","White/Rose Gold"],
                         ["#111111","#7B4A2D","#F5F0E8"],
                         OneSize, ["sunglasses","cat-eye","womens","polarised","retro","vintage"],
                         ["Frame","Lens","UV","Polarised","Lens Width","Temple"],
                         ["Acetate","Polycarbonate Gradient","UV400","Yes, Polarised","60mm","148mm"],
                         "Style code: ZRA-GLASS-W-068. Polarised lenses for glare reduction."),
                    SeedDate, SeedDate
                },
                {
                    69, "Women's Oversized Round Sunglasses", "HM-GLASS-W-069",
                    "Dramatic oversized round frames in acetate. Bold retro attitude.",
                    "Go big or go home — and these oversized round sunglasses go very big indeed. The circular acetate frames reference the free-spirited sixties; UV400 tinted lenses give them a modern protective function. Lightweight and comfortable to wear, they come with a protective hard case and microfibre cleaning cloth.",
                    29.99m, 19, 3, "Women",
                    Img(9982630, "Women's Oversized Round Sunglasses – oversized round sunglasses",
                        4952482, "Women's Oversized Round Sunglasses – vintage frame detail"),
                    Meta(["Caramel Tortoise","Black","Sage Green"],["#C68642","#111111","#B2C2A6"],
                         OneSize, ["sunglasses","round","oversized","womens","retro","60s"],
                         ["Frame","Lens","UV","Lens Diameter","Bridge","Temple"],
                         ["Acetate","Polycarbonate","UV400","62mm (Oversized)","20mm","145mm"],
                         "Style code: HM-GLASS-W-069. Includes hard case and cleaning cloth."),
                    SeedDate, SeedDate
                },
                {
                    70, "Women's Tortoiseshell Frame Optical Glasses", "ZRA-GLASS-W-070",
                    "Classic tortoiseshell frames in lightweight acetate. Intellectually chic.",
                    "The tortoiseshell pattern has a warmth and complexity that flatters every face shape, and these rectangular frames are sized and shaped to be universally flattering. Made from premium Italian acetate, they come with scratch-resistant, anti-reflective clear lenses that are ready for your prescription or can be worn with clear glass as a fashion frame.",
                    45.99m, 19, 2, "Women",
                    Img(2622187, "Women's Tortoiseshell Frame Glasses – glasses with accessories",
                        9982630, "Women's Tortoiseshell Frame Glasses – tortoiseshell acetate close-up"),
                    Meta(["Classic Tortoiseshell","Dark Tortoiseshell","Honey Blonde"],
                         ["#7B4A2D","#4A2A1A","#D4A956"],
                         OneSize, ["glasses","optical","tortoiseshell","womens","acetate","fashion"],
                         ["Frame","Lens","Coating","Lens Width","Bridge","Temple"],
                         ["Premium Italian Acetate","Scratch-Resistant Clear CR-39","Anti-Reflective Coating","52mm","17mm","145mm"],
                         "Style code: ZRA-GLASS-W-070. Available prescription-ready at opticians."),
                    SeedDate, SeedDate
                },
            });
        }

        // -----------------------------------------------------------------------
        // Down — remove all seed data (delete in FK-safe order)
        // -----------------------------------------------------------------------
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DELETE FROM ""Products"" WHERE ""Id"" BETWEEN 1 AND 70");
            migrationBuilder.Sql(@"DELETE FROM ""Brands"" WHERE ""Id"" BETWEEN 1 AND 5");
            // Self-referencing categories: delete children before parents
            migrationBuilder.Sql(@"DELETE FROM ""ProductCategories"" WHERE ""Id"" IN (3,4,6,7,8,10,11,12,17,18,19,13,14,15)");
            migrationBuilder.Sql(@"DELETE FROM ""ProductCategories"" WHERE ""Id"" IN (2,5,9,16)");
            migrationBuilder.Sql(@"DELETE FROM ""ProductCategories"" WHERE ""Id"" = 1");
        }
    }
}
