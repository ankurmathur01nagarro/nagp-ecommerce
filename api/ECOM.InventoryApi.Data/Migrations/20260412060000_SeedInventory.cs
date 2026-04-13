using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.InventoryApi.Data.Migrations
{
    /// <summary>
    /// Seeds inventory and offers for all 70 products from the ProductApi seed.
    ///
    /// Inventory: all 70 products get Quantity=25, Reserved=0, LowStockThreshold=5.
    ///
    /// Offers (10 products, spread across categories):
    ///   Fixed Amount ($) — product IDs  5, 14, 33, 48, 62
    ///   Percentage    (%) — product IDs  7, 19, 28, 55, 68
    ///
    /// All offers are active from 2026-04-12 through 2026-12-31.
    /// </summary>
    public partial class SeedInventory : Migration
    {
        private static readonly DateTimeOffset SeedDate =
            new(2026, 4, 12, 6, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset OfferStart =
            new(2026, 4, 12, 0, 0, 0, TimeSpan.Zero);

        private static readonly DateTimeOffset OfferEnd =
            new(2026, 12, 31, 23, 59, 59, TimeSpan.Zero);

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------------
            // Inventory — 70 products, default stock 25
            // ---------------------------------------------------------------
            var cols = new[]
            {
                "ProductId", "Sku", "Quantity", "Reserved",
                "LowStockThreshold", "Metadata", "CreatedAt", "UpdatedAt"
            };

            migrationBuilder.InsertData("Inventories", cols, new object[,]
            {
                {  1, "ZRA-SHIRT-M-001", 25, 0, 5, null, SeedDate, SeedDate },
                {  2, "HM-SHIRT-M-002",  25, 0, 5, null, SeedDate, SeedDate },
                {  3, "RL-SHIRT-M-003",  25, 0, 5, null, SeedDate, SeedDate },
                {  4, "ZRA-SHIRT-W-004", 25, 0, 5, null, SeedDate, SeedDate },
                {  5, "HM-SHIRT-W-005",  25, 0, 5, null, SeedDate, SeedDate },
                {  6, "RL-JUMP-M-006",   25, 0, 5, null, SeedDate, SeedDate },
                {  7, "HM-JUMP-M-007",   25, 0, 5, null, SeedDate, SeedDate },
                {  8, "ZRA-JUMP-W-008",  25, 0, 5, null, SeedDate, SeedDate },
                {  9, "RL-JUMP-W-009",   25, 0, 5, null, SeedDate, SeedDate },
                { 10, "HM-JUMP-W-010",   25, 0, 5, null, SeedDate, SeedDate },
                { 11, "LVI-501-M-011",   25, 0, 5, null, SeedDate, SeedDate },
                { 12, "ZRA-JEAN-M-012",  25, 0, 5, null, SeedDate, SeedDate },
                { 13, "HM-JEAN-M-013",   25, 0, 5, null, SeedDate, SeedDate },
                { 14, "ZRA-JEAN-W-014",  25, 0, 5, null, SeedDate, SeedDate },
                { 15, "HM-JEAN-W-015",   25, 0, 5, null, SeedDate, SeedDate },
                { 16, "ZRA-TROU-M-016",  25, 0, 5, null, SeedDate, SeedDate },
                { 17, "RL-TROU-M-017",   25, 0, 5, null, SeedDate, SeedDate },
                { 18, "HM-TROU-M-018",   25, 0, 5, null, SeedDate, SeedDate },
                { 19, "ZRA-TROU-W-019",  25, 0, 5, null, SeedDate, SeedDate },
                { 20, "HM-TROU-W-020",   25, 0, 5, null, SeedDate, SeedDate },
                { 21, "ZRA-SHORT-M-021", 25, 0, 5, null, SeedDate, SeedDate },
                { 22, "HM-SHORT-M-022",  25, 0, 5, null, SeedDate, SeedDate },
                { 23, "NK-SHORT-M-023",  25, 0, 5, null, SeedDate, SeedDate },
                { 24, "ZRA-SHORT-W-024", 25, 0, 5, null, SeedDate, SeedDate },
                { 25, "HM-SHORT-W-025",  25, 0, 5, null, SeedDate, SeedDate },
                { 26, "HM-AUTJ-M-026",   25, 0, 5, null, SeedDate, SeedDate },
                { 27, "ZRA-AUTJ-M-027",  25, 0, 5, null, SeedDate, SeedDate },
                { 28, "RL-AUTJ-M-028",   25, 0, 5, null, SeedDate, SeedDate },
                { 29, "ZRA-AUTJ-W-029",  25, 0, 5, null, SeedDate, SeedDate },
                { 30, "HM-AUTJ-W-030",   25, 0, 5, null, SeedDate, SeedDate },
                { 31, "HM-WINJ-M-031",   25, 0, 5, null, SeedDate, SeedDate },
                { 32, "ZRA-WINJ-M-032",  25, 0, 5, null, SeedDate, SeedDate },
                { 33, "HM-WINJ-M-033",   25, 0, 5, null, SeedDate, SeedDate },
                { 34, "ZRA-WINJ-W-034",  25, 0, 5, null, SeedDate, SeedDate },
                { 35, "RL-WINJ-W-035",   25, 0, 5, null, SeedDate, SeedDate },
                { 36, "ZRA-LEATH-M-036", 25, 0, 5, null, SeedDate, SeedDate },
                { 37, "HM-LEATH-M-037",  25, 0, 5, null, SeedDate, SeedDate },
                { 38, "RL-LEATH-M-038",  25, 0, 5, null, SeedDate, SeedDate },
                { 39, "ZRA-LEATH-W-039", 25, 0, 5, null, SeedDate, SeedDate },
                { 40, "HM-LEATH-W-040",  25, 0, 5, null, SeedDate, SeedDate },
                { 41, "ZRA-DRESS-W-041", 25, 0, 5, null, SeedDate, SeedDate },
                { 42, "HM-DRESS-W-042",  25, 0, 5, null, SeedDate, SeedDate },
                { 43, "ZRA-DRESS-W-043", 25, 0, 5, null, SeedDate, SeedDate },
                { 44, "HM-DRESS-W-044",  25, 0, 5, null, SeedDate, SeedDate },
                { 45, "ZRA-DRESS-W-045", 25, 0, 5, null, SeedDate, SeedDate },
                { 46, "RL-SUIT-M-046",   25, 0, 5, null, SeedDate, SeedDate },
                { 47, "ZRA-SUIT-M-047",  25, 0, 5, null, SeedDate, SeedDate },
                { 48, "HM-SUIT-M-048",   25, 0, 5, null, SeedDate, SeedDate },
                { 49, "ZRA-SUIT-W-049",  25, 0, 5, null, SeedDate, SeedDate },
                { 50, "RL-SUIT-W-050",   25, 0, 5, null, SeedDate, SeedDate },
                { 51, "NK-SHOE-M-051",   25, 0, 5, null, SeedDate, SeedDate },
                { 52, "RL-SHOE-M-052",   25, 0, 5, null, SeedDate, SeedDate },
                { 53, "NK-SHOE-M-053",   25, 0, 5, null, SeedDate, SeedDate },
                { 54, "ZRA-SHOE-W-054",  25, 0, 5, null, SeedDate, SeedDate },
                { 55, "ZRA-SHOE-W-055",  25, 0, 5, null, SeedDate, SeedDate },
                { 56, "ZRA-BAG-W-056",   25, 0, 5, null, SeedDate, SeedDate },
                { 57, "ZRA-BAG-W-057",   25, 0, 5, null, SeedDate, SeedDate },
                { 58, "HM-BAG-W-058",    25, 0, 5, null, SeedDate, SeedDate },
                { 59, "HM-BAG-M-059",    25, 0, 5, null, SeedDate, SeedDate },
                { 60, "ZRA-BAG-W-060",   25, 0, 5, null, SeedDate, SeedDate },
                { 61, "HM-UND-M-061",    25, 0, 5, null, SeedDate, SeedDate },
                { 62, "RL-UND-M-062",    25, 0, 5, null, SeedDate, SeedDate },
                { 63, "HM-UND-W-063",    25, 0, 5, null, SeedDate, SeedDate },
                { 64, "HM-UND-W-064",    25, 0, 5, null, SeedDate, SeedDate },
                { 65, "ZRA-UND-W-065",   25, 0, 5, null, SeedDate, SeedDate },
                { 66, "ZRA-GLASS-M-066", 25, 0, 5, null, SeedDate, SeedDate },
                { 67, "HM-GLASS-M-067",  25, 0, 5, null, SeedDate, SeedDate },
                { 68, "ZRA-GLASS-W-068", 25, 0, 5, null, SeedDate, SeedDate },
                { 69, "HM-GLASS-W-069",  25, 0, 5, null, SeedDate, SeedDate },
                { 70, "ZRA-GLASS-W-070", 25, 0, 5, null, SeedDate, SeedDate },
            });

            // ---------------------------------------------------------------
            // Offers
            // Fixed Amount (DiscountType = "FixedAmount"):
            //   Product  5 (HM-SHIRT-W-005)  — £10 off
            //   Product 14 (ZRA-JEAN-W-014)  — £15 off
            //   Product 33 (HM-WINJ-M-033)   — £20 off
            //   Product 48 (HM-SUIT-M-048)   — £25 off
            //   Product 62 (RL-UND-M-062)    — £5  off
            //
            // Percentage (DiscountType = "Percentage"):
            //   Product  7 (HM-JUMP-M-007)   — 10%
            //   Product 19 (ZRA-TROU-W-019)  — 15%
            //   Product 28 (RL-AUTJ-M-028)   — 20%
            //   Product 55 (ZRA-SHOE-W-055)  — 25%
            //   Product 68 (ZRA-GLASS-W-068) — 30%
            // ---------------------------------------------------------------
            var offerCols = new[]
            {
                "Name", "Description", "ProductId", "DiscountType", "DiscountValue",
                "StartsAt", "EndsAt", "IsActive", "Rules", "CreatedAt", "UpdatedAt"
            };

            migrationBuilder.InsertData("Offers", offerCols, new object[,]
            {
                // ── Fixed Amount ────────────────────────────────────────────────
                {
                    "£10 Off Women's Shirts",
                    "Save £10 on the H&M Women's Cotton Poplin Shirt — no code needed.",
                    5, "FixedAmount", 10.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£15 Off Women's Jeans",
                    "Grab £15 off the Zara Women's Slim Fit Jeans while stock lasts.",
                    14, "FixedAmount", 15.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£20 Off Men's Winter Jackets",
                    "Stay warm for less — £20 off the H&M Men's Puffer Jacket.",
                    33, "FixedAmount", 20.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£25 Off Men's Suits",
                    "Look sharp for less with £25 off the H&M Men's Slim Fit Suit.",
                    48, "FixedAmount", 25.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "£5 Off Men's Basics",
                    "Freshen up your essentials — £5 off the Ralph Lauren Men's Underwear.",
                    62, "FixedAmount", 5.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },

                // ── Percentage ──────────────────────────────────────────────────
                {
                    "10% Off Men's Knitwear",
                    "10% off the H&M Men's Crew-Neck Jumper — perfect for the season.",
                    7, "Percentage", 10.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "15% Off Women's Trousers",
                    "15% off the Zara Women's Wide-Leg Trousers — a wardrobe must-have.",
                    19, "Percentage", 15.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "20% Off Men's Autumn Jackets",
                    "Transition your wardrobe with 20% off the Ralph Lauren Autumn Jacket.",
                    28, "Percentage", 20.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "25% Off Women's Shoes",
                    "Step into savings — 25% off the Zara Women's Block Heel Mules.",
                    55, "Percentage", 25.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
                {
                    "30% Off Women's Glasses",
                    "See the world in style — 30% off the Zara Women's Round Sunglasses.",
                    68, "Percentage", 30.00m,
                    OfferStart, OfferEnd, true, null, SeedDate, SeedDate
                },
            });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData("Offers", "ProductId",
                new object[] { 5, 14, 33, 48, 62, 7, 19, 28, 55, 68 });

            for (int id = 1; id <= 70; id++)
                migrationBuilder.DeleteData("Inventories", "ProductId", id);
        }
    }
}
