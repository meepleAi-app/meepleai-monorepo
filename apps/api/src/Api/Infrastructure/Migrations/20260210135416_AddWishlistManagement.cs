using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWishlistManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "wishlist_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    TargetPrice = table.Column<decimal>(type: "numeric(10,2)", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wishlist_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_wishlist_items_shared_games_GameId",
                        column: x => x.GameId,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_wishlist_items_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_GameId",
                table: "wishlist_items",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId",
                table: "wishlist_items",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId_GameId",
                table: "wishlist_items",
                columns: new[] { "UserId", "GameId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WishlistItems_UserId_Priority",
                table: "wishlist_items",
                columns: new[] { "UserId", "Priority" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "wishlist_items");
        }
    }
}
