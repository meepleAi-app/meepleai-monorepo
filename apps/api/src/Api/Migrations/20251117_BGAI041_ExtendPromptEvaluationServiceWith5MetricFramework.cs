using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// BGAI-041: Extend PromptEvaluationService with 5-metric quality framework
    /// Replaces old metrics (HallucinationRate, AvgConfidence, CitationCorrectness, AvgLatencyMs)
    /// with new metrics (Relevance, Completeness, Clarity, CitationQuality)
    /// Accuracy metric is retained and enhanced
    /// </summary>
    public partial class BGAI041_ExtendPromptEvaluationServiceWith5MetricFramework : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old metric columns
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

            // Add new metric columns for 5-metric framework
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

            // Note: accuracy column remains unchanged (already exists)
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop new metric columns
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

            // Restore old metric columns
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
        }
    }
}
