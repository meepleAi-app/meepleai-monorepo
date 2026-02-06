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

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProposalMigrations",
                type: "bytea",
                rowVersion: true,
                nullable: true);

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

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProposalMigrations");
        }
    }
}
