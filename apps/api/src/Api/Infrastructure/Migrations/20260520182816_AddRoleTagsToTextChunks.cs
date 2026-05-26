using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRoleTagsToTextChunks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "role_tags",
                table: "text_chunks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_text_chunks_role_tags",
                table: "text_chunks",
                column: "role_tags",
                filter: "role_tags != 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_text_chunks_role_tags",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "role_tags",
                table: "text_chunks");
        }
    }
}
