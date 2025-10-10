using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Session validation hot path - covers 80% of authentication queries
            // Note: Removed NOW() from predicate as it's not IMMUTABLE
            // Query will still benefit from partial index on non-revoked sessions
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_user_sessions_TokenHash_ExpiresAt""
                ON user_sessions (""TokenHash"", ""ExpiresAt"" DESC)
                WHERE ""RevokedAt"" IS NULL;
            ");

            // Active sessions lookup for admin dashboard
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_user_sessions_UserId_ExpiresAt_Desc""
                ON user_sessions (""UserId"", ""ExpiresAt"" DESC)
                WHERE ""RevokedAt"" IS NULL;
            ");

            // Session cleanup and expiration queries - all sessions ordered by expiration
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_user_sessions_ExpiresAt_Asc""
                ON user_sessions (""ExpiresAt"" ASC);
            ");

            // Chat message retrieval - optimizes pagination with DESC order
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_chat_logs_ChatId_CreatedAt_Desc""
                ON chat_logs (""ChatId"", ""CreatedAt"" DESC);
            ");

            // Recent chats for user dashboard (handles NULL LastMessageAt)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_chats_UserId_LastMessageAt_Desc""
                ON chats (""UserId"", ""LastMessageAt"" DESC NULLS LAST);
            ");

            // Game-specific chats for filtering
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_chats_GameId_StartedAt_Desc""
                ON chats (""GameId"", ""StartedAt"" DESC);
            ");

            // AI request log analytics - endpoint performance tracking
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ai_request_logs_Endpoint_CreatedAt_Desc""
                ON ai_request_logs (""Endpoint"", ""CreatedAt"" DESC);
            ");

            // User activity tracking for admin dashboard
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ai_request_logs_UserId_CreatedAt_Desc""
                ON ai_request_logs (""UserId"", ""CreatedAt"" DESC)
                WHERE ""UserId"" IS NOT NULL;
            ");

            // Game usage analytics (partial index for non-NULL GameId)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_ai_request_logs_GameId_CreatedAt_Desc""
                ON ai_request_logs (""GameId"", ""CreatedAt"" DESC)
                WHERE ""GameId"" IS NOT NULL;
            ");

            // PDF processing status tracking
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_ProcessingStatus_UploadedAt_Desc""
                ON pdf_documents (""ProcessingStatus"", ""UploadedAt"" DESC);
            ");

            // User's uploaded PDFs
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_pdf_documents_UploadedByUserId_UploadedAt_Desc""
                ON pdf_documents (""UploadedByUserId"", ""UploadedAt"" DESC);
            ");

            // Audit log queries for security monitoring (partial index)
            migrationBuilder.Sql(@"
                CREATE INDEX IF NOT EXISTS ""IX_audit_logs_UserId_CreatedAt_Desc""
                ON audit_logs (""UserId"", ""CreatedAt"" DESC)
                WHERE ""UserId"" IS NOT NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Drop indexes in reverse order
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_audit_logs_UserId_CreatedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_pdf_documents_UploadedByUserId_UploadedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_pdf_documents_ProcessingStatus_UploadedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ai_request_logs_GameId_CreatedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ai_request_logs_UserId_CreatedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_ai_request_logs_Endpoint_CreatedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_chats_GameId_StartedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_chats_UserId_LastMessageAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_chat_logs_ChatId_CreatedAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_user_sessions_ExpiresAt_Asc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_user_sessions_UserId_ExpiresAt_Desc"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_user_sessions_TokenHash_ExpiresAt"";");
        }
    }
}
