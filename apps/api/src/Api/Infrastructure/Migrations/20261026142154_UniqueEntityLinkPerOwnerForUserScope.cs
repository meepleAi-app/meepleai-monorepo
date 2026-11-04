using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UniqueEntityLinkPerOwnerForUserScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links");

            migrationBuilder.CreateIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "target_entity_type", "target_entity_id", "link_type" },
                unique: true,
                filter: "is_deleted = false AND scope = 'Shared'");

            migrationBuilder.CreateIndex(
                name: "uq_entity_links_user_source_target_type_owner",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "target_entity_type", "target_entity_id", "link_type", "owner_user_id" },
                unique: true,
                filter: "is_deleted = false AND scope = 'User'");
        }

        /// <inheritdoc />
        /// <remarks>
        /// One-way once the per-owner rule has been used: the old index allows a single live row per
        /// (source, target, type), so recreating it fails with 23505 if two users hold the same User link, or a User
        /// link sits next to a Shared one. The migration is transactional, so the rollback aborts and changes nothing;
        /// resolve the duplicates by hand first.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links");

            migrationBuilder.DropIndex(
                name: "uq_entity_links_user_source_target_type_owner",
                schema: "entity_relationships",
                table: "entity_links");

            migrationBuilder.CreateIndex(
                name: "uq_entity_links_source_target_type",
                schema: "entity_relationships",
                table: "entity_links",
                columns: new[] { "source_entity_type", "source_entity_id", "target_entity_type", "target_entity_id", "link_type" },
                unique: true,
                filter: "is_deleted = false");
        }
    }
}
