using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RetireUserLibraryCustomAgentConfigIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_CustomAgentConfigJson",
                table: "user_library_entries");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_CustomAgentConfigJson",
                table: "user_library_entries",
                column: "CustomAgentConfigJson")
                .Annotation("Npgsql:IndexMethod", "gin");
        }
    }
}
