using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.InventoryApi.Data.Migrations
{
    /// <summary>
    /// Seeds inventory and offers for the 20 new products added in the
    /// ExpandCatalogueAndThirdImages product migration (IDs 71-90).
    ///
    /// Categories covered:
    ///   Hoodies &amp; Sweatshirts (71-75) — 40 units each
    ///   Gym Wear               (76-80) — 35 units each
    ///   Swimwear               (81-85) — 30 units each
    ///   Coats &amp; Raincoats    (86-90) — 25 units each
    ///
    /// Offers (10 products spread across all four new categories):
    ///   Fixed Amount — products 73, 76, 82, 86, 89
    ///   Percentage   — products 72, 77, 84, 87, 90
    ///
    /// All offers active from 2026-04-13 through 2026-12-31.
    /// </summary>
    public partial class SeedNewProductsInventory : Migration
    {
        private static readonly DateTimeOffset SeedDate =
            new(2026, 4, 13, 12, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset OfferStart =
            new(2026, 4, 13, 0, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset OfferEnd =
            new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ──────────────────────────────────────────────────────────────────
            // Inventory — 20 new products
            // ──────────────────────────────────────────────────────────────────
            var cols = new[]
            {
                "ProductId", "Sku", "Quantity", "Reserved",
                "LowStockThreshold", "Metadata", "CreatedAt", "UpdatedAt"
            };

            migrationBuilder.InsertData("Inventories", cols, new object[,]
            {
                // ── Hoodies & Sweatshirts (71-75) — 40 units, threshold 8 ──
                { 71, "HM-HOOD-M-071",   40, 0, 8, null, SeedDate, SeedDate },
                { 72, "NK-HOOD-M-072",   40, 0, 8, null, SeedDate, SeedDate },
                { 73, "ZRA-SWEAT-W-073", 40, 0, 8, null, SeedDate, SeedDate },
                { 74, "HM-HOOD-W-074",   40, 0, 8, null, SeedDate, SeedDate },
                { 75, "ZRA-SWEAT-U-075", 40, 0, 8, null, SeedDate, SeedDate },

                // ── Gym Wear (76-80) — 35 units, threshold 8 ─────────────
                { 76, "NK-GYM-W-076",  35, 0, 8, null, SeedDate, SeedDate },
                { 77, "NK-GYM-M-077",  35, 0, 8, null, SeedDate, SeedDate },
                { 78, "NK-GYM-W-078",  35, 0, 8, null, SeedDate, SeedDate },
                { 79, "NK-GYM-M-079",  35, 0, 8, null, SeedDate, SeedDate },
                { 80, "HM-GYM-W-080",  35, 0, 8, null, SeedDate, SeedDate },

                // ── Swimwear (81-85) — 30 units, threshold 6 ─────────────
                { 81, "ZRA-SWIM-W-081", 30, 0, 6, null, SeedDate, SeedDate },
                { 82, "ZRA-SWIM-W-082", 30, 0, 6, null, SeedDate, SeedDate },
                { 83, "ZRA-SWIM-W-083", 30, 0, 6, null, SeedDate, SeedDate },
                { 84, "HM-SWIM-M-084",  30, 0, 6, null, SeedDate, SeedDate },
                { 85, "NK-SWIM-M-085",  30, 0, 6, null, SeedDate, SeedDate },

                // ── Coats & Raincoats (86-90) — 25 units, threshold 5 ────
                { 86, "ZRA-COAT-W-086", 25, 0, 5, null, SeedDate, SeedDate },
                { 87, "ZRA-COAT-W-087", 25, 0, 5, null, SeedDate, SeedDate },
                { 88, "HM-COAT-M-088",  25, 0, 5, null, SeedDate, SeedDate },
                { 89, "HM-COAT-W-089",  25, 0, 5, null, SeedDate, SeedDate },
                { 90, "RL-COAT-W-090",  20, 0, 4, null, SeedDate, SeedDate },
            });

            // ──────────────────────────────────────────────────────────────────
            // Offers — 10 products
            //
            // Fixed Amount (£):
            //   73  Oversized Graphic Sweatshirt (Women)    — £8  off
            //   76  High-Waist Compression Leggings         — £10 off
            //   82  High-Waist Bikini Bottom                — £6  off
            //   86  Classic Belted Trench Coat              — £30 off
            //   89  Waterproof Parka Coat                   — £20 off
            //
            // Percentage (%):
            //   72  Zip-Up Tech Hoodie (Men)                — 15% off
            //   77  Performance Training Shorts (Men)       — 20% off
            //   84  Classic Swim Shorts (Men)               — 25% off
            //   87  Oversized Trench Coat (Women)           — 10% off
            //   90  Wool Blend Overcoat (Women)             — 12% off
            // ──────────────────────────────────────────────────────────────────
            var offerCols = new[]
            {
                "Name", "Description", "ProductId", "DiscountType", "DiscountValue",
                "StartsAt", "EndsAt", "IsActive", "Rules", "CreatedAt", "UpdatedAt"
            };

            migrationBuilder.InsertData("Offers", offerCols, new object[,]
            {
                // ── Fixed Amount ──────────────────────────────────────────────
                {
                    "£8 Off Women's Sweatshirt",
                    "Save £8 on the Zara Oversized Graphic Sweatshirt — limited time.",
                    73, "FixedAmount", 8.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£10 Off Compression Leggings",
                    "Upgrade your training kit — £10 off the Nike High-Waist Compression Leggings.",
                    76, "FixedAmount", 10.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£6 Off Bikini Bottoms",
                    "Mix and match for less — £6 off the Zara High-Waist Bikini Bottom.",
                    82, "FixedAmount", 6.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£30 Off Classic Trench Coat",
                    "Invest in a timeless trench — £30 off the Zara Classic Belted Trench Coat.",
                    86, "FixedAmount", 30.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£20 Off Waterproof Parka",
                    "Stay dry this season for less — £20 off the H&M Waterproof Parka Coat.",
                    89, "FixedAmount", 20.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },

                // ── Percentage ────────────────────────────────────────────────
                {
                    "15% Off Tech Hoodie",
                    "Train and commute in style — 15% off the Nike Zip-Up Tech Hoodie.",
                    72, "Percentage", 15.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "20% Off Training Shorts",
                    "Push harder for less — 20% off the Nike Performance Training Shorts.",
                    77, "Percentage", 20.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "25% Off Swim Shorts",
                    "Make a splash for less — 25% off the H&M Classic Swim Shorts.",
                    84, "Percentage", 25.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "10% Off Oversized Trench",
                    "Layer up in style — 10% off the Zara Oversized Trench Coat.",
                    87, "Percentage", 10.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "12% Off Wool Blend Overcoat",
                    "Luxury for less — 12% off the Ralph Lauren Wool Blend Overcoat.",
                    90, "Percentage", 12.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("Offers", "ProductId",
                new object[] { 73, 76, 82, 86, 89, 72, 77, 84, 87, 90 });

            for (int id = 71; id <= 90; id++)
                migrationBuilder.DeleteData("Inventories", "ProductId", id);
        }
    }
}
