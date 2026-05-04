using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhotoIngestion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_photo_batch_pages_photo_batch_uploads_PhotoBatchUploadId",
                table: "photo_batch_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_photo_batch_uploads_users_UserId",
                table: "photo_batch_uploads");

            migrationBuilder.RenameColumn(
                name: "Status",
                table: "photo_batch_uploads",
                newName: "status");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "photo_batch_uploads",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "photo_batch_uploads",
                newName: "user_id");

            migrationBuilder.RenameColumn(
                name: "TotalPages",
                table: "photo_batch_uploads",
                newName: "total_pages");

            migrationBuilder.RenameColumn(
                name: "SourceLanguage",
                table: "photo_batch_uploads",
                newName: "source_language");

            migrationBuilder.RenameColumn(
                name: "RowVersion",
                table: "photo_batch_uploads",
                newName: "row_version");

            migrationBuilder.RenameColumn(
                name: "IsDeleted",
                table: "photo_batch_uploads",
                newName: "is_deleted");

            migrationBuilder.RenameColumn(
                name: "IndexedPages",
                table: "photo_batch_uploads",
                newName: "indexed_pages");

            migrationBuilder.RenameColumn(
                name: "GameId",
                table: "photo_batch_uploads",
                newName: "game_id");

            migrationBuilder.RenameColumn(
                name: "DeletedAt",
                table: "photo_batch_uploads",
                newName: "deleted_at");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "photo_batch_uploads",
                newName: "created_at");

            migrationBuilder.RenameColumn(
                name: "CompletedAt",
                table: "photo_batch_uploads",
                newName: "completed_at");

            migrationBuilder.RenameColumn(
                name: "Warnings",
                table: "photo_batch_pages",
                newName: "warnings");

            migrationBuilder.RenameColumn(
                name: "Orientation",
                table: "photo_batch_pages",
                newName: "orientation");

            migrationBuilder.RenameColumn(
                name: "Confidence",
                table: "photo_batch_pages",
                newName: "confidence");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "photo_batch_pages",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "PhotoBatchUploadId",
                table: "photo_batch_pages",
                newName: "photo_batch_upload_id");

            migrationBuilder.RenameColumn(
                name: "PageNumber",
                table: "photo_batch_pages",
                newName: "page_number");

            migrationBuilder.RenameColumn(
                name: "IsBlank",
                table: "photo_batch_pages",
                newName: "is_blank");

            migrationBuilder.RenameColumn(
                name: "IndexedAt",
                table: "photo_batch_pages",
                newName: "indexed_at");

            migrationBuilder.RenameColumn(
                name: "ExtractedText",
                table: "photo_batch_pages",
                newName: "extracted_text");

            migrationBuilder.RenameColumn(
                name: "ConfidenceLevel",
                table: "photo_batch_pages",
                newName: "confidence_level");

            migrationBuilder.RenameColumn(
                name: "BlobKey",
                table: "photo_batch_pages",
                newName: "blob_key");

            migrationBuilder.AddColumn<string>(
                name: "failure_reason",
                table: "photo_batch_uploads",
                type: "text",
                nullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_photo_batch_pages_photo_batch_uploads_photo_batch_upload_id",
                table: "photo_batch_pages",
                column: "photo_batch_upload_id",
                principalTable: "photo_batch_uploads",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_photo_batch_uploads_users_user_id",
                table: "photo_batch_uploads",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_photo_batch_pages_photo_batch_uploads_photo_batch_upload_id",
                table: "photo_batch_pages");

            migrationBuilder.DropForeignKey(
                name: "FK_photo_batch_uploads_users_user_id",
                table: "photo_batch_uploads");

            migrationBuilder.DropColumn(
                name: "failure_reason",
                table: "photo_batch_uploads");

            migrationBuilder.RenameColumn(
                name: "status",
                table: "photo_batch_uploads",
                newName: "Status");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "photo_batch_uploads",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "user_id",
                table: "photo_batch_uploads",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "total_pages",
                table: "photo_batch_uploads",
                newName: "TotalPages");

            migrationBuilder.RenameColumn(
                name: "source_language",
                table: "photo_batch_uploads",
                newName: "SourceLanguage");

            migrationBuilder.RenameColumn(
                name: "row_version",
                table: "photo_batch_uploads",
                newName: "RowVersion");

            migrationBuilder.RenameColumn(
                name: "is_deleted",
                table: "photo_batch_uploads",
                newName: "IsDeleted");

            migrationBuilder.RenameColumn(
                name: "indexed_pages",
                table: "photo_batch_uploads",
                newName: "IndexedPages");

            migrationBuilder.RenameColumn(
                name: "game_id",
                table: "photo_batch_uploads",
                newName: "GameId");

            migrationBuilder.RenameColumn(
                name: "deleted_at",
                table: "photo_batch_uploads",
                newName: "DeletedAt");

            migrationBuilder.RenameColumn(
                name: "created_at",
                table: "photo_batch_uploads",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "completed_at",
                table: "photo_batch_uploads",
                newName: "CompletedAt");

            migrationBuilder.RenameColumn(
                name: "warnings",
                table: "photo_batch_pages",
                newName: "Warnings");

            migrationBuilder.RenameColumn(
                name: "orientation",
                table: "photo_batch_pages",
                newName: "Orientation");

            migrationBuilder.RenameColumn(
                name: "confidence",
                table: "photo_batch_pages",
                newName: "Confidence");

            migrationBuilder.RenameColumn(
                name: "id",
                table: "photo_batch_pages",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "photo_batch_upload_id",
                table: "photo_batch_pages",
                newName: "PhotoBatchUploadId");

            migrationBuilder.RenameColumn(
                name: "page_number",
                table: "photo_batch_pages",
                newName: "PageNumber");

            migrationBuilder.RenameColumn(
                name: "is_blank",
                table: "photo_batch_pages",
                newName: "IsBlank");

            migrationBuilder.RenameColumn(
                name: "indexed_at",
                table: "photo_batch_pages",
                newName: "IndexedAt");

            migrationBuilder.RenameColumn(
                name: "extracted_text",
                table: "photo_batch_pages",
                newName: "ExtractedText");

            migrationBuilder.RenameColumn(
                name: "confidence_level",
                table: "photo_batch_pages",
                newName: "ConfidenceLevel");

            migrationBuilder.RenameColumn(
                name: "blob_key",
                table: "photo_batch_pages",
                newName: "BlobKey");

            migrationBuilder.AddForeignKey(
                name: "FK_photo_batch_pages_photo_batch_uploads_PhotoBatchUploadId",
                table: "photo_batch_pages",
                column: "PhotoBatchUploadId",
                principalTable: "photo_batch_uploads",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_photo_batch_uploads_users_UserId",
                table: "photo_batch_uploads",
                column: "UserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
