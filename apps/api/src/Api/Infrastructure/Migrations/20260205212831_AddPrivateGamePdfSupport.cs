using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivateGamePdfSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrivateGameId",
                table: "pdf_documents",
                type: "uuid",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_PrivateGameId",
                table: "pdf_documents",
                column: "PrivateGameId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_PrivateGameId",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "PrivateGameId",
                table: "pdf_documents");
        }
    }
}
