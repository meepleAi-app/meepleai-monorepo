-- ===============================================================================
-- CREATE INDEXES CONCURRENTLY - Production Deployment Script
-- ===============================================================================
--
-- IMPORTANT: This script creates database indexes using CONCURRENTLY option
-- to avoid blocking production traffic during index creation.
--
-- USAGE:
--   psql -h <host> -U <user> -d <database> -f create-indexes-concurrently.sql
--
-- NOTES:
--   - CONCURRENTLY prevents table locks but takes longer to complete
--   - Does NOT take ACCESS EXCLUSIVE lock (only SHARE UPDATE EXCLUSIVE)
--   - Allows concurrent INSERT/UPDATE/DELETE operations
--   - Cannot be run inside a transaction (run with psql, not in migration)
--   - Safe for production with active traffic
--
-- ROLLBACK:
--   See: drop-indexes-concurrently.sql
--
-- ===============================================================================

\echo 'Starting CONCURRENT index creation...'
\echo 'This may take several minutes depending on table sizes.'
\echo ''

-- ===============================================================================
-- Performance Indexes (from migration 20251010132507)
-- ===============================================================================

\echo 'Creating user_sessions indexes...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_sessions_TokenHash_ExpiresAt"
ON user_sessions ("TokenHash", "ExpiresAt" DESC)
WHERE "RevokedAt" IS NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_sessions_UserId_ExpiresAt_Desc"
ON user_sessions ("UserId", "ExpiresAt" DESC)
WHERE "RevokedAt" IS NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_sessions_ExpiresAt_Asc"
ON user_sessions ("ExpiresAt" ASC);

\echo 'Creating chat indexes...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_chat_logs_ChatId_CreatedAt_Desc"
ON chat_logs ("ChatId", "CreatedAt" DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_chats_UserId_LastMessageAt_Desc"
ON chats ("UserId", "LastMessageAt" DESC NULLS LAST);

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_chats_GameId_StartedAt_Desc"
ON chats ("GameId", "StartedAt" DESC);

\echo 'Creating ai_request_logs indexes...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ai_request_logs_Endpoint_CreatedAt_Desc"
ON ai_request_logs ("Endpoint", "CreatedAt" DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ai_request_logs_UserId_CreatedAt_Desc"
ON ai_request_logs ("UserId", "CreatedAt" DESC)
WHERE "UserId" IS NOT NULL;

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_ai_request_logs_GameId_CreatedAt_Desc"
ON ai_request_logs ("GameId", "CreatedAt" DESC)
WHERE "GameId" IS NOT NULL;

\echo 'Creating pdf_documents indexes...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_pdf_documents_ProcessingStatus_UploadedAt_Desc"
ON pdf_documents ("ProcessingStatus", "UploadedAt" DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_pdf_documents_UploadedByUserId_UploadedAt_Desc"
ON pdf_documents ("UploadedByUserId", "UploadedAt" DESC);

\echo 'Creating audit_logs indexes...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_audit_logs_UserId_CreatedAt_Desc"
ON audit_logs ("UserId", "CreatedAt" DESC)
WHERE "UserId" IS NOT NULL;

-- ===============================================================================
-- Full-Text and Vector Search Indexes (from migration 20251016151230)
-- ===============================================================================

\echo 'Creating full-text search indexes (GIN)...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_pdf_documents_ExtractedText_GIN"
ON pdf_documents USING GIN (to_tsvector('english', COALESCE("ExtractedText", '')));

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_rule_atoms_Text_GIN"
ON rule_atoms USING GIN (to_tsvector('english', "Text"));

\echo 'Creating composite indexes...'

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_pdf_documents_GameId_ProcessingStatus"
ON pdf_documents ("GameId", "ProcessingStatus");

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_pdf_documents_GameId_UploadedAt_Desc"
ON pdf_documents ("GameId", "UploadedAt" DESC);

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_rule_atoms_RuleSpecId_Text"
ON rule_atoms ("RuleSpecId", "Text");

-- ===============================================================================
-- Completion
-- ===============================================================================

\echo ''
\echo '✅ All indexes created successfully using CONCURRENTLY!'
\echo ''
\echo 'Verify index creation:'
\echo '  SELECT schemaname, tablename, indexname FROM pg_indexes WHERE indexname LIKE '\''IX_%'\'' ORDER BY tablename, indexname;'
\echo ''
