# K6 Performance Tests Troubleshooting Guide

## Issue #2020: K6 Performance Tests Failed (PostgreSQL 'role root' Error)

### Symptom
```
2025-12-11 03:19:48.746 UTC [75] FATAL:  role "root" does not exist
2025-12-11 03:19:58.830 UTC [83] FATAL:  role "root" does not exist
```

K6 performance tests fail during both "Apply migrations" and "Wait for API to be ready" steps. PostgreSQL logs show repeated connection attempts with user "root" (GitHub Actions default) instead of "postgres".

### Root Cause (Deep Investigation)
**Primary Cause**: `IDesignTimeDbContextFactory<T>` implementation ignores `--connection` flag.

The `dotnet ef database update --connection` flag is **IGNORED** when a design-time factory exists:
1. EF Core finds `MeepleAiDbContextFactory` implementing `IDesignTimeDbContextFactory<MeepleAiDbContext>`
2. Calls `CreateDbContext(string[] args)` method
3. **The factory reads ONLY from `Environment.GetEnvironmentVariable()`**, NOT from `args[]`
4. Without env var, factory throws exception and defaults to system user ("root")

**Evidence from Code** (`MeepleAiDbContextFactory.cs` line 29-33):
```csharp
var connectionString = Environment.GetEnvironmentVariable("CONNECTIONSTRINGS__POSTGRES")
    ?? Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Database connection string not configured. " +
        "Set CONNECTIONSTRINGS__POSTGRES or POSTGRES_CONNECTION_STRING environment variable.");
```

**Why Issue #1817 Solution Was Incomplete**:
- Issue #1817 added `--connection` flag assuming it would work
- Flag is passed to EF Core CLI but factory **doesn't read it**
- Factory pattern takes precedence over command-line arguments

### Solution (Fixed 2025-12-11)
**Phase 1**: Add `ConnectionStrings__Postgres` to "Wait for API to be ready" step (PR #2057)
**Phase 2**: Remove `--connection` flag, use env var for "Apply migrations" (Option C)

```yaml
# Apply migrations - Use env var (NOT --connection flag)
- name: Apply migrations
  working-directory: apps/api/src/Api
  run: dotnet ef database update --configuration Release
  env:
    ConnectionStrings__Postgres: ${{ secrets.TEST_DB_CONNECTION_STRING || '...' }}

# Wait for API to be ready
- name: Wait for API to be ready
  run: |
    for i in {1..30}; do
      if curl -f http://localhost:8080/health; then
        echo "✅ API is ready"
        exit 0
      fi
      echo "Waiting for API... ($i/30)"
      sleep 2
    done
    echo "❌ API failed to start"
    exit 1
  env:
    ConnectionStrings__Postgres: ${{ secrets.TEST_DB_CONNECTION_STRING || '...' }}
```

**Why Option C (env var only) Over Option A (flag + env var)**:
- Option A = Redundant (flag doesn't work anyway)
- Option B = Modify factory code (higher risk, production change)
- **Option C = Clean, single source of truth, aligns with existing pattern**

### Impact
- **Duration**: 10+ consecutive nightly runs failed (2025-12-01 to 2025-12-11)
- **Severity**: P1-High (blocks production readiness monitoring)
- **Resolution Time**: Phase 1 (~1 hour), Phase 2 (~2 hours investigation + fix)

### Key Learnings
1. **EF Core Design-Time Factories Take Precedence**: Command-line flags like `--connection` are ignored when `IDesignTimeDbContextFactory<T>` exists
2. **Environment Variables Are Authoritative**: EF Core factories read from `Environment.GetEnvironmentVariable()`, not command args
3. **Test All Assumptions**: Issue #1817 assumed `--connection` would work without verifying factory behavior
4. **Consistent Patterns Win**: Using env vars across ALL steps (Build, Migrations, Start, Wait) prevents confusion

### Validation
1. ✅ Manual workflow dispatch with `test_type=smoke`
2. ✅ Verify PostgreSQL logs show no "role root" errors
3. ✅ Confirm K6 tests execute successfully
4. ✅ Monitor next scheduled nightly run (2 AM UTC)

---

## Issue #1976: Database Connection Not Configured (Build Phase)

### Symptom
```
Unable to create a 'DbContext' of type 'RuntimeType'.
The exception 'Database connection string not configured.
Set CONNECTIONSTRINGS__POSTGRES or POSTGRES_CONNECTION_STRING environment variable.'
was thrown while attempting to create an instance.
```

### Root Cause
EF Core tools (`dotnet ef`) require database connection string during build phase to validate DbContext configuration, even when using `--no-restore` flag.

### Solution
Ensure `ConnectionStrings__Postgres` environment variable is set in **all** workflow steps that interact with EF Core:

1. ✅ **Build API** (Issue #1976 fix)
2. ✅ **Apply migrations** (already present)
3. ✅ **Start API server** (already present)
4. ✅ **Wait for API to be ready** (Issue #2020 fix)

### Prevention Checklist
- [ ] All `dotnet build` steps have `ConnectionStrings__Postgres` env var
- [ ] All `dotnet ef` commands have `--connection` flag OR env var
- [ ] All `dotnet run` steps have database connection in `env:` section
- [ ] All health check / API validation steps have `ConnectionStrings__Postgres` env var (Issue #2020)

### Validation Command
```bash
# Local test (requires Docker services running)
cd apps/api/src/Api
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres"
dotnet build --configuration Release --no-restore
```

### Related Issues
- **#2020**: K6 performance tests failed (PostgreSQL 'role root' error) - Fixed 2025-12-11
- **#1976**: Database connection not configured during build - Fixed 2025-11-XX
- **#1817**: Database connectivity validation
- **#1954**: Database creation error handling
- **#1663**: Test user seeding automation

### CI/CD Best Practices
1. **Always use secrets** for production connection strings
2. **Provide fallbacks** for local/test environments: `${{ secrets.VAR || 'default' }}`
3. **Validate early** with explicit health checks before running tests
4. **Centralize env vars** at job/workflow level when possible

### Common Pitfalls
❌ **Wrong**: Assuming EF Core skips validation during build
✅ **Right**: Provide connection string for all EF Core operations

❌ **Wrong**: Only setting env var in `dotnet run` step
✅ **Right**: Set env var in `dotnet build` + `dotnet ef` + `dotnet run`

### Diagnostic Commands
```bash
# Check PostgreSQL connection with explicit user
PGPASSWORD="postgres" psql -h localhost -U postgres -d meepleai_test -c "SELECT version();"

# Verify GitHub Actions PostgreSQL service logs
# In workflow run logs, search for: "FATAL:  role"

# Test health endpoint locally
curl -v http://localhost:8080/health

# Check EF Core connection string resolution
dotnet run --project apps/api/src/Api -- --urls "http://localhost:8080" 2>&1 | grep -i "connection"
```

### Monitoring
- GitHub Actions workflow: `.github/workflows/k6-performance.yml`
- Scheduled runs: Daily at 2 AM UTC (main branch only)
- Auto-issue creation: On failure, creates/updates issue with label `performance,automated`
- Manual trigger: Workflow Dispatch with `test_type` selection
