using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class DDD_Phase2_GameManagementExtendGameEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxPlayTimeMinutes",
                table: "games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxPlayers",
                table: "games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinPlayTimeMinutes",
                table: "games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinPlayers",
                table: "games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Publisher",
                table: "games",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "YearPublished",
                table: "games",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxPlayTimeMinutes",
                table: "games");

            migrationBuilder.DropColumn(
                name: "MaxPlayers",
                table: "games");

            migrationBuilder.DropColumn(
                name: "MinPlayTimeMinutes",
                table: "games");

            migrationBuilder.DropColumn(
                name: "MinPlayers",
                table: "games");

            migrationBuilder.DropColumn(
                name: "Publisher",
                table: "games");

            migrationBuilder.DropColumn(
                name: "YearPublished",
                table: "games");
        }
    }
}
