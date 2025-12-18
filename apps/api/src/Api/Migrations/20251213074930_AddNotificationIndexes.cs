using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    internal partial class AddNotificationIndexes : Migration
    {
        private static readonly string[] UserIdCreatedAtColumns = new[] { "user_id", "created_at" };
        private static readonly bool[] UserIdCreatedAtDescending = new[] { false, true };
        private static readonly string[] UserIdIsReadColumns = new[] { "user_id", "is_read" };
        private static readonly string[] UserIdIsReadCreatedAtColumns = new[] { "user_id", "is_read", "created_at" };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_created_at",
                table: "notifications",
                columns: UserIdCreatedAtColumns,
                descending: UserIdCreatedAtDescending);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_is_read",
                table: "notifications",
                columns: UserIdIsReadColumns);

            migrationBuilder.CreateIndex(
                name: "IX_notifications_user_id_is_read_created_at",
                table: "notifications",
                columns: UserIdIsReadCreatedAtColumns,
                filter: "is_read = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_notifications_user_id_created_at",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_user_id_is_read",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "IX_notifications_user_id_is_read_created_at",
                table: "notifications");
        }
    }
}
