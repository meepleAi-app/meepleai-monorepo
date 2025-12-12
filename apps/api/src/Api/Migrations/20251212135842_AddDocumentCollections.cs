using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentCollections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CollectionId",
                table: "pdf_documents",
                type: "uuid",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DocumentType",
                table: "pdf_documents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "base");

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "pdf_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "document_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DocumentsJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_collections_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_document_collections_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "chat_thread_collections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    ChatThreadId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CollectionId = table.Column<Guid>(type: "uuid", maxLength: 64, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_thread_collections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chat_thread_collections_ChatThreads_ChatThreadId",
                        column: x => x.ChatThreadId,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chat_thread_collections_document_collections_CollectionId",
                        column: x => x.CollectionId,
                        principalTable: "document_collections",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_CollectionId",
                table: "pdf_documents",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_pdf_documents_CollectionId_SortOrder",
                table: "pdf_documents",
                columns: new[] { "CollectionId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_thread_collections_ChatThreadId",
                table: "chat_thread_collections",
                column: "ChatThreadId");

            migrationBuilder.CreateIndex(
                name: "IX_chat_thread_collections_ChatThreadId_CollectionId",
                table: "chat_thread_collections",
                columns: new[] { "ChatThreadId", "CollectionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_chat_thread_collections_CollectionId",
                table: "chat_thread_collections",
                column: "CollectionId");

            migrationBuilder.CreateIndex(
                name: "IX_document_collections_CreatedByUserId_CreatedAt",
                table: "document_collections",
                columns: new[] { "CreatedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_collections_GameId",
                table: "document_collections",
                column: "GameId");

            migrationBuilder.AddForeignKey(
                name: "FK_pdf_documents_document_collections_CollectionId",
                table: "pdf_documents",
                column: "CollectionId",
                principalTable: "document_collections",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            // ISSUE-2051: Data migration - Create default collection for each game with PDFs
            migrationBuilder.Sql(@"
                -- Step 1: Create a collection for each game that has at least one PDF document
                INSERT INTO document_collections (""Id"", ""GameId"", ""Name"", ""Description"", ""CreatedByUserId"", ""CreatedAt"", ""UpdatedAt"", ""DocumentsJson"")
                SELECT
                    gen_random_uuid() AS ""Id"",
                    g.""Id"" AS ""GameId"",
                    CONCAT(g.""Name"", ' - Base Collection') AS ""Name"",
                    'Default collection created during multi-document migration' AS ""Description"",
                    MIN(p.""UploadedByUserId"") AS ""CreatedByUserId"",
                    NOW() AS ""CreatedAt"",
                    NOW() AS ""UpdatedAt"",
                    '[]' AS ""DocumentsJson""
                FROM games g
                INNER JOIN pdf_documents p ON p.""GameId"" = g.""Id""
                WHERE NOT EXISTS (
                    SELECT 1 FROM document_collections WHERE ""GameId"" = g.""Id""
                )
                GROUP BY g.""Id"", g.""Name"";

                -- Step 2: Assign all existing PDFs to their game's collection
                UPDATE pdf_documents p
                SET
                    ""CollectionId"" = (SELECT ""Id"" FROM document_collections WHERE ""GameId"" = p.""GameId"" LIMIT 1),
                    ""DocumentType"" = 'base',
                    ""SortOrder"" = 0
                WHERE ""CollectionId"" IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_pdf_documents_document_collections_CollectionId",
                table: "pdf_documents");

            migrationBuilder.DropTable(
                name: "chat_thread_collections");

            migrationBuilder.DropTable(
                name: "document_collections");

            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_CollectionId",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "IX_pdf_documents_CollectionId_SortOrder",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "CollectionId",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "DocumentType",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "pdf_documents");
        }
    }
}
