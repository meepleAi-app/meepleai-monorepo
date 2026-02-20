using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChatSessionAgentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "agent_id",
                table: "chat_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_name",
                table: "chat_sessions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "agent_type",
                table: "chat_sessions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // Partial indexes: agent_id is nullable, so filter to non-NULL rows for efficiency
            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions",
                column: "agent_id",
                filter: "agent_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions",
                columns: new[] { "user_id", "agent_id" },
                filter: "agent_id IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_agent_id",
                table: "chat_sessions");

            migrationBuilder.DropIndex(
                name: "ix_chat_sessions_user_agent_id",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "agent_id",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "agent_name",
                table: "chat_sessions");

            migrationBuilder.DropColumn(
                name: "agent_type",
                table: "chat_sessions");
        }
    }
}
