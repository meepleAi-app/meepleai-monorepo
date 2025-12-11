# K6 Performance Tests Troubleshooting Guide

## Issue #2020: K6 Performance Tests Failed (PostgreSQL 'role root' Error)

### Symptom
```
2025-12-11 03:19:48.746 UTC [75] FATAL:  role "root" does not exist
2025-12-11 03:19:58.830 UTC [83] FATAL:  role "root" does not exist
```

K6 performance tests fail consistently during the "Wait for API to be ready" step. PostgreSQL logs show repeated connection attempts with user "root" (GitHub Actions default) instead of "postgres".

### Root Cause
The `/health` endpoint may trigger EF Core DbContext initialization during startup. Without `ConnectionStrings__Postgres` environment variable in the "Wait for API" step, EF Core defaults to the system user ("root" in GitHub Actions), causing authentication failures.

### Solution (Fixed 2025-12-11)
Add `ConnectionStrings__Postgres` environment variable to the "Wait for API to be ready" step:

```yaml
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
    ConnectionStrings__Postgres: ${{ secrets.TEST_DB_CONNECTION_STRING || 'Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres' }}
```

### Impact
- **Duration**: 10+ consecutive nightly runs failed (2025-12-01 to 2025-12-11)
- **Severity**: P1-High (blocks production readiness monitoring)
- **Resolution Time**: ~1 hour (analysis + fix + validation)

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
