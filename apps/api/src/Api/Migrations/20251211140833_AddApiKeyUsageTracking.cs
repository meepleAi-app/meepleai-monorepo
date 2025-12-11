using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApiKeyUsageTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UsageCount",
                table: "api_keys",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "api_key_usage_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key_id = table.Column<Guid>(type: "uuid", nullable: false),
                    used_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ip_address = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    status_code = table.Column<int>(type: "integer", nullable: true),
                    response_time_ms = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_key_usage_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_api_key_usage_logs_api_keys_key_id",
                        column: x => x.key_id,
                        principalTable: "api_keys",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_usage_logs_key_id",
                table: "api_key_usage_logs",
                column: "key_id");

            migrationBuilder.CreateIndex(
                name: "ix_api_key_usage_logs_key_id_used_at",
                table: "api_key_usage_logs",
                columns: new[] { "key_id", "used_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_api_key_usage_logs_used_at",
                table: "api_key_usage_logs",
                column: "used_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_key_usage_logs");

            migrationBuilder.DropColumn(
                name: "UsageCount",
                table: "api_keys");
        }
    }
}
