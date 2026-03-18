using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRuleDisputeTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rule_disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiator_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    respondent_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    initiator_claim = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    respondent_claim = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    verdict_json = table.Column<string>(type: "jsonb", nullable: true),
                    votes_json = table.Column<string>(type: "jsonb", nullable: true),
                    final_outcome = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    override_rule = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    related_dispute_ids_json = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rule_disputes", x => x.id);
                    table.ForeignKey(
                        name: "FK_rule_disputes_live_game_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rule_disputes_game_id",
                table: "rule_disputes",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_rule_disputes_game_id_created_at",
                table: "rule_disputes",
                columns: new[] { "game_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_rule_disputes_session_id",
                table: "rule_disputes",
                column: "session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rule_disputes");
        }
    }
}
