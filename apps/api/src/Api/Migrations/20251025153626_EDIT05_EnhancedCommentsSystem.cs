using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class EDIT05_EnhancedCommentsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameIndex(
                name: "IX_rulespec_comments_UserId",
                table: "rulespec_comments",
                newName: "idx_rulespec_comments_user_id");

            migrationBuilder.AddColumn<bool>(
                name: "IsResolved",
                table: "rulespec_comments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LineContext",
                table: "rulespec_comments",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LineNumber",
                table: "rulespec_comments",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MentionedUserIds",
                table: "rulespec_comments",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ParentCommentId",
                table: "rulespec_comments",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResolvedAt",
                table: "rulespec_comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResolvedByUserId",
                table: "rulespec_comments",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_game_version_line",
                table: "rulespec_comments",
                columns: new[] { "GameId", "Version", "LineNumber" });

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_is_resolved",
                table: "rulespec_comments",
                column: "IsResolved");

            migrationBuilder.CreateIndex(
                name: "idx_rulespec_comments_parent_id",
                table: "rulespec_comments",
                column: "ParentCommentId");

            migrationBuilder.CreateIndex(
                name: "IX_rulespec_comments_ResolvedByUserId",
                table: "rulespec_comments",
                column: "ResolvedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_rulespec_comments_rulespec_comments_ParentCommentId",
                table: "rulespec_comments",
                column: "ParentCommentId",
                principalTable: "rulespec_comments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_rulespec_comments_users_ResolvedByUserId",
                table: "rulespec_comments",
                column: "ResolvedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_rulespec_comments_rulespec_comments_ParentCommentId",
                table: "rulespec_comments");

            migrationBuilder.DropForeignKey(
                name: "FK_rulespec_comments_users_ResolvedByUserId",
                table: "rulespec_comments");

            migrationBuilder.DropIndex(
                name: "idx_rulespec_comments_game_version_line",
                table: "rulespec_comments");

            migrationBuilder.DropIndex(
                name: "idx_rulespec_comments_is_resolved",
                table: "rulespec_comments");

            migrationBuilder.DropIndex(
                name: "idx_rulespec_comments_parent_id",
                table: "rulespec_comments");

            migrationBuilder.DropIndex(
                name: "IX_rulespec_comments_ResolvedByUserId",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "IsResolved",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "LineContext",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "LineNumber",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "MentionedUserIds",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "ParentCommentId",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "ResolvedAt",
                table: "rulespec_comments");

            migrationBuilder.DropColumn(
                name: "ResolvedByUserId",
                table: "rulespec_comments");

            migrationBuilder.RenameIndex(
                name: "idx_rulespec_comments_user_id",
                table: "rulespec_comments",
                newName: "IX_rulespec_comments_UserId");
        }
    }
}
