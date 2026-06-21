using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOfferKinds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Items",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalPrice",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Items",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "OriginalPrice",
                schema: "digital_menu",
                table: "SpecialOffers");
        }
    }
}
