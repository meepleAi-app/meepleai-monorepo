using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKbUserFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_game_state_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    board_state_json = table.Column<string>(type: "jsonb", nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: false),
                    active_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector(1024)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_game_state_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_game_state_snapshots_agent_sessions_agent_session_id",
                        column: x => x.agent_session_id,
                        principalTable: "agent_sessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "kb_user_feedback",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comment = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_user_feedback", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_agent_session_id",
                table: "agent_game_state_snapshots",
                column: "agent_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_kb_user_feedback_created_at",
                schema: "knowledge_base",
                table: "kb_user_feedback",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_kb_user_feedback_game_id",
                schema: "knowledge_base",
                table: "kb_user_feedback",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_kb_user_feedback_message_id",
                schema: "knowledge_base",
                table: "kb_user_feedback",
                column: "message_id");

            migrationBuilder.CreateIndex(
                name: "IX_kb_user_feedback_user_id",
                schema: "knowledge_base",
                table: "kb_user_feedback",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_game_state_snapshots");

            migrationBuilder.DropTable(
                name: "kb_user_feedback",
                schema: "knowledge_base");
        }
    }
}
