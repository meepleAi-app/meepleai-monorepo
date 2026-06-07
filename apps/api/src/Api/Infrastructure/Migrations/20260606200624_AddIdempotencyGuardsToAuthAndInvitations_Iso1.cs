using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIdempotencyGuardsToAuthAndInvitations_Iso1 : Migration
    {
        // PR comment: 3 colonne nuove di iso-1 (access_requests.last_notified_event_id,
        // users.last_lockout_event_id, game_night_invitations.rsvp_confirmation_sent_at).
        // Le 5 colonne test_run_id rilevate come drift sono manualmente rimosse: sono già
        // nelle migration di CF-2 (#1946) e CF-3 (#1947). Concurrent merge garantisce single
        // application — la prima a essere applicata vince.

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_lockout_event_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rsvp_confirmation_sent_at",
                table: "game_night_invitations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "last_notified_event_id",
                table: "access_requests",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "last_lockout_event_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "rsvp_confirmation_sent_at",
                table: "game_night_invitations");

            migrationBuilder.DropColumn(
                name: "last_notified_event_id",
                table: "access_requests");
        }
    }
}
