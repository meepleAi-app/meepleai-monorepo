# Fix: Non-Blocking Index Creation with CONCURRENTLY

**Date**: 2025-10-16
**Issue**: Database migrations were creating indexes with ACCESS EXCLUSIVE locks
**Impact**: Production traffic could be blocked during schema upgrades
**Resolution**: Updated migrations to use `CREATE INDEX CONCURRENTLY`

## Problem Statement

Two database migrations were creating indexes using standard `CREATE INDEX` statements inside EF Core transactions:
1. `20251010132507_AddPerformanceIndexes` (12 indexes)
2. `20251016151230_AddFullTextAndVectorSearchIndexes` (5 indexes)

### Production Impact

On PostgreSQL, standard `CREATE INDEX` takes an **ACCESS EXCLUSIVE lock** on the target table:
- **Blocks all operations**: INSERT, UPDATE, DELETE, SELECT
- **Duration**: Entire index build time (proportional to table size)
- **Result**: Application downtime or degraded performance

For large tables (`pdf_documents`, `rule_atoms`, `user_sessions`, etc.), this could freeze production traffic for minutes or hours.

## Root Cause

```csharp
// ❌ BLOCKING - Takes ACCESS EXCLUSIVE lock
migrationBuilder.Sql(@"
    CREATE INDEX IF NOT EXISTS ""IX_table_column""
    ON table (column);
");
```

Issues:
1. Standard `CREATE INDEX` blocks concurrent writes
2. Runs inside EF transaction (default `IsTransactional = true`)
3. No explicit consideration for production traffic

## Solution

### Changed To: CREATE INDEX CONCURRENTLY

```csharp
public partial class Migration : Migration
{
    // Required: CONCURRENTLY cannot run in transaction
    protected override bool IsTransactional => false;

    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ✅ NON-BLOCKING - Only SHARE UPDATE EXCLUSIVE lock
        migrationBuilder.Sql(@"
            CREATE INDEX CONCURRENTLY ""IX_table_column""
            ON table (column);
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ✅ NON-BLOCKING drop
        migrationBuilder.Sql(@"
            DROP INDEX CONCURRENTLY IF EXISTS ""IX_table_column"";
        ");
    }
}
```

### Key Changes

1. **Added `IsTransactional => false`**:
   - Required because `CONCURRENTLY` cannot run in transaction
   - Disables automatic EF transaction wrapping

2. **Changed `CREATE INDEX` → `CREATE INDEX CONCURRENTLY`**:
   - Only takes SHARE UPDATE EXCLUSIVE lock
   - Allows concurrent INSERT/UPDATE/DELETE
   - Two table scans instead of one (slower but non-blocking)

3. **Removed `IF NOT EXISTS`**:
   - `CREATE INDEX CONCURRENTLY` doesn't support it
   - Not needed: re-running migration will fail gracefully if index exists

4. **Changed `DROP INDEX` → `DROP INDEX CONCURRENTLY`**:
   - Non-blocking index removal
   - Safe for production rollbacks

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

## Production Deployment Checklist

- [ ] Verify database has enough space (indexes ~same size as table)
- [ ] Monitor `pg_stat_activity` during deployment
- [ ] Check application logs for no errors
- [ ] Verify all indexes created successfully:
  ```sql
  SELECT indexname, indexdef
  FROM pg_indexes
  WHERE tablename IN ('pdf_documents', 'rule_atoms', 'user_sessions');
  ```
- [ ] Run ANALYZE after index creation:
  ```sql
  ANALYZE pdf_documents;
  ANALYZE rule_atoms;
  ANALYZE user_sessions;
  ```

## Best Practices Going Forward

### For Future Migrations with Indexes

1. **Always use CONCURRENTLY for production**:
   ```csharp
   protected override bool IsTransactional => false;
   // Use CREATE INDEX CONCURRENTLY
   ```

2. **Exception: Small tables only**:
   - Standard `CREATE INDEX` acceptable if table < 1000 rows
   - Or if guaranteed to run in non-production environment

3. **Document expected build time**:
   ```csharp
   // NOTE: Takes ~15 min on production (10GB table)
   migrationBuilder.Sql(@"CREATE INDEX CONCURRENTLY ...");
   ```

4. **Consider off-peak deployment**:
   - CONCURRENTLY still has overhead
   - Deploy during low-traffic windows when possible

## References

- PostgreSQL Docs: [Building Indexes Concurrently](https://www.postgresql.org/docs/current/sql-createindex.html#SQL-CREATEINDEX-CONCURRENTLY)
- EF Core Docs: [Raw SQL Migrations](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing?tabs=dotnet-core-cli#arbitrary-changes-via-raw-sql)
- Blog: [Zero-Downtime PostgreSQL Migrations](https://blog.codinghorror.com/zero-downtime-database-migrations/)

---

**Author**: Claude (AI Assistant)
**Reviewed**: Pending
**Status**: Fixed and committed
