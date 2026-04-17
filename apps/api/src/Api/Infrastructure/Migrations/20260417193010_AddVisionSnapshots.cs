using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisionSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vision_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    extracted_game_state = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vision_snapshots", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "vision_snapshot_images",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    storage_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    media_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    order_index = table.Column<int>(type: "integer", nullable: false),
                    vision_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vision_snapshot_images", x => x.id);
                    table.ForeignKey(
                        name: "FK_vision_snapshot_images_vision_snapshots_vision_snapshot_id",
                        column: x => x.vision_snapshot_id,
                        principalTable: "vision_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_vision_snapshot_images_snapshot_id",
                table: "vision_snapshot_images",
                column: "vision_snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_vision_snapshots_is_deleted",
                table: "vision_snapshots",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_vision_snapshots_session_id",
                table: "vision_snapshots",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_vision_snapshots_session_turn",
                table: "vision_snapshots",
                columns: new[] { "session_id", "turn_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vision_snapshot_images");

            migrationBuilder.DropTable(
                name: "vision_snapshots");
        }
    }
}
