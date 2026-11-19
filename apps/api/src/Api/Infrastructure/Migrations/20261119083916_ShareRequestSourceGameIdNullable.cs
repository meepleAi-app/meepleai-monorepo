using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ShareRequestSourceGameIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // #5074: a NewGameProposal is about a private game and has no shared game yet (source_private_game_id
            // names it). The column was NOT NULL with a foreign key to shared_games, so no proposal could be stored.
            migrationBuilder.AlterColumn<Guid>(
                name: "source_game_id",
                table: "share_requests",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // #5074: a NewGameProposal row has no source game (null). The default below is not a shared game, so the
            // foreign key rejects it: delete or resolve the proposals before rolling this back.
            migrationBuilder.AlterColumn<Guid>(
                name: "source_game_id",
                table: "share_requests",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
