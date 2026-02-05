using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionDeckAndCards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "session_tracking");

            migrationBuilder.CreateTable(
                name: "SessionDecks",
                schema: "session_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DeckType = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastShuffledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DrawPileJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    DiscardPileJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    HandsJson = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionDecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionDecks_session_tracking_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "session_tracking_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Cards",
                schema: "session_tracking",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SessionDeckId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ImageUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Suit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Value = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cards_SessionDecks_SessionDeckId",
                        column: x => x.SessionDeckId,
                        principalSchema: "session_tracking",
                        principalTable: "SessionDecks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SessionDeckId",
                schema: "session_tracking",
                table: "Cards",
                column: "SessionDeckId");

            migrationBuilder.CreateIndex(
                name: "IX_Cards_SortOrder",
                schema: "session_tracking",
                table: "Cards",
                column: "SortOrder");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDecks_IsDeleted",
                schema: "session_tracking",
                table: "SessionDecks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_SessionDecks_SessionId",
                schema: "session_tracking",
                table: "SessionDecks",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Cards",
                schema: "session_tracking");

            migrationBuilder.DropTable(
                name: "SessionDecks",
                schema: "session_tracking");
        }
    }
}
