using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameCatalogPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========================================================================
            // ISSUE #2374 Phase 5: Performance Optimization - Database Indexes
            // Target: P95 search latency < 200ms (10x improvement over BGG ~2000ms)
            // ========================================================================

            // 1. GIN Full-Text Search Index (CRITICAL - Missing from Phase 1-4)
            // Precomputes tsvector for Title + Description to avoid runtime calculation
            // Expected impact: 80-90% search query time reduction
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_fts
                ON shared_games
                USING GIN (to_tsvector('italian', title || ' ' || COALESCE(description, '')))
                WHERE is_deleted = false;
            ");

            // 2. Composite Index for Sorting by Year (Status + YearPublished + Title)
            // Covers default sorting: Published games ordered by year
            // Expected impact: Eliminates sort operation in query plan
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_status_year_title
                ON shared_games (status, year_published DESC, title)
                WHERE is_deleted = false;
            ");

            // 3. Composite Index for Sorting by Rating (Status + AverageRating + Title)
            // Covers rating-based sorting for top-rated games discovery
            // Expected impact: Eliminates sort operation for rating queries
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_status_rating_title
                ON shared_games (status, average_rating DESC NULLS LAST, title)
                WHERE is_deleted = false;
            ");

            // 4. Composite Index for Player Count Filters (MinPlayers + MaxPlayers)
            // Covers WHERE max_players >= X AND min_players <= Y filters
            // Expected impact: Efficient range scans for player count searches
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_players
                ON shared_games (min_players, max_players)
                WHERE is_deleted = false AND status = 2;  -- Published games only
            ");

            // 5. Index for Playing Time Filter (PlayingTimeMinutes)
            // Covers WHERE playing_time_minutes <= X filters
            // Expected impact: Fast filtering for quick games discovery
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_playtime
                ON shared_games (playing_time_minutes)
                WHERE is_deleted = false AND status = 2;  -- Published games only
            ");

            // 6. Many-to-Many Junction Table Indexes (Categories)
            // Covers g.Categories.Any(c => categoryIds.Contains(c.Id)) LINQ queries
            // Expected impact: Eliminates N+1 queries for category filters
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_shared_game_categories_shared_game_id
                ON shared_game_categories (shared_game_id);

                CREATE INDEX IF NOT EXISTS ix_shared_game_categories_game_category_id
                ON shared_game_categories (game_category_id);
            ");

            // 7. Many-to-Many Junction Table Indexes (Mechanics)
            // Covers g.Mechanics.Any(m => mechanicIds.Contains(m.Id)) LINQ queries
            // Expected impact: Eliminates N+1 queries for mechanic filters
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ix_shared_game_mechanics_shared_game_id
                ON shared_game_mechanics (shared_game_id);

                CREATE INDEX IF NOT EXISTS ix_shared_game_mechanics_game_mechanic_id
                ON shared_game_mechanics (game_mechanic_id);
            ");

            // 8. Covering Index for GetById with Cache Misses
            // Covers frequently accessed columns in single index lookup
            // Expected impact: Reduces IO for cache miss scenarios
            migrationBuilder.Sql(@"
                CREATE INDEX ix_shared_games_getbyid_covering
                ON shared_games (id)
                INCLUDE (title, year_published, description, status, image_url, thumbnail_url)
                WHERE is_deleted = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Drop all indexes in reverse order
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_getbyid_covering;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_game_mechanics_game_mechanic_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_game_mechanics_shared_game_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_game_categories_game_category_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_game_categories_shared_game_id;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_playtime;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_players;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_status_rating_title;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_status_year_title;");
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_shared_games_fts;");
        }
    }
}
