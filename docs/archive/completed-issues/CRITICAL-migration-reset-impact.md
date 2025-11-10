# CRITICAL: EF Core Migration Reset Impact Analysis

## Issue
Commits `99a4e488` (Oct 26) and `a852cbfb` (Nov 8) deleted all existing EF Core migrations and created new `InitialCreate` migrations. This **breaks migration continuity** for any database that applied the old migrations.

## Timeline

### Oct 26, 2025 - First Reset (commit 99a4e488)
**Action**: Removed 24 broken migrations, created `20251026150336_InitialCreate`
**Reason**: CONFIG migration column name mismatches (209 test failures)
**User Confirmation**: "No data loss - databases were empty per user confirmation"
**Migrations Removed**:
- All 14 from `apps/api/src/Api/Migrations/` (wrong path)
- 2 from `apps/api/src/Api/Infrastructure/Migrations/` (AI-14, OPS-07)

### Nov 8, 2025 - Second Reset (commit a852cbfb)
**Action**: Removed 3 migrations, created `20251108082640_InitialCreate`
**Reason**: Align model snapshot with current schema (test fixes)
**Migrations Removed**:
- `20251026150336_InitialCreate`
- `20251107000000_MakePasswordHashNullable`
- 1 other migration

## Current State
**Active Migration**: `20251108082640_InitialCreate` only

## Impact Assessment

### ❌ **Affected Scenarios** (BREAKING)
If ANY database has `__EFMigrationsHistory` entries for deleted migrations:
- `dotnet ef database update` will FAIL
- Error: "Migration X has been applied but not known by program"
- **Cannot apply new migrations without manual intervention**

Affected migration IDs:
- `20251026150336_InitialCreate` (deleted Nov 8)
- `20251107000000_MakePasswordHashNullable` (deleted Nov 8)
- All 24 migrations from Oct 26 reset

### ✅ **Safe Scenarios** (NO IMPACT)
- Fresh database installations (no migrations applied yet)
- Databases created AFTER Nov 8, 2025
- Development databases rebuilt from scratch

## Risk Assessment

### Critical Questions
1. **Production database exists?** → Check if production DB has old migrations
2. **Staging database exists?** → Check if staging DB has old migrations
3. **Team member local DBs?** → Check if other developers have old migrations
4. **Docker volumes?** → Check if persistent volumes have old schemas

### Likelihood
- **Development**: HIGH (local Docker volumes likely have old migrations)
- **Staging**: MEDIUM (if staging environment exists)
- **Production**: LOW (project appears pre-production)

## Migration Recovery Strategies

### Option 1: Manual History Cleanup (For Each Affected DB)
```sql
-- 1. Backup database first!
pg_dump meepleai > backup_before_migration_fix.sql

-- 2. Clear migration history
DELETE FROM "__EFMigrationsHistory";

-- 3. Mark current schema as InitialCreate
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20251108082640_InitialCreate', '9.0.0');

-- 4. Now dotnet ef database update will work
```

### Option 2: Database Recreation (Simplest)
```bash
# Local development
docker compose down -v  # Destroys volumes
docker compose up -d postgres qdrant redis
cd apps/api/src/Api && dotnet ef database update

# Pros: Clean start, no migration conflicts
# Cons: Loses all test/development data
```

### Option 3: Restore Old Migrations (NOT RECOMMENDED)
```bash
# Revert commits and restore old migrations
git revert a852cbfb 99a4e488
# Pros: Preserves migration history
# Cons: Brings back broken migrations, 209 test failures return
```

### Option 4: Schema-First Migration (Advanced)
```bash
# 1. Generate SQL script for current schema
dotnet ef migrations script --idempotent --output current_schema.sql

# 2. Apply to existing database
psql -U user -d meepleai < current_schema.sql

# 3. Manually insert migration record
# (see Option 1 SQL)
```

## Recommended Action Plan

### Immediate Actions (Next 30 min)
1. **Survey environments**: Check which databases exist and their migration state
2. **Assess impact**: Determine how many databases are affected
3. **Choose strategy**: Based on data value and environment count

### Short-term (Next 2 hours)
4. **Execute recovery**: Apply chosen strategy to affected databases
5. **Test migration**: Verify `dotnet ef database update` works
6. **Document process**: Create runbook for future reference

### Long-term (Next sprint)
7. **Prevent recurrence**: Add migration validation to CI/CD
8. **Team communication**: Notify team of database reset requirement
9. **Backup strategy**: Implement regular database backups

## Prevention Measures

### CI/CD Checks
```yaml
# .github/workflows/migration-guard.yml
- name: Validate migrations not deleted
  run: |
    # Compare migrations against main branch
    # Fail if existing migrations were deleted
```

### Development Workflow
1. **Never delete applied migrations** in shared branches
2. **Use `dotnet ef migrations add`** for schema changes
3. **Coordinate resets** with team (require manual approval)
4. **Document resets** in commit messages with impact assessment

## Current Recommendation

**FOR THIS PROJECT**:
Given commit messages indicate "databases were empty" and this appears to be **early development**:

1. **No immediate action required** IF all developers/environments can rebuild
2. **Document the reset** in README or deployment docs
3. **Add note** to `.env.example` files about fresh DB requirement
4. **Update** any deployment scripts to expect fresh schema

**IF production/staging databases exist**:
1. **URGENT**: Stop deployments
2. **Backup databases immediately**
3. **Apply Option 1 (Manual History Cleanup)** to each affected DB
4. **Test thoroughly** before resuming deployments

## Files to Update

```
README.md - Add note about database reset on Nov 8
docs/database-schema.md - Update with reset history
infra/README.md - Add fresh database setup instructions
.github/workflows/ - Add migration validation check
```

## Questions for User/Team

1. Do any production or staging databases exist?
2. Do other team members have local databases with old migrations?
3. Is data preservation required, or can all DBs be rebuilt?
4. Should we add migration deletion detection to CI/CD?

---
**Priority**: P1 (Critical if databases exist with old migrations)
**Created**: Nov 8, 2025
**Related**: Commits 99a4e488, a852cbfb, c828a875
