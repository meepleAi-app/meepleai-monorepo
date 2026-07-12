using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WidenMechanicSectionRunStatusRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_section_runs_status_range",
                table: "mechanic_analysis_section_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_section_runs_status_range",
                table: "mechanic_analysis_section_runs",
                sql: "status BETWEEN 0 AND 3");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_section_runs_status_range",
                table: "mechanic_analysis_section_runs");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_section_runs_status_range",
                table: "mechanic_analysis_section_runs",
                sql: "status BETWEEN 0 AND 2");
        }
    }
}
