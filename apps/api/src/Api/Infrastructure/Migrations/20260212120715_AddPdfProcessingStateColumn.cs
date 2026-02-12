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

            // Step 2: Migrate existing data from ProcessingStatus to processing_state
            // Only if ProcessingStatus column exists (it might not in test databases)
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT FROM information_schema.columns
                        WHERE table_name = 'pdf_documents'
                        AND column_name = 'processing_status'
                    ) THEN
                        UPDATE pdf_documents SET processing_state =
                            CASE processing_status
                                WHEN 'pending' THEN 'Pending'
                                WHEN 'processing' THEN 'Extracting'
                                WHEN 'completed' THEN 'Ready'
                                WHEN 'failed' THEN 'Failed'
                                ELSE 'Pending'
                            END;
                    ELSE
                        -- Column doesn't exist, set all to default
                        UPDATE pdf_documents SET processing_state = 'Pending';
                    END IF;
                END $$;
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Issue #4215: Rollback processing_state column
            migrationBuilder.DropColumn(
                name: "processing_state",
                table: "pdf_documents");
        }
    }
}
