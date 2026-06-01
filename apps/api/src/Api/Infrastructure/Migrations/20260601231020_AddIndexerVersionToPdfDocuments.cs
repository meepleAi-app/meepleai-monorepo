using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexerVersionToPdfDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "indexer_version",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Issue #1673: Backfill legacy marker for rows ingested before versioning support.
            migrationBuilder.Sql(
                "UPDATE pdf_documents SET indexer_version = 'v0' WHERE indexer_version IS NULL;");

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_indexer_version",
                table: "pdf_documents",
                column: "indexer_version");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_indexer_version",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "indexer_version",
                table: "pdf_documents");
        }
    }
}
