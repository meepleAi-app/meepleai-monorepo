using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameNightGuestEmailOptOuts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_night_guest_email_opt_outs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    opted_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_invitation_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_night_guest_email_opt_outs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_game_night_guest_email_opt_outs_email",
                table: "game_night_guest_email_opt_outs",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_night_guest_email_opt_outs");
        }
    }
}
