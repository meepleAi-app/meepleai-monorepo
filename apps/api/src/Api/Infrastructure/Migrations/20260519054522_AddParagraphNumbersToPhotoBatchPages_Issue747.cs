using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddParagraphNumbersToPhotoBatchPages_Issue747 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int[]>(
                name: "paragraph_numbers",
                table: "photo_batch_pages",
                type: "integer[]",
                nullable: false,
                defaultValueSql: "'{}'::integer[]");

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_pages_paragraph_numbers_gin",
                table: "photo_batch_pages",
                column: "paragraph_numbers")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_photo_batch_pages_paragraph_numbers_gin",
                table: "photo_batch_pages");

            migrationBuilder.DropColumn(
                name: "paragraph_numbers",
                table: "photo_batch_pages");
        }
    }
}
