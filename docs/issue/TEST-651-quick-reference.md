# TEST-651: Quick Reference Guide

**Full Plan**: See [TEST-651-execution-plan.md](./TEST-651-execution-plan.md)

## TL;DR

**Problem**: 78 test failures (3.9% failure rate)
**Root Cause**: 4 technical patterns, not 78 individual bugs
**Strategy**: Create 6 reusable solutions, apply to groups
**Time**: 15-18 hours (20% faster than one-by-one approach)
**Outcome**: 100% pass rate (2019/2019 tests)

## The 4 Root Causes

| Pattern | Tests | Fix Strategy |
|---------|-------|--------------|
| **Mock Configuration** | 29 | Create `TestMockFactory` with correct service signatures |
| **Assertion Formats** | 21 | Investigate new formats, update test expectations |
| **Timing/Async** | 9 | Testcontainers wait helpers + TaskCompletionSource |
| **Exception Types** | 6 | Update from `UnauthorizedAccessException` → custom exceptions |
| **Unique Issues** | 13 | Individual triage and fix |

## Phase Order & Why

1. **Foundation (3h)**: Exception types + Testcontainers - Enables everything else
2. **Infrastructure (4h)**: Mock factory + assertion investigation - Reusable patterns
3. **Batch Application (6h)**: Apply patterns to 50 tests - High ROI
4. **Specialized (3h)**: Background services + N8n + triage - Low risk
5. **Individual (3h)**: Unique issues - Contained impact
6. **Validation (2h)**: Full suite + PR - Quality gate

## Quick Commands

```bash
# Run category tests
dotnet test --filter "FullyQualifiedName~RagService"
dotnet test --filter "FullyQualifiedName~LogEvent|Sanitiz"
dotnet test --filter "FullyQualifiedName~Scenario_Given"

# Full suite
dotnet test --no-build --logger "console;verbosity=normal"

# Verbose single test
dotnet test --filter "FullyQualifiedName~TestName" --logger "console;verbosity=detailed"
```

## Key Deliverables

### Phase 1
- [ ] 6 exception type assertions updated
- [ ] `WaitForPostgresReadyAsync()` in test fixture
- [ ] `WaitForQdrantReadyAsync()` in test fixture
- [ ] 10 tests passing (6 exception + 4 integration)

### Phase 2
- [ ] `tests/Api.Tests/Helpers/TestMockFactory.cs` created
- [ ] Mock factories for: LlmService, EmbeddingService, QdrantService, CacheService
- [ ] Investigation notes for assertion format changes
- [ ] Foundation ready for Phase 3 batch application

### Phase 3
- [ ] 29 tests using mock factory (RAG/QA + Cache)
- [ ] 21 tests with updated assertions (Logging + PDF + Admin)
- [ ] 50 total tests passing

### Phase 4
- [ ] 5 cache warming tests with TaskCompletionSource
- [ ] 3 N8n template tests with auth/routing fixes
- [ ] Triage report for remaining 13 "Other" tests

### Phase 5
- [ ] 8-10 unique issues resolved
- [ ] All 78 tests passing

### Phase 6
- [ ] Full suite: 2019/2019 passing
- [ ] Comprehensive PR created
- [ ] Issue #651 closed

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| Mock factory breaks existing tests | Incremental adoption, test after each change |
| Assertions don't validate behavior | Manual review each change vs implementation |
| Testcontainers still flaky | Add logs, increase retries, document timing |
| Individual issues take too long | Time-box 30min, defer if complex |

## Success Criteria

✅ **100% pass rate** (2019/2019)
✅ **No regressions** (existing tests still pass)
✅ **Reusable infrastructure** (factory + helpers for future)
✅ **Fast execution** (no >10% test duration increase)
✅ **Documented patterns** (for future test authors)

## Pattern Examples

### Mock Factory Usage
```csharp
// Before (broken)
_mockLlmService.Setup(x => x.GenerateAsync(...)); // Wrong signature

// After (works)
_mockLlmService = TestMockFactory.CreateLlmServiceMock("Expected answer");
```

### Testcontainers Wait
```csharp
// In fixture InitializeAsync()
await _postgresContainer.StartAsync();
await WaitForPostgresReadyAsync(); // NEW: Ensures ready before tests
```

### Background Service Sync
```csharp
// Before (unreliable)
await Task.Delay(1000); // Hope it completes

// After (deterministic)
await _completionSource.Task.WaitAsync(TimeSpan.FromSeconds(5));
```

### Exception Type Fix
```csharp
// Before
await act.Should().ThrowAsync<UnauthorizedAccessException>();

// After
await act.Should().ThrowAsync<ForbiddenException>();
```

## Progress Tracking

**Current**: Analysis complete, ready to execute
**Next Step**: Phase 1 - Exception types + Testcontainers
**Completion Target**: 15-18 hours of focused work

---

**Quick Links**:
- Full Plan: [TEST-651-execution-plan.md](./TEST-651-execution-plan.md)
- Original Analysis: [TEST-651-remaining-failures-analysis.md](./TEST-651-remaining-failures-analysis.md)
- Issue: #651 (parent), #671/TEST-654 (HTTP status codes)
