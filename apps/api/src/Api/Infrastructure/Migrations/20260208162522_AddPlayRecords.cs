using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Prefer 'static readonly' fields
#pragma warning disable CA1825 // Avoid zero-length array allocations
#pragma warning disable MA0005 // Use Array.Empty<T>()

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "play_records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GameId = table.Column<Guid>(type: "uuid", nullable: true),
                    GameName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Visibility = table.Column<int>(type: "integer", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: true),
                    SessionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "interval", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Location = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ScoringConfigJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_records", x => x.Id);
                    table.ForeignKey(
                        name: "FK_play_records_games_GameId",
                        column: x => x.GameId,
                        principalTable: "games",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_play_records_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "record_players",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    DisplayName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_players", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_players_play_records_PlayRecordId",
                        column: x => x.PlayRecordId,
                        principalTable: "play_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_record_players_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "record_scores",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecordPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Dimension = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Value = table.Column<int>(type: "integer", nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_scores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_scores_record_players_RecordPlayerId",
                        column: x => x.RecordPlayerId,
                        principalTable: "record_players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_CreatedByUserId",
                table: "play_records",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_GameId",
                table: "play_records",
                column: "GameId",
                filter: "game_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_SessionDate",
                table: "play_records",
                column: "SessionDate",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_PlayRecords_Status",
                table: "play_records",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecordPlayers_PlayRecordId",
                table: "record_players",
                column: "PlayRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_RecordPlayers_UserId",
                table: "record_players",
                column: "UserId",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RecordScores_Player_Dimension_Unique",
                table: "record_scores",
                columns: new[] { "RecordPlayerId", "Dimension" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "record_scores");

            migrationBuilder.DropTable(
                name: "record_players");

            migrationBuilder.DropTable(
                name: "play_records");
        }
    }
}
