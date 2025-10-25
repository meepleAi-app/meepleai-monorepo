using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAnalyticsIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ADMIN-02: Add indexes for analytics dashboard performance optimization
            // These indexes improve query performance for date range aggregations in AdminStatsService

            migrationBuilder.CreateIndex(
                name: "idx_users_created_at",
                table: "users",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_user_sessions_created_at",
                table: "user_sessions",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_pdf_documents_uploaded_at",
                table: "pdf_documents",
                column: "uploaded_at");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_created_at",
                table: "chat_logs",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "idx_ai_request_logs_created_at",
                table: "ai_request_logs",
                column: "created_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_ai_request_logs_created_at",
                table: "ai_request_logs");

            migrationBuilder.DropIndex(
                name: "idx_chat_logs_created_at",
                table: "chat_logs");

            migrationBuilder.DropIndex(
                name: "idx_pdf_documents_uploaded_at",
                table: "pdf_documents");

            migrationBuilder.DropIndex(
                name: "idx_user_sessions_created_at",
                table: "user_sessions");

            migrationBuilder.DropIndex(
                name: "idx_users_created_at",
                table: "users");
        }
    }
}
