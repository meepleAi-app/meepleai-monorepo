using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingApprovalStateToSharedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #2514: Add PendingApproval state to GameStatus enum
            // Update enum values: Draft=0, PendingApproval=1, Published=2, Archived=3
            // Previous: Draft=0, Published=1, Archived=2

            // CRITICAL: Update in REVERSE order to avoid overlap
            // Update existing Archived games FIRST (2 → 3)
            migrationBuilder.Sql(
                "UPDATE shared_games SET status = 3 WHERE status = 2;");

            // Update existing Published games SECOND (1 → 2)
            migrationBuilder.Sql(
                "UPDATE shared_games SET status = 2 WHERE status = 1;");

            // PendingApproval (1) is new state, no existing data needs migration
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rollback: Remove PendingApproval state
            // Published (2 → 1), Archived (3 → 2)

            // Move any PendingApproval games back to Draft
            migrationBuilder.Sql(
                "UPDATE shared_games SET status = 0 WHERE status = 1;");

            // Rollback Published (2 → 1)
            migrationBuilder.Sql(
                "UPDATE shared_games SET status = 1 WHERE status = 2;");

            // Rollback Archived (3 → 2)
            migrationBuilder.Sql(
                "UPDATE shared_games SET status = 2 WHERE status = 3;");
        }
    }
}
