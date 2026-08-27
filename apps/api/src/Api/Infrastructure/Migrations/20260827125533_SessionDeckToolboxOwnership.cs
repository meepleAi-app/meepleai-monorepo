using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SessionDeckToolboxOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                schema: "session_tracking",
                table: "SessionDecks",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "ToolboxId",
                schema: "session_tracking",
                table: "SessionDecks",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionDecks_ToolboxId",
                schema: "session_tracking",
                table: "SessionDecks",
                column: "ToolboxId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SessionDecks_Owner",
                schema: "session_tracking",
                table: "SessionDecks",
                sql: "(\"SessionId\" IS NOT NULL AND \"ToolboxId\" IS NULL) OR (\"SessionId\" IS NULL AND \"ToolboxId\" IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_SessionDecks_toolboxes_ToolboxId",
                schema: "session_tracking",
                table: "SessionDecks",
                column: "ToolboxId",
                principalSchema: "game_toolbox",
                principalTable: "toolboxes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SessionDecks_toolboxes_ToolboxId",
                schema: "session_tracking",
                table: "SessionDecks");

            migrationBuilder.DropIndex(
                name: "IX_SessionDecks_ToolboxId",
                schema: "session_tracking",
                table: "SessionDecks");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SessionDecks_Owner",
                schema: "session_tracking",
                table: "SessionDecks");

            migrationBuilder.DropColumn(
                name: "ToolboxId",
                schema: "session_tracking",
                table: "SessionDecks");

            migrationBuilder.AlterColumn<Guid>(
                name: "SessionId",
                schema: "session_tracking",
                table: "SessionDecks",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,   // S4581: la forma generata dallo scaffolder non passa l'analizzatore
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
