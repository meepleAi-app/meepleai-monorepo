using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <summary>
    /// #4973: contract of <c>game_memories.last_processed_event_id</c>, the idempotency guard of the dispute-override
    /// handler retired with the structured dispute v2 (#4936). #4960 had kept it as a shadow property, and a shadow
    /// property is still in every SELECT of the table, so with a deployed release the DROP would need one more release
    /// in between. There is none (#4428), so the shadow property and the column leave together. The same column on
    /// <c>player_memories</c> and <c>group_memories</c> stays.
    /// </summary>
    public partial class DropGameMemoriesLastProcessedEventId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("-- safe: drop game_memories.last_processed_event_id, contract of #4936 (#4973); no code reads it (out of the entity since #4960, its shadow property leaves in this same change), and no environment runs an older release against this schema: deploy-staging.yml is disabled and deploy-production.yml exists only as .disabled (#4428) (§8.3)");
            migrationBuilder.DropColumn(
                name: "last_processed_event_id",
                table: "game_memories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "last_processed_event_id",
                table: "game_memories",
                type: "uuid",
                nullable: true);
        }
    }
}
