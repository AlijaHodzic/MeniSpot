using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DigitalMenu.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "digital_menu");

            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "text", nullable: false),
                    UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    SecurityStamp = table.Column<string>(type: "text", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                    PhoneNumber = table.Column<string>(type: "text", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Restaurants",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    LogoUrl = table.Column<string>(type: "text", nullable: true),
                    CoverImageUrl = table.Column<string>(type: "text", nullable: true),
                    Address = table.Column<string>(type: "text", nullable: true),
                    Phone = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    WebsiteUrl = table.Column<string>(type: "text", nullable: true),
                    InstagramUrl = table.Column<string>(type: "text", nullable: true),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Restaurants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "digital_menu",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClaimType = table.Column<string>(type: "text", nullable: true),
                    ClaimValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "digital_menu",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                schema: "digital_menu",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    ProviderKey = table.Column<string>(type: "text", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "digital_menu",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                schema: "digital_menu",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    RoleId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalSchema: "digital_menu",
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "digital_menu",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                schema: "digital_menu",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LoginProvider = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalSchema: "digital_menu",
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BusinessHours",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    DayOfWeek = table.Column<int>(type: "integer", nullable: false),
                    OpensAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    ClosesAt = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    IsClosed = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessHours", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BusinessHours_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuCategories",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuCategories_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SpecialOffers",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SpecialOffers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SpecialOffers_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Subscriptions",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Plan = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartsOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    GracePeriodEndsOn = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subscriptions_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThemeSettings",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    ThemeKey = table.Column<string>(type: "text", nullable: false),
                    PrimaryColor = table.Column<string>(type: "text", nullable: false),
                    AccentColor = table.Column<string>(type: "text", nullable: false),
                    BackgroundImageUrl = table.Column<string>(type: "text", nullable: true),
                    FontFamily = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThemeSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThemeSettings_Restaurants_RestaurantId",
                        column: x => x.RestaurantId,
                        principalSchema: "digital_menu",
                        principalTable: "Restaurants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MenuItems",
                schema: "digital_menu",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RestaurantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Allergens = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    IsVisible = table.Column<bool>(type: "boolean", nullable: false),
                    IsAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsVegetarian = table.Column<bool>(type: "boolean", nullable: false),
                    IsSpicy = table.Column<bool>(type: "boolean", nullable: false),
                    IsFeatured = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuItems_MenuCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalSchema: "digital_menu",
                        principalTable: "MenuCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                schema: "digital_menu",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                schema: "digital_menu",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                schema: "digital_menu",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                schema: "digital_menu",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                schema: "digital_menu",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                schema: "digital_menu",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_RestaurantId",
                schema: "digital_menu",
                table: "AspNetUsers",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                schema: "digital_menu",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessHours_RestaurantId_DayOfWeek",
                schema: "digital_menu",
                table: "BusinessHours",
                columns: new[] { "RestaurantId", "DayOfWeek" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MenuCategories_RestaurantId_SortOrder",
                schema: "digital_menu",
                table: "MenuCategories",
                columns: new[] { "RestaurantId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_CategoryId",
                schema: "digital_menu",
                table: "MenuItems",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_MenuItems_RestaurantId_CategoryId_SortOrder",
                schema: "digital_menu",
                table: "MenuItems",
                columns: new[] { "RestaurantId", "CategoryId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Restaurants_Slug",
                schema: "digital_menu",
                table: "Restaurants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SpecialOffers_RestaurantId",
                schema: "digital_menu",
                table: "SpecialOffers",
                column: "RestaurantId");

            migrationBuilder.CreateIndex(
                name: "IX_Subscriptions_RestaurantId",
                schema: "digital_menu",
                table: "Subscriptions",
                column: "RestaurantId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThemeSettings_RestaurantId",
                schema: "digital_menu",
                table: "ThemeSettings",
                column: "RestaurantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "BusinessHours",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "MenuItems",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "SpecialOffers",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "Subscriptions",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "ThemeSettings",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "AspNetRoles",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "AspNetUsers",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "MenuCategories",
                schema: "digital_menu");

            migrationBuilder.DropTable(
                name: "Restaurants",
                schema: "digital_menu");
        }
    }
}
