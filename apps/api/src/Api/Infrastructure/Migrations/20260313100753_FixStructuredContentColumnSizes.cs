using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixStructuredContentColumnSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ExtractedTables",
                table: "pdf_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8192)",
                oldMaxLength: 8192,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedDiagrams",
                table: "pdf_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8192)",
                oldMaxLength: 8192,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AtomicRules",
                table: "pdf_documents",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(8192)",
                oldMaxLength: 8192,
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "session_invites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    pin = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    link_token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    max_uses = table.Column<int>(type: "integer", nullable: false),
                    current_uses = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_revoked = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_invites", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_invites_live_game_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_session_invites_link_token",
                table: "session_invites",
                column: "link_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_invites_pin",
                table: "session_invites",
                column: "pin");

            migrationBuilder.CreateIndex(
                name: "ix_session_invites_session_id",
                table: "session_invites",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_invites");

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedTables",
                table: "pdf_documents",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedDiagrams",
                table: "pdf_documents",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AtomicRules",
                table: "pdf_documents",
                type: "character varying(8192)",
                maxLength: 8192,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
