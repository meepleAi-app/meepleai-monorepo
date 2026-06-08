using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBggCoverR2KeyToSharedGames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: last_lockout_event_id was already added as snake_case by migration
            // 20260606200624_AddIdempotencyGuardsToAuthAndInvitations_Iso1. No rename needed.
            migrationBuilder.AddColumn<string>(
                name: "bgg_cover_r2_key",
                table: "shared_games",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "bgg_cover_r2_key",
                table: "shared_games");
        }
    }
}
