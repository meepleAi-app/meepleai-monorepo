using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamebookCampaignOutcome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "completed_at",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "outcome",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "completed_at",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions");

            migrationBuilder.DropColumn(
                name: "outcome",
                schema: "session_tracking",
                table: "gamebook_campaign_sessions");
        }
    }
}
