using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Combined migration for BGAI-039 and BGAI-041:
    /// 1. BGAI-039: Add validation_accuracy_baselines table for tracking validation system accuracy
    /// 2. BGAI-041: Update prompt_evaluation_results with 5-metric quality framework
    ///    - Remove: hallucination_rate, avg_confidence, citation_correctness, avg_latency_ms
    ///    - Add: relevance, completeness, clarity, citation_quality
    /// </summary>
    public partial class AddValidationBaselineAndUpdate5MetricFramework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // BGAI-039: Create validation_accuracy_baselines table
            migrationBuilder.CreateTable(
                name: "validation_accuracy_baselines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    context = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dataset_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    evaluation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    measured_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    true_positives = table.Column<int>(type: "integer", nullable: false),
                    true_negatives = table.Column<int>(type: "integer", nullable: false),
                    false_positives = table.Column<int>(type: "integer", nullable: false),
                    false_negatives = table.Column<int>(type: "integer", nullable: false),
                    total_cases = table.Column<int>(type: "integer", nullable: false),
                    precision = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    recall = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    f1_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    accuracy = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    specificity = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    matthews_correlation = table.Column<decimal>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
                    meets_baseline = table.Column<bool>(type: "boolean", nullable: false),
                    quality_level = table.Column<int>(type: "integer", nullable: false),
                    summary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    recommendations_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_validation_accuracy_baselines", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_context",
                table: "validation_accuracy_baselines",
                column: "context");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_dataset_id",
                table: "validation_accuracy_baselines",
                column: "dataset_id");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_measured_at",
                table: "validation_accuracy_baselines",
                column: "measured_at");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_accuracy",
                table: "validation_accuracy_baselines",
                column: "accuracy");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_meets_baseline",
                table: "validation_accuracy_baselines",
                column: "meets_baseline");

            migrationBuilder.CreateIndex(
                name: "ix_validation_accuracy_baselines_evaluation_id",
                table: "validation_accuracy_baselines",
                column: "evaluation_id");

            // BGAI-041: Drop old metric columns from prompt_evaluation_results
            migrationBuilder.DropColumn(
                name: "hallucination_rate",
                table: "prompt_evaluation_results");

            migrationBuilder.DropColumn(
                name: "avg_confidence",
                table: "prompt_evaluation_results");

            migrationBuilder.DropColumn(
                name: "citation_correctness",
                table: "prompt_evaluation_results");

            migrationBuilder.DropColumn(
                name: "avg_latency_ms",
                table: "prompt_evaluation_results");

            // BGAI-041: Add new metric columns for 5-metric framework
            migrationBuilder.AddColumn<double>(
                name: "relevance",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "completeness",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "clarity",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "citation_quality",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // BGAI-041: Reverse prompt_evaluation_results changes
            migrationBuilder.DropColumn(
                name: "relevance",
                table: "prompt_evaluation_results");

            migrationBuilder.DropColumn(
                name: "completeness",
                table: "prompt_evaluation_results");

            migrationBuilder.DropColumn(
                name: "clarity",
                table: "prompt_evaluation_results");

            migrationBuilder.DropColumn(
                name: "citation_quality",
                table: "prompt_evaluation_results");

            migrationBuilder.AddColumn<double>(
                name: "hallucination_rate",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "avg_confidence",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "citation_correctness",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "avg_latency_ms",
                table: "prompt_evaluation_results",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            // BGAI-039: Drop validation_accuracy_baselines table
            migrationBuilder.DropTable(
                name: "validation_accuracy_baselines");
        }
    }
}
