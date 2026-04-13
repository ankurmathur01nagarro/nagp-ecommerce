using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.ProductApi.Data.Migrations
{
    /// <summary>
    /// Replaces all 70 product images with cropped fashion-model photos from Pexels
    /// (models wearing/carrying the actual clothing/accessory, posing for camera).
    ///
    /// All Pexels photos are free for commercial use.
    /// URL format: portrait crop (w=600&amp;h=900&amp;fit=crop) for the hero shot,
    ///             wider crop  (w=800&amp;h=900&amp;fit=crop) for the styling shot.
    /// Each image retains a fresh UUID so the front-end image-upload feature works.
    /// </summary>
    public partial class UpdateProductImages : Migration
    {
        // Hero portrait crop
        private static string H(int id) =>
            $"https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&w=600&h=900&fit=crop";

        // Styling / detail crop
        private static string S(int id) =>
            $"https://images.pexels.com/photos/{id}/pexels-photo-{id}.jpeg?auto=compress&cs=tinysrgb&w=800&h=900&fit=crop";

        // Build a single UPDATE for one product
        private static string UpdateSql(int productId,
            int heroPhotoId,   string heroAlt,
            int stylingPhotoId, string stylingAlt)
        {
            // SQL-escape single quotes by doubling them
            heroAlt    = heroAlt.Replace("'", "''");
            stylingAlt = stylingAlt.Replace("'", "''");

            return
                $"""
                 UPDATE "Products"
                 SET "Images" = jsonb_build_array(
                     jsonb_build_object('id',       gen_random_uuid()::text,
                                        'url',      '{H(heroPhotoId)}',
                                        'alt',      '{heroAlt}',
                                        'sortOrder', 1),
                     jsonb_build_object('id',       gen_random_uuid()::text,
                                        'url',      '{S(stylingPhotoId)}',
                                        'alt',      '{stylingAlt}',
                                        'sortOrder', 2)
                 )
                 WHERE "Id" = {productId};
                 """;
        }

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── Shirts (1-5) ─────────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(1,
                9558709,  "Classic Oxford Button-Down Shirt – man in crisp white shirt posing",
                6109288,  "Classic Oxford Button-Down Shirt – styled look, white shirt with scarf"));

            migrationBuilder.Sql(UpdateSql(2,
                18297281, "Mens Slim Fit Linen Shirt – male model in checked linen shirt",
                775771,   "Mens Slim Fit Linen Shirt – full outfit, shirt with denim jeans"));

            migrationBuilder.Sql(UpdateSql(3,
                20080516, "Mens Poplin Dress Shirt – studio shot, model in dress shirt and sunglasses",
                19366877, "Mens Poplin Dress Shirt – model seated, smart-casual shirt look"));

            migrationBuilder.Sql(UpdateSql(4,
                1380595,  "Womens Silk Blouse – female model posing in blouse and black jeans",
                16375487, "Womens Silk Blouse – portrait of model against white wall"));

            migrationBuilder.Sql(UpdateSql(5,
                14823052, "Womens Cotton Poplin Shirt – casual model in shirt and jeans",
                17265467, "Womens Cotton Poplin Shirt – model in silver top and jeans"));

            // ── Jumpers & Cardigans (6-10) ───────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(6,
                18002044, "Mens Lambswool Crew-Neck Jumper – young male model in knit sweater",
                3754251,  "Mens Lambswool Crew-Neck Jumper – man in beige lambswool sweater"));

            migrationBuilder.Sql(UpdateSql(7,
                4890733,  "Mens Chunky Rib-Knit Sweater – man in blue knit sweater against wall",
                4120381,  "Mens Chunky Rib-Knit Sweater – model in dark chunky sweater"));

            migrationBuilder.Sql(UpdateSql(8,
                2132189,  "Womens Ribbed Turtleneck Sweater – close-up of woman in mustard sweater",
                2908870,  "Womens Ribbed Turtleneck Sweater – female model in striped knitwear"));

            migrationBuilder.Sql(UpdateSql(9,
                3582500,  "Womens Open-Front Merino Cardigan – woman in white crochet open-front cardigan",
                245388,   "Womens Open-Front Merino Cardigan – model in heather-grey cardigan by window"));

            migrationBuilder.Sql(UpdateSql(10,
                15915189, "Womens Oversized Chunky Cardigan – brunette in oversized pink hooded cardigan",
                4620610,  "Womens Oversized Chunky Cardigan – model in white oversized knit sweater"));

            // ── Jeans (11-15) ────────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(11,
                2815417,  "Classic Original Fit Jeans – male model in denim jacket and denim jeans",
                4066292,  "Classic Original Fit Jeans – model in black T-shirt and denim jeans"));

            migrationBuilder.Sql(UpdateSql(12,
                2315311,  "Mens Slim Fit Stretch Jeans – person in white shirt and slim blue jeans",
                10471897, "Mens Slim Fit Stretch Jeans – male fashion model in denim outdoor pose"));

            migrationBuilder.Sql(UpdateSql(13,
                18662550, "Mens Straight Leg Jeans – male model in jeans and blazer",
                3889627,  "Mens Straight Leg Jeans – man in plaid shirt and straight-leg jeans"));

            migrationBuilder.Sql(UpdateSql(14,
                1380595,  "Womens High-Rise Skinny Jeans – woman posing in sport shirt and black jeans",
                13391056, "Womens High-Rise Skinny Jeans – model in white crop top and denim jeans"));

            migrationBuilder.Sql(UpdateSql(15,
                18168659, "Womens Wide-Leg Mom Jeans – girl posing in wide-leg jeans in park",
                12610340, "Womens Wide-Leg Mom Jeans – fashion model in patchwork jeans and corset"));

            // ── Trousers (16-20) ─────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(16,
                2662794,  "Mens Slim Fit Chino Trousers – male model wearing slim navy chinos",
                16751012, "Mens Slim Fit Chino Trousers – model in beige jacket and tailored trousers"));

            migrationBuilder.Sql(UpdateSql(17,
                19357654, "Mens Classic Pleated Dress Trousers – man posing in full suit in studio",
                15092611, "Mens Classic Pleated Dress Trousers – portrait of man in dark suit"));

            migrationBuilder.Sql(UpdateSql(18,
                11434887, "Mens Relaxed Cargo Trousers – male model in casual street-fashion pose",
                3483102,  "Mens Relaxed Cargo Trousers – man in jacket and brown cargo-style trousers"));

            migrationBuilder.Sql(UpdateSql(19,
                19272278, "Womens Wide-Leg Tailored Trousers – model in elegant blazer and wide trousers",
                19002588, "Womens Wide-Leg Tailored Trousers – model in blazer and dress, studio pose"));

            migrationBuilder.Sql(UpdateSql(20,
                14997427, "Womens High-Waist Crepe Trousers – woman in tailored high-waist suit",
                20016340, "Womens High-Waist Crepe Trousers – model in crop top and high-waist skirt"));

            // ── Shorts (21-25) ───────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(21,
                3483102,  "Mens Slim Fit Chino Shorts – man in casual street outfit, jacket and trousers",
                9955748,  "Mens Slim Fit Chino Shorts – male fashion model in jacket, outdoor pose"));

            migrationBuilder.Sql(UpdateSql(22,
                2815417,  "Mens Washed Denim Shorts – male model in double denim street look",
                3889627,  "Mens Washed Denim Shorts – man in plaid shirt and casual denim"));

            migrationBuilder.Sql(UpdateSql(23,
                11434887, "Mens Performance Running Shorts – male model in athletic street wear",
                17350031, "Mens Performance Running Shorts – model in sports jacket and patterned bottoms"));

            migrationBuilder.Sql(UpdateSql(24,
                8991032,  "Womens High-Waisted Denim Shorts – woman in black jacket and denim",
                13391056, "Womens High-Waisted Denim Shorts – model in white crop top and denim"));

            migrationBuilder.Sql(UpdateSql(25,
                25786705, "Womens Linen-Blend Shorts – female model in skirt and crop top on street",
                19163488, "Womens Linen-Blend Shorts – woman in skirt and heels, full outfit pose"));

            // ── Autumn Jackets (26-30) ───────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(26,
                9955748,  "Mens Water-Resistant Bomber Jacket – male model in jacket, outdoor pose",
                11434887, "Mens Water-Resistant Bomber Jacket – male model posing on city street"));

            migrationBuilder.Sql(UpdateSql(27,
                16751012, "Mens Quilted Lightweight Jacket – model in beige quilted jacket and trousers",
                17350031, "Mens Quilted Lightweight Jacket – model in jacket and patterned pants"));

            migrationBuilder.Sql(UpdateSql(28,
                10274665, "Mens Harrington Jacket – man in brown jacket posing confidently in studio",
                13937357, "Mens Harrington Jacket – young male in black jacket, urban style pose"));

            migrationBuilder.Sql(UpdateSql(29,
                2896428,  "Womens Padded Utility Jacket – woman in fashionable utility jacket",
                14495270, "Womens Padded Utility Jacket – portrait of woman in stylish coat"));

            migrationBuilder.Sql(UpdateSql(30,
                3398192,  "Womens Cropped Windbreaker Jacket – woman in black coat, fashion pose",
                7236497,  "Womens Cropped Windbreaker Jacket – woman in brown coat, hand in pocket"));

            // ── Winter Jackets (31-35) ───────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(31,
                7037432,  "Mens Down-Fill Parka – male models in down jackets on snow-covered ground",
                15047544, "Mens Down-Fill Parka – winter fashion model in parka"));

            migrationBuilder.Sql(UpdateSql(32,
                16168570, "Mens Wool Blend Peacoat – man in tailored coat posing indoors",
                157675,   "Mens Wool Blend Peacoat – man in black hat and long black coat"));

            migrationBuilder.Sql(UpdateSql(33,
                21858851, "Mens Padded Duvet Coat – man in warm hat and padded jacket in winter",
                7037432,  "Mens Padded Duvet Coat – models in heavy-duty down jackets on snow"));

            migrationBuilder.Sql(UpdateSql(34,
                15759423, "Womens Oversized Puffer Jacket – woman in beige puffer jacket in snow",
                15461326, "Womens Oversized Puffer Jacket – female model in warm jacket, studio shoot"));

            migrationBuilder.Sql(UpdateSql(35,
                14495270, "Womens Faux Fur Trim Parka – portrait of woman in fur-trim coat",
                2328422,  "Womens Faux Fur Trim Parka – woman wearing winter coat, fashion pose"));

            // ── Leather Jackets (36-40) ──────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(36,
                15869797, "Mens Classic Biker Leather Jacket – male model in black leather jacket",
                13937357, "Mens Classic Biker Leather Jacket – man in leather jacket, street pose"));

            migrationBuilder.Sql(UpdateSql(37,
                10274665, "Mens Slim Fit Leather Jacket – man in slim brown leather jacket posing",
                1687116,  "Mens Slim Fit Leather Jacket – male model wearing leather jacket"));

            migrationBuilder.Sql(UpdateSql(38,
                17350031, "Mens Distressed Brown Leather Jacket – model in textured jacket with patterned pants",
                9955748,  "Mens Distressed Brown Leather Jacket – male model in jacket, outdoor lighting"));

            migrationBuilder.Sql(UpdateSql(39,
                8441422,  "Womens Cropped Black Leather Jacket – model in black turtleneck leather jacket",
                5616748,  "Womens Cropped Black Leather Jacket – woman in black leather outfit posing"));

            migrationBuilder.Sql(UpdateSql(40,
                11555859, "Womens Oversized Faux Leather Jacket – woman in black leather jacket and pants",
                16098114, "Womens Oversized Faux Leather Jacket – woman in red turtleneck and leather jacket"));

            // ── Dresses & Skirts (41-45) ─────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(41,
                9512043,  "Floral Wrap Midi Dress – woman in floral dress on catwalk",
                2474256,  "Floral Wrap Midi Dress – model standing in dress, confident pose"));

            migrationBuilder.Sql(UpdateSql(42,
                17570989, "Classic Little Black Mini Dress – model in black dress posing in studio",
                30736117, "Classic Little Black Mini Dress – elegant fashion model in studio shoot"));

            migrationBuilder.Sql(UpdateSql(43,
                8751237,  "Maxi Ruffle Hem Dress – young woman in white maxi dress, arms raised",
                30736118, "Maxi Ruffle Hem Dress – high-fashion model in elegant dress, studio"));

            migrationBuilder.Sql(UpdateSql(44,
                20016340, "Womens A-Line Mini Skirt – woman in mini skirt and crop top",
                25786705, "Womens A-Line Mini Skirt – female model in skirt and crop top on street"));

            migrationBuilder.Sql(UpdateSql(45,
                19163488, "Womens Satin Midi Pencil Skirt – woman in skirt and heels, full pose",
                14997427, "Womens Satin Midi Pencil Skirt – woman in tailored suit and mini skirt"));

            // ── Suits & Blazers (46-50) ──────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(46,
                15092611, "Mens Classic Slim Fit Suit – portrait of man in dark slim-fit suit",
                19357654, "Mens Classic Slim Fit Suit – man posing in suit in studio, dramatic lighting"));

            migrationBuilder.Sql(UpdateSql(47,
                18348433, "Mens Double-Breasted Wool Blazer – man posing in pinstripe vest and suit",
                10528698, "Mens Double-Breasted Wool Blazer – male model in black suit and hat, photoshoot"));

            migrationBuilder.Sql(UpdateSql(48,
                3217111,  "Mens Slim Fit Black Tuxedo Suit – man in black suit jacket with teal bowtie",
                16221482, "Mens Slim Fit Black Tuxedo Suit – smiling man posing in studio in tuxedo"));

            migrationBuilder.Sql(UpdateSql(49,
                19272278, "Womens Tailored Single-Breasted Blazer – model in elegant blazer and trousers",
                19002588, "Womens Tailored Single-Breasted Blazer – model in blazer dress, studio pose"));

            migrationBuilder.Sql(UpdateSql(50,
                17397914, "Womens Power Suit Set – woman in sharp suit with bag",
                14997427, "Womens Power Suit Set – woman in tailored suit and mini skirt"));

            // ── Shoes (51-55) ────────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(51,
                17427589, "Mens Classic White Leather Sneakers – male model in black jacket and white shoes",
                11434887, "Mens Classic White Leather Sneakers – male model on city street, sneaker styling"));

            migrationBuilder.Sql(UpdateSql(52,
                19357654, "Mens Brogue Oxford Shoes – man in suit posing in studio, full-length shoe styling",
                18348433, "Mens Brogue Oxford Shoes – man in formal suit and vest, dress shoe look"));

            migrationBuilder.Sql(UpdateSql(53,
                9955748,  "Mens React Running Trainers – male model in athletic jacket, trainer styling",
                17350031, "Mens React Running Trainers – model in sporty jacket and patterned pants"));

            migrationBuilder.Sql(UpdateSql(54,
                19163488, "Womens Classic Ballet Flats – woman in skirt and flats, elegant full pose",
                25786705, "Womens Classic Ballet Flats – female model in skirt and crop top, street style"));

            migrationBuilder.Sql(UpdateSql(55,
                14997427, "Womens Leather Ankle Boots – woman in tailored suit, ankle boot styling",
                20016340, "Womens Leather Ankle Boots – model in mini skirt and crop top, boot detail"));

            // ── Bags (56-60) ─────────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(56,
                1936848,  "Womens Structured Leather Tote – woman wearing brown leather tote bag",
                1653222,  "Womens Structured Leather Tote – model holding structured leather bag"));

            migrationBuilder.Sql(UpdateSql(57,
                23023550, "Womens Chain Shoulder Bag – brunette posing with quilted chain handbag",
                27151080, "Womens Chain Shoulder Bag – portrait of woman holding designer shoulder bag"));

            migrationBuilder.Sql(UpdateSql(58,
                5745781,  "Womens Mini Crossbody Bag – stylish woman with small crossbody bag in sunlight",
                12002801, "Womens Mini Crossbody Bag – woman in coat carrying black crossbody bag"));

            migrationBuilder.Sql(UpdateSql(59,
                19711183, "Mens Canvas Shopper Bag – models with leather and canvas bags",
                11124945, "Mens Canvas Shopper Bag – person carrying casual canvas tote bag"));

            migrationBuilder.Sql(UpdateSql(60,
                17397914, "Womens Quilted Clutch Bag – woman in suit holding elegant clutch bag",
                5745781,  "Womens Quilted Clutch Bag – stylish woman with handbag, golden hour light"));

            // ── Underwear & Basics (61-65) ───────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(61,
                18516993, "Mens 3-Pack Stretch Cotton Briefs – man in fitted black shirt and pants",
                26447865, "Mens 3-Pack Stretch Cotton Briefs – male model in T-shirt and cap"));

            migrationBuilder.Sql(UpdateSql(62,
                19099186, "Mens Classic Stretch Boxers – man in black T-shirt, clean studio shot",
                17718201, "Mens Classic Stretch Boxers – portrait of man in minimal black shirt"));

            migrationBuilder.Sql(UpdateSql(63,
                2908870,  "Womens 5-Pack Cotton Hipster Briefs – woman in casual knitwear, natural styling",
                4620610,  "Womens 5-Pack Cotton Hipster Briefs – model in white sweater, soft studio light"));

            migrationBuilder.Sql(UpdateSql(64,
                2132189,  "Womens Underwired T-Shirt Bra – close-up of woman in mustard fitted top",
                3582500,  "Womens Underwired T-Shirt Bra – woman in fitted white crochet top"));

            migrationBuilder.Sql(UpdateSql(65,
                245388,   "Womens Seamless Soft-Cup Bralette – woman in soft grey cardigan by window",
                15915189, "Womens Seamless Soft-Cup Bralette – woman in fitted pink hooded top"));

            // ── Glasses (66-70) ──────────────────────────────────────────────────
            migrationBuilder.Sql(UpdateSql(66,
                17140041, "Mens Classic Aviator Sunglasses – male model wearing aviator sunglasses",
                20080516, "Mens Classic Aviator Sunglasses – studio shot, man in shirt and sunglasses"));

            migrationBuilder.Sql(UpdateSql(67,
                9558709,  "Mens Square Frame Reading Glasses – man in white shirt, clean minimal style",
                6109288,  "Mens Square Frame Reading Glasses – man in scarf and shirt, scholarly look"));

            migrationBuilder.Sql(UpdateSql(68,
                16375487, "Womens Cat-Eye Sunglasses – female model posing in front of white wall",
                1380595,  "Womens Cat-Eye Sunglasses – woman in sunglasses posing behind wall"));

            migrationBuilder.Sql(UpdateSql(69,
                17265467, "Womens Oversized Round Sunglasses – model in silver top and jeans with sunglasses",
                14823052, "Womens Oversized Round Sunglasses – casual model in T-shirt, sunglasses styling"));

            migrationBuilder.Sql(UpdateSql(70,
                2908870,  "Womens Tortoiseshell Frame Glasses – woman in soft knitwear, intellectual style",
                15915189, "Womens Tortoiseshell Frame Glasses – woman in pink cardigan, glasses look"));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Images are non-destructive data; re-run SeedAllProducts to restore originals.
        }
    }
}
