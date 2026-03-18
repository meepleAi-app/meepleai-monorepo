using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Creates the GIN expression index ix_shared_games_fts for PostgreSQL Full-Text Search.
    /// This index was referenced in SharedGameEntityConfiguration (line 140) but never created.
    /// The expression matches the queries in SharedGameCatalogHealthCheck and SearchSharedGamesQueryHandler:
    ///   to_tsvector('italian', title || ' ' || description)
    /// Without this index, FTS queries do a sequential scan computing tsvector per row (~267ms for 10K games).
    /// With the GIN index, the same query uses Bitmap Index Scan (~5-20ms).
    /// </summary>
    public partial class AddSharedGamesFtsIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // GIN expression index for FTS on shared_games.
            // Expression MUST match exactly what EF.Functions.ToTsVector("italian", g.Title + " " + g.Description)
            // generates: to_tsvector('italian'::regconfig, title || ' ' || description)
            // Description is NOT NULL (string with default ''), so no COALESCE needed.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_shared_games_fts
                ON shared_games
                USING GIN (to_tsvector('italian', title || ' ' || description));
                """);

            // Update table statistics so the query planner uses the new index immediately
            migrationBuilder.Sql("ANALYZE shared_games;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_fts;");
        }
    }
}
