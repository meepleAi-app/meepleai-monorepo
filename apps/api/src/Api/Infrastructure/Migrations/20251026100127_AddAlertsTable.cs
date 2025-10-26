using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "search_vector",
                table: "pdf_documents",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alert_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    severity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    triggered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    channel_sent = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alerts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "text_chunks",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PdfDocumentId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Content = table.Column<string>(type: "text", nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: true),
                    CharacterCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    search_vector = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_text_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_text_chunks_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_text_chunks_pdf_documents_PdfDocumentId",
                        column: x => x.PdfDocumentId,
                        principalTable: "pdf_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_alert_type_triggered_at",
                table: "alerts",
                columns: new[] { "alert_type", "triggered_at" });

            migrationBuilder.CreateIndex(
                name: "IX_alerts_is_active",
                table: "alerts",
                column: "is_active",
                filter: "is_active = true");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_ChunkIndex",
                table: "text_chunks",
                column: "ChunkIndex");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_GameId",
                table: "text_chunks",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_PageNumber",
                table: "text_chunks",
                column: "PageNumber");

            migrationBuilder.CreateIndex(
                name: "IX_text_chunks_PdfDocumentId",
                table: "text_chunks",
                column: "PdfDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "text_chunks");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "pdf_documents");
        }
    }
}
