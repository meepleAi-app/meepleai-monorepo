using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCopyrightDisclaimerAndRagToggle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "copyright_disclaimer_accepted_at",
                table: "pdf_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "copyright_disclaimer_accepted_by",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_active_for_rag",
                table: "pdf_documents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.CreateIndex(
                name: "ix_pdf_documents_is_active_for_rag",
                table: "pdf_documents",
                column: "is_active_for_rag");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_pdf_documents_is_active_for_rag",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "copyright_disclaimer_accepted_at",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "copyright_disclaimer_accepted_by",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "is_active_for_rag",
                table: "pdf_documents");
        }
    }
}
