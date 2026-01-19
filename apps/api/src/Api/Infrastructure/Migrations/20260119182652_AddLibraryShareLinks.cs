using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLibraryShareLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "library_share_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    share_token = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    privacy_level = table.Column<int>(type: "integer", nullable: false),
                    include_notes = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    view_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_library_share_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_library_share_links_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_expires_at",
                table: "library_share_links",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_privacy_level",
                table: "library_share_links",
                column: "privacy_level");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_revoked_at",
                table: "library_share_links",
                column: "revoked_at");

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_share_token",
                table: "library_share_links",
                column: "share_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_library_share_links_user_id",
                table: "library_share_links",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "library_share_links");
        }
    }
}
