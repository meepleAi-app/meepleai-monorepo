using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamebookPhotoAndGlossaryTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "gamebook_glossary_entries",
                schema: "session_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    term_en = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    term_it = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gamebook_glossary_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "gamebook_photo_artifacts",
                schema: "session_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    s3_key = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    page_type = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    ocr_full_text = table.Column<string>(type: "text", nullable: true),
                    segments = table.Column<string>(type: "jsonb", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gamebook_photo_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "translated_paragraphs",
                schema: "session_tracking",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    photo_artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    paragraph_number = table.Column<int>(type: "integer", nullable: false),
                    page_type = table.Column<int>(type: "integer", nullable: false),
                    source_text_en = table.Column<string>(type: "text", nullable: false),
                    translated_text_it = table.Column<string>(type: "text", nullable: false),
                    applied_glossary_terms = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translated_paragraphs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "uq_gamebook_glossary_entries_campaign_term_en",
                schema: "session_tracking",
                table: "gamebook_glossary_entries",
                columns: new[] { "campaign_id", "term_en" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_gamebook_photo_artifacts_campaign_id",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_gamebook_photo_artifacts_expires_at_active",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts",
                column: "expires_at",
                filter: "status <> 99");

            migrationBuilder.CreateIndex(
                name: "ix_translated_paragraphs_campaign_paragraph",
                schema: "session_tracking",
                table: "translated_paragraphs",
                columns: new[] { "campaign_id", "paragraph_number" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "gamebook_glossary_entries",
                schema: "session_tracking");

            migrationBuilder.DropTable(
                name: "gamebook_photo_artifacts",
                schema: "session_tracking");

            migrationBuilder.DropTable(
                name: "translated_paragraphs",
                schema: "session_tracking");
        }
    }
}
