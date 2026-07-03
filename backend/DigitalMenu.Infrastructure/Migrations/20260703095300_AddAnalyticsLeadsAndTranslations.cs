using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsLeadsAndTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DescriptionDe",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemsDe",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItemsEn",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleDe",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleEn",
                schema: "digital_menu",
                table: "SpecialOffers",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionDe",
                schema: "digital_menu",
                table: "MenuItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                schema: "digital_menu",
                table: "MenuItems",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameDe",
                schema: "digital_menu",
                table: "MenuItems",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                schema: "digital_menu",
                table: "MenuItems",
                type: "character varying(160)",
                maxLength: 160,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionDe",
                schema: "digital_menu",
                table: "MenuCategories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                schema: "digital_menu",
                table: "MenuCategories",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameDe",
                schema: "digital_menu",
                table: "MenuCategories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NameEn",
                schema: "digital_menu",
                table: "MenuCategories",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Leads",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BusinessName = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Email = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    Phone = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Message = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MenuItemViews",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    MenuItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ViewedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Source = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItemViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItemViews_MenuItems_MenuItemId",
                        column: x => x.MenuItemId,
                        principalSchema: "digital_menu",
                        principalTable: "MenuItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MenuItemViews_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Leads_Status_CreatedAt",
                schema: "digital_menu",
                table: "Leads",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemViews_MenuItemId_ViewedOn",
                schema: "digital_menu",
                table: "MenuItemViews",
                columns: new[] { "MenuItemId", "ViewedOn" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItemViews_RestaurantId_ViewedOn",
                schema: "digital_menu",
                table: "MenuItemViews",
                columns: new[] { "RestaurantId", "ViewedOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Leads",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "MenuItemViews",
                schema: "digital_menu");

            migrationBuilder.DropColumn(
                name: "DescriptionDe",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "ItemsDe",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "ItemsEn",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "TitleDe",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "TitleEn",
                schema: "digital_menu",
                table: "SpecialOffers");

            migrationBuilder.DropColumn(
                name: "DescriptionDe",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "NameDe",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "NameEn",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "DescriptionDe",
                schema: "digital_menu",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                schema: "digital_menu",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "NameDe",
                schema: "digital_menu",
                table: "MenuCategories");

            migrationBuilder.DropColumn(
                name: "NameEn",
                schema: "digital_menu",
                table: "MenuCategories");
        }
    }
}
