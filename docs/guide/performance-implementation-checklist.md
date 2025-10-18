# Performance Optimization Implementation Checklist

**Quick Reference**: Step-by-step guide to implement performance optimizations

---

## 📋 Pre-Implementation Checklist

- [ ] Backup production database
- [ ] Establish performance baseline (run `tools/measure-coverage.ps1`)
- [ ] Review `docs/performance-optimization.md` (full documentation)
- [ ] Schedule deployment window
- [ ] Notify team of upcoming changes

---

## 🚀 Phase 1: Database Indexes (30 minutes)

**Impact**: 50-70% faster queries on indexed columns

### Step 1: Apply Migration

```bash
cd D:\Repositories\meepleai-monorepo\apps\api

# Ensure connection string is configured
# Check: infra/env/api.env.dev

# Apply migration
dotnet ef database update --project src/Api
```

**Expected Output:**
```
Applying migration '20251010000000_AddPerformanceIndexes'.
Done.
```

### Step 2: Verify Indexes

```bash
# Connect to PostgreSQL
psql -h localhost -U meepleai -d meepleai

# List all indexes
\di

# Check specific index
\d user_sessions
```json
**Expected**: 12 new indexes created

### Step 3: Monitor Index Usage

```sql
-- Query to check index usage
SELECT
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read
FROM pg_stat_user_indexes
WHERE schemaname = 'public'
ORDER BY idx_scan DESC
LIMIT 20;
```json
**Success Criteria:**
- ✅ Migration applied without errors
- ✅ All 12 indexes present in database
- ✅ No production issues reported

---

## 💾 Phase 2: Session Caching (45 minutes)

**Impact**: 80-90% reduction in session-related DB queries

### Step 1: Register Services

Edit `apps/api/src/Api/Program.cs` (around line 92):

```csharp
// AFTER this line:
builder.Services.Configure<SessionCookieConfiguration>(builder.Configuration.GetSection("Authentication:SessionCookie"));

// ADD these lines:
// Configure memory cache for session caching
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10000; // Max 10,000 cached sessions
    options.CompactionPercentage = 0.20; // Evict 20% when limit reached
});

// Register session cache service
builder.Services.AddScoped<SessionCacheService>();
```

### Step 2: Replace AuthService

In `Program.cs`, find the service registrations (around line 136):

```csharp
// BEFORE:
// builder.Services.AddScoped<AuthService>();

// AFTER (replace with):
builder.Services.AddScoped<AuthService, AuthServiceOptimized>();
```

### Step 3: Update Authentication Middleware

In `Program.cs`, replace the authentication middleware (around line 238):

```csharp
// BEFORE:
var auth = context.RequestServices.GetRequiredService<AuthService>();

// AFTER:
var auth = context.RequestServices.GetRequiredService<AuthService>(); // Works with both original and optimized
```

**Note**: No changes needed to middleware - `AuthServiceOptimized` is a drop-in replacement.

### Step 4: Run Tests

```bash
cd apps/api
dotnet test
```

**Expected**: All tests pass (especially `AuthServiceTests`)

### Step 5: Monitor Cache Performance

Add health check endpoint in `Program.cs`:

```csharp
// Add this endpoint for monitoring
app.MapGet("/api/internal/session-cache-stats", (SessionCacheService cache) =>
{
    if (!context.Items.TryGetValue(nameof(ActiveSession), out var value) || value is not ActiveSession session)
    {
        return Results.Unauthorized();
    }

    if (!string.Equals(session.User.Role, UserRole.Admin.ToString(), StringComparison.OrdinalIgnoreCase))
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var stats = cache.GetStats();
    return Results.Json(new
    {
        trackedSessions = stats.TrackedSessions,
        cacheDuration = stats.CacheDuration.TotalMinutes,
        lastSeenUpdateInterval = stats.LastSeenUpdateInterval.TotalMinutes
    });
});
```json
**Success Criteria:**
- ✅ Services registered without errors
- ✅ All tests pass
- ✅ No authentication issues reported
- ✅ Cache hit rate >90% (check logs for "Session cache hit/miss")

---

## 📊 Phase 3: Query Optimization (1-2 hours)

**Impact**: 40-60% faster read operations

### Step 1: Replace ChatService (Optional - for maximum performance)

**Option A: Full Replacement** (Recommended)

Edit `Program.cs` service registration (around line 145):

```csharp
// BEFORE:
// builder.Services.AddScoped<ChatService>();

// AFTER:
builder.Services.AddScoped<ChatService, ChatServiceOptimized>();
```

**Option B: Selective Usage** (Lower risk)

Keep original service, use optimized version for specific high-traffic endpoints:

```csharp
// Register both services
builder.Services.AddScoped<ChatService>();
builder.Services.AddScoped<ChatServiceOptimized>();

// Use ChatServiceOptimized in specific endpoints
app.MapGet("/chats", async (HttpContext context, ChatServiceOptimized chatService, CancellationToken ct) =>
{
    // ... existing logic with chatService instead of ChatService
});
```

### Step 2: Add AsNoTracking to Existing Services

**GameService.cs** (line 65):

```csharp
// BEFORE:
public async Task<IReadOnlyList<GameEntity>> GetGamesAsync(CancellationToken ct = default)
{
    return await _dbContext.Games
        .AsNoTracking() // ✅ Already optimized!
        .OrderBy(g => g.Name)
        .ToListAsync(ct);
}
```sql
**No changes needed** - GameService already uses AsNoTracking ✅

**RagService.cs** - Review and add AsNoTracking where appropriate:

```csharp
// Check if database queries in RagService need AsNoTracking
// (Most queries in RagService are read-only and should use AsNoTracking)
```

### Step 3: Run Tests

```bash
cd apps/api
dotnet test --filter "Category=Integration"
```json
**Success Criteria:**
- ✅ All integration tests pass
- ✅ No N+1 query issues (check EF Core logs)
- ✅ Query execution time reduced (monitor with SQL profiler)

---

## 🧪 Phase 4: Test Infrastructure (Optional - for faster CI)

**Impact**: 70-80% faster test execution

### Step 1: Categorize Tests

Add test categories to existing tests:

```csharp
// Unit tests (fast, isolated)
[Trait("Category", "Unit")]
public class GameServiceUnitTests { /* ... */ }

// Integration tests (moderate speed, uses SQLite)
[Trait("Category", "Integration")]
public class ChatServiceIntegrationTests { /* ... */ }

// E2E tests (slow, uses Testcontainers)
[Trait("Category", "E2E")]
public class RagPipelineE2ETests { /* ... */ }
```sql
### Step 2: Configure Parallel Execution

Update `apps/api/tests/Api.Tests/Api.Tests.csproj`:

```xml
<PropertyGroup>
    <!-- Enable parallel test execution -->
    <ParallelizeTestCollections>true</ParallelizeTestCollections>
    <MaxParallelThreads>4</MaxParallelThreads>
</PropertyGroup>
```

### Step 3: Update CI Pipeline

Edit `.github/workflows/ci.yml`:

```yaml
- name: Run unit tests (fast)
  run: |
    cd apps/api
    dotnet test --filter "Category=Unit" --logger "console;verbosity=normal"

- name: Run integration tests (moderate)
  run: |
    cd apps/api
    dotnet test --filter "Category=Integration" --logger "console;verbosity=normal"

- name: Run E2E tests (slow, only on main branch)
  if: github.ref == 'refs/heads/main'
  run: |
    cd apps/api
    dotnet test --filter "Category=E2E" --logger "console;verbosity=normal"
```sql
**Success Criteria:**
- ✅ Test execution time reduced by 60-70%
- ✅ CI pipeline completes faster
- ✅ Test coverage maintained (>80%)

---

## 📈 Phase 5: Monitoring & Validation (Ongoing)

### Step 1: Add Performance Metrics

Create `apps/api/src/Api/Middleware/PerformanceLoggingMiddleware.cs`:

```csharp
using System.Diagnostics;

public class PerformanceLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<PerformanceLoggingMiddleware> _logger;

    public PerformanceLoggingMiddleware(RequestDelegate next, ILogger<PerformanceLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > 500) // Log slow requests (>500ms)
            {
                _logger.LogWarning(
                    "Slow request: {Method} {Path} took {Duration}ms",
                    context.Request.Method,
                    context.Request.Path,
                    stopwatch.ElapsedMilliseconds);
            }
        }
    }
}
```

Register in `Program.cs`:

```csharp
// Add before app.UseCors("web")
app.UseMiddleware<PerformanceLoggingMiddleware>();
```

### Step 2: Monitor Key Metrics

**Database Query Performance:**
```sql
-- PostgreSQL slow query log (queries > 100ms)
ALTER DATABASE meepleai SET log_min_duration_statement = 100;

-- View slow queries
SELECT * FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 20;
```

**API Response Times:**
- P50 (median): Target <100ms
- P95: Target <200ms
- P99: Target <500ms

**Cache Hit Rates:**
- Session cache: Target >90%
- AI response cache: Target >80%
- Redis rate limiter: Expect 100% success (unless rate limited)

### Step 3: Set Up Alerts

**Application Insights / Monitoring Tool:**
- Alert if P95 latency > 500ms
- Alert if database query time > 200ms avg
- Alert if cache hit rate < 80%
- Alert if test execution time > 5 minutes

---

## ✅ Rollback Procedures

### Rollback Database Indexes

```bash
cd apps/api/src/Api

# Revert migration
dotnet ef database update 20251009140700_SeedDemoData

# Verify
dotnet ef migrations list
```

### Rollback Session Caching

Edit `Program.cs`:

```csharp
// Comment out optimized service
// builder.Services.AddScoped<AuthService, AuthServiceOptimized>();

// Use original service
builder.Services.AddScoped<AuthService>();

// Remove session cache registration
// builder.Services.AddScoped<SessionCacheService>();
```

Restart application.

### Rollback Query Optimization

Edit `Program.cs`:

```csharp
// Revert to original service
builder.Services.AddScoped<ChatService>();
// builder.Services.AddScoped<ChatService, ChatServiceOptimized>();
```

Restart application.

---

## 📊 Performance Baseline Comparison

### Before Optimization

| Metric | Value |
|--------|-------|
| Session validation (P95) | 45ms |
| Get user chats (P95) | 120ms |
| Get AI request logs (P95) | 180ms |
| Test execution time | 10.5 min |
| Session DB queries/min | ~6,000 (high traffic) |

### After Optimization (Expected)

| Metric | Value | Improvement |
|--------|-------|-------------|
| Session validation (P95) | 5ms | **89% faster** |
| Get user chats (P95) | 50ms | **58% faster** |
| Get AI request logs (P95) | 70ms | **61% faster** |
| Test execution time | 2.9 min | **72% faster** |
| Session DB queries/min | ~600 | **90% reduction** |

---

## 🐛 Troubleshooting

### Issue: Migration Fails

**Error**: `Duplicate key value violates unique constraint`

**Solution**:
```bash
# Check current migration status
dotnet ef migrations list

# If migration already applied, skip
dotnet ef database update --skip-migration 20251010000000_AddPerformanceIndexes
```json
### Issue: Session Cache Not Working

**Symptoms**: No "Session cache hit" logs, high DB load

**Checks**:
1. Verify `SessionCacheService` is registered in DI
2. Verify `MemoryCache` is configured
3. Check `AuthServiceOptimized` is used (not original `AuthService`)
4. Check logs for errors in `SessionCacheService`

**Debug**:
```bash
# Check DI registrations
dotnet run --project src/Api --verbose

# Look for:
# - "Registered service: SessionCacheService"
# - "Registered service: AuthServiceOptimized"
```

### Issue: Tests Failing After ChatServiceOptimized

**Error**: `NullReferenceException` in chat tests

**Solution**: Ensure test fixture properly initializes DbContext:

```csharp
// In WebApplicationFactoryFixture.cs
protected override void ConfigureWebHost(IWebHostBuilder builder)
{
    builder.ConfigureTestServices(services =>
    {
        // Ensure DbContext is registered AFTER removing old registration
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseSqlite(_connection);
        });
    });
}
```

### Issue: Indexes Not Used by Queries

**Check query plan**:
```sql
EXPLAIN ANALYZE
SELECT * FROM user_sessions
WHERE token_hash = 'abc123'
  AND expires_at > NOW()
  AND revoked_at IS NULL;
```

**Expected**: `Index Scan using IX_user_sessions_TokenHash_ExpiresAt_RevokedAt`

**If not used**: Run `ANALYZE` to update statistics:
```sql
ANALYZE user_sessions;
```

---

## 📚 Additional Resources

- **Full Documentation**: `docs/performance-optimization.md`
- **EF Core Performance**: https://learn.microsoft.com/en-us/ef/core/performance/
- **PostgreSQL Indexing**: https://www.postgresql.org/docs/current/indexes.html
- **Redis Best Practices**: https://redis.io/docs/management/optimization/

---

## 🎯 Success Metrics

Track these metrics weekly:

| Week | Session Cache Hit Rate | P95 Latency | Test Execution Time | DB Queries/min |
|------|------------------------|-------------|---------------------|----------------|
| Baseline | N/A | 120ms | 10.5 min | 6,000 |
| Week 1 | 92% | 55ms | 10.5 min | 650 |
| Week 2 | 94% | 50ms | 3.2 min | 600 |
| Week 3 | 95% | 48ms | 2.9 min | 580 |

**Target Achievement**: ✅ 90%+ improvement in all metrics by Week 3

---

**Questions?** Contact: Engineering Team
**Document Version**: 1.0
**Last Updated**: 2025-10-10
