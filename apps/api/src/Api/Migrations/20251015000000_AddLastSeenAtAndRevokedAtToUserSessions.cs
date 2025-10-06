using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLastSeenAtAndRevokedAtToUserSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Use SQL to drop indexes only if they exist
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_rule_specs_GameId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_rule_atoms_RuleSpecId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_pdf_documents_GameId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_chats_GameId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_chat_logs_ChatId\";");
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"IX_agents_GameId\";");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "user_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "user_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GameEntityId",
                table: "rule_specs",
                type: "character varying(64)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameEntityId",
                table: "rule_specs",
                column: "GameEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_rule_specs_games_GameEntityId",
                table: "rule_specs",
                column: "GameEntityId",
                principalTable: "games",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rule_specs_games_GameEntityId",
                table: "rule_specs");

            migrationBuilder.DropIndex(
                name: "IX_rule_specs_GameEntityId",
                table: "rule_specs");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "GameEntityId",
                table: "rule_specs");

            migrationBuilder.CreateIndex(
                name: "IX_rule_specs_GameId",
                table: "rule_specs",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_rule_atoms_RuleSpecId",
                table: "rule_atoms",
                column: "RuleSpecId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_GameId",
                table: "pdf_documents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_chats_GameId",
                table: "chats",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_logs_ChatId",
                table: "chat_logs",
                column: "ChatId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId",
                table: "agents",
                column: "GameId");
        }
    }
}
