using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserIdAndLastMessageAtToChats : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastMessageAt",
                table: "chats",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "chats",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_chats_UserId_LastMessageAt",
                table: "chats",
                columns: new[] { "UserId", "LastMessageAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_chats_users_UserId",
                table: "chats",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_chats_users_UserId",
                table: "chats");

            migrationBuilder.DropIndex(
                name: "IX_chats_UserId_LastMessageAt",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "LastMessageAt",
                table: "chats");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "chats");
        }
    }
}
