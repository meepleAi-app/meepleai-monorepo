# Test Suite Optimization Implementation Plan - Issue #1820

**Status**: ✅ Phase 1 Complete (Infrastructure) | 🔄 Phase 2 In Progress (Application)
**Baseline**: 17 minutes 13 seconds
**Target**: <7 minutes (60-70% improvement)
**Estimated**: 4-6 minutes after full implementation

## Overview

Consolidation of 3 optimization strategies:
1. **Test Categories (#1740)**: Selective execution (fast tests first)
2. **Parallel Execution (#1744)**: Remove blocking [Collection] attributes
3. **Shared Testcontainers (#1745)**: Reduce container startup overhead (~340s savings)

## Phase 1: Infrastructure (✅ Complete)

### Created Files

1. **TestCategories.cs** (`apps/api/tests/Api.Tests/Constants/TestCategories.cs`)
   - Centralized category constants
   - Categories: Unit, Integration, Performance, Security, Slow, E2E
   - Usage: `[Trait("Category", TestCategories.Integration)]`

2. **SharedTestcontainersFixture.cs** (`apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs`)
   - Shared PostgreSQL + Redis containers
   - Collection: `[Collection("SharedTestcontainers")]`
   - Reduces ~10s overhead × 34 test classes = ~340s savings
   - Provides: `CreateIsolatedDatabaseAsync()`, `FlushRedisByPrefixAsync()`

## Phase 2: Application (Target: 238 Test Files)

### Step 1: Apply Test Categories

**Scope**: All 238 test files
**Pattern**:

```csharp
// Add using statement
using Api.Tests.Constants;

// Add trait before class
[Trait("Category", TestCategories.Integration)]
public sealed class YourIntegrationTests : IAsyncLifetime
```

**Category Classification Rules**:
- `*IntegrationTests.cs` → Integration
- `*E2ETests.cs` → Integration + E2E (or Slow)
- `*SecurityTests.cs` → Security
- `*PerformanceTests.cs` → Performance
- All others → Unit (default)

**Automation Script**: `apps/api/tests/scripts/optimize-test-suite.ps1` (needs debugging)

### Step 2: Remove [Collection] Attributes

**Scope**: 34 files with `[Collection("...")]`
**Files**:
```
Integration/PdfUploadQuotaEnforcementIntegrationTests.cs
Integration/UploadPdfIntegrationTests.cs
Integration/UploadPdfMidPhaseCancellationTests.cs
Integration/ThreeStagePdfPipelineE2ETests.cs
Integration/SmolDoclingIntegrationTests.cs
Integration/UnstructuredPdfExtractionIntegrationTests.cs
... (28 more files - see Serena search results)
```

**Action**: Remove lines like:
```csharp
[Collection("PdfPipeline")]
[Collection("QuotaEnforcement")]
```

**Special Case**: Delete `PdfPipelineCollectionDefinition.cs` entirely
```
apps/api/tests/Api.Tests/Infrastructure/PdfPipelineCollectionDefinition.cs
```

### Step 3: Add Unique Redis Key Prefixes

**Scope**: 7 files using Redis
**Files**:
- `PdfUploadQuotaEnforcementIntegrationTests.cs`
- `TwoFactorSecurityPenetrationTests.cs`
- `TotpReplayAttackPreventionTests.cs`
- `RedisOAuthStateStoreTests.cs`
- `RedisBackgroundTaskOrchestratorTests.cs`
- Others from Serena search

**Pattern**:
```csharp
public class YourTests
{
    private readonly string _redisKeyPrefix = $"test:{Guid.NewGuid()}:";

    // In Redis operations:
    var key = $"{_redisKeyPrefix}pdf:upload:quota:{userId}";
    await redis.StringSetAsync(key, value);
}
```

### Step 4: Convert to SharedTestcontainersFixture

**Pattern** (for files creating own containers):
```csharp
// Before:
[Collection("MyCollection")]
public class MyTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;

    public async Task InitializeAsync()
    {
        _postgresContainer = new ContainerBuilder()...
        await _postgresContainer.StartAsync();
    }
}

// After:
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public class MyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _connectionString = string.Empty;
    private readonly string _redisKeyPrefix = $"test:{Guid.NewGuid()}:";

    public MyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        var dbName = $"test_mydb_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(dbName);
        // Use _connectionString for DbContext
    }

    public async Task DisposeAsync()
    {
        await _fixture.FlushRedisByPrefixAsync(_redisKeyPrefix + "*");
        // Database auto-cleaned on test run end
    }
}
```

## Phase 3: CI Pipeline Updates

### File: `.github/workflows/ci.yml`

**Add Category-Based Test Jobs**:

```yaml
ci-api-unit:
  name: API - Unit Tests (Fast)
  runs-on: ubuntu-latest
  steps:
    - name: Run Unit Tests
      run: dotnet test --filter "Category=Unit"
    - name: Run Security Tests
      run: dotnet test --filter "Category=Security"

ci-api-integration:
  name: API - Integration Tests (Slow)
  needs: ci-api-unit
  runs-on: ubuntu-latest
  steps:
    - name: Run Integration Tests
      run: dotnet test --filter "Category=Integration"
    - name: Run Performance Tests
      run: dotnet test --filter "Category=Performance"
```

**Execution Order**: Unit + Security (parallel) → Integration → Performance → E2E

## Expected Performance Improvements

### Breakdown by Optimization

1. **Shared Testcontainers**: -340s (34 classes × 10s overhead)
   - Before: 17min 13s (1033s)
   - After:  11min 33s (693s)
   - Improvement: 33%

2. **Parallel Execution**: -300s (estimated, 50% of sequential overhead)
   - Before: 11min 33s (693s)
   - After:  6min 33s (393s)
   - Additional: 43%

3. **Category-Based CI**: -30s (fast tests first, fail fast)
   - Before: 6min 33s (393s)
   - After:  6min 03s (363s)
   - Additional: 8%

**Total Expected**:
- Before: 17min 13s (1033s)
- After:  6min 03s (363s)
- **Improvement: 65% faster** ✅ (exceeds 60-70% target)

## Implementation Status

### ✅ Completed
- [x] TestCategories.cs constants class
- [x] SharedTestcontainersFixture.cs infrastructure
- [x] SharedTestcontainersCollectionDefinition
- [x] Documentation and implementation plan
- [x] Baseline measurement: 17m 13s

### 🔄 In Progress
- [ ] Apply categories to 238 test files (manual or automated)
- [ ] Remove 34 [Collection] attributes
- [ ] Add unique Redis key prefixes to 7 files
- [ ] Delete PdfPipelineCollectionDefinition.cs
- [ ] Convert key tests to SharedTestcontainersFixture

### ⏳ Pending
- [ ] Update CI pipeline workflow
- [ ] Validate no test breakage
- [ ] Measure optimized performance
- [ ] Create PR and code review
- [ ] Update Issue #1820 status

## Manual Implementation Steps

For each test file category:

### Unit Tests (Fastest)
```bash
# Add category only
find apps/api/tests -name "*Tests.cs" ! -name "*Integration*" ! -name "*E2E*" \\
  -exec sed -i '/public class/i [Trait("Category", TestCategories.Unit)]' {} \\;
```

### Integration Tests
```bash
# Add category + review [Collection]
find apps/api/tests -name "*IntegrationTests.cs" -o -name "*E2ETests.cs"
# Manual review and edit each
```

## Validation Checklist

- [ ] All 238 files have `[Trait("Category", ...)]`
- [ ] No `[Collection]` attributes remain (except SharedTestcontainers)
- [ ] All Redis tests use unique key prefixes
- [ ] Build succeeds: `dotnet build`
- [ ] Unit tests pass: `dotnet test --filter "Category=Unit"`
- [ ] Integration tests pass: `dotnet test --filter "Category=Integration"`
- [ ] Full suite <7 minutes: `time dotnet test`
- [ ] No new warnings introduced

## Rollback Plan

If issues arise:
```bash
git checkout main -- apps/api/tests
git clean -fd apps/api/tests
```

## References

- Issue: #1820 (consolidates #1740, #1744, #1745)
- Baseline log: `/tmp/test-baseline.log`
- Implementation branch: `issue-1820-test-performance-optimization`

---

**Last Updated**: 2025-12-06
**Author**: Claude Code
**Status**: Infrastructure Complete, Application In Progress
