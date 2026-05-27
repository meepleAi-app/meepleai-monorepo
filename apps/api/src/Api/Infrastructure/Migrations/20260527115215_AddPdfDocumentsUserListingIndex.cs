using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfDocumentsUserListingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_UploadedByUserId",
                table: "pdf_documents");

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_uploaded_by_user_id_processed_at_desc",
                table: "pdf_documents",
                columns: new[] { "UploadedByUserId", "ProcessedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_uploaded_by_user_id_processed_at_desc",
                table: "pdf_documents");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_UploadedByUserId",
                table: "pdf_documents",
                column: "UploadedByUserId");
        }
    }
}
