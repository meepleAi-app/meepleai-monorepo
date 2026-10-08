using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMechanicClaimStructureV3 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "kind",
                table: "mechanic_claims",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "overrides",
                table: "mechanic_claims",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "priority",
                table: "mechanic_claims",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "trigger",
                table: "mechanic_claims",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_claims_analysis_kind",
                table: "mechanic_claims",
                columns: new[] { "analysis_id", "kind" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_claims_kind_range",
                table: "mechanic_claims",
                sql: "kind BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_claims_priority_range",
                table: "mechanic_claims",
                sql: "priority BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_mechanic_claims_analysis_kind",
                table: "mechanic_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_claims_kind_range",
                table: "mechanic_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_claims_priority_range",
                table: "mechanic_claims");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "mechanic_claims");

            migrationBuilder.DropColumn(
                name: "overrides",
                table: "mechanic_claims");

            migrationBuilder.DropColumn(
                name: "priority",
                table: "mechanic_claims");

            migrationBuilder.DropColumn(
                name: "trigger",
                table: "mechanic_claims");
        }
    }
}
