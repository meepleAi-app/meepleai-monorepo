using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBaseDocumentIdToPdfDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "base_document_id",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_base_document_id",
                table: "pdf_documents",
                column: "base_document_id");

            migrationBuilder.AddForeignKey(
                name: "FK_pdf_documents_pdf_documents_base_document_id",
                table: "pdf_documents",
                column: "base_document_id",
                principalTable: "pdf_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pdf_documents_pdf_documents_base_document_id",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_base_document_id",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "base_document_id",
                table: "pdf_documents");
        }
    }
}
