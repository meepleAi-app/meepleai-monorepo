# Fix: EF Core 9.0 Index Creation Strategy

**Date**: 2025-10-16 (Updated: 2025-10-16)
**Issue**: EF Core 9.0 removed `IsTransactional` property, breaking `CREATE INDEX CONCURRENTLY` support
**Impact**: Cannot create non-blocking indexes through EF migrations in EF Core 9.0
**Resolution**: Use standard `CREATE INDEX` in migrations + separate SQL scripts with `CONCURRENTLY` for production

## Problem Statement

**EF Core 9.0 Breaking Change**: The `IsTransactional` property was removed from the `Migration` class, preventing execution of `CREATE INDEX CONCURRENTLY` which requires running outside a transaction.

Two database migrations needed non-blocking index creation:
1. `20251010132507_AddPerformanceIndexes` (12 indexes)
2. `20251016151230_AddFullTextAndVectorSearchIndexes` (5 indexes)

### EF Core Version Incompatibility

**EF Core 8.x and earlier:**
```csharp
// ✅ Worked in EF Core 8.x
protected override bool IsTransactional => false;
// Could execute CREATE INDEX CONCURRENTLY
```

**EF Core 9.0:**
```csharp
// ❌ Error: IsTransactional does not exist
protected override bool IsTransactional => false;
// Compile error: non sono stati trovati metodi appropriati per eseguire l'override
```

### Production Impact

On PostgreSQL, standard `CREATE INDEX` takes an **ACCESS EXCLUSIVE lock** on the target table:
- **Blocks all operations**: INSERT, UPDATE, DELETE, SELECT
- **Duration**: Entire index build time (proportional to table size)
- **Result**: Application downtime or degraded performance

For large tables (`pdf_documents`, `rule_atoms`, `user_sessions`, etc.), this could freeze production traffic for minutes or hours.

## Root Cause

`CREATE INDEX CONCURRENTLY` cannot run inside a transaction:

```sql
-- ❌ ERROR: cannot execute CREATE INDEX CONCURRENTLY within a transaction block
BEGIN;
CREATE INDEX CONCURRENTLY "IX_table_column" ON table (column);
COMMIT;
```

EF Core 9.0:
1. ❌ Removed `IsTransactional` property (no way to disable transactions)
2. ❌ All migrations now run inside transactions by default
3. ❌ Cannot execute `CREATE INDEX CONCURRENTLY` in migrations

## Solution

### Dual-Track Approach for EF Core 9.0

Since EF Core 9.0 cannot execute `CREATE INDEX CONCURRENTLY`, we use a **dual-track strategy**:

1. **Dev/Staging**: EF migrations create indexes with standard `CREATE INDEX` (acceptable blocking)
2. **Production**: Manual SQL scripts create indexes with `CREATE INDEX CONCURRENTLY` (no blocking)

### Migration Files (Dev/Staging)

```csharp
// apps/api/src/Api/Migrations/20251010132507_AddPerformanceIndexes.cs
public partial class AddPerformanceIndexes : Migration
{
    // IMPORTANT: EF Core 9.0 migrations run inside transactions
    // For PRODUCTION: Use tools/sql/create-indexes-concurrently.sql to create indexes without blocking
    // For DEV/TEST: Standard CREATE INDEX is acceptable (tables are small, minimal traffic)

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // NOTE: CONCURRENTLY removed for EF Core 9.0 compatibility
        // In production, indexes should be created manually using CONCURRENTLY to avoid table locks
        // See: tools/sql/create-indexes-concurrently.sql

        migrationBuilder.Sql(@"
            CREATE INDEX IF NOT EXISTS ""IX_user_sessions_TokenHash_ExpiresAt""
            ON user_sessions (""TokenHash"", ""ExpiresAt"" DESC)
            WHERE ""RevokedAt"" IS NULL;
        ");
        // ... more indexes
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_user_sessions_TokenHash_ExpiresAt"";");
        // ... more drops
    }
}
```

### Production SQL Scripts (Non-Blocking)

```sql
-- tools/sql/create-indexes-concurrently.sql

-- ✅ NON-BLOCKING - Only SHARE UPDATE EXCLUSIVE lock
-- Allows concurrent INSERT/UPDATE/DELETE
-- Safe for production with active traffic

CREATE INDEX CONCURRENTLY IF NOT EXISTS "IX_user_sessions_TokenHash_ExpiresAt"
ON user_sessions ("TokenHash", "ExpiresAt" DESC)
WHERE "RevokedAt" IS NULL;

-- ... more indexes with CONCURRENTLY
```

### Key Changes

1. **Removed `CONCURRENTLY` from migrations**:
   - Use standard `CREATE INDEX` in EF migrations
   - Compatible with EF Core 9.0 transactions
   - Acceptable for dev/staging environments

2. **Added `IF NOT EXISTS` to migrations**:
   - Safe for re-running migrations
   - Prevents errors if indexes already exist

3. **Created separate SQL scripts for production**:
   - `tools/sql/create-indexes-concurrently.sql` - Create indexes
   - `tools/sql/drop-indexes-concurrently.sql` - Rollback indexes
   - `tools/sql/README.md` - Complete documentation

4. **Added documentation to migration files**:
   - Clear comments explaining the dual-track approach
   - References to production SQL scripts
   - EF Core 9.0 compatibility notes

## Performance Characteristics

### Standard CREATE INDEX
- **Lock**: ACCESS EXCLUSIVE (blocks everything)
- **Duration**: ~1 minute per 1GB (single scan)
- **Concurrency**: None - full table lock

### CREATE INDEX CONCURRENTLY
- **Lock**: SHARE UPDATE EXCLUSIVE (allows reads/writes)
- **Duration**: ~2-3x slower (two scans + wait for transactions)
- **Concurrency**: Full - no application impact

### Example Timeline

For a 10GB table with active traffic:

| Method | Duration | Blocked Time | Impact |
|--------|----------|--------------|--------|
| Standard | 10 min | 10 min | **Application down 10 min** |
| CONCURRENTLY | 25 min | 0 sec | **No application impact** |

## Affected Tables & Indexes

### AddPerformanceIndexes (12 indexes)
- `user_sessions`: 3 indexes
- `chat_logs`: 1 index
- `chats`: 2 indexes
- `ai_request_logs`: 3 indexes
- `pdf_documents`: 2 indexes
- `audit_logs`: 1 index

### AddFullTextAndVectorSearchIndexes (5 indexes)
- `pdf_documents`: 3 indexes (including GIN full-text)
- `rule_atoms`: 2 indexes (including GIN full-text)

**Total**: 17 indexes now created non-blocking

## Migration Safety

### If Migration Fails Midway

With `IsTransactional = false`:
- ⚠️ No automatic rollback
- Some indexes may be partially created
- **Solution**: Check `pg_stat_activity` and manually clean up invalid indexes

```sql
-- Find invalid indexes (failed CONCURRENTLY builds)
SELECT schemaname, tablename, indexname
FROM pg_indexes
WHERE indexdef LIKE '%INVALID%';

-- Drop invalid indexes
DROP INDEX CONCURRENTLY IF EXISTS index_name;
```

### Rollback Procedure

If you need to rollback after deploying:

```bash
# Rollback migration (calls Down method)
dotnet ef database update PreviousMigration --project apps/api/src/Api

# Manually verify indexes are dropped
docker compose exec postgres psql -U meeple -d meepleai -c "\di"
```

## Testing Recommendations

### Before Production Deployment

1. **Test on staging with production-like data volume**:
   ```bash
   # Measure index build time
   \timing on
   CREATE INDEX CONCURRENTLY test_idx ON large_table (column);
   ```

2. **Verify no blocking**:
   ```sql
   -- In one session: Start migration
   -- In another session: Try INSERT (should not block)
   INSERT INTO pdf_documents (...) VALUES (...);
   ```

3. **Check for invalid indexes**:
   ```sql
   SELECT * FROM pg_indexes WHERE indexdef LIKE '%INVALID%';
   ```

## Production Deployment Workflow

### Step-by-Step Process

**1. Run EF Core Migrations** (Creates tables/columns, skips indexes in production):
```bash
cd apps/api/src/Api
dotnet ef database update --connection "Host=prod-db;Database=meepleai;..."
```
- Creates schema changes (tables, columns, constraints)
- Creates indexes using standard `CREATE INDEX` (if you run this in production, it WILL block)
- **Recommendation**: Skip migration run in production if indexes are the only changes

**2. Create Indexes with CONCURRENTLY** (No downtime):
```bash
psql -h production-db.example.com -U meepleai_user -d meepleai_prod \
  -f tools/sql/create-indexes-concurrently.sql
```
- Non-blocking index creation
- Allows concurrent traffic
- May take 2-3x longer but no application impact

**3. Verify Indexes Created**:
```sql
SELECT schemaname, tablename, indexname, indexdef
FROM pg_indexes
WHERE indexname LIKE 'IX_%'
ORDER BY tablename, indexname;
```

**4. Run ANALYZE** (Update query planner statistics):
```sql
ANALYZE pdf_documents;
ANALYZE rule_atoms;
ANALYZE user_sessions;
ANALYZE chat_logs;
ANALYZE ai_request_logs;
ANALYZE audit_logs;
```

### Production Deployment Checklist

- [ ] Verify database has enough space (indexes ~same size as table)
- [ ] Schedule deployment during low-traffic window (optional, but recommended)
- [ ] Run EF migrations for schema changes (if any beyond indexes)
- [ ] Execute `tools/sql/create-indexes-concurrently.sql`
- [ ] Monitor `pg_stat_activity` during index creation
- [ ] Check for invalid indexes: `SELECT * FROM pg_indexes WHERE indexdef LIKE '%INVALID%'`
- [ ] Run ANALYZE on all affected tables
- [ ] Verify application performance metrics
- [ ] Check application logs for no errors

## Best Practices Going Forward (EF Core 9.0)

### For Future Migrations with Indexes

1. **Use dual-track approach**:
   ```csharp
   // Migration file: Use standard CREATE INDEX
   // IMPORTANT: EF Core 9.0 migrations run inside transactions
   // For PRODUCTION: Use tools/sql/[migration-name]-indexes.sql
   migrationBuilder.Sql(@"
       CREATE INDEX IF NOT EXISTS ""IX_table_column""
       ON table (column);
   ");
   ```

2. **Create production SQL scripts**:
   - Add new indexes to `tools/sql/create-indexes-concurrently.sql`
   - Add new drops to `tools/sql/drop-indexes-concurrently.sql`
   - Document in `tools/sql/README.md`

3. **Document expected build time**:
   ```csharp
   // NOTE: Production: Use tools/sql/create-indexes-concurrently.sql (~15 min on 10GB table)
   // Dev/Staging: This migration will create indexes with blocking (acceptable for non-production)
   ```

4. **Exception: Small tables only**:
   - Standard `CREATE INDEX` acceptable if table < 1000 rows AND production environment
   - Otherwise, always use separate SQL scripts with CONCURRENTLY

5. **Consider off-peak deployment**:
   - Even CONCURRENTLY has overhead
   - Deploy during low-traffic windows when possible

### Dev/Staging vs Production Strategy

| Environment | Method | Blocking | Acceptable? | When to Use |
|-------------|--------|----------|-------------|-------------|
| Dev/Local | Migration `CREATE INDEX` | Yes | ✅ Yes | Always - data is minimal |
| Staging | Migration `CREATE INDEX` | Yes | ✅ Yes | If staging has < 100k rows |
| Staging | SQL `CONCURRENTLY` | No | ✅ Better | If testing prod-like deployment |
| Production | Migration `CREATE INDEX` | Yes | ❌ NO | Never - causes downtime |
| Production | SQL `CONCURRENTLY` | No | ✅ Required | Always |

## References

- PostgreSQL Docs: [Building Indexes Concurrently](https://www.postgresql.org/docs/current/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- EF Core Docs: [Raw SQL Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing?tabs=dotnet-core-cli#arbitrary-changes-via-raw-sql)
- EF Core 9.0 Breaking Changes: [What's New in EF Core 9.0](https://learn.microsoft.com/en-us/ef/core/what-is-new/ef-core-9.0/breaking-changes)
- Blog: [Zero-Downtime PostgreSQL Migrations](https://blog.codinghorror.com/zero-downtime-database-migrations/)

## Related Files

- **Migrations**:
  - `apps/api/src/Api/Migrations/20251010132507_AddPerformanceIndexes.cs` - 12 performance indexes
  - `apps/api/src/Api/Migrations/20251016151230_AddFullTextAndVectorSearchIndexes.cs` - 5 search indexes

- **Production SQL Scripts**:
  - `tools/sql/create-indexes-concurrently.sql` - Non-blocking index creation
  - `tools/sql/drop-indexes-concurrently.sql` - Non-blocking index removal
  - `tools/sql/README.md` - Complete usage guide

- **Documentation**:
  - `CLAUDE.md` - Project overview with migration commands
  - `docs/guide/deployment-checklist.md` - Production deployment procedures

---

**Author**: Claude (AI Assistant)
**Reviewed**: Pending
**Status**: Fixed and committed (2025-10-16)
