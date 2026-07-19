using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RestoreGameNightSessionInProgressUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #3157 C1 — reconcile pre-existing violations of the "max 1 InProgress
            // link per GameNight" invariant BEFORE the unique index is created, otherwise
            // CREATE UNIQUE INDEX would fail on any environment carrying duplicate/orphaned
            // InProgress rows (the invariant was only app-enforced since the #2875 flatten
            // silently dropped the original partial unique index, and FinalizeSessionCommand
            // left finalized sessions' links stuck at InProgress).

            // (a) Orphaned live slots: link is InProgress but its session is finalized → close it.
            migrationBuilder.Sql(@"
                UPDATE game_night_sessions gns
                SET status = 'Completed', completed_at = COALESCE(gns.completed_at, NOW())
                FROM session_tracking_sessions s
                WHERE gns.status = 'InProgress'
                  AND s.id = gns.session_id
                  AND s.finalized_at IS NOT NULL;");

            // (b) Any remaining duplicates per night: keep the most-recently-started link
            //     InProgress and demote the rest to Pending (the same demotion PauseSession uses).
            migrationBuilder.Sql(@"
                WITH ranked AS (
                    SELECT id, ROW_NUMBER() OVER (
                        PARTITION BY game_night_event_id
                        ORDER BY started_at DESC NULLS LAST, id) AS rn
                    FROM game_night_sessions
                    WHERE status = 'InProgress')
                UPDATE game_night_sessions
                SET status = 'Pending'
                WHERE id IN (SELECT id FROM ranked WHERE rn > 1);");

            migrationBuilder.CreateIndex(
                name: "ix_game_night_sessions_unique_active",
                table: "game_night_sessions",
                column: "game_night_event_id",
                unique: true,
                filter: "status = 'InProgress'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_game_night_sessions_unique_active",
                table: "game_night_sessions");
        }
    }
}
