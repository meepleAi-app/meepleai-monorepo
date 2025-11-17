using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// BGAI-039: Add validation accuracy baseline tracking table
    /// Measures how accurately the validation system identifies correct vs. incorrect responses
    /// Target: >= 80% accuracy baseline
    /// </summary>
    public partial class BGAI039_AddValidationAccuracyBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                    precision = table.Column<double>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    recall = table.Column<double>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    f1_score = table.Column<double>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    accuracy = table.Column<double>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    specificity = table.Column<double>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    matthews_correlation = table.Column<double>(type: "numeric(6,4)", precision: 6, scale: 4, nullable: false),
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "validation_accuracy_baselines");
        }
    }
}
