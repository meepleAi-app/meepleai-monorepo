using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoIngestionTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "glossary_entries_json",
                table: "game_memories",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "photo_batch_uploads",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_language = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_pages = table.Column<int>(type: "integer", nullable: false),
                    indexed_pages = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true),
                    row_version = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false),
                    low_confidence_page_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_batch_uploads", x => x.id);
                    table.ForeignKey(
                        name: "FK_photo_batch_uploads_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "photo_batch_pages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_batch_upload_id = table.Column<Guid>(type: "uuid", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    blob_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    confidence = table.Column<double>(type: "double precision", nullable: false),
                    confidence_level = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    orientation = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_blank = table.Column<bool>(type: "boolean", nullable: false),
                    warnings = table.Column<string[]>(type: "text[]", nullable: false),
                    extracted_text = table.Column<string>(type: "text", nullable: true),
                    indexed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_photo_batch_pages", x => x.id);
                    table.ForeignKey(
                        name: "FK_photo_batch_pages_photo_batch_uploads_photo_batch_upload_id",
                        column: x => x.photo_batch_upload_id,
                        principalTable: "photo_batch_uploads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_pages_batch_id",
                table: "photo_batch_pages",
                column: "photo_batch_upload_id");

            migrationBuilder.CreateIndex(
                name: "uq_photo_batch_pages_batch_page",
                table: "photo_batch_pages",
                columns: new[] { "photo_batch_upload_id", "page_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_uploads_game_id_status",
                table: "photo_batch_uploads",
                columns: new[] { "game_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_photo_batch_uploads_user_id",
                table: "photo_batch_uploads",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "photo_batch_pages");

            migrationBuilder.DropTable(
                name: "photo_batch_uploads");

            migrationBuilder.DropColumn(
                name: "glossary_entries_json",
                table: "game_memories");
        }
    }
}
