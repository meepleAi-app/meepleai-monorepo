using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ContextEngineeringSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

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
                    embedding = table.Column<Vector>(type: "vector", nullable: true)
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
                name: "conversation_memory",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    message_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    embedding = table.Column<Vector>(type: "vector", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_memory", x => x.id);
                    table.ForeignKey(
                        name: "FK_conversation_memory_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "strategy_patterns",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pattern_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    applicable_phase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    evaluation_score = table.Column<double>(type: "double precision", nullable: true),
                    board_conditions_json = table.Column<string>(type: "jsonb", nullable: true),
                    move_sequence_json = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    embedding = table.Column<Vector>(type: "vector", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_strategy_patterns", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_agent_session_id",
                table: "agent_game_state_snapshots",
                column: "agent_session_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_created_at",
                table: "agent_game_state_snapshots",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_game_id",
                table: "agent_game_state_snapshots",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_agent_game_state_snapshots_game_id_turn_number",
                table: "agent_game_state_snapshots",
                columns: new[] { "game_id", "turn_number" });

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_session_id",
                table: "conversation_memory",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_timestamp",
                table: "conversation_memory",
                column: "timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_user_id",
                table: "conversation_memory",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_memory_user_id_game_id",
                table: "conversation_memory",
                columns: new[] { "user_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "IX_strategy_patterns_evaluation_score",
                table: "strategy_patterns",
                column: "evaluation_score");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_patterns_game_id",
                table: "strategy_patterns",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_strategy_patterns_game_id_applicable_phase",
                table: "strategy_patterns",
                columns: new[] { "game_id", "applicable_phase" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_game_state_snapshots");

            migrationBuilder.DropTable(
                name: "conversation_memory");

            migrationBuilder.DropTable(
                name: "strategy_patterns");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
