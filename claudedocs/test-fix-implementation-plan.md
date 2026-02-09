# Test Failures Fix - Implementation Plan

**Generated**: 2026-02-08
**Based on**: Coverage Report Post-Migration Reset

## Overview

**Total Failures**: 30+ backend, 23 frontend
**Already Fixed**: 7 tests (JSON deserialization + NullReference + import paths)
**Remaining**: 24+ backend issues requiring systematic fixes

---

## Priority 1: Authorization Forbidden (7 tests) ⚠️ HIGH IMPACT

### Problem
PrivateGame integration tests return `403 Forbidden` instead of expected status codes (`200 OK`, `201 Created`, `404 NotFound`)

### Root Cause Analysis
```csharp
// Test creates authenticated request:
var httpRequest = TestSessionHelper.CreateAuthenticatedRequest(
    HttpMethod.Get,
    "/api/v1/private-games/{id}",
    sessionToken);

// Expected: OK/Unauthorized
// Actual: Forbidden (403)
```

**Hypothesis**:
- Authorization middleware setup issue in test `WebApplicationFactory`
- Session token validation failing
- Missing claims/roles in test user

### Files to Investigate
- `tests/Api.Tests/Integration/UserLibrary/PrivateGameEndpointsIntegrationTests.cs`
- `tests/Api.Tests/TestHelpers/TestSessionHelper.cs`
- `tests/Api.Tests/E2E/Infrastructure/E2ETestBase.cs` (WebApplicationFactory setup)
- `src/Api/Middleware/AuthenticationMiddleware.cs`

### Fix Strategy
1. Verify test session creation includes all required claims
2. Check WebApplicationFactory auth middleware configuration
3. Ensure test environment bypasses email verification if needed
4. Validate session token format matches runtime expectations

---

## Priority 1: Database Timeouts (2 tests) ⚠️ HIGH IMPACT

### Problem
```
Npgsql.NpgsqlException: The operation has timed out
---- System.TimeoutException: The operation has timed out.
```

### Affected Tests
- `AddPrivateGame_WithoutAuth_ReturnsUnauthorized`
- `UpdatePrivateGame_WithoutAuth_ReturnsUnauthorized`

### Root Cause Analysis
**Hypothesis**:
- Connection pool exhaustion after 30+ minutes of test execution
- Database deadlock from concurrent test execution
- Migration initialization taking too long in test setup

### Fix Strategy
1. Increase connection timeout in test appsettings
2. Use connection pooling with higher limits
3. Consider database isolation per test class (Testcontainers)
4. Add retry logic with exponential backoff

---

## Priority 2: BatchJob Entity Tracking (3 tests) 🔧 MEDIUM IMPACT

### Problem
```
InvalidOperationException: The instance of entity type 'BatchJob'
cannot be tracked because another instance with same key is already being tracked
```

### Root Cause
```csharp
// BatchJobRepository.cs:87
public async Task UpdateAsync(BatchJob batchJob, ...)
{
    _context.BatchJobs.Update(batchJob); // ❌ Tries to track already-tracked entity
    await _context.SaveChangesAsync(...);
}
```

**Pattern**: Test loads entity, modifies it, then calls UpdateAsync which tries to track it again

### Fix Strategy

**Option A** - Detach before update:
```csharp
public async Task UpdateAsync(BatchJob batchJob, ...)
{
    var entry = _context.Entry(batchJob);
    if (entry.State == EntityState.Detached)
    {
        _context.BatchJobs.Update(batchJob);
    }
    await _context.SaveChangesAsync(...);
}
```

**Option B** - Use AsNoTracking in queries:
```csharp
// In test setup or repository GetById
var job = await _context.BatchJobs
    .AsNoTracking()
    .FirstOrDefaultAsync(x => x.Id == id);
```

**Recommended**: Option A (more robust)

### Files to Modify
- `src/Api/BoundedContexts/Administration/Infrastructure/Repositories/BatchJobRepository.cs:84-88`

---

## Priority 2: BGG Import Routing (5 tests) 🔧 MEDIUM IMPACT

### Problem
BGG import endpoints return `404 NotFound` instead of `200 OK`, `201 Created`, `400 BadRequest`

### Affected Tests
- `EnqueueSingle_WithValidBggId_ReturnsCreated`
- `EnqueueSingle_WithInvalidBggId_ReturnsBadRequest`
- `RetryFailedImport_WithNonFailedStatus_ReturnsNotFound`
- `CancelQueuedImport_WithNonQueuedStatus_ReturnsNotFound`
- `StreamQueueProgress_SendsPeriodicUpdates`

### Root Cause Analysis
**Hypothesis**:
- BGG import endpoints not registered in test WebApplicationFactory
- Routing configuration missing for these endpoints
- Endpoints commented out or conditionally registered

### Files to Investigate
```bash
# Find BGG import endpoint registration
grep -r "bgg.*import\|import.*queue" apps/api/src/Api/Routing/
grep -r "MapBggImport\|BggImport" apps/api/src/Api/
```

### Fix Strategy
1. Verify endpoint registration in `Program.cs` or routing files
2. Check if endpoints are conditionally registered (feature flags)
3. Ensure test WebApplicationFactory includes BGG endpoints
4. Validate route patterns match test URLs

---

## Priority 3: Redis Admin Mode (4 tests) 📊 LOW IMPACT

### Problem
```
RedisCommandException: This operation is not available unless admin mode is enabled: FLUSHALL/INFO
```

### Fix Strategy Options

**Option A** - Mock Redis in unit tests:
```csharp
var mockRedis = new Mock<IConnectionMultiplexer>();
// Configure mock to return test data
```

**Option B** - Configure Testcontainers Redis with admin mode:
```csharp
var redisContainer = new ContainerBuilder()
    .WithImage("redis:7-alpine")
    .WithCommand("redis-server", "--save", "", "--appendonly", "no")
    .Build();
```

**Option C** - Skip tests requiring admin commands in CI:
```csharp
[Fact(Skip = "Requires Redis admin mode")]
```

**Recommended**: Option B for integration tests, Option A for unit tests

---

## Implementation Checklist

### Phase 1: High Priority Fixes
- [ ] **P1.1**: Investigate PrivateGame authorization middleware setup
  - [ ] Check TestSessionHelper.CreateAuthenticatedRequest implementation
  - [ ] Verify WebApplicationFactory auth configuration
  - [ ] Ensure test user has required claims/roles
  - [ ] Test session token validation in middleware

- [ ] **P1.2**: Fix database timeout issues
  - [ ] Increase connection timeout in test configuration
  - [ ] Review connection pooling settings
  - [ ] Check for deadlocks or long-running transactions
  - [ ] Consider per-class database isolation

### Phase 2: Medium Priority Fixes
- [ ] **P2.1**: Fix BatchJob entity tracking
  - [ ] Add detach check in BatchJobRepository.UpdateAsync
  - [ ] Update tests to use AsNoTracking where appropriate
  - [ ] Verify all 3 affected tests pass

- [ ] **P2.2**: Fix BGG import endpoint routing
  - [ ] Locate BGG import endpoint registration
  - [ ] Verify routes are included in test WebApplicationFactory
  - [ ] Validate route patterns match test URLs
  - [ ] Run all 5 BGG import tests

### Phase 3: Low Priority Fixes
- [ ] **P3.1**: Configure Redis for admin commands
  - [ ] Update Testcontainers Redis setup
  - [ ] Or mock Redis in affected unit tests
  - [ ] Verify 4 cache tests pass

- [ ] **P3.2**: Miscellaneous fixes
  - [ ] Update exception type assertions (2 tests)
  - [ ] Fix FluentValidation wildcard pattern (1 test)
  - [ ] Register IDashboardStreamService in test DI (1 test)
  - [ ] Remove AuditLogRepository.UpdateAsync if exists (1 test)
  - [ ] Fix FeatureFlag tier access logic (2 tests)

---

## Execution Plan

### Step 1: Root Cause Investigation (P1 Issues)
```bash
# Authorization investigation
cd apps/api/tests/Api.Tests
grep -r "CreateAuthenticatedRequest\|TestSessionHelper" TestHelpers/
grep -r "WebApplicationFactory\|ConfigureWebHost" E2E/Infrastructure/

# Timeout investigation
grep -r "ConnectionString\|Timeout" appsettings.Test.json
grep -r "Testcontainers\|PostgreSql" E2E/Infrastructure/
```

### Step 2: Apply Fixes Systematically
1. Fix BatchJobRepository (quick win)
2. Fix authorization setup (complex but high impact)
3. Fix BGG routing (medium complexity)
4. Fix remaining issues (varied complexity)

### Step 3: Validation
```bash
# Run specific test categories after each fix
dotnet test --filter "Category=Integration&FullyQualifiedName~PrivateGame"
dotnet test --filter "FullyQualifiedName~BatchJob"
dotnet test --filter "FullyQualifiedName~BggImport"
```

---

## Risk Assessment

### High Risk Areas
- **Authorization changes**: Could break authentication flow
- **Database timeout fixes**: May mask underlying issues
- **Entity tracking**: Could affect other repositories

### Mitigation
- Test each fix in isolation
- Run full test suite after each category of fixes
- Keep changes minimal and focused
- Document any architectural decisions

---

## Expected Outcomes

### After Phase 1 (P1 fixes)
- 9 tests fixed (7 auth + 2 timeout)
- **Impact**: 30% reduction in failures
- **Deployment**: Unblocks PrivateGame feature testing

### After Phase 2 (P2 fixes)
- 8 additional tests fixed (3 BatchJob + 5 BGG)
- **Impact**: 57% reduction in total failures
- **Deployment**: Core functionality validated

### After Phase 3 (P3 fixes)
- Remaining 7+ tests fixed
- **Impact**: >80% failure reduction
- **Test Suite**: Mostly green with only edge cases remaining

---

## Next Steps

1. **Immediate**: Investigate P1.1 (Authorization) root cause
2. **Quick Win**: Apply P2.1 fix (BatchJob tracking) - 5 minute fix
3. **Systematic**: Work through priorities with validation at each step
4. **Document**: Update this plan with findings from investigation

**Estimated Total Effort**: 4-6 hours systematic work
**Recommended Approach**: Fix in phases with validation gates
