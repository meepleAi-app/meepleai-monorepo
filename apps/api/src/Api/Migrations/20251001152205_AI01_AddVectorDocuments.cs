using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AI01_AddVectorDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "vector_documents",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfDocumentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ChunkCount = table.Column<int>(type: "integer", nullable: false),
                    TotalCharacters = table.Column<int>(type: "integer", nullable: false),
                    IndexingStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IndexingError = table.Column<string>(type: "text", nullable: true),
                    EmbeddingModel = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    EmbeddingDimensions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vector_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_vector_documents_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vector_documents_pdf_documents_PdfDocumentId",
                        column: x => x.PdfDocumentId,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_vector_documents_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_GameId",
                table: "vector_documents",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_PdfDocumentId",
                table: "vector_documents",
                column: "PdfDocumentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_TenantId_GameId",
                table: "vector_documents",
                columns: new[] { "TenantId", "GameId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vector_documents");
        }
    }
}
