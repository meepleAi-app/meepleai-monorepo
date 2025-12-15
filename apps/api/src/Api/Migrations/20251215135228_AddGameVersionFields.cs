using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddGameVersionFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionNumber",
                table: "games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersionType",
                table: "games",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Language",
                table: "games");

            migrationBuilder.DropColumn(
                name: "VersionNumber",
                table: "games");

            migrationBuilder.DropColumn(
                name: "VersionType",
                table: "games");
        }
    }
}
