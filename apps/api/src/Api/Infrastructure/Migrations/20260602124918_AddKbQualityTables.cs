using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddKbQualityTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "document_evaluation_runs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PdfDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GoldsetVersion = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GoldsetGenerationSeed = table.Column<long>(type: "bigint", nullable: false),
                    CostUsd = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: true),
                    TriggeredByAdminId = table.Column<Guid>(type: "uuid", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Metrics = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_evaluation_runs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "kb_quality_budget_counters",
                columns: table => new
                {
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    YearMonth = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    SpentUsd = table.Column<decimal>(type: "numeric(10,4)", precision: 10, scale: 4, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_kb_quality_budget_counters", x => new { x.TenantId, x.YearMonth });
                });

            migrationBuilder.CreateIndex(
                name: "IX_document_evaluation_runs_CompletedAt",
                table: "document_evaluation_runs",
                column: "CompletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_document_evaluation_runs_PdfDocumentId_StartedAt",
                table: "document_evaluation_runs",
                columns: new[] { "PdfDocumentId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_evaluation_runs_TriggeredByAdminId",
                table: "document_evaluation_runs",
                column: "TriggeredByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_kb_quality_budget_counters_YearMonth",
                table: "kb_quality_budget_counters",
                column: "YearMonth");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_evaluation_runs");

            migrationBuilder.DropTable(
                name: "kb_quality_budget_counters");
        }
    }
}
