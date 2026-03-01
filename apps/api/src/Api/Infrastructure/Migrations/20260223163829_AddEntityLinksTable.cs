using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEntityLinksTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "entity_relationships");

            migrationBuilder.AlterColumn<Guid>(
                name: "game_id",
                table: "session_tracking_sessions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "entity_links",
                schema: "entity_relationships",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_entity_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    target_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_bidirectional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    is_admin_approved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_bgg_imported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_links", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_owner",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "owner_user_id", "is_deleted" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_source",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "is_deleted" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_target",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "target_entity_type", "target_entity_id", "is_deleted" },
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "target_entity_type", "target_entity_id", "link_type" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_links",
                schema: "entity_relationships");

            migrationBuilder.AlterColumn<Guid>(
                name: "game_id",
                table: "session_tracking_sessions",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }
    }
}
