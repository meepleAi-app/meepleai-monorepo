using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAgentProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "agent_profiles",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_profiles", x => x.id);
                    table.CheckConstraint("ck_agent_profiles_singleton", "id = 'a9e7f5c1-4167-4095-9a1e-000000000001'");
                });

            migrationBuilder.CreateTable(
                name: "agent_profile_versions",
                schema: "knowledge_base",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agent_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_agent_profile_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_agent_profile_versions_agent_profiles_agent_profile_id",
                        column: x => x.agent_profile_id,
                        principalSchema: "knowledge_base",
                        principalTable: "agent_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_agent_profile_versions_one_draft",
                schema: "knowledge_base",
                table: "agent_profile_versions",
                column: "agent_profile_id",
                unique: true,
                filter: "status = 0");

            migrationBuilder.CreateIndex(
                name: "ux_agent_profile_versions_one_published",
                schema: "knowledge_base",
                table: "agent_profile_versions",
                column: "agent_profile_id",
                unique: true,
                filter: "status = 1");

            migrationBuilder.CreateIndex(
                name: "ux_agent_profile_versions_profile_version",
                schema: "knowledge_base",
                table: "agent_profile_versions",
                columns: new[] { "agent_profile_id", "version_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "agent_profile_versions",
                schema: "knowledge_base");

            migrationBuilder.DropTable(
                name: "agent_profiles",
                schema: "knowledge_base");
        }
    }
}
