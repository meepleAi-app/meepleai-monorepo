using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBggTosHash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bgg_tos_hashes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_checked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_changed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    change_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bgg_tos_hashes", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bgg_tos_hashes");
        }
    }
}
