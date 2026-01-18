using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingColumnsToSharedGamesAndUserLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ========== SharedGames Table Additions ==========

            // Add ProposedChanges JSONB column for approval workflow
            migrationBuilder.AddColumn<string>(
                name: "proposed_changes",
                table: "shared_games",
                type: "jsonb",
                nullable: true);

            // Add Tags array for categories/mechanics
            migrationBuilder.AddColumn<string[]>(
                name: "tags",
                table: "shared_games",
                type: "text[]",
                nullable: true);

            // Add Approval audit columns
            migrationBuilder.AddColumn<Guid>(
                name: "approved_by",
                table: "shared_games",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "approved_at",
                table: "shared_games",
                type: "timestamp with time zone",
                nullable: true);

            // Add DeletedAt for soft-delete audit trail
            migrationBuilder.AddColumn<DateTime>(
                name: "deleted_at",
                table: "shared_games",
                type: "timestamp with time zone",
                nullable: true);

            // ========== UserLibrary Table Additions ==========

            // Add UpdatedAt audit column
            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "user_library_entries",
                type: "timestamp with time zone",
                nullable: true);

            // ========== Indexes ==========

            // GIN index for ProposedChanges JSONB queries
            migrationBuilder.CreateIndex(
                name: "IX_SharedGames_ProposedChanges",
                table: "shared_games",
                column: "proposed_changes")
                .Annotation("Npgsql:IndexMethod", "gin");

            // GIN index for Tags array full-text search
            migrationBuilder.CreateIndex(
                name: "IX_SharedGames_Tags",
                table: "shared_games",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");

            // Index for ApprovedBy FK (improves JOIN performance)
            migrationBuilder.CreateIndex(
                name: "IX_SharedGames_ApprovedBy",
                table: "shared_games",
                column: "approved_by");

            // ========== Foreign Keys ==========

            // FK: ApprovedBy → Users
            // Restrict: Preserve audit integrity (admin who approved cannot be deleted)
            migrationBuilder.AddForeignKey(
                name: "FK_shared_games_users_approved_by",
                table: "shared_games",
                column: "approved_by",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ========== Data Migration ==========

            // Populate DeletedAt for existing soft-deleted records
            migrationBuilder.Sql(
                """
                UPDATE shared_games
                SET deleted_at = modified_at
                WHERE is_deleted = true AND deleted_at IS NULL;
                """);

            // Populate UpdatedAt for existing user library entries
            migrationBuilder.Sql(
                """
                UPDATE user_library_entries
                SET updated_at = "AddedAt"
                WHERE updated_at IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop foreign keys
            migrationBuilder.DropForeignKey(
                name: "FK_shared_games_users_approved_by",
                table: "shared_games");

            // Drop indexes
            migrationBuilder.DropIndex(
                name: "IX_SharedGames_ProposedChanges",
                table: "shared_games");

            migrationBuilder.DropIndex(
                name: "IX_SharedGames_Tags",
                table: "shared_games");

            migrationBuilder.DropIndex(
                name: "IX_SharedGames_ApprovedBy",
                table: "shared_games");

            // Drop SharedGames columns
            migrationBuilder.DropColumn(
                name: "proposed_changes",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "tags",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "approved_by",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "approved_at",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                table: "shared_games");

            // Drop UserLibrary columns
            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "user_library_entries");
        }
    }
}
