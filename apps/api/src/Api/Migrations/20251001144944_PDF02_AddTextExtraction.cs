using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class PDF02_AddTextExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CharacterCount",
                table: "pdf_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExtractedText",
                table: "pdf_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PageCount",
                table: "pdf_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProcessedAt",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingError",
                table: "pdf_documents",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProcessingStatus",
                table: "pdf_documents",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CharacterCount",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "ExtractedText",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "PageCount",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "ProcessedAt",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "ProcessingError",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "ProcessingStatus",
                table: "pdf_documents");
        }
    }
}
