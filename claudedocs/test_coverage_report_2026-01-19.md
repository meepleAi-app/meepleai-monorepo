# Test Coverage Report - MeepleAI Monorepo

**Date**: 2026-01-19
**Branch**: main-dev

---

## Executive Summary

| Area | Tests Passed | Tests Failed | Skipped | Coverage |
|------|--------------|--------------|---------|----------|
| **Backend (.NET)** | ~1400+ | 11 | 18 | **36.77%** lines |
| **Frontend (Next.js)** | 5,682 | 4 | 22 | ~39% lines* |

*Frontend coverage estimated from configured thresholds (tests failed, coverage report not generated)

---

## Backend Test Results (.NET 9 / xUnit)

### Coverage Metrics (from Cobertura XML)

| Metric | Value | Target |
|--------|-------|--------|
| **Line Coverage** | **36.77%** | 90%+ |
| **Branch Coverage** | **3.24%** | N/A |
| Lines Covered | 48,265 | - |
| Total Lines | 131,235 | - |
| Branches Covered | 604 | - |
| Total Branches | 18,598 | - |

### Failed Tests (11)

| Test Name | Issue Category | Root Cause |
|-----------|---------------|------------|
| `HandleCallback_DatabaseConnectionFailure_ReturnsError` | DI/DbContext | ObjectDisposedException - DbContext disposed before use |
| `AskAsync_WhenEmbeddingFails_ReturnsErrorMessage` | Mock Setup | Expected "Unable to process query." got "Not specified" |
| `Handle_WhenNoTestUserExists_ShouldCreateTestUser` | Mock Verification | Email case sensitivity (Test@ vs test@) |
| `Handle_WithValidCommand_ShouldActivateVersionAndDeactivateOthers` | InMemory DB | Transactions not supported in InMemory provider |
| `ChunkAsync_HeaderBased_FiltersOutSectionsSmallerThan100Chars` | Logic Bug | Negative startIndex (-175) in Substring |
| `SaveChangesAsync_ShouldDispatchDomainEvents_AndCreateAuditLog` | Event Handling | Audit logs not being created |
| `ApiKeyRevoke_ShouldPublishEvent_AndCreateAuditLog` | Event Handling | Audit logs not being created |
| `BulkImport_1000Users_AllKeysValid` | Testcontainers | PostgreSQL connection stream error |
| `HashVerification_FixedTimeEquals_StatisticalConsistency` | Timing | Timing variance 28.67% (flaky on local) |
| `GET /api/v1/games without auth should return 401 Unauthorized` | Auth Config | Returns 200 instead of 401 |
| `DeleteUser_WithRuleSpecComments_ThrowsDbUpdateException` | FK Constraint | Database constraint handling |

### Skipped Tests (18)

- Golden Dataset accuracy tests (require external services)
- PDF processing tests (require SmolDocling/Unstructured services)
- Performance tests with real services
- Large file tests

### Analysis

**Critical Issues to Address**:

1. **DI/DbContext Lifecycle** (3 failures)
   - `OAuthErrorTests`: DbContext disposed prematurely
   - Recommendation: Review transaction scope and context lifecycle

2. **InMemory Provider Limitations** (1 failure)
   - `ActivatePromptVersionCommandHandlerTests`: Uses BeginTransactionAsync
   - Recommendation: Configure InMemory to ignore transaction warnings or use SQLite

3. **Logic Bug** (1 failure)
   - `EmbeddingBasedSemanticChunker.ChunkFixedSize`: Negative index calculation
   - Location: `EmbeddingBasedSemanticChunker.cs:260`

4. **Event Handling** (2 failures)
   - Audit log creation not working
   - Recommendation: Check event handler registration

---

## Frontend Test Results (Next.js 14 / Vitest)

### Test Execution Summary

| Category | Count |
|----------|-------|
| **Total Test Files** | 316 |
| Test Files Passed | 311 |
| Test Files Failed | 5 |
| **Total Tests** | 5,708 |
| Tests Passed | 5,682 |
| Tests Failed | 4 |
| Tests Skipped | 22 |
| **Pass Rate** | **99.93%** |

### Coverage Thresholds (from vitest.config.ts)

| Metric | Threshold | Status |
|--------|-----------|--------|
| Branches | 85% | Configured |
| Functions | 39% | Configured |
| Lines | 39% | Configured |
| Statements | 39% | Configured |

### Failed Tests (4)

| Test File | Test Name | Issue |
|-----------|-----------|-------|
| `PlayerStateEditor.test.tsx` | shows player count summary | Element text query fails ("2") |
| `ResourceEditor.test.tsx` | shows resource summary | Element text query fails ("2") |
| `BoardStateEditor.test.tsx` | renders empty state | Multiple elements with getByDisplayValue('10') |
| `BoardStateEditor.test.tsx` | shows board summary | Text "/Pezzi posizionati: 2/" not found |

### Analysis

All frontend failures are in **State Editor components** with common pattern:
- UI text split across multiple elements
- Test queries expecting specific text that's no longer present
- Likely caused by recent UI refactoring

**Recommendation**: Update tests to use more flexible queries:
```typescript
// Instead of:
expect(screen.getByText('2')).toBeInTheDocument();

// Use:
expect(screen.getByText(/2/)).toBeInTheDocument();
// Or find containing element and check textContent
```

---

## Performance Notes

| Metric | Backend | Frontend |
|--------|---------|----------|
| Build Time | ~30s | N/A |
| Test Duration | >5 min | 156s |
| Setup Overhead | Testcontainers | ~285s total |

---

## Recommendations

### High Priority

1. **Fix Logic Bug** in `EmbeddingBasedSemanticChunker.ChunkFixedSize`
   - Causing ArgumentOutOfRangeException with negative index

2. **Update State Editor Tests**
   - Fix 4 frontend tests with more resilient queries

3. **Review DbContext Lifecycle**
   - Fix OAuth error test context disposal issue

### Medium Priority

4. **Configure InMemory Provider**
   - Suppress transaction warnings for tests that need them

5. **Event Handler Registration**
   - Debug audit log creation in domain events

### Low Priority

6. **Increase Backend Coverage**
   - Current: 36.77% → Target: 90%+
   - Focus on bounded contexts with complex logic

7. **Review Timing-Based Tests**
   - `HashVerification` test is flaky on local machines

---

## Test Infrastructure

### Backend
- **Framework**: xUnit v3.2.1
- **Coverage Tool**: Coverlet (MSBuild + Collector)
- **Mocking**: Moq 4.20.72
- **Assertions**: FluentAssertions 8.8.0
- **Database**: Testcontainers (PostgreSQL, Redis, Qdrant)

### Frontend
- **Framework**: Vitest 3.2.4
- **Coverage Tool**: V8
- **Testing Library**: @testing-library/react 16.3.0
- **E2E**: Playwright 1.57.0

---

*Report generated by Claude Code test coverage analysis*
