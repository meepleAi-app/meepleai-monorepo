using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTurnOrderTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions");

            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions");

            migrationBuilder.AddColumn<Guid>(
                name: "shared_game_id",
                table: "vector_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_ready",
                table: "session_tracking_participants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "session_tracking_participants",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Player");

            migrationBuilder.AddColumn<string>(
                name: "kb_card_ids",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "game_reviews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Rating = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_reviews", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "game_strategies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SharedGameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Upvotes = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    Tags = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_strategies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_chat_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sender_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    message_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    turn_number = table.Column<int>(type: "integer", nullable: true),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    agent_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    confidence = table.Column<float>(type: "real", nullable: true),
                    citations_json = table.Column<string>(type: "text", nullable: true),
                    mentions_json = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_chat_messages", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_chat_messages_session_tracking_participant~",
                        column: x => x.sender_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_session_tracking_chat_messages_session_tracking_sessions_se~",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_tracking_media",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    participant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    file_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    content_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    media_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    thumbnail_file_id = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    turn_number = table.Column<int>(type: "integer", nullable: true),
                    is_shared_with_session = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_tracking_media", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_tracking_media_session_tracking_participants_partic~",
                        column: x => x.participant_id,
                        principalTable: "session_tracking_participants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_tracking_media_session_tracking_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "turn_orders",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    player_order_json = table.Column<string>(type: "jsonb", nullable: false),
                    current_index = table.Column<int>(type: "integer", nullable: false),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_turn_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_shared_game_id",
                table: "vector_documents",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions",
                column: "agent_id",
                filter: "agent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions",
                columns: new[] { "user_id", "agent_id" },
                filter: "agent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_SharedGameId",
                table: "game_reviews",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameReviews_SharedGameId_UserId",
                table: "game_reviews",
                columns: new[] { "SharedGameId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameStrategies_SharedGameId",
                table: "game_strategies",
                column: "SharedGameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameStrategies_SharedGameId_Upvotes",
                table: "game_strategies",
                columns: new[] { "SharedGameId", "Upvotes" });

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_chat_messages_sender_id",
                table: "session_tracking_chat_messages",
                column: "sender_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_chat_messages_session_id",
                table: "session_tracking_chat_messages",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_chat_messages_session_id_sequence_number",
                table: "session_tracking_chat_messages",
                columns: new[] { "session_id", "sequence_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_participant_id",
                table: "session_tracking_media",
                column: "participant_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_session_id",
                table: "session_tracking_media",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_session_id_created_at",
                table: "session_tracking_media",
                columns: new[] { "session_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_session_tracking_media_snapshot_id",
                table: "session_tracking_media",
                column: "snapshot_id");

            migrationBuilder.CreateIndex(
                name: "ix_turn_orders_session_id",
                table: "turn_orders",
                column: "session_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_reviews");

            migrationBuilder.DropTable(
                name: "game_strategies");

            migrationBuilder.DropTable(
                name: "session_tracking_chat_messages");

            migrationBuilder.DropTable(
                name: "session_tracking_media");

            migrationBuilder.DropTable(
                name: "turn_orders");

            migrationBuilder.DropIndex(
                name: "IX_vector_documents_shared_game_id",
                table: "vector_documents");

            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions");

            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "shared_game_id",
                table: "vector_documents");

            migrationBuilder.DropColumn(
                name: "is_ready",
                table: "session_tracking_participants");

            migrationBuilder.DropColumn(
                name: "role",
                table: "session_tracking_participants");

            migrationBuilder.DropColumn(
                name: "kb_card_ids",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions",
                column: "agent_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions",
                columns: new[] { "user_id", "agent_id" });
        }
    }
}
