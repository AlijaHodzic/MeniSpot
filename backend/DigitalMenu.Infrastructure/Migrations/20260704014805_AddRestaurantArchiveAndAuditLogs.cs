using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRestaurantArchiveAndAuditLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                schema: "digital_menu",
                table: "Restaurants",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                schema: "digital_menu",
                table: "Restaurants",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ActorEmail = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: true),
                    ActorRole = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Action = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: true),
                    Summary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    IpAddress = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_ArchivedAt",
                schema: "digital_menu",
                table: "Restaurants",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_Status",
                schema: "digital_menu",
                table: "Restaurants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_EntityType_EntityId_CreatedAt",
                schema: "digital_menu",
                table: "AuditLogs",
                columns: new[] { "EntityType", "EntityId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_RestaurantId_CreatedAt",
                schema: "digital_menu",
                table: "AuditLogs",
                columns: new[] { "RestaurantId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "digital_menu");

            migrationBuilder.DropIndex(
                name: "IX_Restaurants_ArchivedAt",
                schema: "digital_menu",
                table: "Restaurants");

            migrationBuilder.DropIndex(
                name: "IX_Restaurants_Status",
                schema: "digital_menu",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                schema: "digital_menu",
                table: "Restaurants");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                schema: "digital_menu",
                table: "Restaurants");
        }
    }
}
