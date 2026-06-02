using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfDocumentMetadataEditableFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<List<string>>(
                name: "tags",
                table: "pdf_documents",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'::text[]");

            migrationBuilder.AddColumn<string>(
                name: "title",
                table: "pdf_documents",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_tags_gin",
                table: "pdf_documents",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_tags_gin",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "title",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "pdf_documents");
        }
    }
}
