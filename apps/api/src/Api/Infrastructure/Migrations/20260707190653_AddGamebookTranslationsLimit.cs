using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGamebookTranslationsLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_gamebook_translations_per_month",
                table: "tier_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 50);

            // Issue #2750 (C14): the tier seeder is idempotent (it skips already-seeded rows),
            // so existing premium/unlimited definitions must be corrected explicitly. The free
            // tier stays at the column default (50).
            migrationBuilder.Sql(
                "UPDATE tier_definitions SET max_gamebook_translations_per_month = 500 WHERE name = 'premium'");
            migrationBuilder.Sql(
                "UPDATE tier_definitions SET max_gamebook_translations_per_month = 2147483647 WHERE name = 'unlimited'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_gamebook_translations_per_month",
                table: "tier_definitions");
        }
    }
}
