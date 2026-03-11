using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    /// <remarks>
    /// This migration is a no-op because the InitialCreate migration (20260221111608)
    /// already includes all columns (daily_credits_used, weekly_credits_used,
    /// last_daily_reset, last_weekly_reset on user_token_usage; daily_credits_limit,
    /// weekly_credits_limit on token_tiers). Since this migration's timestamp (20260218)
    /// predates InitialCreate (20260221), it runs first on a fresh database where the
    /// tables don't exist yet, causing PostgreSQL error 42P01.
    /// </remarks>
    public partial class AddCreditBudgetTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // No-op: columns already included in InitialCreate (20260221111608)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No-op: columns are part of InitialCreate
        }
    }
}
