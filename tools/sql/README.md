# Production SQL Scripts

This directory contains SQL scripts for production database operations that cannot be executed within EF Core migrations.

## Background

**EF Core 9.0** removed the `IsTransactional` property from migrations. This means all migration commands now run inside transactions, which is incompatible with PostgreSQL's `CREATE INDEX CONCURRENTLY` command.

**Why CONCURRENTLY matters:**
- Standard `CREATE INDEX` takes an **ACCESS EXCLUSIVE lock** on the table, blocking all reads and writes
- `CREATE INDEX CONCURRENTLY` uses only a **SHARE UPDATE EXCLUSIVE lock**, allowing concurrent operations
- For production databases with active traffic, CONCURRENTLY is essential to avoid downtime

## Scripts

### create-indexes-concurrently.sql

Creates all performance and search indexes using `CONCURRENTLY` option.

**When to use:**
- Before deploying to production
- After running EF Core migrations on production database
- When performance indexes are missing or invalid

**Usage:**
```bash
# Production
psql -h production-db.example.com -U meepleai_user -d meepleai_prod -f create-indexes-concurrently.sql

# Staging
psql -h staging-db.example.com -U meepleai_user -d meepleai_staging -f create-indexes-concurrently.sql
```

**Duration:**
- Small datasets (< 10k rows): 1-2 minutes
- Medium datasets (10k-100k rows): 5-15 minutes
- Large datasets (> 100k rows): 30+ minutes

### drop-indexes-concurrently.sql

Drops all performance and search indexes using `CONCURRENTLY` option.

**When to use:**
- Rolling back index changes
- Rebuilding corrupted indexes
- Testing index impact

**Usage:**
```bash
psql -h production-db.example.com -U meepleai_user -d meepleai_prod -f drop-indexes-concurrently.sql
```

## Production Deployment Workflow

### Standard Deployment

1. **Run EF Core migrations** (creates tables/columns, but indexes use non-CONCURRENT mode):
   ```bash
   cd apps/api/src/Api
   dotnet ef database update --connection "Host=prod-db;Database=meepleai;..."
   ```

2. **Create indexes with CONCURRENTLY** (no downtime):
   ```bash
   psql -h prod-db -U user -d meepleai -f tools/sql/create-indexes-concurrently.sql
   ```

3. **Verify indexes created**:
   ```sql
   SELECT schemaname, tablename, indexname
   FROM pg_indexes
   WHERE indexname LIKE 'IX_%'
   ORDER BY tablename, indexname;
   ```

### Development/Staging

For dev/staging environments with minimal traffic, you can skip step 2. The migrations will create indexes using standard (blocking) `CREATE INDEX`, which is acceptable for low-traffic environments.

## Index Inventory

### Performance Indexes (20251010132507_AddPerformanceIndexes)

| Index Name | Table | Columns | Type | Purpose |
|------------|-------|---------|------|---------|
| `IX_user_sessions_TokenHash_ExpiresAt` | user_sessions | TokenHash, ExpiresAt DESC | Partial | Session validation hot path |
| `IX_user_sessions_UserId_ExpiresAt_Desc` | user_sessions | UserId, ExpiresAt DESC | Partial | Active sessions lookup |
| `IX_user_sessions_ExpiresAt_Asc` | user_sessions | ExpiresAt ASC | Standard | Session cleanup queries |
| `IX_chat_logs_ChatId_CreatedAt_Desc` | chat_logs | ChatId, CreatedAt DESC | Standard | Chat message pagination |
| `IX_chats_UserId_LastMessageAt_Desc` | chats | UserId, LastMessageAt DESC | Standard | Recent chats dashboard |
| `IX_chats_GameId_StartedAt_Desc` | chats | GameId, StartedAt DESC | Standard | Game-specific chats |
| `IX_ai_request_logs_Endpoint_CreatedAt_Desc` | ai_request_logs | Endpoint, CreatedAt DESC | Standard | Endpoint performance tracking |
| `IX_ai_request_logs_UserId_CreatedAt_Desc` | ai_request_logs | UserId, CreatedAt DESC | Partial | User activity tracking |
| `IX_ai_request_logs_GameId_CreatedAt_Desc` | ai_request_logs | GameId, CreatedAt DESC | Partial | Game usage analytics |
| `IX_pdf_documents_ProcessingStatus_UploadedAt_Desc` | pdf_documents | ProcessingStatus, UploadedAt DESC | Standard | PDF processing status |
| `IX_pdf_documents_UploadedByUserId_UploadedAt_Desc` | pdf_documents | UploadedByUserId, UploadedAt DESC | Standard | User's uploaded PDFs |
| `IX_audit_logs_UserId_CreatedAt_Desc` | audit_logs | UserId, CreatedAt DESC | Partial | Security monitoring |

### Full-Text and Vector Search Indexes (20251016151230_AddFullTextAndVectorSearchIndexes)

| Index Name | Table | Columns | Type | Purpose |
|------------|-------|---------|------|---------|
| `IX_pdf_documents_ExtractedText_GIN` | pdf_documents | to_tsvector('english', ExtractedText) | GIN | Full-text search on PDF content |
| `IX_rule_atoms_Text_GIN` | rule_atoms | to_tsvector('english', Text) | GIN | Full-text search on rules |
| `IX_pdf_documents_GameId_ProcessingStatus` | pdf_documents | GameId, ProcessingStatus | Standard | Filtered PDF listing |
| `IX_pdf_documents_GameId_UploadedAt_Desc` | pdf_documents | GameId, UploadedAt DESC | Standard | Recent PDFs per game |
| `IX_rule_atoms_RuleSpecId_Text` | rule_atoms | RuleSpecId, Text | Standard | Spec-scoped rule search |

## Troubleshooting

### "ERROR: could not create unique index" or invalid indexes

Indexes may become invalid if creation is interrupted. Check with:

```sql
SELECT indexrelid::regclass, indisvalid
FROM pg_index
WHERE NOT indisvalid;
```

Rebuild invalid indexes:

```sql
-- Drop invalid index
DROP INDEX CONCURRENTLY IF EXISTS "IX_IndexName";

-- Recreate
CREATE INDEX CONCURRENTLY "IX_IndexName" ON table_name (...);
```

### "ERROR: cannot execute CREATE INDEX CONCURRENTLY within a transaction block"

This error occurs if you try to run the script inside a transaction. Solutions:

1. **Use psql command-line** (recommended):
   ```bash
   psql -f create-indexes-concurrently.sql
   ```

2. **If using pgAdmin or other GUI**, execute statements one at a time, not in a transaction block

### Performance Impact

Monitor index creation progress:

```sql
-- Check long-running queries
SELECT pid, now() - pg_stat_activity.query_start AS duration, query
FROM pg_stat_activity
WHERE query LIKE '%CREATE INDEX%';

-- Check table sizes (estimate time)
SELECT schemaname, tablename,
       pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size
FROM pg_tables
WHERE tablename IN ('user_sessions', 'chat_logs', 'pdf_documents', 'rule_atoms', 'ai_request_logs', 'audit_logs')
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

## References

- [PostgreSQL: Building Indexes Concurrently](https://www.postgresql.org/docs/current/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- [EF Core 9.0 Breaking Changes](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes)
- MeepleAI Issue #302: Full-text and vector search indexes
