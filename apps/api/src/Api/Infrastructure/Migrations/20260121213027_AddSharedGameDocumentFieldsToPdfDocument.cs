using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameDocumentFieldsToPdfDocument : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContributorId",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SharedGameId",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SourceDocumentId",
                table: "pdf_documents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContributorId",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "SharedGameId",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "SourceDocumentId",
                table: "pdf_documents");
        }
    }
}
