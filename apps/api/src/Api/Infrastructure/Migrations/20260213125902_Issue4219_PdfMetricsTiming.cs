using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores - migration name format
    public partial class Issue4219_PdfMetricsTiming : Migration
#pragma warning restore CA1707
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "chunking_started_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "embedding_started_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "extracting_started_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "indexing_started_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "uploading_started_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "chunking_started_at",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "embedding_started_at",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "extracting_started_at",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "indexing_started_at",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "uploading_started_at",
                table: "pdf_documents");
        }
    }
}
