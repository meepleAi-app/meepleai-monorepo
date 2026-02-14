using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1707 // Identifiers should not contain underscores

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Issue4315_ActivityTimelineIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_AddedAt",
                table: "user_library_entries",
                columns: new[] { "UserId", "AddedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_UserId_AddedAt",
                table: "user_library_entries");
        }
    }
}
