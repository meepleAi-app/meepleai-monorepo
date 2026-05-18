using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #1169 — Public-RSVP guest display name.
    ///
    /// Adds a nullable <c>responded_by_name</c> column (≤120 chars) to
    /// <c>game_night_invitations</c> so guests can attach a display name when
    /// RSVPing via the public token endpoint. Distinct from
    /// <c>responded_by_user_id</c> (cookie-authenticated identity) because:
    ///   1. anonymous guests still want to introduce themselves;
    ///   2. an authenticated user may RSVP on behalf of someone else.
    ///
    /// Backfill: NONE — existing rows keep NULL. The column is nullable for
    /// the lifetime of the table; no INSERT default is set so writers must
    /// explicitly opt in (the domain aggregate handles trim + cap).
    /// </summary>
    public partial class AddRespondedByNameToGameNightInvitation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "responded_by_name",
                table: "game_night_invitations",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "responded_by_name",
                table: "game_night_invitations");
        }
    }
}
