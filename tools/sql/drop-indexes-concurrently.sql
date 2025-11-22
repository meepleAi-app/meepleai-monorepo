-- ===============================================================================
-- DROP INDEXES CONCURRENTLY - Production Rollback Script
-- ===============================================================================
--
-- IMPORTANT: This script drops database indexes using CONCURRENTLY option
-- to avoid blocking production traffic during index removal.
--
-- USAGE:
--   psql -h <host> -U <user> -d <database> -f drop-indexes-concurrently.sql
--
-- NOTES:
--   - CONCURRENTLY prevents table locks during drop operation
--   - Cannot be run inside a transaction (run with psql, not in migration)
--   - Safe for production with active traffic
--   - Indexes are dropped in reverse order of creation
--
-- ===============================================================================

\echo 'Starting CONCURRENT index removal...'
\echo ''

-- ===============================================================================
-- Full-Text and Vector Search Indexes (reverse order)
-- ===============================================================================

\echo 'Dropping composite indexes...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_rule_atoms_RuleSpecId_Text";
DROP INDEX CONCURRENTLY IF EXISTS "IX_pdf_documents_GameId_UploadedAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_pdf_documents_GameId_ProcessingStatus";

\echo 'Dropping full-text search indexes (GIN)...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_rule_atoms_Text_GIN";
DROP INDEX CONCURRENTLY IF EXISTS "IX_pdf_documents_ExtractedText_GIN";

-- ===============================================================================
-- Performance Indexes (reverse order)
-- ===============================================================================

\echo 'Dropping audit_logs indexes...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_audit_logs_UserId_CreatedAt_Desc";

\echo 'Dropping pdf_documents indexes...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_pdf_documents_UploadedByUserId_UploadedAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_pdf_documents_ProcessingStatus_UploadedAt_Desc";

\echo 'Dropping ai_request_logs indexes...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_ai_request_logs_GameId_CreatedAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_ai_request_logs_UserId_CreatedAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_ai_request_logs_Endpoint_CreatedAt_Desc";

\echo 'Dropping chat indexes...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_chats_GameId_StartedAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_chats_UserId_LastMessageAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_chat_logs_ChatId_CreatedAt_Desc";

\echo 'Dropping user_sessions indexes...'

DROP INDEX CONCURRENTLY IF EXISTS "IX_user_sessions_ExpiresAt_Asc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_user_sessions_UserId_ExpiresAt_Desc";
DROP INDEX CONCURRENTLY IF EXISTS "IX_user_sessions_TokenHash_ExpiresAt";

-- ===============================================================================
-- Completion
-- ===============================================================================

\echo ''
\echo '✅ All indexes dropped successfully using CONCURRENTLY!'
\echo ''
\echo 'Verify index removal:'
\echo '  SELECT schemaname, tablename, indexname FROM pg_indexes WHERE indexname LIKE '\''IX_%'\'' ORDER BY tablename, indexname;'
\echo ''
