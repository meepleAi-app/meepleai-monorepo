using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameDeleteRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "shared_game_delete_requests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    review_comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    reviewed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_game_delete_requests", x => x.id);
                    table.ForeignKey(
                        name: "FK_shared_game_delete_requests_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_delete_requests_created_at",
                table: "shared_game_delete_requests",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_delete_requests_shared_game_id",
                table: "shared_game_delete_requests",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_delete_requests_status",
                table: "shared_game_delete_requests",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "shared_game_delete_requests");
        }
    }
}
