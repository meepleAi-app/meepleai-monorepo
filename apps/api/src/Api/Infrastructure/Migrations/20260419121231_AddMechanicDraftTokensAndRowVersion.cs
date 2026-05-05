using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicDraftTokensAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "estimated_cost_usd",
                table: "mechanic_drafts",
                type: "numeric(12,6)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "row_version",
                table: "mechanic_drafts",
                type: "bytea",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "total_tokens_used",
                table: "mechanic_drafts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "estimated_cost_usd",
                table: "mechanic_drafts");

            migrationBuilder.DropColumn(
                name: "row_version",
                table: "mechanic_drafts");

            migrationBuilder.DropColumn(
                name: "total_tokens_used",
                table: "mechanic_drafts");
        }
    }
}
