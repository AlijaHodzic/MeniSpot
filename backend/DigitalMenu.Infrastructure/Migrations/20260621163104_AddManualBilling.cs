using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualBilling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MonthlyPrice",
                schema: "digital_menu",
                table: "Subscriptions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: false,
                defaultValue: 39.90m);

            migrationBuilder.CreateTable(
                name: "SubscriptionPayments",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    PaidOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodStartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodEndsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    CoverageMonths = table.Column<int>(type: "integer", nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Reference = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubscriptionPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubscriptionPayments_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubscriptionPayments_RestaurantId_PaidOn",
                schema: "digital_menu",
                table: "SubscriptionPayments",
                columns: new[] { "RestaurantId", "PaidOn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubscriptionPayments",
                schema: "digital_menu");

            migrationBuilder.DropColumn(
                name: "MonthlyPrice",
                schema: "digital_menu",
                table: "Subscriptions");
        }
    }
}
