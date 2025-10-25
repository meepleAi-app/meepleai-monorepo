using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRateLimitDefaultConfigurations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Seed rate limit configurations (CONFIG-02)
            // 13 configurations: 1 global flag + 12 role-based limits (3 roles × 4 limit types)
            migrationBuilder.InsertData(
                table: "system_configurations",
                columns: new[] { "id", "configuration_key", "value", "value_type", "description", "is_editable", "category", "is_active", "environment", "created_at", "created_by_user_id" },
                values: new object[,]
                {
                    // Global Feature Flag
                    {
                        Guid.NewGuid(),
                        "RateLimit.Enabled",
                        "true",
                        "boolean",
                        "Global switch to enable/disable rate limiting across all endpoints",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001" // System admin user from seed data
                    },

                    // Admin Role - MaxTokens
                    {
                        Guid.NewGuid(),
                        "RateLimit.MaxTokens.admin",
                        "1000",
                        "integer",
                        "Maximum tokens (burst capacity) for admin users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // Admin Role - RefillRate
                    {
                        Guid.NewGuid(),
                        "RateLimit.RefillRate.admin",
                        "10.0",
                        "double",
                        "Tokens added per second (refill rate) for admin users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // Editor Role - MaxTokens
                    {
                        Guid.NewGuid(),
                        "RateLimit.MaxTokens.editor",
                        "500",
                        "integer",
                        "Maximum tokens (burst capacity) for editor users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // Editor Role - RefillRate
                    {
                        Guid.NewGuid(),
                        "RateLimit.RefillRate.editor",
                        "5.0",
                        "double",
                        "Tokens added per second (refill rate) for editor users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // User Role - MaxTokens
                    {
                        Guid.NewGuid(),
                        "RateLimit.MaxTokens.user",
                        "100",
                        "integer",
                        "Maximum tokens (burst capacity) for regular users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // User Role - RefillRate
                    {
                        Guid.NewGuid(),
                        "RateLimit.RefillRate.user",
                        "1.0",
                        "double",
                        "Tokens added per second (refill rate) for regular users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // Anonymous Role - MaxTokens
                    {
                        Guid.NewGuid(),
                        "RateLimit.MaxTokens.anonymous",
                        "60",
                        "integer",
                        "Maximum tokens (burst capacity) for anonymous (unauthenticated) users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    },

                    // Anonymous Role - RefillRate
                    {
                        Guid.NewGuid(),
                        "RateLimit.RefillRate.anonymous",
                        "1.0",
                        "double",
                        "Tokens added per second (refill rate) for anonymous users",
                        true,
                        "RateLimit",
                        true,
                        "All",
                        DateTime.UtcNow,
                        "demo-admin-001"
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove all rate limit configurations
            migrationBuilder.Sql(@"
                DELETE FROM system_configurations
                WHERE configuration_key LIKE 'RateLimit.%'
                AND category = 'RateLimit';
            ");
        }
    }
}
