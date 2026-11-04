using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnsubscribeKeyToGameNightInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "unsubscribe_key",
                table: "game_night_invitations",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // The invitations already sent have no key. Giving each row one lets the cancellation and the
            // reminder of an invitation sent yesterday carry a working opt-out link. 22 hex characters of a random
            // UUID: opaque, unguessable, and far from colliding; the unique index below would say if two did.
            migrationBuilder.Sql("""
                UPDATE game_night_invitations
                SET unsubscribe_key = substr(replace(gen_random_uuid()::text, '-', ''), 1, 22)
                WHERE unsubscribe_key IS NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_game_night_invitations_unsubscribe_key",
                table: "game_night_invitations",
                column: "unsubscribe_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_game_night_invitations_unsubscribe_key",
                table: "game_night_invitations");

            migrationBuilder.DropColumn(
                name: "unsubscribe_key",
                table: "game_night_invitations");
        }
    }
}
