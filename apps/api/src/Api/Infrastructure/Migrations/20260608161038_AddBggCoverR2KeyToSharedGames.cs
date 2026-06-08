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
            migrationBuilder.RenameColumn(
                name: "LastLockoutEventId",
                table: "users",
                newName: "last_lockout_event_id");

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

            migrationBuilder.RenameColumn(
                name: "last_lockout_event_id",
                table: "users",
                newName: "LastLockoutEventId");
        }
    }
}
