using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLangDetectionToPhotoArtifact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "detected_source_lang",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts",
                type: "character varying(2)",
                maxLength: 2,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "lang_detection_confidence",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "detected_source_lang",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts");

            migrationBuilder.DropColumn(
                name: "lang_detection_confidence",
                schema: "session_tracking",
                table: "gamebook_photo_artifacts");
        }
    }
}
