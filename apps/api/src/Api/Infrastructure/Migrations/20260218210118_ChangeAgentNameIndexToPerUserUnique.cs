using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ChangeAgentNameIndexToPerUserUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_Name",
                table: "agents");

            migrationBuilder.CreateIndex(
                name: "IX_agents_CreatedByUserId_Name",
                table: "agents",
                columns: new[] { "CreatedByUserId", "Name" },
                unique: true,
                filter: "\"CreatedByUserId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_agents_Name",
                table: "agents",
                column: "Name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_agents_CreatedByUserId_Name",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_Name",
                table: "agents");

            migrationBuilder.CreateIndex(
                name: "IX_agents_Name",
                table: "agents",
                column: "Name",
                unique: true);
        }
    }
}
