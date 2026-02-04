using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentApprovalStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "approval_notes",
                table: "shared_game_documents",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "approval_status",
                table: "shared_game_documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "shared_game_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "shared_game_documents",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "approval_notes",
                table: "shared_game_documents");

            migrationBuilder.DropColumn(
                name: "approval_status",
                table: "shared_game_documents");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "shared_game_documents");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "shared_game_documents");
        }
    }
}
