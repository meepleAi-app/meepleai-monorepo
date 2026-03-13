using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionParticipantAndTierDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsContributor",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "session_participants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    agent_access_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    connection_token = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    joined_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    left_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_participants", x => x.id);
                    table.ForeignKey(
                        name: "FK_session_participants_live_game_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "live_game_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_session_participants_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "tier_definitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    display_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_private_games = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_uploads_per_month = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    max_agents = table.Column<int>(type: "integer", nullable: false),
                    max_agent_queries_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_session_queries = table.Column<int>(type: "integer", nullable: false),
                    max_session_players = table.Column<int>(type: "integer", nullable: false),
                    max_photos_per_session = table.Column<int>(type: "integer", nullable: false),
                    session_save_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    max_catalog_proposals_per_week = table.Column<int>(type: "integer", nullable: false),
                    llm_model_tier = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tier_definitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_session_participants_connection_token",
                table: "session_participants",
                column: "connection_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_session_participants_session_id",
                table: "session_participants",
                column: "session_id");

            migrationBuilder.CreateIndex(
                name: "ix_session_participants_user_id",
                table: "session_participants",
                column: "user_id",
                filter: "user_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_tier_definitions_name",
                table: "tier_definitions",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_participants");

            migrationBuilder.DropTable(
                name: "tier_definitions");

            migrationBuilder.DropColumn(
                name: "IsContributor",
                table: "users");
        }
    }
}
