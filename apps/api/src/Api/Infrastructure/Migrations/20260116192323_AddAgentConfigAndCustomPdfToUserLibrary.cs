using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentConfigAndCustomPdfToUserLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomAgentConfigJson",
                table: "user_library_entries",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CustomPdfFileSizeBytes",
                table: "user_library_entries",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomPdfOriginalFileName",
                table: "user_library_entries",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CustomPdfUploadedAt",
                table: "user_library_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomPdfUrl",
                table: "user_library_entries",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_CustomAgentConfigJson",
                table: "user_library_entries",
                column: "CustomAgentConfigJson")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_CustomAgentConfigJson",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CustomAgentConfigJson",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CustomPdfFileSizeBytes",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CustomPdfOriginalFileName",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CustomPdfUploadedAt",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CustomPdfUrl",
                table: "user_library_entries");
        }
    }
}
