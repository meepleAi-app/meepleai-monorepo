using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Avoid constant arrays as arguments (generated code)

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameSessionStateEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_session_states",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_state_json = table.Column<string>(type: "jsonb", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    last_updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_updated_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_session_states", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_session_states_GameSessions_game_session_id",
                        column: x => x.game_session_id,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_state_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_state_id = table.Column<Guid>(type: "uuid", nullable: false),
                    state_json = table.Column<string>(type: "jsonb", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_state_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_state_snapshots_game_session_states_session_state_id",
                        column: x => x.session_state_id,
                        principalTable: "game_session_states",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_game_session_states_game_session_id",
                table: "game_session_states",
                column: "game_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_game_session_states_last_updated_at",
                table: "game_session_states",
                column: "last_updated_at");

            migrationBuilder.CreateIndex(
                name: "ix_game_session_states_template_id",
                table: "game_session_states",
                column: "template_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_snapshots_created_at",
                table: "game_state_snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_snapshots_session_state_id",
                table: "game_state_snapshots",
                column: "session_state_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_state_snapshots_session_state_id_turn_number",
                table: "game_state_snapshots",
                columns: new[] { "session_state_id", "turn_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_state_snapshots");

            migrationBuilder.DropTable(
                name: "game_session_states");
        }
    }
}
