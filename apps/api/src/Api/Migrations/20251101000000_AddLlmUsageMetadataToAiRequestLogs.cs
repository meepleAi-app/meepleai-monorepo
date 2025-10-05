using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLlmUsageMetadataToAiRequestLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE ai_request_logs SET \"TokenCount\" = 0 WHERE \"TokenCount\" IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "TokenCount",
                table: "ai_request_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletionTokens",
                table: "ai_request_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FinishReason",
                table: "ai_request_logs",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "ai_request_logs",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PromptTokens",
                table: "ai_request_logs",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletionTokens",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "FinishReason",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "ai_request_logs");

            migrationBuilder.DropColumn(
                name: "PromptTokens",
                table: "ai_request_logs");

            migrationBuilder.AlterColumn<int>(
                name: "TokenCount",
                table: "ai_request_logs",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
