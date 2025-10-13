using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class SeedDemoData : Migration
    {
        // Pre-computed password hash for "Demo123!" using PBKDF2 (210,000 iterations)
        // This allows users to test the system with known credentials
        // Hash generated and verified by VerifyPasswordHashTest.cs
        private const string DemoPasswordHash = "v1.210000.8dE/5q2EBcd0MSLdKi8x6g==.zx114sOsC0WtjeEBN0aYdaqQxbcxWcJfwEbNQ5id1fM=";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            var now = new DateTime(2025, 10, 9, 14, 0, 0, DateTimeKind.Utc);

            // Seed Demo Users (idempotent - only inserts if not exists)
            // Admin user
            migrationBuilder.Sql(@"
                INSERT INTO users (""Id"", ""Email"", ""DisplayName"", ""PasswordHash"", ""Role"", ""CreatedAt"")
                SELECT 'demo-admin-001', 'admin@meepleai.dev', 'Demo Admin', '" + DemoPasswordHash + @"', 'Admin', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM users WHERE ""Email"" = 'admin@meepleai.dev');
            ");

            // Editor user
            migrationBuilder.Sql(@"
                INSERT INTO users (""Id"", ""Email"", ""DisplayName"", ""PasswordHash"", ""Role"", ""CreatedAt"")
                SELECT 'demo-editor-001', 'editor@meepleai.dev', 'Demo Editor', '" + DemoPasswordHash + @"', 'Editor', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM users WHERE ""Email"" = 'editor@meepleai.dev');
            ");

            // Regular user
            migrationBuilder.Sql(@"
                INSERT INTO users (""Id"", ""Email"", ""DisplayName"", ""PasswordHash"", ""Role"", ""CreatedAt"")
                SELECT 'demo-user-001', 'user@meepleai.dev', 'Demo User', '" + DemoPasswordHash + @"', 'User', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM users WHERE ""Email"" = 'user@meepleai.dev');
            ");

            // Seed Demo Games (idempotent)
            migrationBuilder.Sql(@"
                INSERT INTO games (""Id"", ""Name"", ""CreatedAt"")
                SELECT 'tic-tac-toe', 'Tic-Tac-Toe', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM games WHERE ""Id"" = 'tic-tac-toe');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO games (""Id"", ""Name"", ""CreatedAt"")
                SELECT 'chess', 'Chess', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM games WHERE ""Id"" = 'chess');
            ");

            // Seed Demo Rule Specs (idempotent)
            migrationBuilder.Sql(@"
                INSERT INTO rule_specs (""Id"", ""GameId"", ""Version"", ""CreatedAt"", ""CreatedByUserId"")
                SELECT 'f5e4d3c2-b1a0-4f3e-9d8c-7b6a5e4d3c21'::uuid, 'tic-tac-toe', 'v1.0', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz, 'demo-admin-001'
                WHERE NOT EXISTS (SELECT 1 FROM rule_specs WHERE ""GameId"" = 'tic-tac-toe' AND ""Version"" = 'v1.0');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO rule_specs (""Id"", ""GameId"", ""Version"", ""CreatedAt"", ""CreatedByUserId"")
                SELECT 'a1b2c3d4-e5f6-47a8-b9c0-d1e2f3a4b5c6'::uuid, 'chess', 'v1.0', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz, 'demo-admin-001'
                WHERE NOT EXISTS (SELECT 1 FROM rule_specs WHERE ""GameId"" = 'chess' AND ""Version"" = 'v1.0');
            ");

            // Seed Demo Agents (idempotent)
            migrationBuilder.Sql(@"
                INSERT INTO agents (""Id"", ""GameId"", ""Name"", ""Kind"", ""CreatedAt"")
                SELECT 'agent-ttt-explain', 'tic-tac-toe', 'Tic-Tac-Toe Explainer', 'explain', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM agents WHERE ""Id"" = 'agent-ttt-explain');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO agents (""Id"", ""GameId"", ""Name"", ""Kind"", ""CreatedAt"")
                SELECT 'agent-ttt-qa', 'tic-tac-toe', 'Tic-Tac-Toe Q&A', 'qa', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM agents WHERE ""Id"" = 'agent-ttt-qa');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO agents (""Id"", ""GameId"", ""Name"", ""Kind"", ""CreatedAt"")
                SELECT 'agent-chess-explain', 'chess', 'Chess Explainer', 'explain', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM agents WHERE ""Id"" = 'agent-chess-explain');
            ");

            migrationBuilder.Sql(@"
                INSERT INTO agents (""Id"", ""GameId"", ""Name"", ""Kind"", ""CreatedAt"")
                SELECT 'agent-chess-qa', 'chess', 'Chess Q&A', 'qa', '" + now.ToString("yyyy-MM-dd HH:mm:ss") + @"'::timestamptz
                WHERE NOT EXISTS (SELECT 1 FROM agents WHERE ""Id"" = 'agent-chess-qa');
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Remove seed data in reverse order (respecting foreign keys)
            migrationBuilder.Sql("DELETE FROM agents WHERE \"Id\" IN ('agent-ttt-explain', 'agent-ttt-qa', 'agent-chess-explain', 'agent-chess-qa');");
            migrationBuilder.Sql("DELETE FROM rule_specs WHERE \"GameId\" IN ('tic-tac-toe', 'chess');");
            migrationBuilder.Sql("DELETE FROM games WHERE \"Id\" IN ('tic-tac-toe', 'chess');");
            migrationBuilder.Sql("DELETE FROM users WHERE \"Email\" IN ('admin@meepleai.dev', 'editor@meepleai.dev', 'user@meepleai.dev');");
        }
    }
}
