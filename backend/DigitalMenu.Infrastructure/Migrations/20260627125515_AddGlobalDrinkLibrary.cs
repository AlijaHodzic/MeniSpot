using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGlobalDrinkLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GlobalDrinkId",
                schema: "digital_menu",
                table: "MenuItems",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "GlobalDrinks",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GlobalDrinks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_GlobalDrinkId",
                schema: "digital_menu",
                table: "MenuItems",
                column: "GlobalDrinkId");

            migrationBuilder.CreateIndex(
                name: "IX_GlobalDrinks_Category_SortOrder",
                schema: "digital_menu",
                table: "GlobalDrinks",
                columns: new[] { "Category", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_GlobalDrinks_Slug",
                schema: "digital_menu",
                table: "GlobalDrinks",
                column: "Slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_MenuItems_GlobalDrinks_GlobalDrinkId",
                schema: "digital_menu",
                table: "MenuItems",
                column: "GlobalDrinkId",
                principalSchema: "digital_menu",
                principalTable: "GlobalDrinks",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MenuItems_GlobalDrinks_GlobalDrinkId",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropTable(
                name: "GlobalDrinks",
                schema: "digital_menu");

            migrationBuilder.DropIndex(
                name: "IX_MenuItems_GlobalDrinkId",
                schema: "digital_menu",
                table: "MenuItems");

            migrationBuilder.DropColumn(
                name: "GlobalDrinkId",
                schema: "digital_menu",
                table: "MenuItems");
        }
    }
}
