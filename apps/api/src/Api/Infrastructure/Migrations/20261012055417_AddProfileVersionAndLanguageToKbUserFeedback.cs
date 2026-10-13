using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProfileVersionAndLanguageToKbUserFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "language",
                schema: "knowledge_base",
                table: "kb_user_feedback",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "profile_version_id",
                schema: "knowledge_base",
                table: "kb_user_feedback",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "language",
                schema: "knowledge_base",
                table: "kb_user_feedback");

            migrationBuilder.DropColumn(
                name: "profile_version_id",
                schema: "knowledge_base",
                table: "kb_user_feedback");
        }
    }
}
