using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvitationFlowTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "InvitationExpiresAt",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "InvitedByUserId",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "custom_message",
                table: "invitation_tokens",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "pending_user_id",
                table: "invitation_tokens",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    suggested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_dismissed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_accepted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "FK_game_suggestions_users_suggested_by_user_id",
                        column: x => x.suggested_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_game_suggestions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "invitation_game_suggestions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invitation_token_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invitation_game_suggestions", x => x.id);
                    table.ForeignKey(
                        name: "FK_invitation_game_suggestions_invitation_tokens_invitation_to~",
                        column: x => x.invitation_token_id,
                        principalTable: "invitation_tokens",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvitationTokens_PendingUserId",
                table: "invitation_tokens",
                column: "pending_user_id",
                filter: "pending_user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_game_suggestions_suggested_by_user_id",
                table: "game_suggestions",
                column: "suggested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_GameSuggestions_UserId",
                table: "game_suggestions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_GameSuggestions_UserId_GameId",
                table: "game_suggestions",
                columns: new[] { "user_id", "game_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameSuggestions_UserId_Status",
                table: "game_suggestions",
                columns: new[] { "user_id", "is_dismissed", "is_accepted" });

            migrationBuilder.CreateIndex(
                name: "IX_InvitationGameSuggestions_InvitationTokenId",
                table: "invitation_game_suggestions",
                column: "invitation_token_id");

            migrationBuilder.CreateIndex(
                name: "IX_InvitationGameSuggestions_TokenId_GameId",
                table: "invitation_game_suggestions",
                columns: new[] { "invitation_token_id", "game_id" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_invitation_tokens_users_pending_user_id",
                table: "invitation_tokens",
                column: "pending_user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_invitation_tokens_users_pending_user_id",
                table: "invitation_tokens");

            migrationBuilder.DropTable(
                name: "game_suggestions");

            migrationBuilder.DropTable(
                name: "invitation_game_suggestions");

            migrationBuilder.DropIndex(
                name: "IX_InvitationTokens_PendingUserId",
                table: "invitation_tokens");

            migrationBuilder.DropColumn(
                name: "InvitationExpiresAt",
                table: "users");

            migrationBuilder.DropColumn(
                name: "InvitedByUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "custom_message",
                table: "invitation_tokens");

            migrationBuilder.DropColumn(
                name: "pending_user_id",
                table: "invitation_tokens");
        }
    }
}
