using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixEmbeddingVectorDimensionTo1024 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "strategy_patterns",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "session_checkpoints",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "session_checkpoints",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "conversation_memory",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "agent_game_state_snapshots",
                type: "vector(1024)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1536)",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_session_checkpoints_IsDeleted",
                table: "session_checkpoints",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_session_checkpoints_IsDeleted",
                table: "session_checkpoints");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "session_checkpoints");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "session_checkpoints");

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "strategy_patterns",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1024)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "conversation_memory",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1024)",
                oldNullable: true);

            migrationBuilder.AlterColumn<Vector>(
                name: "embedding",
                table: "agent_game_state_snapshots",
                type: "vector(1536)",
                nullable: true,
                oldClrType: typeof(Vector),
                oldType: "vector(1024)",
                oldNullable: true);
        }
    }
}
