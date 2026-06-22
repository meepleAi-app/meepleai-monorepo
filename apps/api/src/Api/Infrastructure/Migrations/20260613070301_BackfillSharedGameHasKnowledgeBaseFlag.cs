using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #2244 (epic #2242 Sub #2): backfill <c>shared_games.has_knowledge_base</c>
    /// for rows that already have at least one vector-indexed PDF.
    ///
    /// Why this is needed
    /// ------------------
    /// Until <see cref="Api.BoundedContexts.KnowledgeBase.Application.Services.IPdfIndexingPipeline"/>
    /// was introduced, the five ingestion call sites in <c>DocumentProcessing</c>
    /// wrote <c>VectorDocumentEntity</c> rows directly via EF, bypassing the
    /// <c>VectorDocument</c> domain aggregate whose constructor raises
    /// <c>VectorDocumentIndexedEvent</c>. The downstream
    /// <c>VectorDocumentIndexedForKbFlagHandler</c> never ran, so even
    /// successfully indexed PDFs left <c>shared_games.has_knowledge_base</c>
    /// stuck on <c>false</c>.
    ///
    /// The pipeline closes the bug for NEW ingestion. This migration heals
    /// the EXISTING stale rows so the Sub #6 SLO metric
    /// <c>meepleai_pdf_indexed_no_kb_flag_total</c> can drift towards zero
    /// on dev/staging without waiting for a re-index.
    ///
    /// Strategy
    /// --------
    /// Idempotent UPDATE: flip <c>has_knowledge_base = true</c> for every
    /// <c>shared_games</c> row that has at least one
    /// <c>pdf_documents</c> row with <c>processing_state = 'Ready'</c> and
    /// <c>is_active_for_rag = true</c>. Rows already flipped are skipped by
    /// the predicate <c>has_knowledge_base = false</c>, so re-running this
    /// migration is a no-op.
    ///
    /// Down() flips back the rows it touched, but only those — the
    /// <c>touched_at</c> sentinel column does not exist, so we can't be
    /// surgical. Acceptable trade-off: if you really need a clean rollback,
    /// snapshot <c>has_knowledge_base</c> before running and restore from
    /// the snapshot.
    /// </summary>
    public partial class BackfillSharedGameHasKnowledgeBaseFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE shared_games sg
                SET has_knowledge_base = true
                WHERE has_knowledge_base = false
                  AND EXISTS (
                      SELECT 1
                      FROM pdf_documents pd
                      WHERE pd.shared_game_id = sg.id
                        AND pd.processing_state = 'Ready'
                        AND pd.is_active_for_rag = true
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: revert the rows this Up() touched. Same predicate, opposite flag.
            // Caveat: this also reverts rows that were independently true at the time
            // of Up() — there is no marker column to distinguish. See class docs.
            migrationBuilder.Sql(@"
                UPDATE shared_games sg
                SET has_knowledge_base = false
                WHERE has_knowledge_base = true
                  AND EXISTS (
                      SELECT 1
                      FROM pdf_documents pd
                      WHERE pd.shared_game_id = sg.id
                        AND pd.processing_state = 'Ready'
                        AND pd.is_active_for_rag = true
                  );
            ");
        }
    }
}
