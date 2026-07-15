using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixMechanicSectionRangeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_golden_claims_section_range",
                table: "mechanic_golden_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_claims_section_range",
                table: "mechanic_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_section_runs_section_range",
                table: "mechanic_analysis_section_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_golden_claims_section_range",
                table: "mechanic_golden_claims",
                sql: "section BETWEEN 0 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_claims_section_range",
                table: "mechanic_claims",
                sql: "section BETWEEN 0 AND 8");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_section_runs_section_range",
                table: "mechanic_analysis_section_runs",
                sql: "section BETWEEN 0 AND 8");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_golden_claims_section_range",
                table: "mechanic_golden_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_claims_section_range",
                table: "mechanic_claims");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_section_runs_section_range",
                table: "mechanic_analysis_section_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_golden_claims_section_range",
                table: "mechanic_golden_claims",
                sql: "section BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_claims_section_range",
                table: "mechanic_claims",
                sql: "section BETWEEN 0 AND 5");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_section_runs_section_range",
                table: "mechanic_analysis_section_runs",
                sql: "section BETWEEN 0 AND 5");
        }
    }
}
