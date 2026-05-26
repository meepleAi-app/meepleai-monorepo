using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveGamebookPageType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "page_type",
                schema: "session_tracking",
                table: "translated_paragraphs");

            migrationBuilder.DropColumn(
                name: "page_type",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "page_type",
                schema: "session_tracking",
                table: "translated_paragraphs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "page_type",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
