using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AI08PageNumberExtraction : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_chat_logs_deleted_at",
                table: "chat_logs");

            migrationBuilder.DropIndex(
                name: "idx_chat_logs_user_id",
                table: "chat_logs");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_deleted_at",
                table: "chat_logs",
                column: "DeletedAt",
                filter: "\"DeletedAt\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_user_id",
                table: "chat_logs",
                column: "UserId",
                filter: "\"UserId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "idx_chat_logs_deleted_at",
                table: "chat_logs");

            migrationBuilder.DropIndex(
                name: "idx_chat_logs_user_id",
                table: "chat_logs");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_deleted_at",
                table: "chat_logs",
                column: "DeletedAt",
                filter: "deleted_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "idx_chat_logs_user_id",
                table: "chat_logs",
                column: "UserId",
                filter: "user_id IS NOT NULL");
        }
    }
}
