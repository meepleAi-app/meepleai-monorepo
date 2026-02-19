using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveGameSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "live_game_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    game_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    group_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    current_turn_index = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    paused_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_saved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    scoring_config_json = table.Column<string>(type: "jsonb", nullable: false),
                    game_state_json = table.Column<string>(type: "jsonb", nullable: true),
                    turn_order_json = table.Column<string>(type: "jsonb", nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    agent_mode = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    chat_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_game_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_game_sessions_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_live_game_sessions_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_teams",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    team_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_teams", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_teams_live_game_sessions_live_game_session_id",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_players",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    team_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_score = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    current_rank = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_players", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_players_live_game_sessions_live_game_session_id",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_players_session_teams_team_id",
                        column: x => x.team_id,
                        principalTable: "session_teams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_session_players_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "live_session_round_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round = table.Column<int>(type: "integer", nullable: false),
                    dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    value = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_round_scores", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_round_scores_live_game_sessions_live_game_sess~",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_live_session_round_scores_session_players_player_id",
                        column: x => x.player_id,
                        principalTable: "session_players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "live_session_turn_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    live_game_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    turn_index = table.Column<int>(type: "integer", nullable: false),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phase_index = table.Column<int>(type: "integer", nullable: true),
                    phase_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_live_session_turn_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_live_session_turn_records_live_game_sessions_live_game_sess~",
                        column: x => x.live_game_session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_live_session_turn_records_session_players_player_id",
                        column: x => x.player_id,
                        principalTable: "session_players",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_created_at",
                table: "live_game_sessions",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_created_by_user_id",
                table: "live_game_sessions",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_game_status",
                table: "live_game_sessions",
                columns: new[] { "game_id", "status" },
                filter: "game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_session_code",
                table: "live_game_sessions",
                column: "session_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_status",
                table: "live_game_sessions",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_live_game_sessions_user_status",
                table: "live_game_sessions",
                columns: new[] { "created_by_user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_live_round_scores_session_id",
                table: "live_session_round_scores",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_round_scores_session_player_round_dim",
                table: "live_session_round_scores",
                columns: new[] { "live_game_session_id", "player_id", "round", "dimension" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_live_session_round_scores_player_id",
                table: "live_session_round_scores",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "IX_live_session_turn_records_player_id",
                table: "live_session_turn_records",
                column: "player_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_turn_records_session_id",
                table: "live_session_turn_records",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_live_turn_records_session_turn",
                table: "live_session_turn_records",
                columns: new[] { "live_game_session_id", "turn_index" });

            migrationBuilder.CreateIndex(
                name: "ix_session_players_session_id",
                table: "session_players",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_players_session_user",
                table: "session_players",
                columns: new[] { "live_game_session_id", "user_id" },
                unique: true,
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_session_players_team_id",
                table: "session_players",
                column: "team_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_players_user_id",
                table: "session_players",
                column: "user_id",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_session_teams_session_id",
                table: "session_teams",
                column: "live_game_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_teams_session_name",
                table: "session_teams",
                columns: new[] { "live_game_session_id", "name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "live_session_round_scores");

            migrationBuilder.DropTable(
                name: "live_session_turn_records");

            migrationBuilder.DropTable(
                name: "session_players");

            migrationBuilder.DropTable(
                name: "session_teams");

            migrationBuilder.DropTable(
                name: "live_game_sessions");
        }
    }
}
