using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionTurnOrderAndGameNightInProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "current_turn_index",
                table: "session_tracking_sessions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "turn_order_json",
                table: "session_tracking_sessions",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "turn_order_method",
                table: "session_tracking_sessions",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "turn_order_seed",
                table: "session_tracking_sessions",
                type: "integer",
                nullable: true);

            // Session Flow v2.1: enforce "1 Active Session per GameNight" invariant.
            // Uses a partial unique index on game_night_sessions filtered by status='InProgress'.
            // (Avoids cross-table subquery partial index which PostgreSQL does not support.)
            migrationBuilder.Sql(@"
CREATE UNIQUE INDEX IF NOT EXISTS ""ix_game_night_sessions_unique_active""
ON ""game_night_sessions"" (""game_night_event_id"")
WHERE ""status"" = 'InProgress';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS \"ix_game_night_sessions_unique_active\";");

            migrationBuilder.DropColumn(
                name: "current_turn_index",
                table: "session_tracking_sessions");

            migrationBuilder.DropColumn(
                name: "turn_order_json",
                table: "session_tracking_sessions");

            migrationBuilder.DropColumn(
                name: "turn_order_method",
                table: "session_tracking_sessions");

            migrationBuilder.DropColumn(
                name: "turn_order_seed",
                table: "session_tracking_sessions");
        }
    }
}
