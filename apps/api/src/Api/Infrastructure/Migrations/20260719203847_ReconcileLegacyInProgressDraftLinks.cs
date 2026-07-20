using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Epic #3188 Slice 5 — idempotent data reconciliation for legacy split-brain live slots.
    ///
    /// <para>After the born-status flip (Slice 3), the DB may still hold <c>game_night_sessions</c>
    /// links that were born <c>InProgress</c> by the OLD direct-create path whose tracking
    /// <c>session_tracking_sessions</c> row was NEVER opened live (<c>started_at IS NULL</c>) — a
    /// split-brain vs the canonical liveness rule (<c>Session.IsLive = started_at != null AND
    /// finalized_at == null</c>, decision D4). Such stale <c>InProgress</c> links permanently occupy
    /// the night's single live slot (the partial-unique index <c>ix_game_night_sessions_unique_active</c>
    /// filters <c>status = 'InProgress'</c>) and would block new go-lives.</para>
    ///
    /// <para>This migration reconciles every <c>InProgress</c> link against its tracking session,
    /// mirroring the raw-SQL demote-duplicate pattern established by
    /// <see cref="RestoreGameNightSessionInProgressUniqueIndex"/>. There is NO schema change; the
    /// auto-generated model snapshot is model-identical. Every statement is idempotent — re-running
    /// on already-reconciled data is a no-op (each UPDATE re-filters on <c>status = 'InProgress'</c>,
    /// which the prior run cleared for the rows it touched).</para>
    ///
    /// <para>The <c>status</c> column stores the <c>GameNightSessionStatus</c> enum as a STRING
    /// (<c>'Pending'</c> | <c>'InProgress'</c> | <c>'Completed'</c> | ...), and both
    /// <c>session_tracking_sessions.finalized_at</c> and <c>game_night_sessions.completed_at</c> are
    /// <c>timestamp with time zone</c>, so the <c>completed_at</c> assignment needs no cast.</para>
    /// </summary>
    public partial class ReconcileLegacyInProgressDraftLinks : Migration
    {
        /// <summary>
        /// Rule 1 — tracking session finalized (<c>finalized_at IS NOT NULL</c>): CLOSE the link.
        /// It must not stay <c>InProgress</c>. Preserves an already-set <c>completed_at</c>, otherwise
        /// stamps it with the session's <c>finalized_at</c>. Idempotent: the row is no longer
        /// <c>InProgress</c> after the first run.
        /// </summary>
        public const string CloseFinalizedInProgressLinksSql = @"
            UPDATE game_night_sessions gns
            SET status = 'Completed',
                completed_at = COALESCE(gns.completed_at, s.finalized_at)
            FROM session_tracking_sessions s
            WHERE gns.status = 'InProgress'
              AND s.id = gns.session_id
              AND s.finalized_at IS NOT NULL;";

        /// <summary>
        /// Rule 3 — tracking session never went live (<c>started_at IS NULL AND finalized_at IS NULL</c>):
        /// DEMOTE the mislabeled draft to <c>Pending</c> and clear the link's <c>started_at</c>. This
        /// frees the single live slot and realigns with D4. Rule 2 (genuinely live: <c>started_at IS
        /// NOT NULL AND finalized_at IS NULL</c>) is intentionally left untouched by BOTH this and the
        /// finalized rule, so the real live session keeps its slot. Idempotent for the same reason.
        /// </summary>
        public const string DemoteNeverLiveInProgressLinksSql = @"
            UPDATE game_night_sessions gns
            SET status = 'Pending',
                started_at = NULL
            FROM session_tracking_sessions s
            WHERE gns.status = 'InProgress'
              AND s.id = gns.session_id
              AND s.started_at IS NULL
              AND s.finalized_at IS NULL;";

        /// <summary>
        /// Rule 4 — defensive max-1-live. Should never fire given the partial-unique index, but if any
        /// night still carries more than one <c>InProgress</c> link after rules 1 &amp; 3, keep the
        /// most-recently-started one (<c>ORDER BY started_at DESC NULLS LAST, id</c> — mirrors
        /// <see cref="RestoreGameNightSessionInProgressUniqueIndex"/>) and demote the rest to
        /// <c>Pending</c>. Idempotent: after the first run each night has at most one <c>InProgress</c>
        /// link, so the <c>rn &gt; 1</c> set is empty on re-run.
        /// </summary>
        public const string DedupeSurvivingInProgressLinksSql = @"
            WITH ranked AS (
                SELECT id, ROW_NUMBER() OVER (
                    PARTITION BY game_night_event_id
                    ORDER BY started_at DESC NULLS LAST, id) AS rn
                FROM game_night_sessions
                WHERE status = 'InProgress')
            UPDATE game_night_sessions
            SET status = 'Pending',
                started_at = NULL
            WHERE id IN (SELECT id FROM ranked WHERE rn > 1);";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(CloseFinalizedInProgressLinksSql);
            migrationBuilder.Sql(DemoteNeverLiveInProgressLinksSql);
            migrationBuilder.Sql(DedupeSurvivingInProgressLinksSql);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentional no-op. This is a one-way data reconciliation, not a schema change: the
            // original split-brain state (draft links mislabeled as InProgress, links stuck InProgress
            // for finalized sessions) was a BUG, and the pre-migration values are not retained, so
            // there is nothing to faithfully restore. Reverting would re-introduce the corrupt live
            // slots. We deliberately do not throw — an empty Down keeps `ef migrations remove` / a
            // clean rollback of a LATER migration from failing on this one.
        }
    }
}
