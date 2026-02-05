using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRagPipelineStrategies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rag_pipeline_strategies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    nodes_json = table.Column<string>(type: "jsonb", nullable: false),
                    edges_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    template_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tags_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_pipeline_strategies", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_rag_pipeline_strategies_created_by_user_id",
                table: "rag_pipeline_strategies",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_rag_pipeline_strategies_is_template",
                table: "rag_pipeline_strategies",
                column: "is_template",
                filter: "is_template = true");

            migrationBuilder.CreateIndex(
                name: "ix_rag_pipeline_strategies_name_user",
                table: "rag_pipeline_strategies",
                columns: new[] { "name", "created_by_user_id" },
                unique: true,
                filter: "is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_pipeline_strategies");
        }
    }
}
