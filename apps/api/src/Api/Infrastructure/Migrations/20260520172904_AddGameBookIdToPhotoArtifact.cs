using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGameBookIdToPhotoArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // C4 (2026-05-19): GameBookId NOT NULL. Default = Guid.Empty is acceptable here
            // because the gamebook_photo_artifacts table is empty in dev/staging (this is
            // in-progress Phase C work, pre-Phase-E client wiring — no production data exists).
            // If any row sneaks in with Guid.Empty, downstream consumers reading game_book_id
            // will surface failures loudly rather than silently degrading.
            migrationBuilder.AddColumn<Guid>(
                name: "game_book_id",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "game_book_id",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts");
        }
    }
}
