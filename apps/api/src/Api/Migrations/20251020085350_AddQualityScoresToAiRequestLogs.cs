using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddQualityScoresToAiRequestLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "CitationQuality",
                table: "ai_request_logs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsLowQuality",
                table: "ai_request_logs",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "LlmConfidence",
                table: "ai_request_logs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OverallConfidence",
                table: "ai_request_logs",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RagConfidence",
                table: "ai_request_logs",
                type: "double precision",
                nullable: true);

            // Create indexes for efficient querying
            migrationBuilder.CreateIndex(
                name: "ix_ai_request_logs_is_low_quality",
                table: "ai_request_logs",
                column: "IsLowQuality");

            migrationBuilder.CreateIndex(
                name: "ix_ai_request_logs_low_quality_created_at",
                table: "ai_request_logs",
                columns: new[] { "IsLowQuality", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes first
            migrationBuilder.DropIndex(
                name: "ix_ai_request_logs_low_quality_created_at",
                table: "ai_request_logs");

            migrationBuilder.DropIndex(
                name: "ix_ai_request_logs_is_low_quality",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "CitationQuality",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "IsLowQuality",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "LlmConfidence",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "OverallConfidence",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "RagConfidence",
                table: "ai_request_logs");
        }
    }
}
