using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropGamesTable_Issue1345 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_games_GameId",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_chat_sessions_games_game_id",
                table: "chat_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_chats_games_GameId",
                table: "chats");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatThreads_games_GameId",
                table: "ChatThreads");

            migrationBuilder.DropForeignKey(
                name: "FK_chunked_upload_sessions_games_GameId",
                table: "chunked_upload_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_game_phase_templates_games_game_id",
                table: "game_phase_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_GameEntityRelations_games_GameId",
                table: "GameEntityRelations");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSessions_games_GameId",
                table: "GameSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GameToolkits_games_GameId",
                table: "GameToolkits");

            migrationBuilder.DropForeignKey(
                name: "FK_live_game_sessions_games_game_id",
                table: "live_game_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_play_records_games_GameId",
                table: "play_records");

            migrationBuilder.DropForeignKey(
                name: "FK_RaptorSummaries_games_GameId",
                table: "RaptorSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_rule_conflict_faqs_games_GameId",
                table: "rule_conflict_faqs");

            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_games_GameEntityId",
                table: "rule_specs");

            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_games_GameId",
                table: "rule_specs");

            migrationBuilder.DropForeignKey(
                name: "FK_rulespec_comments_games_GameId",
                table: "rulespec_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_session_tracking_sessions_games_game_id",
                table: "session_tracking_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_text_chunks_games_GameId",
                table: "text_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_games_GameId",
                table: "vector_documents");

            migrationBuilder.DropTable(
                name: "games");

            migrationBuilder.DropIndex(
                name: "IX_rule_specs_GameEntityId",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "GameEntityId",
                table: "rule_specs");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_shared_games_GameId",
                table: "agent_sessions",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_sessions_shared_games_game_id",
                table: "chat_sessions",
                column: "game_id",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chats_shared_games_GameId",
                table: "chats",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatThreads_shared_games_GameId",
                table: "ChatThreads",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_chunked_upload_sessions_shared_games_GameId",
                table: "chunked_upload_sessions",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_game_phase_templates_shared_games_game_id",
                table: "game_phase_templates",
                column: "game_id",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameEntityRelations_shared_games_GameId",
                table: "GameEntityRelations",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSessions_shared_games_GameId",
                table: "GameSessions",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameToolkits_shared_games_GameId",
                table: "GameToolkits",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_live_game_sessions_shared_games_game_id",
                table: "live_game_sessions",
                column: "game_id",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_play_records_shared_games_GameId",
                table: "play_records",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RaptorSummaries_shared_games_GameId",
                table: "RaptorSummaries",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rule_conflict_faqs_shared_games_GameId",
                table: "rule_conflict_faqs",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_shared_games_GameId",
                table: "rule_specs",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rulespec_comments_shared_games_GameId",
                table: "rulespec_comments",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_session_tracking_sessions_shared_games_game_id",
                table: "session_tracking_sessions",
                column: "game_id",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_text_chunks_shared_games_GameId",
                table: "text_chunks",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_shared_games_GameId",
                table: "vector_documents",
                column: "GameId",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agent_sessions_shared_games_GameId",
                table: "agent_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_chat_sessions_shared_games_game_id",
                table: "chat_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_chats_shared_games_GameId",
                table: "chats");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatThreads_shared_games_GameId",
                table: "ChatThreads");

            migrationBuilder.DropForeignKey(
                name: "FK_chunked_upload_sessions_shared_games_GameId",
                table: "chunked_upload_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_game_phase_templates_shared_games_game_id",
                table: "game_phase_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_GameEntityRelations_shared_games_GameId",
                table: "GameEntityRelations");

            migrationBuilder.DropForeignKey(
                name: "FK_GameSessions_shared_games_GameId",
                table: "GameSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_GameToolkits_shared_games_GameId",
                table: "GameToolkits");

            migrationBuilder.DropForeignKey(
                name: "FK_live_game_sessions_shared_games_game_id",
                table: "live_game_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_play_records_shared_games_GameId",
                table: "play_records");

            migrationBuilder.DropForeignKey(
                name: "FK_RaptorSummaries_shared_games_GameId",
                table: "RaptorSummaries");

            migrationBuilder.DropForeignKey(
                name: "FK_rule_conflict_faqs_shared_games_GameId",
                table: "rule_conflict_faqs");

            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_shared_games_GameId",
                table: "rule_specs");

            migrationBuilder.DropForeignKey(
                name: "FK_rulespec_comments_shared_games_GameId",
                table: "rulespec_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_session_tracking_sessions_shared_games_game_id",
                table: "session_tracking_sessions");

            migrationBuilder.DropForeignKey(
                name: "FK_text_chunks_shared_games_GameId",
                table: "text_chunks");

            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_shared_games_GameId",
                table: "vector_documents");

            migrationBuilder.AddColumn<Guid>(
                name: "GameEntityId",
                table: "rule_specs",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "games",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: true),
                    approval_status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    BggId = table.Column<int>(type: "integer", nullable: true),
                    BggMetadata = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    icon_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    image_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, computedColumnSql: "(approval_status = 2 AND published_at IS NOT NULL)", stored: true),
                    Language = table.Column<string>(type: "text", nullable: true),
                    MaxPlayTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    MaxPlayers = table.Column<int>(type: "integer", nullable: true),
                    MinPlayTimeMinutes = table.Column<int>(type: "integer", nullable: true),
                    MinPlayers = table.Column<int>(type: "integer", nullable: true),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Publisher = table.Column<string>(type: "text", nullable: true),
                    VersionNumber = table.Column<string>(type: "text", nullable: true),
                    VersionType = table.Column<string>(type: "text", nullable: true),
                    YearPublished = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_games", x => x.Id);
                    table.ForeignKey(
                        name: "FK_games_shared_games_SharedGameId",
                        column: x => x.SharedGameId,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameEntityId",
                table: "rule_specs",
                column: "GameEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_games_Name",
                table: "games",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Games_SharedGameId",
                table: "games",
                column: "SharedGameId");

            migrationBuilder.AddForeignKey(
                name: "FK_agent_sessions_games_GameId",
                table: "agent_sessions",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_chat_sessions_games_game_id",
                table: "chat_sessions",
                column: "game_id",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_chats_games_GameId",
                table: "chats",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatThreads_games_GameId",
                table: "ChatThreads",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_chunked_upload_sessions_games_GameId",
                table: "chunked_upload_sessions",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_game_phase_templates_games_game_id",
                table: "game_phase_templates",
                column: "game_id",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameEntityRelations_games_GameId",
                table: "GameEntityRelations",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameSessions_games_GameId",
                table: "GameSessions",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_GameToolkits_games_GameId",
                table: "GameToolkits",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_live_game_sessions_games_game_id",
                table: "live_game_sessions",
                column: "game_id",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_play_records_games_GameId",
                table: "play_records",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_RaptorSummaries_games_GameId",
                table: "RaptorSummaries",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rule_conflict_faqs_games_GameId",
                table: "rule_conflict_faqs",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_games_GameEntityId",
                table: "rule_specs",
                column: "GameEntityId",
                principalTable: "games",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_games_GameId",
                table: "rule_specs",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_rulespec_comments_games_GameId",
                table: "rulespec_comments",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_session_tracking_sessions_games_game_id",
                table: "session_tracking_sessions",
                column: "game_id",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_text_chunks_games_GameId",
                table: "text_chunks",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_games_GameId",
                table: "vector_documents",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
