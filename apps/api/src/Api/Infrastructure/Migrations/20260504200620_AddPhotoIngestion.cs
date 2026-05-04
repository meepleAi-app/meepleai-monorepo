using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "photo_batch_uploads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceLanguage = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TotalPages = table.Column<int>(type: "integer", nullable: false),
                    IndexedPages = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_batch_uploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photo_batch_uploads_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "photo_batch_pages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PhotoBatchUploadId = table.Column<Guid>(type: "uuid", nullable: false),
                    PageNumber = table.Column<int>(type: "integer", nullable: false),
                    BlobKey = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Confidence = table.Column<double>(type: "double precision", nullable: false),
                    ConfidenceLevel = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Orientation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsBlank = table.Column<bool>(type: "boolean", nullable: false),
                    Warnings = table.Column<string[]>(type: "text[]", nullable: false),
                    ExtractedText = table.Column<string>(type: "text", nullable: true),
                    IndexedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_batch_pages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_photo_batch_pages_photo_batch_uploads_PhotoBatchUploadId",
                        column: x => x.PhotoBatchUploadId,
                        principalTable: "photo_batch_uploads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_pages_batch_id",
                table: "photo_batch_pages",
                column: "PhotoBatchUploadId");

            migrationBuilder.CreateIndex(
                name: "uq_photo_batch_pages_batch_page",
                table: "photo_batch_pages",
                columns: new[] { "PhotoBatchUploadId", "PageNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_uploads_game_id_status",
                table: "photo_batch_uploads",
                columns: new[] { "GameId", "Status" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_uploads_user_id",
                table: "photo_batch_uploads",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo_batch_pages");

            migrationBuilder.DropTable(
                name: "photo_batch_uploads");
        }
    }
}
