using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkHierarchyToTextChunkEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ElementType",
                table: "text_chunks",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NarrativeText");

            migrationBuilder.AddColumn<string>(
                name: "Heading",
                table: "text_chunks",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Level",
                table: "text_chunks",
                type: "smallint",
                nullable: false,
                defaultValue: (short)1);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentChunkId",
                table: "text_chunks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_ParentChunkId",
                table: "text_chunks",
                column: "ParentChunkId");

            migrationBuilder.CreateIndex(
                name: "ix_text_chunks_pdf_chunk_index",
                table: "text_chunks",
                columns: new[] { "PdfDocumentId", "ChunkIndex" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_text_chunks_ParentChunkId",
                table: "text_chunks");

            migrationBuilder.DropIndex(
                name: "ix_text_chunks_pdf_chunk_index",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "ElementType",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "Heading",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "ParentChunkId",
                table: "text_chunks");
        }
    }
}
