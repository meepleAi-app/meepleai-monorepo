using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCategoryToPdfDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "document_category",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Rulebook");

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_document_category",
                table: "pdf_documents",
                column: "document_category");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_document_category",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "document_category",
                table: "pdf_documents");
        }
    }
}
