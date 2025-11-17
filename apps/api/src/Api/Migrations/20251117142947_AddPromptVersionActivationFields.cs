using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptVersionActivationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ActivatedAt",
                table: "prompt_versions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActivatedByUserId",
                table: "prompt_versions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ActivationReason",
                table: "prompt_versions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeNotes",
                table: "prompt_versions",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ActivatedAt",
                table: "prompt_versions");

            migrationBuilder.DropColumn(
                name: "ActivatedByUserId",
                table: "prompt_versions");

            migrationBuilder.DropColumn(
                name: "ActivationReason",
                table: "prompt_versions");

            migrationBuilder.DropColumn(
                name: "ChangeNotes",
                table: "prompt_versions");
        }
    }
}
