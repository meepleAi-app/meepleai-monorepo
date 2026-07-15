using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropMechanicDraftsVariantC : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mechanic_drafts");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mechanic_drafts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    shared_game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(12,6)", nullable: false, defaultValue: 0m),
                    game_title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    last_modified = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    mechanics_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    mechanics_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    pdf_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    phases_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    phases_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    questions_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    questions_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    resources_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    resources_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    status = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    summary_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    summary_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    total_tokens_used = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    victory_draft = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    victory_notes = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
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
        }
    }
}
