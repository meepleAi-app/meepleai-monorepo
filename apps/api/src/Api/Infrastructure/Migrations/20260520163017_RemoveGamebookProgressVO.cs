using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGamebookProgressVO : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "progress",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "progress",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                type: "jsonb",
                nullable: false,
                // Issue #1393: '' is not valid jsonb syntax — PostgreSQL rejects it with
                // "invalid input syntax for type json". '{}' is the canonical empty-object literal.
                defaultValue: "{}");
        }
    }
}
