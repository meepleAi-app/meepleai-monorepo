using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionGamebookCampaignId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "gamebook_campaign_id",
                table: "session_tracking_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_sessions_gamebook_campaign",
                table: "session_tracking_sessions",
                column: "gamebook_campaign_id",
                filter: "gamebook_campaign_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_sessions_gamebook_campaign",
                table: "session_tracking_sessions");

            migrationBuilder.DropColumn(
                name: "gamebook_campaign_id",
                table: "session_tracking_sessions");
        }
    }
}
