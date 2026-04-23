using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class M1_2_MechanicAnalysisSectionRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mechanic_analysis_section_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    analysis_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section = table.Column<int>(type: "integer", nullable: false),
                    run_order = table.Column<int>(type: "integer", nullable: false),
                    provider = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model_used = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    prompt_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    completion_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    total_tokens = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    estimated_cost_usd = table.Column<decimal>(type: "numeric(12,6)", nullable: false, defaultValue: 0m),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    started_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mechanic_analysis_section_runs", x => x.id);
                    table.CheckConstraint("ck_mechanic_section_runs_cost_non_negative", "estimated_cost_usd >= 0");
                    table.CheckConstraint("ck_mechanic_section_runs_error_when_failed", "status <> 1 OR error_message IS NOT NULL");
                    table.CheckConstraint("ck_mechanic_section_runs_latency_non_negative", "latency_ms >= 0");
                    table.CheckConstraint("ck_mechanic_section_runs_section_range", "section BETWEEN 0 AND 5");
                    table.CheckConstraint("ck_mechanic_section_runs_status_range", "status BETWEEN 0 AND 2");
                    table.CheckConstraint("ck_mechanic_section_runs_tokens_non_negative", "prompt_tokens >= 0 AND completion_tokens >= 0 AND total_tokens >= 0");
                    table.ForeignKey(
                        name: "FK_mechanic_analysis_section_runs_mechanic_analyses_analysis_id",
                        column: x => x.analysis_id,
                        principalTable: "mechanic_analyses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_section_runs_analysis_id",
                table: "mechanic_analysis_section_runs",
                column: "analysis_id");

            migrationBuilder.CreateIndex(
                name: "ix_mechanic_section_runs_provider",
                table: "mechanic_analysis_section_runs",
                column: "provider");

            migrationBuilder.CreateIndex(
                name: "ux_mechanic_section_runs_analysis_run_order",
                table: "mechanic_analysis_section_runs",
                columns: new[] { "analysis_id", "run_order" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mechanic_analysis_section_runs");
        }
    }
}
