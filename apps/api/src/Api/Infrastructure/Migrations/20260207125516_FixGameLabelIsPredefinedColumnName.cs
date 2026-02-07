using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class FixGameLabelIsPredefinedColumnName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "administration");

            migrationBuilder.RenameColumn(
                name: "IsPredefined",
                table: "game_labels",
                newName: "is_predefined");

            migrationBuilder.CreateTable(
                name: "batch_jobs",
                schema: "administration",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Parameters = table.Column<string>(type: "jsonb", nullable: false),
                    Progress = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationSeconds = table.Column<int>(type: "integer", nullable: true),
                    ResultData = table.Column<string>(type: "jsonb", nullable: true),
                    ResultSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OutputFileUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ErrorStack = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_batch_jobs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "token_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    tokens_per_month = table.Column<int>(type: "integer", nullable: false),
                    tokens_per_day = table.Column<int>(type: "integer", nullable: false),
                    messages_per_day = table.Column<int>(type: "integer", nullable: false),
                    max_collection_size = table.Column<int>(type: "integer", nullable: false),
                    max_pdf_uploads_per_month = table.Column<int>(type: "integer", nullable: false),
                    max_agents_created = table.Column<int>(type: "integer", nullable: false),
                    monthly_fee = table.Column<decimal>(type: "numeric(10,2)", precision: 18, scale: 2, nullable: false),
                    cost_per_extra_token = table.Column<decimal>(type: "numeric(10,6)", precision: 18, scale: 6, nullable: true),
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
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tokens_used = table.Column<int>(type: "integer", nullable: false),
                    messages_count = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(10,2)", precision: 18, scale: 4, nullable: false),
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
                name: "ix_batch_jobs_created_at",
                schema: "administration",
                table: "batch_jobs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "ix_batch_jobs_status",
                schema: "administration",
                table: "batch_jobs",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "ix_batch_jobs_status_created_at",
                schema: "administration",
                table: "batch_jobs",
                columns: new[] { "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_token_tiers_is_active",
                table: "token_tiers",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "IX_token_tiers_name",
                table: "token_tiers",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_last_reset",
                table: "user_token_usage",
                column: "last_reset");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_tier_id",
                table: "user_token_usage",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_token_usage_user_id",
                table: "user_token_usage",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserTokenUsage_TierId_IsBlocked",
                table: "user_token_usage",
                columns: new[] { "tier_id", "is_blocked" });

            migrationBuilder.CreateIndex(
                name: "IX_UserTokenUsage_UserId_TokensUsed",
                table: "user_token_usage",
                columns: new[] { "user_id", "tokens_used" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "batch_jobs",
                schema: "administration");

            migrationBuilder.DropTable(
                name: "token_tiers");

            migrationBuilder.DropTable(
                name: "user_token_usage");

            migrationBuilder.RenameColumn(
                name: "is_predefined",
                table: "game_labels",
                newName: "IsPredefined");
        }
    }
}
