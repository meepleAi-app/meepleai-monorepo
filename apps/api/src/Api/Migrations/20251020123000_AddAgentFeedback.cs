using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentFeedback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_feedback",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MessageId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Endpoint = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    GameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_feedback", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_Endpoint",
                table: "agent_feedback",
                column: "Endpoint");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_GameId",
                table: "agent_feedback",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_UserId",
                table: "agent_feedback",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_feedback_MessageId_UserId",
                table: "agent_feedback",
                columns: new[] { "MessageId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_feedback");
        }
    }
}
