using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropRuleDisputes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("-- safe: drop the structured dispute v2 table, contract of #4936 (#4960); the release carrying #4936 (PR #4972) maps it but never queries it, no navigation leads to it, and no UI has written it since 2026-05-22 (#1423) (§8.3)");
            migrationBuilder.DropTable(
                name: "rule_disputes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rule_disputes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    final_outcome = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    initiator_claim = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    initiator_player_id = table.Column<Guid>(type: "uuid", nullable: false),
                    override_rule = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    related_dispute_ids_json = table.Column<string>(type: "jsonb", nullable: true),
                    respondent_claim = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    respondent_player_id = table.Column<Guid>(type: "uuid", nullable: true),
                    verdict_json = table.Column<string>(type: "jsonb", nullable: true),
                    votes_json = table.Column<string>(type: "jsonb", nullable: true)
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
    }
}
