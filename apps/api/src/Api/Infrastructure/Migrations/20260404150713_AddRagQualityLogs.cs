using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRagQualityLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "rag_quality_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    thread_id = table.Column<Guid>(type: "uuid", nullable: true),
                    game_id = table.Column<Guid>(type: "uuid", nullable: true),
                    query_length = table.Column<int>(type: "integer", nullable: false),
                    chunks_retrieved = table.Column<int>(type: "integer", nullable: false),
                    chunks_used = table.Column<int>(type: "integer", nullable: false),
                    context_precision = table.Column<decimal>(type: "numeric", nullable: true),
                    citations_count = table.Column<int>(type: "integer", nullable: false),
                    strategy = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_used = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: true),
                    output_tokens = table.Column<int>(type: "integer", nullable: true),
                    latency_ms = table.Column<int>(type: "integer", nullable: false),
                    cache_hit = table.Column<bool>(type: "boolean", nullable: false),
                    no_relevant_context = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rag_quality_logs", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "rag_quality_logs");
        }
    }
}
