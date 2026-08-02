using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameCoverAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "manual_cover_attested_at",
                table: "shared_games",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "manual_cover_attested_by",
                table: "shared_games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manual_cover_attribution",
                table: "shared_games",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manual_cover_license",
                table: "shared_games",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manual_cover_r2_key",
                table: "shared_games",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "manual_cover_source_url",
                table: "shared_games",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_cover_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    context = table.Column<short>(type: "smallint", nullable: false),
                    source = table.Column<short>(type: "smallint", nullable: false),
                    focal_x = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.5),
                    focal_y = table.Column<double>(type: "double precision", nullable: false, defaultValue: 0.5),
                    generated_r2_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_cover_assignments", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_cover_assignments_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_game_cover_assignments_game_context",
                table: "game_cover_assignments",
                columns: new[] { "shared_game_id", "context" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_cover_assignments");

            migrationBuilder.DropColumn(
                name: "manual_cover_attested_at",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "manual_cover_attested_by",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "manual_cover_attribution",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "manual_cover_license",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "manual_cover_r2_key",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "manual_cover_source_url",
                table: "shared_games");
        }
    }
}
