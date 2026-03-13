using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfDocumentLanguageFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "language_confidence",
                table: "pdf_documents",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "language_override",
                table: "pdf_documents",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language_confidence",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "language_override",
                table: "pdf_documents");
        }
    }
}
