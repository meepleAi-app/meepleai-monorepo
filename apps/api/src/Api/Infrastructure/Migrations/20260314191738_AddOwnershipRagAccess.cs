using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOwnershipRagAccess : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OwnershipDeclaredAt",
                table: "user_library_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_rag_public",
                table: "shared_games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: set OwnershipDeclaredAt for existing entries that are InPrestito (1) or Owned (3)
            migrationBuilder.Sql(
                """
                UPDATE user_library_entries
                SET "OwnershipDeclaredAt" = NOW()
                WHERE "CurrentState" IN (1, 3)
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OwnershipDeclaredAt",
                table: "user_library_entries");

            migrationBuilder.DropColumn(
                name: "is_rag_public",
                table: "shared_games");
        }
    }
}
