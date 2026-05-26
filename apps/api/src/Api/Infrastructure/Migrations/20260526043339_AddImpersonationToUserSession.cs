using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddImpersonationToUserSession : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "impersonated_by_user_id",
                table: "user_sessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "impersonated_until",
                table: "user_sessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_sessions_impersonated_by_user_id",
                table: "user_sessions",
                column: "impersonated_by_user_id",
                filter: "\"impersonated_by_user_id\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_user_sessions_impersonated_by_user_id",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "impersonated_by_user_id",
                table: "user_sessions");

            migrationBuilder.DropColumn(
                name: "impersonated_until",
                table: "user_sessions");
        }
    }
}
