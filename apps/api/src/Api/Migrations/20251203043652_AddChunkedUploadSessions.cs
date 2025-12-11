using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable MA0048 // File name must match type name - EF Core migration

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddChunkedUploadSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "text_chunks");

            migrationBuilder.DropColumn(
                name: "search_vector",
                table: "pdf_documents");

            migrationBuilder.CreateTable(
                name: "chunked_upload_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    TotalFileSize = table.Column<long>(type: "bigint", nullable: false),
                    TotalChunks = table.Column<int>(type: "integer", nullable: false),
                    ReceivedChunks = table.Column<int>(type: "integer", nullable: false),
                    TempDirectory = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    ReceivedChunkIndices = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chunked_upload_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_chunked_upload_sessions_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_chunked_upload_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_ExpiresAt",
                table: "chunked_upload_sessions",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_GameId",
                table: "chunked_upload_sessions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_Status",
                table: "chunked_upload_sessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_UserId",
                table: "chunked_upload_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_chunked_upload_sessions_UserId_Status",
                table: "chunked_upload_sessions",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "chunked_upload_sessions");

            migrationBuilder.AddColumn<string>(
                name: "search_vector",
                table: "text_chunks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "search_vector",
                table: "pdf_documents",
                type: "text",
                nullable: true);
        }
    }
}
