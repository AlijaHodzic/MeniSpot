using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDrinkServingOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServingSize",
                schema: "digital_menu",
                table: "MenuItems",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServingOptions",
                schema: "digital_menu",
                table: "GlobalDrinks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServingSize",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "ServingOptions",
                schema: "digital_menu",
                table: "GlobalDrinks");
        }
    }
}
