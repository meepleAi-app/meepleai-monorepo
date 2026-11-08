using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLiveSessionIdToChatThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LiveSessionId",
                table: "ChatThreads",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatThreads_LiveSessionId",
                table: "ChatThreads",
                column: "LiveSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ChatThreads_LiveSessionId",
                table: "ChatThreads");

            migrationBuilder.DropColumn(
                name: "LiveSessionId",
                table: "ChatThreads");
        }
    }
}
