using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBggIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BggId",
                table: "games",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BggMetadata",
                table: "games",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BggId",
                table: "games");

            migrationBuilder.DropColumn(
                name: "BggMetadata",
                table: "games");
        }
    }
}
