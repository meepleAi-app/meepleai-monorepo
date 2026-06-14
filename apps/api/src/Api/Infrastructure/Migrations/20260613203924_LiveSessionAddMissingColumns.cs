using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LiveSessionAddMissingColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_phase_index",
                table: "live_game_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_snapshot_timestamp",
                table: "live_game_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phase_names_json",
                table: "live_game_sessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "snapshot_trigger_config_json",
                table: "live_game_sessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "turn_advance_policy",
                table: "live_game_sessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "current_phase_index",
                table: "live_game_sessions");

            migrationBuilder.DropColumn(
                name: "last_snapshot_timestamp",
                table: "live_game_sessions");

            migrationBuilder.DropColumn(
                name: "phase_names_json",
                table: "live_game_sessions");

            migrationBuilder.DropColumn(
                name: "snapshot_trigger_config_json",
                table: "live_game_sessions");

            migrationBuilder.DropColumn(
                name: "turn_advance_policy",
                table: "live_game_sessions");
        }
    }
}
