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

            migrationBuilder.CreateTable(
                name: "entity_links",
                schema: "entity_relationships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SourceEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetEntityType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<Guid>(type: "uuid", nullable: false),
                    LinkType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsBidirectional = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    Scope = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true),
                    IsAdminApproved = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsBggImported = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_links", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_owner",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "OwnerUserId", "IsDeleted" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_source",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "SourceEntityType", "SourceEntityId", "IsDeleted" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "ix_entity_links_target",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "TargetEntityType", "TargetEntityId", "IsDeleted" },
                filter: "\"IsDeleted\" = false");

            migrationBuilder.CreateIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "SourceEntityType", "SourceEntityId", "TargetEntityType", "TargetEntityId", "LinkType" },
                unique: true,
                filter: "\"IsDeleted\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "entity_links",
                schema: "entity_relationships");
        }
    }
}
