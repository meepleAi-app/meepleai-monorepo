using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LiveSessionRowVersionTrigger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #2097 / ADR-060: The live_game_sessions.row_version column was created as
            // bytea NOT NULL by the InitialCreate migration but without a trigger to auto-populate it.
            // The in-memory ConcurrentDictionary implementation never executed an EF INSERT so this
            // gap was invisible. Now that LiveSessionRepository is EF-backed, every INSERT/UPDATE
            // must have row_version filled by a trigger for optimistic concurrency to work.
            //
            // This migration creates a shared trigger function (idempotent) and attaches it to
            // live_game_sessions. The same function can be reused by other tables if needed.
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION ef_update_row_version()
                RETURNS trigger AS $$
                BEGIN
                    NEW.row_version := clock_timestamp()::text::bytea;
                    RETURN NEW;
                END;
                $$ LANGUAGE plpgsql;
            ");

            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_live_game_sessions_row_version ON live_game_sessions;
                CREATE TRIGGER trg_live_game_sessions_row_version
                BEFORE INSERT OR UPDATE ON live_game_sessions
                FOR EACH ROW EXECUTE FUNCTION ef_update_row_version();
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_live_game_sessions_row_version ON live_game_sessions;
            ");
            // Note: ef_update_row_version function is kept (may be used by other tables).
        }
    }
}
