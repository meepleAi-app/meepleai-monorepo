using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLastProcessedEventIdToMemoryTables_CF3 : Migration
    {
        // PR comment: questa migration è stata regenerata e poi RIPULITA manualmente per rimuovere
        // le aggiunte `test_run_id` rilevate come drift di asse-d task B (#1928), shippato in
        // main-dev SENZA migration corrispondente. Le colonne TestRunId sono incluse nella CF-2
        // migration di PR #1946 (committed first); concorrente merge garantisce single application.
        // Vedi PR comment per il tracker del drift.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_processed_event_id",
                table: "player_memories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_processed_event_id",
                table: "group_memories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_processed_event_id",
                table: "game_memories",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_processed_event_id",
                table: "player_memories");

            migrationBuilder.DropColumn(
                name: "last_processed_event_id",
                table: "group_memories");

            migrationBuilder.DropColumn(
                name: "last_processed_event_id",
                table: "game_memories");
        }
    }
}
