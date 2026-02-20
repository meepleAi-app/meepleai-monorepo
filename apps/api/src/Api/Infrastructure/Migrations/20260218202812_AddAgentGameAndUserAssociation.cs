using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentGameAndUserAssociation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agents_games_GameEntityId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_GameEntityId",
                table: "agents");

            migrationBuilder.RenameColumn(
                name: "GameEntityId",
                table: "agents",
                newName: "GameId");

            // Note: credit budget columns (daily_credits_used, last_daily_reset, last_weekly_reset,
            // weekly_credits_used, daily_credits_limit, weekly_credits_limit) already added by
            // migration 20260218123027_AddCreditBudgetTracking - removed duplicates

            migrationBuilder.AddColumn<Guid>(
                name: "CreatedByUserId",
                table: "agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_CreatedByUserId",
                table: "agents",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_CreatedByUserId",
                table: "agents",
                columns: new[] { "GameId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_Type",
                table: "agents",
                columns: new[] { "GameId", "Type" });

            migrationBuilder.AddForeignKey(
                name: "FK_agents_games_GameId",
                table: "agents",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_agents_users_CreatedByUserId",
                table: "agents",
                column: "CreatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agents_games_GameId",
                table: "agents");

            migrationBuilder.DropForeignKey(
                name: "FK_agents_users_CreatedByUserId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_CreatedByUserId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_GameId_CreatedByUserId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_GameId_Type",
                table: "agents");

            // Note: credit budget columns NOT dropped here - they belong to
            // migration 20260218123027_AddCreditBudgetTracking

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "agents");

            migrationBuilder.RenameColumn(
                name: "GameId",
                table: "agents",
                newName: "GameEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameEntityId",
                table: "agents",
                column: "GameEntityId");

            migrationBuilder.AddForeignKey(
                name: "FK_agents_games_GameEntityId",
                table: "agents",
                column: "GameEntityId",
                principalTable: "games",
                principalColumn: "Id");
        }
    }
}
