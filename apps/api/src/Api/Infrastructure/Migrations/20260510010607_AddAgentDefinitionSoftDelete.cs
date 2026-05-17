using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentDefinitionSoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Limits_RaptorRebuildEnabled",
                table: "tier_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_deleted",
                schema: "knowledge_base",
                table: "agent_definitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Limits_RaptorRebuildEnabled",
                table: "tier_definitions");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "knowledge_base",
                table: "agent_definitions");

            migrationBuilder.DropColumn(
                name: "is_deleted",
                schema: "knowledge_base",
                table: "agent_definitions");
        }
    }
}
