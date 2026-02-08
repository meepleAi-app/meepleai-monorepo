using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceFingerprintToSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #3677: Enable pgcrypto extension for digest() function
            // NOTE: Must be created before using digest() in SQL below
            migrationBuilder.Sql(@"CREATE EXTENSION IF NOT EXISTS pgcrypto;");

            migrationBuilder.AddColumn<string>(
                name: "DeviceFingerprint",
                table: "user_sessions",
                type: "character varying(88)",
                maxLength: 88,
                nullable: true);

            // Issue #3677: Backfill device fingerprints for existing sessions
            // NOTE: Using digest() for SHA256 + encode() for Base64 to match .NET's Convert.ToBase64String
            // PostgreSQL's sha256() returns bytea, digest() is from pgcrypto extension
            migrationBuilder.Sql(@"
                UPDATE user_sessions
                SET ""DeviceFingerprint"" = encode(digest(lower(trim(""UserAgent"")), 'sha256'), 'base64')
                WHERE ""UserAgent"" IS NOT NULL
                  AND ""DeviceFingerprint"" IS NULL;
            ");

            // NOTE: RowVersion column for ProposalMigrations was already added in
            // migration 20260206064656_AddProposalMigrationTable.cs (line 26)
            // Removed duplicate AddColumn to fix migration conflict

            migrationBuilder.CreateIndex(
                name: "IX_user_sessions_UserId_DeviceFingerprint",
                table: "user_sessions",
                columns: new[] { "UserId", "DeviceFingerprint" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_user_sessions_UserId_DeviceFingerprint",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "DeviceFingerprint",
                table: "user_sessions");

            // NOTE: RowVersion column for ProposalMigrations was already added in
            // migration 20260206064656_AddProposalMigrationTable.cs (line 26)
            // So we don't drop it here - it should be dropped when that migration is reverted

            // NOTE: We don't drop pgcrypto extension in Down() because:
            // 1. Other migrations or database features might depend on it
            // 2. Extensions are typically database-wide and should be managed separately
            // 3. Dropping it could break other functionality
        }
    }
}
