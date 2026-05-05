using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M2_2_ExpandMechanicAnalysisStatusRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_status_audit_status_range",
                table: "mechanic_status_audit");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_analyses_status_range",
                table: "mechanic_analyses");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_status_audit_status_range",
                table: "mechanic_status_audit",
                sql: "from_status BETWEEN 0 AND 4 AND to_status BETWEEN 0 AND 4");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_analyses_status_range",
                table: "mechanic_analyses",
                sql: "status BETWEEN 0 AND 4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_status_audit_status_range",
                table: "mechanic_status_audit");

            migrationBuilder.DropCheckConstraint(
                name: "ck_mechanic_analyses_status_range",
                table: "mechanic_analyses");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_status_audit_status_range",
                table: "mechanic_status_audit",
                sql: "from_status BETWEEN 0 AND 3 AND to_status BETWEEN 0 AND 3");

            migrationBuilder.AddCheckConstraint(
                name: "ck_mechanic_analyses_status_range",
                table: "mechanic_analyses",
                sql: "status BETWEEN 0 AND 3");
        }
    }
}
