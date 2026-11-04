using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessedSessionIdsToAgentMemories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "processed_session_ids",
                table: "player_memories",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "processed_session_ids",
                table: "group_memories",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "processed_session_ids",
                table: "player_memories");

            migrationBuilder.DropColumn(
                name: "processed_session_ids",
                table: "group_memories");
        }
    }
}
