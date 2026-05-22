using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVisualMetadataToGameCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "color",
                table: "game_categories",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "emoji",
                table: "game_categories",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "game_categories",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                table: "game_categories",
                type: "uuid",
                nullable: true);

            // Issue #1440 — seed the eight default visual categories used by
            // the admin categories management surface. INSERT … ON CONFLICT
            // makes the seed idempotent: rerunning the migration on a DB that
            // already has these names is a no-op. Backfills emoji/color for
            // existing rows that match by name (UPDATE branch).
            migrationBuilder.Sql(@"
INSERT INTO game_categories (id, name, slug, emoji, color, created_at)
VALUES
    (gen_random_uuid(), 'Strategy',      'strategy',      '🎯', '#ef4444', NOW()),
    (gen_random_uuid(), 'Party',         'party',         '🎉', '#ec4899', NOW()),
    (gen_random_uuid(), 'Cooperative',   'cooperative',   '🤝', '#10b981', NOW()),
    (gen_random_uuid(), 'Deck Building', 'deck-building', '🃏', '#8b5cf6', NOW()),
    (gen_random_uuid(), 'Family',        'family',        '👨‍👩‍👧‍👦', '#f59e0b', NOW()),
    (gen_random_uuid(), 'Abstract',      'abstract',      '🔷', '#06b6d4', NOW()),
    (gen_random_uuid(), 'Thematic',      'thematic',      '🗺️', '#ef4444', NOW()),
    (gen_random_uuid(), 'Euro',          'euro',          '🏛️', '#6366f1', NOW())
ON CONFLICT (name) DO UPDATE
    SET emoji = EXCLUDED.emoji,
        color = EXCLUDED.color;
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "color",
                table: "game_categories");

            migrationBuilder.DropColumn(
                name: "emoji",
                table: "game_categories");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "game_categories");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "game_categories");
        }
    }
}
