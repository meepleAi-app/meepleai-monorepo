using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agents_games_GameId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_GameId_Name",
                table: "agents");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "agents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(128)",
                oldMaxLength: 128);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "agents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "agents",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<Guid>(
                name: "GameEntityId",
                table: "agents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "InvocationCount",
                table: "agents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "agents",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastInvokedAt",
                table: "agents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StrategyName",
                table: "agents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "StrategyParametersJson",
                table: "agents",
                type: "jsonb",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "agents",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameEntityId",
                table: "agents",
                column: "GameEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_agents_IsActive",
                table: "agents",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_agents_LastInvokedAt",
                table: "agents",
                column: "LastInvokedAt");

            migrationBuilder.CreateIndex(
                name: "IX_agents_Name",
                table: "agents",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_Type",
                table: "agents",
                column: "Type");

            migrationBuilder.AddForeignKey(
                name: "FK_agents_games_GameEntityId",
                table: "agents",
                column: "GameEntityId",
                principalTable: "games",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_agents_games_GameEntityId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_GameEntityId",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_IsActive",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_LastInvokedAt",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_Name",
                table: "agents");

            migrationBuilder.DropIndex(
                name: "IX_agents_Type",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "GameEntityId",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "InvocationCount",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "LastInvokedAt",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "StrategyName",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "StrategyParametersJson",
                table: "agents");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "agents");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "agents",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "Kind",
                table: "agents",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "GameId",
                table: "agents",
                type: "uuid",
                maxLength: 64,
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_agents_GameId_Name",
                table: "agents",
                columns: new[] { "GameId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_agents_games_GameId",
                table: "agents",
                column: "GameId",
                principalTable: "games",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
