using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - EF Core migration naming standard
    public partial class Issue3715_PdfAnalyticsTables : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdf_processing_metrics",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    step = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    duration_seconds = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    pdf_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    page_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pdf_processing_metrics", x => x.id);
                    table.ForeignKey(
                        name: "FK_pdf_processing_metrics_pdf_documents_pdf_document_id",
                        column: x => x.pdf_document_id,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_processing_metrics_pdf_document_id",
                table: "pdf_processing_metrics",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_processing_metrics_step_created_at",
                table: "pdf_processing_metrics",
                columns: new[] { "step", "created_at" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdf_processing_metrics");
        }
    }
}
