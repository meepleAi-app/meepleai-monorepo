# Migration Ordering Issue - Critical Bug

**Discovered**: 2025-11-14 during SPRINT-5 integration test implementation (Issue #870)
**Severity**: High (blocks integration tests, potential production risk)
**Status**: Documented, needs resolution

## Problem

EF Core migrations have **broken chronological order** that prevents `MigrateAsync()` from working on fresh databases.

### Migration Timeline (Broken Order)

```
1. 20251113055726_AddLlmCostTracking.cs (Nov 13)
   - Creates foreign key: FK_llm_cost_logs_users_user_id
   - **REFERENCES "users" table** ← TABLE DOESN'T EXIST YET ❌

2. 20251114092909_AddUserIdAndStatusToChatThreads.cs (Nov 14)
   - Likely references existing tables

3. 20251114102435_Issue861_GameSessionIndexesAndConfiguration.cs (Nov 14)
   - Likely references existing tables

4. 20251114153506_InitialMigration.cs (Nov 14)
   - **CREATES "users" table** ← TOO LATE ❌
   - Should have been FIRST migration
```

### Root Cause

**Misleading Migration Name**: `InitialMigration` was created Nov 14, **AFTER** the database already had data.

**Consequence**: Fresh databases (test containers, new deployments) cannot run migrations because:
1. Migration #1 tries to create FK to non-existent "users" table
2. Migration #4 finally creates "users" table
3. Order is wrong → SQL error: `42P01: relation "users" does not exist`

## Impact

### Integration Tests ❌
```csharp
await _dbContext.Database.MigrateAsync(); // FAILS on fresh DB
```
**Error**: `Npgsql.PostgresException : 42P01: relation "users" does not exist`

### Workaround for Tests ✅
```csharp
await _dbContext.Database.EnsureCreatedAsync(); // Works - bypasses migrations
```

### Production Risk ⚠️
- New deployments to fresh databases will fail
- Cannot rollback to empty database state
- CI/CD pipelines may fail if they create fresh test databases

## Solutions

### Option 1: Squash All Migrations (RECOMMENDED for clean state)

**Steps:**
```bash
# 1. Backup current database
pg_dump meepleai > backup.sql

# 2. Delete all migration files
rm apps/api/src/Api/Migrations/*.cs

# 3. Create fresh initial migration with correct order
dotnet ef migrations add InitialCreate --project apps/api/src/Api

# 4. Verify migration creates all tables in correct order
dotnet ef migrations script --project apps/api/src/Api > verify.sql

# 5. Test on fresh database
docker run --rm -e POSTGRES_PASSWORD=test postgres:16-alpine
dotnet ef database update --project apps/api/src/Api
```

**Pros:**
- ✅ Clean migration history
- ✅ Correct table creation order
- ✅ Works on fresh databases

**Cons:**
- ❌ Requires database backup/restore
- ❌ Breaks existing deployments (need migration strategy)
- ❌ High risk if not tested thoroughly

### Option 2: Reorder Migration Timestamps (Complex)

**Steps:**
1. Rename migration files to correct chronological order
2. Update `__EFMigrationsHistory` table manually
3. Test thoroughly

**Pros:**
- ✅ Preserves migration history

**Cons:**
- ❌ Error-prone manual process
- ❌ Requires database modification
- ❌ Risk of data corruption

### Option 3: Keep EnsureCreated for Tests (Current Workaround)

**Implementation:**
```csharp
// In test setup
if (Environment.GetEnvironmentVariable("CI") == "true")
{
    await _dbContext.Database.EnsureCreatedAsync(); // For tests
}
else
{
    await _dbContext.Database.MigrateAsync(); // For production
}
```

**Pros:**
- ✅ Quick fix for tests
- ✅ No production changes needed
- ✅ Low risk

**Cons:**
- ❌ Doesn't solve root cause
- ❌ Tests don't validate migrations
- ❌ Production risk remains

### Option 4: Fix First Migration (Surgical Fix)

**Steps:**
1. Edit `20251113055726_AddLlmCostTracking.cs`
2. Remove FK constraint to "users" (or make it nullable)
3. Add migration to create FK after users table exists

**Pros:**
- ✅ Minimal changes
- ✅ Preserves history

**Cons:**
- ❌ Requires careful SQL editing
- ❌ May break existing deployments

## Recommendation

**Immediate (Tests)**: Use Option 3 (EnsureCreated) ✅ **IMPLEMENTED**

**Long-term (Production)**: Schedule Option 1 (Squash migrations) for next maintenance window

**Priority**: Medium-High (blocks integration tests, but production works via existing DB)

## Files Affected

- `apps/api/src/Api/Migrations/20251113055726_AddLlmCostTracking.cs`
- `apps/api/src/Api/Migrations/20251114153506_InitialMigration.cs`
- All integration test files using `MigrateAsync()`

## Related Issues

- Issue #870: Integration Test Suite (blocked by this)
- Issue #1142: Test implementation (workaround applied)

## Testing Validation

```bash
# Test fresh database migration
docker run --rm -p 5433:5432 -e POSTGRES_PASSWORD=test postgres:16-alpine &
dotnet ef database update --connection "Host=localhost;Port=5433;Database=test;Username=postgres;Password=test;" --project apps/api/src/Api

# Should fail with: relation "users" does not exist
# After fix: Should succeed
```

## Resolution Tracking

- [ ] Create issue for migration squashing
- [ ] Schedule maintenance window
- [ ] Test migration squash on staging
- [ ] Apply to production
- [ ] Update integration tests to use MigrateAsync again

---

**Last Updated**: 2025-11-14
**Discovered By**: Claude Code (root-cause-analyst)
**Workaround**: EnsureCreatedAsync() in tests ✅
