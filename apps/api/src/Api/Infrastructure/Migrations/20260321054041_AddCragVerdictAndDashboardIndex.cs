using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCragVerdictAndDashboardIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games");

            migrationBuilder.AddColumn<string>(
                name: "crag_verdict",
                schema: "knowledge_base",
                table: "rag_executions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games",
                sql: "(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games",
                sql: "playing_time_minutes >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games",
                sql: "year_published = 0 OR (year_published > 1900 AND year_published <= 2100)");

            migrationBuilder.CreateIndex(
                name: "IX_rag_executions_strategy_created_at",
                schema: "knowledge_base",
                table: "rag_executions",
                columns: new[] { "strategy", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games");

            migrationBuilder.DropIndex(
                name: "IX_rag_executions_strategy_created_at",
                schema: "knowledge_base",
                table: "rag_executions");

            migrationBuilder.DropColumn(
                name: "crag_verdict",
                schema: "knowledge_base",
                table: "rag_executions");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games",
                sql: "min_players > 0 AND max_players >= min_players");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games",
                sql: "playing_time_minutes > 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games",
                sql: "year_published > 1900 AND year_published <= 2100");
        }
    }
}
