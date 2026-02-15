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
            // NO-OP: pdf_processing_metrics table already created by Issue4212_ProcessingMetricsTable.
            // This migration was generated on a parallel branch and duplicated the same CREATE TABLE.
            // Keeping as empty migration to preserve migration history ordering.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // NO-OP: Table ownership belongs to Issue4212_ProcessingMetricsTable.
        }
    }
}
