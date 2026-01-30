using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AgentId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameSessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    TypologyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentGameStateJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_agent_sessions_GameSessions_GameSessionId",
                        column: x => x.GameSessionId,
                        principalTable: "GameSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_agent_sessions_agent_typologies_TypologyId",
                        column: x => x.TypologyId,
                        principalTable: "agent_typologies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_agents_AgentId",
                        column: x => x.AgentId,
                        principalTable: "agents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_agent_sessions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_AgentId",
                table: "agent_sessions",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_GameId",
                table: "agent_sessions",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_agent_sessions_TypologyId",
                table: "agent_sessions",
                column: "TypologyId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_GameSessionId",
                table: "agent_sessions",
                column: "GameSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_GameSessionId_UserId_Unique",
                table: "agent_sessions",
                columns: new[] { "GameSessionId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_IsActive",
                table: "agent_sessions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_UserId",
                table: "agent_sessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AgentSessions_UserId_IsActive",
                table: "agent_sessions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_sessions");
        }
    }
}
