using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPrivatePdfToUserLibraryEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PrivatePdfId",
                table: "user_library_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserLibraryEntries_PrivatePdfId",
                table: "user_library_entries",
                column: "PrivatePdfId");

            migrationBuilder.AddForeignKey(
                name: "FK_user_library_entries_pdf_documents_PrivatePdfId",
                table: "user_library_entries",
                column: "PrivatePdfId",
                principalTable: "pdf_documents",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_user_library_entries_pdf_documents_PrivatePdfId",
                table: "user_library_entries");

            migrationBuilder.DropIndex(
                name: "IX_UserLibraryEntries_PrivatePdfId",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "PrivatePdfId",
                table: "user_library_entries");
        }
    }
}
