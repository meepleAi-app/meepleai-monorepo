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
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ChatThreads",
                type: "text",
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<Guid>(
                name: "UserId",
                table: "ChatThreads",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

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
