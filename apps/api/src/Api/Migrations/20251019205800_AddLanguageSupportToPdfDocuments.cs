using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <summary>
    /// AI-09: Add language support to PDF documents
    /// Adds language column to pdf_documents table for multilingual support
    /// </summary>
    public partial class AddLanguageSupportToPdfDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add language column to pdf_documents table
            migrationBuilder.AddColumn<string>(
                name: "language",
                table: "pdf_documents",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                defaultValue: "en",
                comment: "ISO 639-1 language code (en, it, de, fr, es)");

            // Create index on language column for filtering
            migrationBuilder.CreateIndex(
                name: "idx_pdf_documents_language",
                table: "pdf_documents",
                column: "language");

            // Update existing records to have default language 'en'
            migrationBuilder.Sql(
                @"UPDATE pdf_documents
                  SET language = 'en'
                  WHERE language IS NULL OR language = '';");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop index
            migrationBuilder.DropIndex(
                name: "idx_pdf_documents_language",
                table: "pdf_documents");

            // Drop column
            migrationBuilder.DropColumn(
                name: "language",
                table: "pdf_documents");
        }
    }
}
