using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LiveSessionRowVersionToXmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop the trigger and helper function added by LiveSessionRowVersionTrigger (#2097).
            // xmin is a system column managed by Postgres — no trigger needed.
            migrationBuilder.Sql(@"
                DROP TRIGGER IF EXISTS trg_live_game_sessions_row_version ON live_game_sessions;
            ");

            // Drop the function ONLY if no other table currently uses it. As of this PR no
            // other table does — verify with the comment query below if reapplied later.
            // SELECT proname FROM pg_proc WHERE proname='ef_update_row_version';
            migrationBuilder.Sql(@"
                DROP FUNCTION IF EXISTS ef_update_row_version();
            ");

            // Drop the legacy bytea column. EF's xmin/xid mapping handles concurrency via
            // the Postgres system column directly — no schema change needed for that side.
            migrationBuilder.DropColumn(
                name: "row_version",
                table: "live_game_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Restore the legacy bytea column + trigger if rolling back.
            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "live_game_sessions",
                type: "bytea",
                nullable: false,
                defaultValue: new byte[0]);

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
    }
}
