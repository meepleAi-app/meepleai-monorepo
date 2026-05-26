using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #1399 Phase 4 (B): cosmetic backfill for <c>pdf_documents.FilePath</c>.
    ///
    /// The storage-layout migration (#1314) moved S3 objects from
    /// <c>pdf_uploads/{resourceKey}/...</c> to <c>pdfs/{resourceKey}/...</c>,
    /// but the <c>pdf_documents.FilePath</c> column still carries the legacy
    /// <c>pdf_uploads/...</c> strings. Read paths recompute the S3 key from
    /// <c>BlobCategory + resourceKey + fileId</c> so this column is metadata
    /// only — the application does not depend on its content. Backfill anyway
    /// for operator clarity and to keep grep/audit signals consistent.
    ///
    /// Idempotent: the <c>WHERE LIKE 'pdf_uploads/%'</c> guard makes re-runs
    /// a no-op. Expected staging UPDATE count: ~216 rows.
    /// </summary>
    public partial class BackfillPdfDocumentsFilePathToNewLayout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE pdf_documents
                SET "FilePath" = REPLACE("FilePath", 'pdf_uploads/', 'pdfs/')
                WHERE "FilePath" LIKE 'pdf_uploads/%';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Reverse the rename. Same idempotency property (LIKE 'pdfs/%' guard).
            migrationBuilder.Sql("""
                UPDATE pdf_documents
                SET "FilePath" = REPLACE("FilePath", 'pdfs/', 'pdf_uploads/')
                WHERE "FilePath" LIKE 'pdfs/%';
                """);
        }
    }
}
