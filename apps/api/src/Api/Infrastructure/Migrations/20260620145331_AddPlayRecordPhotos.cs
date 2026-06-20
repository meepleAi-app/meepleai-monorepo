using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayRecordPhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "play_record_photos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayRecordId = table.Column<Guid>(type: "uuid", nullable: false),
                    BlobUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    ThumbnailUrl = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    FileSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256Hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    OcrText = table.Column<string>(type: "text", nullable: true),
                    OcrConfidence = table.Column<double>(type: "double precision", nullable: true),
                    Caption = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_play_record_photos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_play_record_photos_play_records_PlayRecordId",
                        column: x => x.PlayRecordId,
                        principalTable: "play_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_play_record_photos_PlayRecordId",
                table: "play_record_photos",
                column: "PlayRecordId");

            migrationBuilder.CreateIndex(
                name: "UX_play_record_photos_playrecord_sha256",
                table: "play_record_photos",
                columns: new[] { "PlayRecordId", "Sha256Hash" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "play_record_photos");
        }
    }
}
