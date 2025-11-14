using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdAndStatusToChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add Status column (always safe, has default value)
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChatThreads",
                type: "text",
                nullable: false,
                defaultValue: "active");

            // Step 1: Add UserId as NULLABLE initially (safe for existing rows)
            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ChatThreads",
                type: "uuid",
                nullable: true, // Nullable during migration
                defaultValue: null);

            // Step 2: Backfill existing rows with first admin user
            // This SQL finds the first admin user and assigns all orphaned threads to them
            migrationBuilder.Sql(@"
                UPDATE ""ChatThreads""
                SET ""UserId"" = (
                    SELECT ""Id""
                    FROM ""users""
                    WHERE ""Role"" = 'Admin'
                    ORDER BY ""CreatedAt""
                    LIMIT 1
                )
                WHERE ""UserId"" IS NULL;
            ");

            // Step 3: Make UserId NOT NULL after backfill
            migrationBuilder.AlterColumn<Guid>(
                name: "UserId",
                table: "ChatThreads",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            // Step 4: Add index and FK constraint (safe now that all rows have valid UserId)
            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatThreads_users_UserId",
                table: "ChatThreads",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatThreads_users_UserId",
                table: "ChatThreads");

            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_UserId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "ChatThreads");
        }
    }
}
