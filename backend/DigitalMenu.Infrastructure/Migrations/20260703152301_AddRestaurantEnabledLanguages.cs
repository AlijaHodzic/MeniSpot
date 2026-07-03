using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantEnabledLanguages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DefaultLanguage",
                schema: "digital_menu",
                table: "Restaurants",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "EnabledLanguages",
                schema: "digital_menu",
                table: "Restaurants",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "bs,en");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnabledLanguages",
                schema: "digital_menu",
                table: "Restaurants");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultLanguage",
                schema: "digital_menu",
                table: "Restaurants",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);
        }
    }
}
