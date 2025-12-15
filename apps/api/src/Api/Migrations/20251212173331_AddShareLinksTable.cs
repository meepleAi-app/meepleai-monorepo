using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    internal partial class AddShareLinksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "share_links",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    creator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    access_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_accessed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_share_links", x => x.id);
                    table.ForeignKey(
                        name: "FK_share_links_ChatThreads_thread_id",
                        column: x => x.thread_id,
                        principalTable: "ChatThreads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_share_links_users_creator_id",
                        column: x => x.creator_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_share_links_creator_id",
                table: "share_links",
                column: "creator_id");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_expires_at",
                table: "share_links",
                column: "expires_at");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_revoked_at",
                table: "share_links",
                column: "revoked_at");

            migrationBuilder.CreateIndex(
                name: "ix_share_links_thread_id",
                table: "share_links",
                column: "thread_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "share_links");
        }
    }
}
