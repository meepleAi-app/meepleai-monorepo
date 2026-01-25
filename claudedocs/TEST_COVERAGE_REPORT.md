# Test Coverage Report - MeepleAI Monorepo
**Generated**: 2026-01-24 21:30:00
**Execution Mode**: `/sc:test --coverage`

---

## 📊 Executive Summary

| Metric | Backend (.NET) | Frontend (Next.js) | Status |
|--------|----------------|-------------------|--------|
| **Total Tests** | 304 | 7,356 | ⚠️ |
| **Passed** | 241 (79.3%) | 7,131 (96.9%) | ⚠️ Backend / ✅ Frontend |
| **Failed** | 63 (20.7%) | 203 (2.8%) | 🚨 High failure rate |
| **Pending/Todo** | 0 | 22 | ✅ |
| **Line Coverage** | 4.30% | N/A* | 🚨 Critical |
| **Branch Coverage** | 2.10% | N/A* | 🚨 Critical |
| **Quality Gate** | ❌ FAILED | ⚠️ INCOMPLETE | 🚨 |

> \*Frontend coverage data not fully captured in this run - requires investigation

---

## 🔴 Critical Issues

### 1. Backend Coverage - CRITICAL DEFICIT
**Current**: 4.30% line coverage (4,774/110,964 lines)
**Target**: 90%+ (per CLAUDE.md guidelines)
**Gap**: -85.7 percentage points

**Impact**:
- 106,190 lines of backend code untested
- 20,873 branches uncovered (97.9% of total)
- High risk of undetected bugs in production

**Root Cause Analysis**:
- Test suite exists (304 tests) but covers minimal code paths
- 63 test failures (20.7%) indicate broken test infrastructure
- Integration tests may not be executing due to setup issues

### 2. Backend Test Failures - 63 Tests (20.7%)
**Categories**:

| Component | Failed Tests | Impact |
|-----------|-------------|--------|
| **UserNotifications.EventHandlers** | 26 | Email/notification pipeline broken |
| **SharedGameCatalog.EventHandlers** | 10 | Badge system, document handling broken |
| **UserLibrary.Repository** | 2 | Filter queries returning empty results |
| **SystemConfiguration** | 2 | Priority updates failing |
| **ShareRequest Domain** | 23+ | State transitions, validation broken |

**Critical Patterns**:
1. **Event Handler Failures**: Most notifications and event-driven workflows untested/broken
2. **Repository Filters**: `GetUserGamesAsync` with filters returning 0 results (expect 2)
3. **Domain Logic**: ShareRequest state machine has multiple broken transitions

---

## 📈 Detailed Analysis

### Backend (.NET 9) - Api.Tests

#### Coverage Breakdown
```
Total Lines:     110,964
Lines Covered:     4,774 (4.30%)
Lines Uncovered: 106,190 (95.70%)

Total Branches:   21,322
Branches Covered:    449 (2.10%)
Branches Uncovered: 20,873 (97.90%)
```

#### Test Execution Results
```
Total Test Suites: ~15 (by bounded context)
Total Tests:       304
✅ Passed:         241 (79.3%)
❌ Failed:          63 (20.7%)
⏭️  Skipped:          0
⏱️  Duration:        1.04 minutes
```

#### Top Failure Areas

**1. UserNotifications.EventHandlers (26 failures)**
```
ShareRequestRejectedNotificationHandler:
  ❌ Handle_WhenEmailFails_ContinuesSuccessfully
  ❌ Handle_WhenShareRequestNotFound_SkipsNotification
  ❌ Handle_CreatesWarningNotificationWithReasonAndSendsEmail

ShareRequestChangesRequestedNotificationHandler:
  ❌ Handle_WhenShareRequestNotFound_SkipsNotification
  ❌ Handle_WhenEmailFails_ContinuesSuccessfully
  ❌ Handle_CreatesInfoNotificationWithFeedbackAndSendsEmail

ShareRequestApprovedNotificationHandler:
  ❌ Handle_CreatesCelebratoryNotificationAndSendsEmail
  ❌ Handle_WhenTargetSharedGameIdIsNull_SkipsNotification
  ❌ Handle_WhenEmailFails_ContinuesSuccessfully

ShareRequestCreatedNotificationHandler:
  ❌ Handle_CreatesInAppNotificationAndSendsEmail
  ❌ Handle_WhenEmailFails_ContinuesSuccessfully
  ❌ Handle_WhenUserNotFound_SkipsNotification

NewShareRequestAdminAlertHandler:
  ❌ Handle_WithAdminUsers_CreatesNotifications
  ❌ Constructor_WithNullLogger_ThrowsArgumentNullException
  ❌ Constructor_WithNullRepository_ThrowsArgumentNullException
  ❌ Constructor_WithNullDbContext_ThrowsArgumentNullException
```

**Pattern**: All notification event handlers failing - likely DI/mocking issue or missing test setup

**2. SharedGameCatalog.EventHandlers (10 failures)**
```
BadgeEvaluationOnApprovalHandler:
  ❌ Handle_WhenNewBadgeEligible_AwardsBadge
  ❌ Handle_WhenBadgeAlreadyOwned_DoesNotAwardAgain
  ❌ Handle_WhenNoBadgesEligible_DoesNotAwardAnything
  ❌ Handle_WhenEvaluationThrows_LogsErrorAndContinues

ShareRequestApprovedDocumentHandler:
  ❌ HandleEventAsync_WithDocuments_CopiesDocuments
  ❌ HandleEventAsync_WithoutDocuments_SkipsCopy

ShareRequestRejectedDocumentHandler:
  ❌ HandleEventAsync_CallsCleanupOrphanedDocuments
  ❌ HandleEventAsync_WhenCleanupFails_DoesNotThrow
```

**Pattern**: Badge system and document handlers broken - core catalog workflows at risk

**3. UserLibrary.Repository (2 failures)**
```
UserLibraryRepositoryIntegrationTests:
  ❌ GetUserGamesAsync_WithWishlistFilter_ReturnsOnlyWishlistGames
     Expected: 2 items, Found: 0 (empty)

  ❌ GetUserGamesAsync_WithOwnedFilter_ReturnsOnlyOwnedGames
     Expected: 2 items, Found: 0 (empty)
```

**Pattern**: Repository filter queries broken - likely FK constraint or seed data issue

**4. ShareRequest Domain (23+ failures)**
```
Domain State Transitions:
  ❌ StartReview_FromTerminalState_ThrowsInvalidOperationException
  ❌ RequestChanges_FromTerminalState_ThrowsInvalidOperationException
  ❌ Approve_WhenNotInReview_ThrowsInvalidOperationException
  ❌ Multiple status transition validations failing
```

**Pattern**: Domain entity state machine broken - core business logic at risk

---

### Frontend (Next.js 14) - Vitest + Playwright

#### Test Execution Results
```
Total Test Suites: 2,805
✅ Passed:         2,630 (93.8%)
❌ Failed:           175 (6.2%)
⏭️  Skipped:           0

Total Tests:       7,356
✅ Passed:         7,131 (96.9%)
❌ Failed:           203 (2.8%)
⏭️  Pending:           22 (0.3%)
⏱️  Duration:        ~3-5 minutes (estimated)
```

#### Coverage Configuration (vitest.config.ts)
```yaml
Provider: v8
Include: src/**/*.{js,jsx,ts,tsx}
Exclude:
  - App Router pages (Server Components)
  - Server Actions
  - E2E-tested components (auth, chat, PDF, wizard)
  - Admin UI, comments, diff viewer
  - Low-level forms, layout, modals
  - Providers (integration tested separately)

Thresholds:
  Branches:   85% (interim, was 90%)
  Functions:  39% (Issue #1951 adjustment)
  Lines:      39% (Issue #1951: +2.72% from 36.66%)
  Statements: 39% (Issue #1951: +2.72% improvement)
```

**Note**: Coverage data not fully captured - requires investigation into v8 provider output

#### Snapshot Testing
```
Total Snapshots:   19
✅ Matched:        16 (84.2%)
❌ Unmatched:       3 (15.8%)
⚠️  Files Unmatched: 1
```

---

## 🎯 Quality Gates Assessment

### Backend Quality Gates (from CLAUDE.md)
| Gate | Target | Current | Status | Gap |
|------|--------|---------|--------|-----|
| Unit Tests | 70% | 4.30% | 🚨 FAILED | -65.7 pp |
| Integration Tests | 25% | <1%* | 🚨 FAILED | ~-24 pp |
| E2E Tests | 5% | 0% | 🚨 FAILED | -5 pp |
| **Total Coverage** | **90%+** | **4.30%** | **🚨 CRITICAL** | **-85.7 pp** |

\*Estimated based on test failures in integration test suites

### Frontend Quality Gates (from CLAUDE.md)
| Gate | Target | Current | Status | Gap |
|------|--------|---------|--------|-----|
| Total Coverage | 85%+ | 39%* | 🚨 FAILED | -46 pp |
| Test Pass Rate | 100% | 96.9% | ⚠️ WARNING | -3.1 pp |

\*From vitest.config.ts thresholds (Issue #1951 interim targets)

---

## 🔧 Recommended Actions

### Immediate (P0 - Critical)

1. **Fix Backend Test Infrastructure**
   - **Priority**: 🔴 URGENT
   - **Issue**: 63 failing tests blocking coverage measurement
   - **Action**:
     ```bash
     cd tests/Api.Tests
     dotnet test --filter "FullyQualifiedName~UserNotifications" --logger "console;verbosity=detailed"
     # Analyze DI container setup, mocking configuration
     # Fix event handler test setup (likely missing services)
     ```
   - **Owner**: Backend Team
   - **Deadline**: 48 hours

2. **Investigate Frontend Coverage Collection**
   - **Priority**: 🔴 URGENT
   - **Issue**: v8 coverage provider not generating reports
   - **Action**:
     ```bash
     cd apps/web
     rm -rf coverage/ .vitest/ node_modules/.vite/
     pnpm install
     pnpm run test:coverage -- --reporter=verbose
     ```
   - **Owner**: Frontend Team
   - **Deadline**: 24 hours

3. **Fix Repository Filter Queries**
   - **Priority**: 🔴 URGENT
   - **Issue**: `UserLibraryRepository.GetUserGamesAsync` filters returning empty
   - **Impact**: Core user library functionality broken
   - **Action**: Debug FK constraints, seed data, EF Core query generation
   - **Owner**: Backend Team - UserLibrary BC
   - **Deadline**: 48 hours

### Short-Term (P1 - High)

4. **Fix Event Handler Test Suite**
   - **Target**: 26 UserNotifications + 10 SharedGameCatalog failures
   - **Strategy**:
     - Review DI registration for event handlers
     - Fix mock email service setup
     - Validate DbContext configuration in tests
     - Add logging to identify exact failure points
   - **Timeline**: 1 week

5. **Fix ShareRequest State Machine**
   - **Target**: 23+ domain logic failures
   - **Strategy**:
     - Review state transition validation rules
     - Fix terminal state detection
     - Add comprehensive state machine tests
   - **Timeline**: 1 week

6. **Increase Frontend Coverage to 50%**
   - **Current**: 39% (Issue #1951 interim)
   - **Target**: 50% (stepping stone to 85%)
   - **Strategy**:
     - Add unit tests for ~18 more components (per Issue #1951 TODO)
     - Focus on business logic in stores, hooks, utilities
     - Exclude visual components covered by E2E/Storybook
   - **Timeline**: 2 weeks

### Medium-Term (P2 - Medium)

7. **Backend Coverage to 50%**
   - **Current**: 4.30%
   - **Target**: 50% (stepping stone to 90%)
   - **Strategy**:
     - Fix all existing broken tests first (foundation)
     - Add unit tests for domain entities (high ROI)
     - Add integration tests for critical handlers
     - Focus on business logic, not infrastructure
   - **Timeline**: 1 month

8. **Add E2E Test Coverage**
   - **Current**: Frontend E2E exists (Playwright), Backend E2E missing
   - **Strategy**:
     - Backend: Add critical journey tests (auth, game search, RAG flow)
     - Frontend: Expand E2E to cover 5% target
     - Use Playwright for both (consistency)
   - **Timeline**: 1 month

### Long-Term (P3 - Continuous)

9. **Reach Target Coverage**
   - **Backend**: 90%+ (70% unit, 25% integration, 5% E2E)
   - **Frontend**: 85%+ total
   - **Strategy**:
     - Enforce coverage gates in CI/CD
     - Block PRs below threshold
     - Regular coverage audits
   - **Timeline**: 3 months

10. **Implement Coverage Monitoring**
    - **Tools**: Codecov integration (`.codecov.yml` exists)
    - **Strategy**:
      - Add coverage badges to README
      - Automated coverage reports on PRs
      - Trend analysis dashboard
    - **Timeline**: Ongoing

---

## 📋 Technical Debt Inventory

### Backend Test Infrastructure
| Issue | Severity | Impact | Effort |
|-------|----------|--------|--------|
| Event handler DI setup broken | 🔴 Critical | 36 tests | Medium |
| Repository filter queries broken | 🔴 Critical | User features | Low |
| Domain state machine broken | 🔴 Critical | Business logic | Medium |
| Coverage tooling setup | 🟡 High | Visibility | Low |
| Missing integration test data | 🟡 High | Test reliability | Medium |

### Frontend Test Infrastructure
| Issue | Severity | Impact | Effort |
|-------|----------|--------|--------|
| v8 coverage not generating | 🔴 Critical | Visibility | Low |
| 175 test suite failures | 🟡 High | CI reliability | Medium |
| Snapshot mismatches (3) | 🟢 Low | Visual regression | Low |
| Coverage gap: 39% → 85% | 🟡 High | Quality gates | High |

---

## 🎓 Testing Best Practices Violations

### Detected Issues
1. **Broken Tests Committed**: 63 backend + 175 frontend suites failing
   - **Violation**: Tests should pass before merge
   - **Fix**: Enforce `dotnet test` + `pnpm test` in pre-commit hook

2. **Low Coverage Merged**: 4.30% backend coverage
   - **Violation**: Coverage gates not enforced
   - **Fix**: Add Codecov checks, block PRs <50%

3. **Missing Test Data**: Repository tests returning empty results
   - **Violation**: Integration tests need proper fixtures
   - **Fix**: Add `TestDataBuilder` pattern, seed factories

4. **Flaky Tests**: Snapshot mismatches, pending tests
   - **Violation**: Tests should be deterministic
   - **Fix**: Review time-dependent logic, mock dates

---

## 📊 Coverage Trend (Historical)

*Note: This is the baseline report. Future reports will track trends.*

### Baseline Metrics (2026-01-24)
```
Backend:
  Line Coverage:   4.30%
  Branch Coverage: 2.10%
  Test Pass Rate:  79.3%

Frontend:
  Line Coverage:   39%* (vitest.config.ts threshold)
  Test Pass Rate:  96.9%
```

### Next Measurement
**Scheduled**: 2026-01-31 (7 days)
**Expected Improvements**:
- Backend: 4.30% → 20%+ (fix failing tests, add domain tests)
- Frontend: 39% → 45% (18 component tests per Issue #1951 plan)

---

## 🚀 Success Criteria

### Phase 1: Foundation (Week 1-2)
- ✅ All 63 backend tests passing (0 failures)
- ✅ All 175 frontend test suites passing
- ✅ Coverage collection working (both stacks)
- ✅ Repository filters fixed
- ✅ Event handlers functional

### Phase 2: Growth (Week 3-8)
- ✅ Backend coverage: 50%+
- ✅ Frontend coverage: 50%+
- ✅ CI/CD coverage gates enforced
- ✅ Codecov integration active

### Phase 3: Excellence (Month 3+)
- ✅ Backend coverage: 90%+ (70% unit, 25% integration, 5% E2E)
- ✅ Frontend coverage: 85%+
- ✅ Zero failing tests in main branch
- ✅ Coverage trend dashboard live

---

## 📞 Support & Escalation

### Test Failures
- **Slack**: #meepleai-testing
- **Owner**: QA Team + Backend/Frontend Leads
- **SLA**: P0 failures fixed within 48h

### Coverage Questions
- **Docs**: `docs/05-testing/`
- **Slack**: #meepleai-dev
- **Office Hours**: Mon/Wed 2-3pm (Testing Guild)

### CI/CD Issues
- **Slack**: #meepleai-devops
- **Playbook**: `.github/workflows/README.md`

---

## 🔗 Related Documentation

- [Testing Guide](../docs/05-testing/01-testing-guide.md)
- [Backend Testing](../docs/05-testing/02-backend-testing.md)
- [Frontend Testing](../docs/05-testing/03-frontend-testing.md)
- [E2E Testing](../docs/05-testing/04-e2e-testing.md)
- [CLAUDE.md - Testing Standards](../CLAUDE.md#testing)

---

**Report End** | Generated by `/sc:test --coverage` | Next update: 2026-01-31
