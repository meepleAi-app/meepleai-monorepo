using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicDraftsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "GameToolkits",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNotes",
                table: "GameToolkits",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "GameToolkits",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReviewedByUserId",
                table: "GameToolkits",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TemplateStatus",
                table: "GameToolkits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "mechanic_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    game_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    summary_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    mechanics_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    victory_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    resources_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    phases_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    questions_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    summary_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    mechanics_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    victory_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    resources_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    phases_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    questions_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_drafts", x => x.id);
                    table.ForeignKey(
                        name: "FK_mechanic_drafts_shared_games_shared_game_id",
                        column: x => x.shared_game_id,
                        principalTable: "shared_games",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_attachments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_index = table.Column<int>(type: "integer", nullable: true),
                    player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attachment_type = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    blob_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    caption = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    content_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    file_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_attachments", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_drafts_game_pdf_status",
                table: "mechanic_drafts",
                columns: new[] { "shared_game_id", "pdf_document_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_drafts_pdf_document_id",
                table: "mechanic_drafts",
                column: "pdf_document_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_drafts_shared_game_id",
                table: "mechanic_drafts",
                column: "shared_game_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_cleanup",
                table: "session_attachments",
                column: "created_at",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_player_id",
                table: "session_attachments",
                column: "player_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_session_id",
                table: "session_attachments",
                column: "session_id",
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_session_attachment_snapshot",
                table: "session_attachments",
                columns: new[] { "session_id", "snapshot_index" },
                filter: "is_deleted = false AND snapshot_index IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mechanic_drafts");

            migrationBuilder.DropTable(
                name: "session_attachments");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "ReviewNotes",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "GameToolkits");

            migrationBuilder.DropColumn(
                name: "TemplateStatus",
                table: "GameToolkits");
        }
    }
}
