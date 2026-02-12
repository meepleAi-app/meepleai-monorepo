using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPdfProcessingStateColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Issue #4215: Add granular processing state tracking

            // Step 1: Add new processing_state column (nullable initially)
            migrationBuilder.AddColumn<string>(
                name: "processing_state",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Step 2: Migrate existing data from processing_status to processing_state
            migrationBuilder.Sql(@"
                UPDATE pdf_documents SET processing_state =
                    CASE processing_status
                        WHEN 'pending' THEN 'Pending'
                        WHEN 'processing' THEN 'Extracting'
                        WHEN 'completed' THEN 'Ready'
                        WHEN 'failed' THEN 'Failed'
                        ELSE 'Pending'
                    END;
            ");

            // Step 3: Make processing_state NOT NULL with default
            migrationBuilder.AlterColumn<string>(
                name: "processing_state",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            // Note: Keep processing_status column for backward compatibility
            // Will be removed in future migration after full transition

            // Issue #4216: Add retry mechanism tracking columns
            migrationBuilder.AddColumn<int>(
                name: "retry_count",
                table: "pdf_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "error_category",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "failed_at_state",
                table: "pdf_documents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Issue #4216: Rollback retry tracking columns
            migrationBuilder.DropColumn(
                name: "retry_count",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "error_category",
                table: "pdf_documents");

            migrationBuilder.DropColumn(
                name: "failed_at_state",
                table: "pdf_documents");

            // Issue #4215: Rollback processing_state column
            migrationBuilder.DropColumn(
                name: "processing_state",
                table: "pdf_documents");
        }
    }
}
