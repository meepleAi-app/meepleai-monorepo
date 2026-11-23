using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddContributorUniqueIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contribution_records_contributor_version",
                table: "contribution_records");

            migrationBuilder.CreateIndex(
                name: "ix_contributors_one_primary_per_shared_game_unique",
                table: "contributors",
                column: "shared_game_id",
                unique: true,
                filter: "is_primary_contributor");

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_contributor_version_unique",
                table: "contribution_records",
                columns: new[] { "contributor_id", "version" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_contributors_one_primary_per_shared_game_unique",
                table: "contributors");

            migrationBuilder.DropIndex(
                name: "ix_contribution_records_contributor_version_unique",
                table: "contribution_records");

            migrationBuilder.CreateIndex(
                name: "ix_contribution_records_contributor_version",
                table: "contribution_records",
                columns: new[] { "contributor_id", "version" });
        }
    }
}
