using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixSkeletonConstraintsAndJsonbMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games");

            migrationBuilder.EnsureSchema(
                name: "game_toolbox");

            migrationBuilder.RenameColumn(
                name: "RulesExternalUrl",
                table: "shared_games",
                newName: "rules_external_url");

            migrationBuilder.RenameColumn(
                name: "HasUploadedPdf",
                table: "shared_games",
                newName: "has_uploaded_pdf");

            migrationBuilder.RenameColumn(
                name: "GameDataStatus",
                table: "shared_games",
                newName: "game_data_status");

            migrationBuilder.RenameColumn(
                name: "BggRawData",
                table: "shared_games",
                newName: "bgg_raw_data");

            migrationBuilder.AlterColumn<string>(
                name: "rules_external_url",
                table: "shared_games",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "has_uploaded_pdf",
                table: "shared_games",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<int>(
                name: "game_data_status",
                table: "shared_games",
                type: "integer",
                nullable: false,
                defaultValue: 5,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<string>(
                name: "bgg_raw_data",
                table: "shared_games",
                type: "jsonb",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "toolbox_templates",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tools_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    phases_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    shared_context_defaults_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolboxes",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    shared_context = table.Column<string>(type: "jsonb", nullable: false),
                    current_phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolboxes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_phases",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active_tool_ids = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_phases", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolbox_phases_toolboxes_toolbox_id",
                        column: x => x.toolbox_id,
                        principalSchema: "game_toolbox",
                        principalTable: "toolboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "toolbox_tools",
                schema: "game_toolbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolbox_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    state = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolbox_tools", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolbox_tools_toolboxes_toolbox_id",
                        column: x => x.toolbox_id,
                        principalSchema: "game_toolbox",
                        principalTable: "toolboxes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games",
                sql: "(min_players = 0 AND max_players = 0) OR (min_players > 0 AND max_players >= min_players)");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games",
                sql: "playing_time_minutes >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games",
                sql: "year_published = 0 OR (year_published > 1900 AND year_published <= 2100)");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_phases_toolbox_id",
                schema: "game_toolbox",
                table: "toolbox_phases",
                column: "toolbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_templates_game_id",
                schema: "game_toolbox",
                table: "toolbox_templates",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolbox_tools_toolbox_id",
                schema: "game_toolbox",
                table: "toolbox_tools",
                column: "toolbox_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolboxes_game_id",
                schema: "game_toolbox",
                table: "toolboxes",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "ix_toolboxes_is_deleted",
                schema: "game_toolbox",
                table: "toolboxes",
                column: "is_deleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "toolbox_phases",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolbox_templates",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolbox_tools",
                schema: "game_toolbox");

            migrationBuilder.DropTable(
                name: "toolboxes",
                schema: "game_toolbox");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games");

            migrationBuilder.DropCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games");

            migrationBuilder.RenameColumn(
                name: "rules_external_url",
                table: "shared_games",
                newName: "RulesExternalUrl");

            migrationBuilder.RenameColumn(
                name: "has_uploaded_pdf",
                table: "shared_games",
                newName: "HasUploadedPdf");

            migrationBuilder.RenameColumn(
                name: "game_data_status",
                table: "shared_games",
                newName: "GameDataStatus");

            migrationBuilder.RenameColumn(
                name: "bgg_raw_data",
                table: "shared_games",
                newName: "BggRawData");

            migrationBuilder.AlterColumn<string>(
                name: "RulesExternalUrl",
                table: "shared_games",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "HasUploadedPdf",
                table: "shared_games",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.AlterColumn<int>(
                name: "GameDataStatus",
                table: "shared_games",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldDefaultValue: 5);

            migrationBuilder.AlterColumn<string>(
                name: "BggRawData",
                table: "shared_games",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "jsonb",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_players",
                table: "shared_games",
                sql: "min_players > 0 AND max_players >= min_players");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_playing_time",
                table: "shared_games",
                sql: "playing_time_minutes > 0");

            migrationBuilder.AddCheckConstraint(
                name: "chk_shared_games_year_published",
                table: "shared_games",
                sql: "year_published > 1900 AND year_published <= 2100");
        }
    }
}
