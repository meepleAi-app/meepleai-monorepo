# GitHub Secrets Fix - Issue #2284

**Status**: ⚠️ REQUIRES ADMIN ACTION
**Priority**: 🔴 HIGH
**Issue**: [#2284](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2284)

---

## Problem

GitHub Actions secret `TEST_DB_CONNECTION_STRING` contains **outdated username** causing PostgreSQL authentication failures in CI.

### Current Error
```
Npgsql.PostgresException: 28P01: password authentication failed for user "meeple"
Detail: Role "meeple" does not exist.
```

### Root Cause
- **Secret Value**: Uses username `meeple` (legacy)
- **Service Config**: PostgreSQL service uses `postgres` (standard)
- **Migration**: Issue #2152 changed default from `meepleai` to `postgres` for consistency

### Affected Jobs
- ❌ API - Smoke Tests (Postman/Newman)
- ❌ API - Unit & Integration Tests (90%)

---

## Fix Required (Admin Only)

### Prerequisites
- Repository admin access
- GitHub Settings → Secrets and variables → Actions

### Step 1: Update Secret

Navigate to repository secrets and update `TEST_DB_CONNECTION_STRING`:

**OLD VALUE** (current - causes failures):
```
Host=localhost;Port=5432;Database=meepleai_dev;Username=meeple;Password=meepleai_dev_password
```

**NEW VALUE** (correct):
```
Host=localhost;Port=5432;Database=meepleai_dev;Username=postgres;Password=meepleai_dev_password
```

### Step 2: Verify Related Secrets

Ensure consistency across all test-related secrets:

| Secret Name | Correct Value | Status |
|-------------|---------------|--------|
| `TEST_POSTGRES_USER` | `postgres` | ✅ Already correct |
| `TEST_POSTGRES_PASSWORD` | `meepleai_dev_password` | ✅ Already correct |
| `TEST_DB_CONNECTION_STRING` | (see above) | ⚠️ **NEEDS UPDATE** |

### Step 3: Test Fix

After updating the secret:

1. **Trigger workflow**: Push a commit or re-run failed workflow
2. **Verify success**: Check "API - Smoke Tests" and "API - Unit & Integration Tests" jobs
3. **Expected result**: Tests should pass without auth errors

---

## Verification Commands

For local testing (developers can verify the fix locally):

```bash
# Correct connection string format
dotnet test --filter "Category=Integration" \
  /p:ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=testpass"

# Should connect successfully without "Role does not exist" errors
```

---

## Why This Happened

1. **Legacy migration**: Original setup used `meeple` username
2. **Standardization** (Issue #2152): Migrated to PostgreSQL standard `postgres` user
3. **Secret lag**: CI secret wasn't updated during migration
4. **Detection**: Discovered during PR #2283 GitHub Actions investigation

---

## Impact Timeline

- **Before Fix**: API tests fail in CI (false negatives)
- **After Fix**: Full test coverage restored (~90% of API test suite)

---

## Related Issues

- Issue #2152 - Database username migration (postgres standard)
- Issue #2284 - CI pre-existing test failures (this fix)
- PR #2283 - GitHub Actions fixes (uncovered this issue)

---

## Admin Checklist

- [ ] Update `TEST_DB_CONNECTION_STRING` secret with correct username
- [ ] Verify secret value matches format exactly (no typos)
- [ ] Trigger test workflow to confirm fix
- [ ] Update issue #2284 with completion status
- [ ] Close this documentation task

---

**Questions?** Contact: Engineering Lead / DevOps Team
**Last Updated**: 2025-12-22
