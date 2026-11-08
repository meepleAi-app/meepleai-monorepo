using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaxLibraryGamesToTierDefinitions : Migration
    {
        /// <inheritdoc />
        /// <summary>
        /// Issue #4212: the library limit moves into the plan. Until now GameLibraryQuotaService
        /// enforced constants (free 5, normal 20, premium 50, anything else 5): the backfill
        /// writes exactly those, so the limit a user meets does not change on release.
        /// The column default is 5, not 0: a row inserted by the previous code version after a
        /// rollback would otherwise get a library that holds nothing.
        /// The "normal" plan does not exist yet in existing databases; the seeder creates it at
        /// startup with the same 20.
        /// </summary>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "max_library_games",
                table: "tier_definitions",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.Sql(
                """
                UPDATE tier_definitions
                SET max_library_games = CASE name
                    WHEN 'normal' THEN 20
                    WHEN 'premium' THEN 50
                    ELSE 5
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "max_library_games",
                table: "tier_definitions");
        }
    }
}
