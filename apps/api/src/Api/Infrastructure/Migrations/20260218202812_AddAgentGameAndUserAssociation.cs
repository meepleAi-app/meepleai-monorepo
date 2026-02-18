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

            migrationBuilder.AddColumn<decimal>(
                name: "daily_credits_used",
                table: "user_token_usage",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_daily_reset",
                table: "user_token_usage",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "last_weekly_reset",
                table: "user_token_usage",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<decimal>(
                name: "weekly_credits_used",
                table: "user_token_usage",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "daily_credits_limit",
                table: "token_tiers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "weekly_credits_limit",
                table: "token_tiers",
                type: "numeric(18,2)",
                nullable: false,
                defaultValue: 0m);

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

            migrationBuilder.DropColumn(
                name: "daily_credits_used",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "last_daily_reset",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "last_weekly_reset",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "weekly_credits_used",
                table: "user_token_usage");

            migrationBuilder.DropColumn(
                name: "daily_credits_limit",
                table: "token_tiers");

            migrationBuilder.DropColumn(
                name: "weekly_credits_limit",
                table: "token_tiers");

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
