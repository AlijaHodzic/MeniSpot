using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantQrDownloadTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "QrDownloadedAt",
                schema: "digital_menu",
                table: "Restaurants",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QrDownloadedAt",
                schema: "digital_menu",
                table: "Restaurants");
        }
    }
}
