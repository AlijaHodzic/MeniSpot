using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuCategoryType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Type",
                schema: "digital_menu",
                table: "MenuCategories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE digital_menu."MenuCategories"
                SET "Type" = 1
                WHERE "Id" IN (
                    SELECT DISTINCT "CategoryId"
                    FROM digital_menu."MenuItems"
                    WHERE "GlobalDrinkId" IS NOT NULL
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Type",
                schema: "digital_menu",
                table: "MenuCategories");
        }
    }
}
