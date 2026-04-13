using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECOM.InventoryApi.Data.Migrations
{
    /// <inheritdoc />
    public partial class CartUserIdIntFix : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the unique index, clear test data, alter uuid→integer, recreate index.
            migrationBuilder.DropIndex(name: "IX_Carts_UserId", table: "Carts");
            migrationBuilder.Sql(@"DELETE FROM ""Carts"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Carts"" ALTER COLUMN ""UserId"" TYPE integer USING 0;");
            migrationBuilder.CreateIndex(name: "IX_Carts_UserId", table: "Carts", column: "UserId", unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "Carts",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
