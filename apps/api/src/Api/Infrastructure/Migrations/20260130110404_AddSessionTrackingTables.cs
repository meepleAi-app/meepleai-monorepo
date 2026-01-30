using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTrackingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "session_tracking_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    session_code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    session_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    session_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    location = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    finalized_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_sessions_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_session_tracking_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_owner = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    join_order = table.Column<int>(type: "integer", nullable: false),
                    final_rank = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SessionId1 = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_participants_session_tracking_sessions_Ses~",
                        column: x => x.SessionId1,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_session_tracking_participants_session_tracking_sessions_ses~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_card_draws",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deck_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    deck_id = table.Column<Guid>(type: "uuid", nullable: true),
                    card_value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_card_draws", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_card_draws_session_tracking_participants_p~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_card_draws_session_tracking_sessions_sessi~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_dice_rolls",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dice_type = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    roll_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    results = table.Column<string>(type: "jsonb", nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_dice_rolls", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_dice_rolls_session_tracking_participants_p~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_dice_rolls_session_tracking_sessions_sessi~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_notes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    template_key = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    content = table.Column<string>(type: "text", nullable: false),
                    is_hidden = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_notes", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_notes_session_tracking_participants_partic~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_notes_session_tracking_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_score_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: true),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    score_value = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    timestamp = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_score_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_score_entries_session_tracking_participant~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_score_entries_session_tracking_sessions_se~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_cards_session",
                table: "session_tracking_card_draws",
                columns: new[] { "session_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_card_draws_participant_id",
                table: "session_tracking_card_draws",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "idx_dice_session",
                table: "session_tracking_dice_rolls",
                columns: new[] { "session_id", "timestamp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_dice_rolls_participant_id",
                table: "session_tracking_dice_rolls",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "idx_notes_session_participant",
                table: "session_tracking_notes",
                columns: new[] { "session_id", "participant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_notes_participant_id",
                table: "session_tracking_notes",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "idx_participants_session",
                table: "session_tracking_participants",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_participants_SessionId1",
                table: "session_tracking_participants",
                column: "SessionId1");

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_participants_user_id",
                table: "session_tracking_participants",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "idx_scores_round",
                table: "session_tracking_score_entries",
                columns: new[] { "session_id", "round_number" });

            migrationBuilder.CreateIndex(
                name: "idx_scores_session_participant",
                table: "session_tracking_score_entries",
                columns: new[] { "session_id", "participant_id" });

            migrationBuilder.CreateIndex(
                name: "IX_session_tracking_score_entries_participant_id",
                table: "session_tracking_score_entries",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_code",
                table: "session_tracking_sessions",
                column: "session_code",
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_game_date",
                table: "session_tracking_sessions",
                columns: new[] { "game_id", "session_date" },
                descending: new[] { false, true },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "idx_sessions_user_status",
                table: "session_tracking_sessions",
                columns: new[] { "user_id", "status" },
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_tracking_card_draws");

            migrationBuilder.DropTable(
                name: "session_tracking_dice_rolls");

            migrationBuilder.DropTable(
                name: "session_tracking_notes");

            migrationBuilder.DropTable(
                name: "session_tracking_score_entries");

            migrationBuilder.DropTable(
                name: "session_tracking_participants");

            migrationBuilder.DropTable(
                name: "session_tracking_sessions");
        }
    }
}
