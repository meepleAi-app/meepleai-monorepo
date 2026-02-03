using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "chat_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_library_entry_id = table.Column<Guid>(type: "uuid", nullable: true),
                    agent_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    agent_config_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_message_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    messages_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_sessions", x => x.id);
                    table.ForeignKey(
                        name: "FK_chat_sessions_games_game_id",
                        column: x => x.game_id,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_agent_session_id",
                table: "chat_sessions",
                column: "agent_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_game_id",
                table: "chat_sessions",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_last_message_at",
                table: "chat_sessions",
                column: "last_message_at");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_game",
                table: "chat_sessions",
                columns: new[] { "user_id", "game_id" });

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_id",
                table: "chat_sessions",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chat_sessions");
        }
    }
}
