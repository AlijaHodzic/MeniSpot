using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddThemeEntitlements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdditionalThemeKeys",
                schema: "digital_menu",
                table: "ThemeSettings",
                type: "character varying(800)",
                maxLength: 800,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE digital_menu."ThemeSettings" AS theme
                SET "AdditionalThemeKeys" = theme."ThemeKey"
                FROM digital_menu."Subscriptions" AS subscription
                WHERE subscription."RestaurantId" = theme."RestaurantId"
                  AND subscription."Plan" IN ('Pro', 'Standard')
                  AND theme."ThemeKey" NOT IN ('classic-light', 'classic-dark');

                UPDATE digital_menu."ThemeSettings" AS theme
                SET "ThemeKey" = 'classic-light',
                    "PrimaryColor" = '#ffffff',
                    "AccentColor" = '#84cc16'
                FROM digital_menu."Subscriptions" AS subscription
                WHERE subscription."RestaurantId" = theme."RestaurantId"
                  AND subscription."Plan" IN ('Start', 'Basic')
                  AND theme."ThemeKey" NOT IN ('classic-light', 'classic-dark');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdditionalThemeKeys",
                schema: "digital_menu",
                table: "ThemeSettings");
        }
    }
}
