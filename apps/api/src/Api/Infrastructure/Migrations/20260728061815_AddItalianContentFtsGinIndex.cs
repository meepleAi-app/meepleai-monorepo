using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddItalianContentFtsGinIndex : Migration
    {
        // Issue #3336: text_chunks.search_vector is a GENERATED english-only tsvector column
        // (a generated column cannot key off a per-row regconfig). KeywordSearchService resolves the
        // per-game FTS config (#3242) and matches non-english chunks against a query-time
        // to_tsvector(cfg::regconfig, "Content"), which has no supporting index — so every Italian
        // keyword query does a per-row to_tsvector over the game's chunks instead of a GIN lookup.
        //
        // This adds a GIN expression index matching that exact expression for italian, the only
        // non-english language with corpus volume (staging: it=5711 chunks vs de=1 doc). Verified on
        // staging that the planner uses it for BOTH the literal and the parameterized (@cfg::regconfig)
        // form the code emits — no C# change needed. EXPLAIN cost 1063 -> 47 (~23x) on the TM query.
        // The index builds from existing rows, so no re-index is required.
        //
        // Raw SQL (not model-tracked): an expression GIN index is invisible to the EF model, so a
        // future `ef migrations add Initial` flatten will DROP it (see #2875 / the
        // RestoreSearchVectorColumns precedent). IF NOT EXISTS keeps it idempotent across environments
        // where it was applied manually (staging migrations are applied by hand).

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_text_chunks_content_tsv_italian
                    ON public.text_chunks
                    USING gin (to_tsvector('italian'::regconfig, ""Content""));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS public.ix_text_chunks_content_tsv_italian;");
        }
    }
}
