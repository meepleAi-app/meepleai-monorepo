using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeIsPublishedComputed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PublishedAt",
                table: "games",
                newName: "published_at");

            migrationBuilder.RenameColumn(
                name: "IsPublished",
                table: "games",
                newName: "is_published");

            migrationBuilder.RenameColumn(
                name: "ImageUrl",
                table: "games",
                newName: "image_url");

            migrationBuilder.RenameColumn(
                name: "IconUrl",
                table: "games",
                newName: "icon_url");

            migrationBuilder.RenameColumn(
                name: "ApprovalStatus",
                table: "games",
                newName: "approval_status");

            migrationBuilder.AlterColumn<string>(
                name: "image_url",
                table: "games",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "icon_url",
                table: "games",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "approval_status",
                table: "games",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<bool>(
                name: "is_published",
                table: "games",
                type: "boolean",
                nullable: false,
                computedColumnSql: "(approval_status = 2 AND published_at IS NOT NULL)",
                stored: true,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "published_at",
                table: "games",
                newName: "PublishedAt");

            migrationBuilder.RenameColumn(
                name: "is_published",
                table: "games",
                newName: "IsPublished");

            migrationBuilder.RenameColumn(
                name: "image_url",
                table: "games",
                newName: "ImageUrl");

            migrationBuilder.RenameColumn(
                name: "icon_url",
                table: "games",
                newName: "IconUrl");

            migrationBuilder.RenameColumn(
                name: "approval_status",
                table: "games",
                newName: "ApprovalStatus");

            migrationBuilder.AlterColumn<bool>(
                name: "IsPublished",
                table: "games",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldComputedColumnSql: "(approval_status = 2 AND published_at IS NOT NULL)");

            migrationBuilder.AlterColumn<string>(
                name: "ImageUrl",
                table: "games",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "IconUrl",
                table: "games",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ApprovalStatus",
                table: "games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 0);
        }
    }
}
