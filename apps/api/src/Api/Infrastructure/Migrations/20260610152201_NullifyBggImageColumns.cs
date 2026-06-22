using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// Issue #2123 — BGG ToS compliance:
    /// 1. Relax NOT NULL on <c>shared_games.image_url</c> + <c>thumbnail_url</c>
    ///    so the seeder can write NULL on every create path.
    /// 2. Nullify existing rows that store a BGG-hosted URL — these violate the
    ///    ADR-059 §5 ban on user-side BGG asset traffic. After this migration,
    ///    <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.CoverUrlResolver"/>
    ///    falls back to the L2 Wikidata layer (or returns null → FE placeholder).
    /// </summary>
    public partial class NullifyBggImageColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "thumbnail_url",
                table: "shared_games",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                table: "shared_games",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            // Strip existing BGG-hosted URLs to enforce ADR-059 §5 ban end-to-end.
            // Pattern matches: cf.geekdo-images.com, geekdo-images.com, images.geekdo.com,
            // any *.boardgamegeek.com host. Case-insensitive via ILIKE.
            migrationBuilder.Sql(@"
                UPDATE shared_games
                SET image_url = NULL
                WHERE image_url ILIKE '%geekdo%' OR image_url ILIKE '%boardgamegeek%';

                UPDATE shared_games
                SET thumbnail_url = NULL
                WHERE thumbnail_url ILIKE '%geekdo%' OR thumbnail_url ILIKE '%boardgamegeek%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "thumbnail_url",
                table: "shared_games",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                table: "shared_games",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
