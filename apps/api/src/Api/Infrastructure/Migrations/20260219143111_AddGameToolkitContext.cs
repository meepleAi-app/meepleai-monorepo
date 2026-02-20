using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameToolkitContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GameToolkits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DiceToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CardToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    TimerToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    CounterToolsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ScoringTemplateJson = table.Column<string>(type: "jsonb", nullable: true),
                    TurnTemplateJson = table.Column<string>(type: "jsonb", nullable: true),
                    StateTemplate = table.Column<string>(type: "jsonb", nullable: true),
                    AgentConfig = table.Column<string>(type: "jsonb", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GameToolkits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GameToolkits_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_GameId",
                table: "GameToolkits",
                column: "GameId");

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_GameId_Version",
                table: "GameToolkits",
                columns: new[] { "GameId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GameToolkits_IsPublished",
                table: "GameToolkits",
                column: "IsPublished");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GameToolkits");
        }
    }
}
