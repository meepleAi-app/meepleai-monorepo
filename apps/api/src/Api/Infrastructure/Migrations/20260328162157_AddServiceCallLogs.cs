using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCallLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "service_call_logs",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    HttpMethod = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    RequestUrl = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: true),
                    LatencyMs = table.Column<long>(type: "bigint", nullable: false),
                    IsSuccess = table.Column<bool>(type: "boolean", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TimestampUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RequestSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    ResponseSummary = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_call_logs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_service_call_logs_correlation_id",
                schema: "administration",
                table: "service_call_logs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "ix_service_call_logs_is_success",
                schema: "administration",
                table: "service_call_logs",
                column: "IsSuccess");

            migrationBuilder.CreateIndex(
                name: "ix_service_call_logs_service_name",
                schema: "administration",
                table: "service_call_logs",
                column: "ServiceName");

            migrationBuilder.CreateIndex(
                name: "ix_service_call_logs_service_timestamp",
                schema: "administration",
                table: "service_call_logs",
                columns: new[] { "ServiceName", "TimestampUtc" });

            migrationBuilder.CreateIndex(
                name: "ix_service_call_logs_timestamp",
                schema: "administration",
                table: "service_call_logs",
                column: "TimestampUtc",
                descending: new bool[0]);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "service_call_logs",
                schema: "administration");
        }
    }
}
