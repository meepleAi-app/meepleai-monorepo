using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Prefer static readonly fields for constant arrays in migrations

namespace Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserGameStateAndSessionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AvgDuration",
                table: "user_library_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompetitiveSessions",
                table: "user_library_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "CurrentState",
                table: "user_library_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastPlayed",
                table: "user_library_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "user_library_entries",
                type: "bytea",
                rowVersion: true,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StateChangedAt",
                table: "user_library_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StateNotes",
                table: "user_library_entries",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "TimesPlayed",
                table: "user_library_entries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "WinRate",
                table: "user_library_entries",
                type: "numeric(5,2)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_checklists",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLibraryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Order = table.Column<int>(type: "integer", nullable: false),
                    IsCompleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AdditionalInfo = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_checklists", x => x.Id);
                    table.CheckConstraint("chk_game_checklists_order", "\"order\" >= 0");
                    table.ForeignKey(
                        name: "FK_game_checklists_user_library_entries_UserLibraryEntryId",
                        column: x => x.UserLibraryEntryId,
                        principalTable: "user_library_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "game_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLibraryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    DidWin = table.Column<bool>(type: "boolean", nullable: true),
                    Players = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "NOW()"),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_sessions", x => x.Id);
                    table.CheckConstraint("chk_game_sessions_duration", "duration_minutes > 0");
                    table.ForeignKey(
                        name: "FK_game_sessions_user_library_entries_UserLibraryEntryId",
                        column: x => x.UserLibraryEntryId,
                        principalTable: "user_library_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_CurrentState",
                table: "user_library_entries",
                column: "CurrentState");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_LastPlayed",
                table: "user_library_entries",
                column: "LastPlayed");

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_UserId_CurrentState",
                table: "user_library_entries",
                columns: new[] { "UserId", "CurrentState" });

            migrationBuilder.CreateIndex(
                name: "ix_game_checklists_entry_order",
                table: "game_checklists",
                columns: new[] { "UserLibraryEntryId", "Order" });

            migrationBuilder.CreateIndex(
                name: "ix_game_checklists_user_library_entry_id",
                table: "game_checklists",
                column: "UserLibraryEntryId");

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_entry_played",
                table: "game_sessions",
                columns: new[] { "UserLibraryEntryId", "PlayedAt" });

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_played_at",
                table: "game_sessions",
                column: "PlayedAt");

            migrationBuilder.CreateIndex(
                name: "ix_game_sessions_user_library_entry_id",
                table: "game_sessions",
                column: "UserLibraryEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "game_checklists");

            migrationBuilder.DropTable(
                name: "game_sessions");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_CurrentState",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_LastPlayed",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_UserId_CurrentState",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "AvgDuration",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CompetitiveSessions",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "CurrentState",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "LastPlayed",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "StateChangedAt",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "StateNotes",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "TimesPlayed",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "WinRate",
                table: "user_library_entries");
        }
    }
}
