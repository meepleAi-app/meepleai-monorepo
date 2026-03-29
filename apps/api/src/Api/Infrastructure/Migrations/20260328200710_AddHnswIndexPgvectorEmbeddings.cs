using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHnswIndexPgvectorEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Id",
                schema: "administration",
                table: "service_call_logs",
                newName: "id");

            migrationBuilder.RenameColumn(
                name: "TimestampUtc",
                schema: "administration",
                table: "service_call_logs",
                newName: "timestamp_utc");

            migrationBuilder.RenameColumn(
                name: "StatusCode",
                schema: "administration",
                table: "service_call_logs",
                newName: "status_code");

            migrationBuilder.RenameColumn(
                name: "ServiceName",
                schema: "administration",
                table: "service_call_logs",
                newName: "service_name");

            migrationBuilder.RenameColumn(
                name: "ResponseSummary",
                schema: "administration",
                table: "service_call_logs",
                newName: "response_summary");

            migrationBuilder.RenameColumn(
                name: "RequestUrl",
                schema: "administration",
                table: "service_call_logs",
                newName: "request_url");

            migrationBuilder.RenameColumn(
                name: "RequestSummary",
                schema: "administration",
                table: "service_call_logs",
                newName: "request_summary");

            migrationBuilder.RenameColumn(
                name: "LatencyMs",
                schema: "administration",
                table: "service_call_logs",
                newName: "latency_ms");

            migrationBuilder.RenameColumn(
                name: "IsSuccess",
                schema: "administration",
                table: "service_call_logs",
                newName: "is_success");

            migrationBuilder.RenameColumn(
                name: "HttpMethod",
                schema: "administration",
                table: "service_call_logs",
                newName: "http_method");

            migrationBuilder.RenameColumn(
                name: "ErrorMessage",
                schema: "administration",
                table: "service_call_logs",
                newName: "error_message");

            migrationBuilder.RenameColumn(
                name: "CorrelationId",
                schema: "administration",
                table: "service_call_logs",
                newName: "correlation_id");

            migrationBuilder.CreateTable(
                name: "pgvector_embeddings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    vector_document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    game_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_content = table.Column<string>(type: "text", nullable: false),
                    vector = table.Column<Vector>(type: "vector(768)", nullable: false),
                    model = table.Column<string>(type: "text", nullable: false),
                    chunk_index = table.Column<int>(type: "integer", nullable: false),
                    page_number = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lang = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false, defaultValue: "en"),
                    source_chunk_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_translation = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pgvector_embeddings", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_pgvector_embeddings_game_id",
                table: "pgvector_embeddings",
                column: "game_id");

            migrationBuilder.CreateIndex(
                name: "IX_pgvector_embeddings_game_id_chunk_index",
                table: "pgvector_embeddings",
                columns: new[] { "game_id", "chunk_index" });

            // Use raw SQL for HNSW index with IF NOT EXISTS for idempotency in production deployments.
            // CONCURRENTLY is omitted: the table is empty at migration time, so there is no lock
            // contention to avoid, and a plain CREATE INDEX keeps the migration transactional.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_pgvector_embeddings_vector_hnsw
                ON pgvector_embeddings USING hnsw (vector vector_cosine_ops)
                WITH (m = 16, ef_construction = 64);
                """);

            migrationBuilder.CreateIndex(
                name: "IX_pgvector_embeddings_vector_document_id",
                table: "pgvector_embeddings",
                column: "vector_document_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_pgvector_embeddings_vector_hnsw;");
            // Note: plain DROP INDEX (without CONCURRENTLY) is transaction-safe, unlike CREATE INDEX CONCURRENTLY.
            // No suppressTransaction needed here.

            migrationBuilder.DropTable(
                name: "pgvector_embeddings");

            migrationBuilder.RenameColumn(
                name: "id",
                schema: "administration",
                table: "service_call_logs",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "timestamp_utc",
                schema: "administration",
                table: "service_call_logs",
                newName: "TimestampUtc");

            migrationBuilder.RenameColumn(
                name: "status_code",
                schema: "administration",
                table: "service_call_logs",
                newName: "StatusCode");

            migrationBuilder.RenameColumn(
                name: "service_name",
                schema: "administration",
                table: "service_call_logs",
                newName: "ServiceName");

            migrationBuilder.RenameColumn(
                name: "response_summary",
                schema: "administration",
                table: "service_call_logs",
                newName: "ResponseSummary");

            migrationBuilder.RenameColumn(
                name: "request_url",
                schema: "administration",
                table: "service_call_logs",
                newName: "RequestUrl");

            migrationBuilder.RenameColumn(
                name: "request_summary",
                schema: "administration",
                table: "service_call_logs",
                newName: "RequestSummary");

            migrationBuilder.RenameColumn(
                name: "latency_ms",
                schema: "administration",
                table: "service_call_logs",
                newName: "LatencyMs");

            migrationBuilder.RenameColumn(
                name: "is_success",
                schema: "administration",
                table: "service_call_logs",
                newName: "IsSuccess");

            migrationBuilder.RenameColumn(
                name: "http_method",
                schema: "administration",
                table: "service_call_logs",
                newName: "HttpMethod");

            migrationBuilder.RenameColumn(
                name: "error_message",
                schema: "administration",
                table: "service_call_logs",
                newName: "ErrorMessage");

            migrationBuilder.RenameColumn(
                name: "correlation_id",
                schema: "administration",
                table: "service_call_logs",
                newName: "CorrelationId");
        }
    }
}
