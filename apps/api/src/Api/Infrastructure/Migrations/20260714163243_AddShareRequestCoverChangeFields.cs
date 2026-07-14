using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShareRequestCoverChangeFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cover_page_index",
                table: "share_requests",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "pending_cover_r2_key",
                table: "share_requests",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "source_pdf_document_id",
                table: "share_requests",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cover_page_index",
                table: "share_requests");

            migrationBuilder.DropColumn(
                name: "pending_cover_r2_key",
                table: "share_requests");

            migrationBuilder.DropColumn(
                name: "source_pdf_document_id",
                table: "share_requests");
        }
    }
}
