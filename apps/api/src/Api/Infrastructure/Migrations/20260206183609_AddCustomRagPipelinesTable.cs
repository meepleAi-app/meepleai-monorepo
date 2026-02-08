using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCustomRagPipelinesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "custom_rag_pipelines",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    pipeline_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_published = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    tags = table.Column<string[]>(type: "text[]", nullable: false),
                    is_template = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    access_tier = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_custom_rag_pipelines", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_created_by",
                table: "custom_rag_pipelines",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_is_published",
                table: "custom_rag_pipelines",
                column: "is_published");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_is_template",
                table: "custom_rag_pipelines",
                column: "is_template");

            migrationBuilder.CreateIndex(
                name: "ix_custom_rag_pipelines_tags",
                table: "custom_rag_pipelines",
                column: "tags")
                .Annotation("Npgsql:IndexMethod", "gin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "custom_rag_pipelines");
        }
    }
}
