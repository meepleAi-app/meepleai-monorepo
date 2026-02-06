using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTokenManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.CreateTable(
                name: "token_tiers",
                schema: "administration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    tokens_per_month = table.Column<int>(type: "integer", nullable: false),
                    tokens_per_day = table.Column<int>(type: "integer", nullable: false),
                    messages_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_collection_size = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_uploads_per_month = table.Column<int>(type: "integer", nullable: false),
                    max_agents_created = table.Column<int>(type: "integer", nullable: false),
                    monthly_fee = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    cost_per_extra_token = table.Column<decimal>(type: "numeric(10,6)", nullable: true),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_token_tiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_token_usage",
                schema: "administration",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    messages_count = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    last_reset = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    is_near_limit = table.Column<bool>(type: "boolean", nullable: false),
                    warnings = table.Column<string>(type: "jsonb", nullable: false),
                    history = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_token_usage", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_token_tiers_name",
                schema: "administration",
                table: "token_tiers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_tier_id",
                schema: "administration",
                table: "user_token_usage",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_user_id",
                schema: "administration",
                table: "user_token_usage",
                column: "user_id",
                unique: true);

            // Seed default tiers (Issue #3692)
            migrationBuilder.InsertData(
                schema: "administration",
                table: "token_tiers",
                columns: new[] { "id", "name", "tokens_per_month", "tokens_per_day", "messages_per_day",
                    "max_collection_size", "max_pdf_uploads_per_month", "max_agents_created",
                    "monthly_fee", "cost_per_extra_token", "currency", "is_active", "created_at" },
                values: new object[,]
                {
                    { Guid.Parse("11111111-1111-1111-1111-111111111111"), "Free", 10000, 500, 10, 20, 5, 1, 0m, null, "EUR", true, DateTime.UtcNow },
                    { Guid.Parse("22222222-2222-2222-2222-222222222222"), "Basic", 50000, 2000, 50, 50, 20, 3, 9.99m, 0.0001m, "EUR", true, DateTime.UtcNow },
                    { Guid.Parse("33333333-3333-3333-3333-333333333333"), "Pro", 200000, 10000, 200, 200, 100, 10, 29.99m, 0.00008m, "EUR", true, DateTime.UtcNow },
                    { Guid.Parse("44444444-4444-4444-4444-444444444444"), "Enterprise", int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, int.MaxValue, 0m, null, "EUR", true, DateTime.UtcNow }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "token_tiers",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "user_token_usage",
                schema: "administration");
        }
    }
}
