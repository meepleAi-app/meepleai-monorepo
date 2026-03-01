using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddToolkitTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "game_toolkit");

            migrationBuilder.CreateTable(
                name: "toolkits",
                schema: "game_toolkit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolkits", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "toolkit_widgets",
                schema: "game_toolkit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    toolkit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    display_order = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    config = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_toolkit_widgets", x => x.id);
                    table.ForeignKey(
                        name: "FK_toolkit_widgets_toolkits_toolkit_id",
                        column: x => x.toolkit_id,
                        principalSchema: "game_toolkit",
                        principalTable: "toolkits",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "uq_toolkit_widgets_toolkit_type",
                schema: "game_toolkit",
                table: "toolkit_widgets",
                columns: new[] { "toolkit_id", "type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_toolkits_game_id",
                schema: "game_toolkit",
                table: "toolkits",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "uq_toolkits_game_owner",
                schema: "game_toolkit",
                table: "toolkits",
                columns: new[] { "game_id", "owner_user_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "toolkit_widgets",
                schema: "game_toolkit");

            migrationBuilder.DropTable(
                name: "toolkits",
                schema: "game_toolkit");
        }
    }
}
