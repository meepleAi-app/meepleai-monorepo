using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfCoverExtractionColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "cover_generation_error",
                table: "pdf_documents",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_generation_status",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "cover_page_index",
                table: "pdf_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "cover_r2_key",
                table: "pdf_documents",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "kb_quality_budget_counters",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_cover_generation_status",
                table: "pdf_documents",
                column: "cover_generation_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_cover_generation_status",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "cover_generation_error",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "cover_generation_status",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "cover_page_index",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "cover_r2_key",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "kb_quality_budget_counters");
        }
    }
}
