using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShareRequestsOneProposalPerPrivateGame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ux_share_requests_one_proposal_per_private_game",
                table: "share_requests",
                column: "source_private_game_id",
                unique: true,
                filter: "contribution_type = 2 AND status IN (0, 1, 2, 3) AND source_private_game_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_share_requests_one_proposal_per_private_game",
                table: "share_requests");
        }
    }
}
