using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameLabelsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Color = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    IsPredefined = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_game_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_game_labels_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_game_labels",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserLibraryEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    LabelId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_game_labels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_user_game_labels_game_labels_LabelId",
                        column: x => x.LabelId,
                        principalTable: "game_labels",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_game_labels_user_library_entries_UserLibraryEntryId",
                        column: x => x.UserLibraryEntryId,
                        principalTable: "user_library_entries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_IsPredefined",
                table: "game_labels",
                column: "IsPredefined");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_Name_Predefined",
                table: "game_labels",
                column: "Name",
                unique: true,
                filter: "is_predefined = true");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_UserId",
                table: "game_labels",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_GameLabels_UserId_Name",
                table: "game_labels",
                columns: new[] { "UserId", "Name" },
                unique: true,
                filter: "is_predefined = false");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameLabels_EntryId_LabelId",
                table: "user_game_labels",
                columns: new[] { "UserLibraryEntryId", "LabelId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserGameLabels_LabelId",
                table: "user_game_labels",
                column: "LabelId");

            migrationBuilder.CreateIndex(
                name: "IX_UserGameLabels_UserLibraryEntryId",
                table: "user_game_labels",
                column: "UserLibraryEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_game_labels");

            migrationBuilder.DropTable(
                name: "game_labels");
        }
    }
}
