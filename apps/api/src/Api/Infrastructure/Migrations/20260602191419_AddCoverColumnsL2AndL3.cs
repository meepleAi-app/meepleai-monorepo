using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCoverColumnsL2AndL3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "custom_cover_r2_key",
                table: "user_library_entries",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wikidata_cover_attribution",
                table: "shared_games",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wikidata_cover_license",
                table: "shared_games",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wikidata_cover_r2_key",
                table: "shared_games",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "wikidata_cover_source_url",
                table: "shared_games",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "custom_cover_r2_key",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "wikidata_cover_attribution",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "wikidata_cover_license",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "wikidata_cover_r2_key",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "wikidata_cover_source_url",
                table: "shared_games");
        }
    }
}
