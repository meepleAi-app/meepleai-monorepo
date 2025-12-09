# K6 Performance Tests Troubleshooting Guide

## Issue #1976: Database Connection Not Configured

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

### Prevention Checklist
- [ ] All `dotnet build` steps have `ConnectionStrings__Postgres` env var
- [ ] All `dotnet ef` commands have `--connection` flag OR env var
- [ ] All `dotnet run` steps have database connection in `env:` section

### Validation Command
```bash
# Local test (requires Docker services running)
cd apps/api/src/Api
export ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=meepleai_test;Username=postgres;Password=postgres"
dotnet build --configuration Release --no-restore
```

### Related Issues
- **#1817**: Database connectivity validation
- **#1954**: Database creation error handling
- **#1663**: Test user seeding automation
- **#1976**: K6 performance test failure (this issue)

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

### Monitoring
- GitHub Actions workflow: `.github/workflows/k6-performance.yml`
- Scheduled runs: Daily at 2 AM UTC (main branch only)
- Auto-issue creation: On failure, creates/updates issue with label `performance,automated`
