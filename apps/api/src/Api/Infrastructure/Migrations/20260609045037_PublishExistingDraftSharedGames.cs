using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #2043 Bug 1: data state migration to make the public hub catalog non-empty.
    /// <para>
    /// Until #2514 the <c>SharedGame</c> domain aggregate constructor unconditionally sets
    /// <c>_status = GameStatus.Draft</c>, and no upload/import pipeline (PDF upload, BGG enrichment
    /// via repository) ever transitions to <c>Published</c>. As a result the existing rows in
    /// <c>shared_games</c> sit at <c>status=0</c> (Draft) while <see
    /// cref="Api.BoundedContexts.SharedGameCatalog.Application.Queries.SearchSharedGamesQueryHandler"/>
    /// filters the public catalog to <c>status=2</c> (Published) — causing <c>/hub/games</c> to render
    /// <c>0/0/0</c> KPI even when <c>SELECT COUNT(*) FROM shared_games</c> returns 159.
    /// </para>
    /// <para>
    /// This is the one-shot Draft → Published promotion for catalog rows that existed before the
    /// approval workflow (#2514) gained an admin auto-publish path. Rows in <c>PendingApproval</c>
    /// or <c>Archived</c> are left untouched (legitimate non-public states). Soft-deleted rows are
    /// also left untouched.
    /// </para>
    /// <para>
    /// Down is intentionally a no-op: by the time we'd roll back, real <c>Published</c> rows have
    /// likely been added via the normal workflow, and we cannot distinguish them from rows promoted
    /// here. Reverse manually with a targeted SQL if ever needed.
    /// </para>
    /// </summary>
    public partial class PublishExistingDraftSharedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GameStatus.Draft = 0, GameStatus.Published = 2 (Domain/Entities/GameStatus.cs).
            migrationBuilder.Sql(@"
                UPDATE shared_games
                SET status = 2
                WHERE status = 0
                  AND is_deleted = false;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty. See class summary.
        }
    }
}
