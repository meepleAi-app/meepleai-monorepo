using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
#pragma warning disable CA1707 // Identifiers should not contain underscores
    public partial class AddBadgeAndUserBadgeEntities_Issue2731 : Migration
#pragma warning restore CA1707 // Identifiers should not contain underscores
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    icon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    tier = table.Column<int>(type: "integer", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    requirement = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    modified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_badges", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_badges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    badge_id = table.Column<Guid>(type: "uuid", nullable: false),
                    earned_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    triggering_share_request_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_displayed = table.Column<bool>(type: "boolean", nullable: false),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_badges", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_badges_badges_badge_id",
                        column: x => x.badge_id,
                        principalTable: "badges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_badges_category",
                table: "badges",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_badges_code_unique",
                table: "badges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_badges_display_order",
                table: "badges",
                column: "display_order");

            migrationBuilder.CreateIndex(
                name: "ix_badges_is_active",
                table: "badges",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_badges_tier",
                table: "badges",
                column: "tier");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_badge_id",
                table: "user_badges",
                column: "badge_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_revoked_at",
                table: "user_badges",
                column: "revoked_at",
                filter: "revoked_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_triggering_share_request_id",
                table: "user_badges",
                column: "triggering_share_request_id",
                filter: "triggering_share_request_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_user_badge_unique",
                table: "user_badges",
                columns: new[] { "user_id", "badge_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_badges_user_id",
                table: "user_badges",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_badges");

            migrationBuilder.DropTable(
                name: "badges");
        }
    }
}
