using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionToGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "agent_definition_id",
                table: "shared_games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "agent_definition_id",
                table: "private_games",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_shared_games_agent_definition_id",
                table: "shared_games",
                column: "agent_definition_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_private_games_agent_definition_id",
                table: "private_games",
                column: "agent_definition_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_private_games_agent_definitions_agent_definition_id",
                table: "private_games",
                column: "agent_definition_id",
                principalSchema: "knowledge_base",
                principalTable: "agent_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_shared_games_agent_definitions_agent_definition_id",
                table: "shared_games",
                column: "agent_definition_id",
                principalSchema: "knowledge_base",
                principalTable: "agent_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_private_games_agent_definitions_agent_definition_id",
                table: "private_games");

            migrationBuilder.DropForeignKey(
                name: "FK_shared_games_agent_definitions_agent_definition_id",
                table: "shared_games");

            migrationBuilder.DropIndex(
                name: "IX_shared_games_agent_definition_id",
                table: "shared_games");

            migrationBuilder.DropIndex(
                name: "IX_private_games_agent_definition_id",
                table: "private_games");

            migrationBuilder.DropColumn(
                name: "agent_definition_id",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "agent_definition_id",
                table: "private_games");
        }
    }
}
