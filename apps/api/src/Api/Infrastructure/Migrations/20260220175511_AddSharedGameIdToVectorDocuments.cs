using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSharedGameIdToVectorDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_games_GameId",
                table: "vector_documents");

            migrationBuilder.RenameColumn(
                name: "GameId",
                table: "vector_documents",
                newName: "game_id");

            migrationBuilder.RenameIndex(
                name: "IX_vector_documents_GameId",
                table: "vector_documents",
                newName: "IX_vector_documents_game_id");

            migrationBuilder.AlterColumn<Guid>(
                name: "game_id",
                table: "vector_documents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<Guid>(
                name: "shared_game_id",
                table: "vector_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_vector_documents_shared_game_id",
                table: "vector_documents",
                column: "shared_game_id");

            migrationBuilder.AddCheckConstraint(
                name: "CK_vector_documents_game_or_shared_game",
                table: "vector_documents",
                sql: "(game_id IS NOT NULL OR shared_game_id IS NOT NULL)");

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_games_game_id",
                table: "vector_documents",
                column: "game_id",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_shared_games_shared_game_id",
                table: "vector_documents",
                column: "shared_game_id",
                principalTable: "shared_games",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_games_game_id",
                table: "vector_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_vector_documents_shared_games_shared_game_id",
                table: "vector_documents");

            migrationBuilder.DropIndex(
                name: "IX_vector_documents_shared_game_id",
                table: "vector_documents");

            migrationBuilder.DropCheckConstraint(
                name: "CK_vector_documents_game_or_shared_game",
                table: "vector_documents");

            migrationBuilder.DropColumn(
                name: "shared_game_id",
                table: "vector_documents");

            migrationBuilder.RenameColumn(
                name: "game_id",
                table: "vector_documents",
                newName: "GameId");

            migrationBuilder.RenameIndex(
                name: "IX_vector_documents_game_id",
                table: "vector_documents",
                newName: "IX_vector_documents_GameId");

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "vector_documents",
                type: "uuid",
                maxLength: 64,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_vector_documents_games_GameId",
                table: "vector_documents",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
