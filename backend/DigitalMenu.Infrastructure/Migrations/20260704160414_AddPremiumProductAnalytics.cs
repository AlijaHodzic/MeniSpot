using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPremiumProductAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SessionId",
                schema: "digital_menu",
                table: "MenuItemViews",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Calories",
                schema: "digital_menu",
                table: "MenuItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Carbs",
                schema: "digital_menu",
                table: "MenuItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Fat",
                schema: "digital_menu",
                table: "MenuItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Ingredients",
                schema: "digital_menu",
                table: "MenuItems",
                type: "character varying(1200)",
                maxLength: 1200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Protein",
                schema: "digital_menu",
                table: "MenuItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Salt",
                schema: "digital_menu",
                table: "MenuItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Sugar",
                schema: "digital_menu",
                table: "MenuItems",
                type: "numeric(8,2)",
                precision: 8,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemViews_RestaurantId_MenuItemId_ViewedOn_SessionId",
                schema: "digital_menu",
                table: "MenuItemViews",
                columns: new[] { "RestaurantId", "MenuItemId", "ViewedOn", "SessionId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MenuItemViews_RestaurantId_MenuItemId_ViewedOn_SessionId",
                schema: "digital_menu",
                table: "MenuItemViews");

            migrationBuilder.DropColumn(
                name: "SessionId",
                schema: "digital_menu",
                table: "MenuItemViews");

            migrationBuilder.DropColumn(
                name: "Calories",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Carbs",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Fat",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Ingredients",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Protein",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Salt",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "Sugar",
                schema: "digital_menu",
                table: "MenuItems");
        }
    }
}
