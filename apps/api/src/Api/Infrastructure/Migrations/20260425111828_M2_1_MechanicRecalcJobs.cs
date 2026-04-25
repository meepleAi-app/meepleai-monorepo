using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// Structural DDL for the async mechanic recalc pipeline (ADR-051 M2.1, Sprint 2, Task 5).
    ///
    /// Creates <c>mechanic_recalc_jobs</c> — one row per background recalc run triggered by
    /// an admin. The status column uses integer codes: 0=Pending, 1=Running, 2=Completed,
    /// 3=Failed, 4=Cancelled. A partial index on (status, created_at) WHERE status IN (0,1)
    /// supports the FOR-UPDATE-SKIP-LOCKED worker claim query added in Task 7.
    ///
    /// The domain aggregate (<c>MechanicRecalcJob</c>), EF entity, DbSet registration, and
    /// repository are introduced in Tasks 6–7. This migration is forward-only via raw SQL.
    /// Down() drops the table.
    /// </remarks>
    public partial class M2_1_MechanicRecalcJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
    CREATE TABLE mechanic_recalc_jobs (
        id uuid PRIMARY KEY,
        status integer NOT NULL DEFAULT 0,                    -- 0=Pending,1=Running,2=Completed,3=Failed,4=Cancelled
        triggered_by_user_id uuid NOT NULL REFERENCES users(""Id"") ON DELETE RESTRICT,
        total integer NOT NULL DEFAULT 0,
        processed integer NOT NULL DEFAULT 0,
        failed integer NOT NULL DEFAULT 0,
        skipped integer NOT NULL DEFAULT 0,
        consecutive_failures integer NOT NULL DEFAULT 0,
        last_error text NULL,
        last_processed_analysis_id uuid NULL,
        cancellation_requested boolean NOT NULL DEFAULT false,
        created_at timestamptz NOT NULL DEFAULT now(),
        started_at timestamptz NULL,
        completed_at timestamptz NULL,
        heartbeat_at timestamptz NULL
    );
    CREATE INDEX ix_mechanic_recalc_jobs_status_created
        ON mechanic_recalc_jobs(status, created_at)
        WHERE status IN (0, 1);
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS mechanic_recalc_jobs;");
        }
    }
}
