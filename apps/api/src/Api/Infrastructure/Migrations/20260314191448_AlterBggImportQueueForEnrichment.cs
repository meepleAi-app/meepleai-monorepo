using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlterBggImportQueueForEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BggRawData",
                table: "shared_games",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "GameDataStatus",
                table: "shared_games",
                type: "integer",
                nullable: false,
                defaultValue: 5); // 5 = Complete — all existing games are fully enriched

            migrationBuilder.AddColumn<bool>(
                name: "HasUploadedPdf",
                table: "shared_games",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RulesExternalUrl",
                table: "shared_games",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "BggId",
                table: "BggImportQueue",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddColumn<Guid>(
                name: "BatchId",
                table: "BggImportQueue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "JobType",
                table: "BggImportQueue",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "SharedGameId",
                table: "BggImportQueue",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BggRawData",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "GameDataStatus",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "HasUploadedPdf",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "RulesExternalUrl",
                table: "shared_games");

            migrationBuilder.DropColumn(
                name: "BatchId",
                table: "BggImportQueue");

            migrationBuilder.DropColumn(
                name: "JobType",
                table: "BggImportQueue");

            migrationBuilder.DropColumn(
                name: "SharedGameId",
                table: "BggImportQueue");

            migrationBuilder.AlterColumn<int>(
                name: "BggId",
                table: "BggImportQueue",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }
    }
}
