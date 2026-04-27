using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWaitlistEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waitlist_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    game_preference_id = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    game_preference_other = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    newsletter_opt_in = table.Column<bool>(type: "boolean", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    contacted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waitlist_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_created_at",
                table: "waitlist_entries",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_email",
                table: "waitlist_entries",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waitlist_entries_position",
                table: "waitlist_entries",
                column: "position");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waitlist_entries");
        }
    }
}
