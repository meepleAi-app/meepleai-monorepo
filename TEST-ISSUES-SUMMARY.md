# Test Coverage Issues - Action Plan

**Date**: 2025-11-05
**Status**: 🔴 CRITICAL - Immediate Action Required
**Total Issues**: 7 (2 Critical, 2 High, 2 Medium, 1 Low)

---

## Quick Overview

| Priority | Count | Est. Hours | Assignable |
|----------|-------|------------|-----------|
| 🔴 CRITICAL | 2 | 24-31h | Yes |
| 🟡 HIGH | 2 | 16-20h | Yes |
| 🟢 MEDIUM | 2 | 16-22h | Yes |
| 🔵 LOW | 1 | 4-6h | Yes |
| **TOTAL** | **7** | **60-79h** | **7-10 days** |

---

## Issue List

### 🔴 CRITICAL (Fix Within 48 Hours)

1. **TEST-ISSUE-001**: Auth Component Test Coverage (30% → 80%+)
   - **Impact**: Security-critical component
   - **Effort**: 8-12 hours
   - **File**: `components/auth/`
   - **Link**: [Details](#test-issue-001-auth-component-coverage)

2. **TEST-ISSUE-002**: Fix 125 Failing Frontend Tests
   - **Impact**: CI/CD reliability, deployment blocking
   - **Effort**: 16-19 hours
   - **Suites**: 17 test suites failing
   - **Link**: [Details](#test-issue-002-fix-failing-tests)

### 🟡 HIGH (Fix Within 7 Days)

3. **TEST-ISSUE-003**: Test Infrastructure Coverage
   - **Impact**: Test reliability and maintainability
   - **Effort**: 6-8 hours
   - **Files**: `__tests__/utils/`, `__tests__/pages/chat/shared/`
   - **Link**: [Details](#test-issue-003-test-infrastructure)

4. **TEST-ISSUE-004**: Chat Component Coverage (73% → 85%+)
   - **Impact**: Core feature testing
   - **Effort**: 10-12 hours
   - **Components**: `components/chat/`
   - **Link**: [Details](#test-issue-004-chat-coverage)

### 🟢 MEDIUM (Fix Within 30 Days)

5. **TEST-ISSUE-005**: Execute E2E Test Suite
   - **Impact**: End-to-end validation
   - **Effort**: 8-10 hours
   - **Files**: 28 E2E specs in `apps/web/e2e/`
   - **Link**: [Details](#test-issue-005-e2e-tests)

6. **TEST-ISSUE-006**: Component Coverage Gaps
   - **Impact**: Component reliability
   - **Effort**: 8-12 hours
   - **Components**: diff, admin, editor
   - **Link**: [Details](#test-issue-006-component-gaps)

### 🔵 LOW (Nice to Have)

7. **TEST-ISSUE-007**: Documentation & Maintenance
   - **Impact**: Developer productivity
   - **Effort**: 4-6 hours
   - **Type**: Documentation and tooling
   - **Link**: [Details](#test-issue-007-documentation)

---

## Detailed Issue Descriptions

### TEST-ISSUE-001: Auth Component Coverage

**Priority**: 🔴 CRITICAL
**Current Coverage**: 30% statements, 0% branches, 20% functions
**Target Coverage**: 80%+ across all metrics
**Estimated Effort**: 8-12 hours

#### Problem Statement

The authentication components (`components/auth/`) have critically low test coverage at 30%, with 0% branch coverage. Authentication is a security-critical area that requires comprehensive testing.

#### Affected Components

- `components/auth/OAuthButtons.tsx` (Primary concern)
- OAuth login flow integration
- OAuth error handling
- OAuth provider selection

#### Current Test Failures

- `OAuthButtons.test.tsx` - Complete suite failing
- Auth integration tests missing
- OAuth mock setup incomplete

#### Acceptance Criteria

- [ ] 80%+ statement coverage
- [ ] 80%+ branch coverage
- [ ] 80%+ function coverage
- [ ] All OAuth provider buttons tested
- [ ] OAuth error scenarios covered
- [ ] OAuth success flow validated
- [ ] All tests passing

#### Implementation Tasks

1. **Fix OAuthButtons.test.tsx** (4 hours)
   - Set up proper OAuth provider mocks
   - Test button rendering for each provider
   - Test click handlers
   - Test error states
   - Test loading states

2. **Add OAuth Flow Tests** (3 hours)
   - Test successful OAuth redirect
   - Test OAuth callback handling
   - Test OAuth error handling
   - Test state parameter validation

3. **Add Integration Tests** (3 hours)
   - Test complete login flow
   - Test account linking
   - Test account unlinking
   - Test multiple provider support

4. **Edge Cases & Error Scenarios** (2 hours)
   - Test network failures
   - Test invalid state tokens
   - Test expired tokens
   - Test provider unavailability

#### Files to Create/Modify

- `src/components/auth/__tests__/OAuthButtons.test.tsx` (fix)
- `src/components/auth/__tests__/OAuthFlow.test.tsx` (new)
- `src/components/auth/__tests__/OAuthErrors.test.tsx` (new)
- `src/lib/__tests__/oauth-utils.test.ts` (new, if utils exist)

#### Dependencies

- None (can start immediately)

#### Success Metrics

- Coverage increases from 30% to 80%+
- All test suites passing
- No regression in other tests
- CI/CD pipeline green

---

### TEST-ISSUE-002: Fix Failing Tests

**Priority**: 🔴 CRITICAL
**Failed Tests**: 125 tests across 17 suites
**Pass Rate**: 96.5% (should be 100%)
**Estimated Effort**: 16-19 hours

#### Problem Statement

125 frontend tests are currently failing, preventing reliable CI/CD deployment. Test failures span multiple components and include act() warnings, timeouts, and component rendering issues.

#### Failed Test Suites

1. timer-test-helpers.test.ts (HIGH priority)
2. Message.test.tsx (HIGH priority)
3. UploadQueueItem.test.tsx (MEDIUM)
4. OAuthButtons.test.tsx (CRITICAL - see ISSUE-001)
5. UploadQueue.test.tsx (MEDIUM)
6. DiffToolbar.test.tsx (HIGH priority)
7. MessageActions.test.tsx (MEDIUM)
8. admin-prompts-compare.test.tsx (MEDIUM)
9. EditorToolbar.test.tsx (MEDIUM)
10. ChatHistory.test.tsx (MEDIUM)
11. DiffSearchInput.test.tsx (LOW)
12. setup.test.tsx (MEDIUM)
13. analytics.test.tsx (LOW)
14. CommentThread.test.tsx (MEDIUM)
15. upload.test.tsx (HIGH - timeout)
16-17. Additional suites (analyze in detail)

#### Common Failure Patterns

**Pattern 1: React act() Warnings** (10+ tests)
```
Warning: An update to TestComponent inside a test was not wrapped in act(...)
```
- **Root Cause**: Async state updates not properly wrapped
- **Affected**: useUploadQueue.test.ts, timer tests, multiple component tests
- **Fix Strategy**: Wrap state updates in `act()` or use `waitFor()`

**Pattern 2: Timeout Errors** (5+ tests)
```
Exceeded timeout of 5000 ms for a test
```
- **Root Cause**: Slow test execution or missing mocks
- **Affected**: upload.test.tsx (validation tests), setup.test.tsx
- **Fix Strategy**: Increase timeout or optimize test setup

**Pattern 3: Missing DOM Elements** (DiffToolbar suite)
```
Unable to find an element by: [aria-label="Clear search"]
```
- **Root Cause**: Component doesn't have expected aria-label
- **Affected**: DiffToolbar.test.tsx, DiffSearchInput.test.tsx
- **Fix Strategy**: Update component or test selector

**Pattern 4: Component Rendering Failures** (Multiple)
- Message component not rendering correctly
- MessageActions interactions failing
- ChatHistory not loading
- **Fix Strategy**: Debug component lifecycle and props

#### Implementation Plan

##### Phase 1: React act() Warnings (4-6 hours)

**Files**:
- `src/hooks/__tests__/useUploadQueue.test.ts`
- `src/test-utils/__tests__/timer-test-helpers.test.ts`
- Other affected component tests

**Approach**:
```typescript
// BEFORE (causes warning)
setQueue(prev => [...prev, newItem]);

// AFTER (properly wrapped)
await act(async () => {
  setQueue(prev => [...prev, newItem]);
});

// OR use waitFor
await waitFor(() => {
  expect(screen.getByText('Expected')).toBeInTheDocument();
});
```

##### Phase 2: Timeout Errors (3-4 hours)

**Files**:
- `src/__tests__/pages/upload.test.tsx`
- `src/__tests__/pages/setup.test.tsx`

**Approach**:
```typescript
// Increase timeout for specific tests
it('should validate PDF upload', async () => {
  // test code
}, 10000); // 10s timeout

// OR optimize test setup
beforeEach(async () => {
  // Use faster mocks instead of actual delays
  jest.useFakeTimers();
});
```

##### Phase 3: DiffToolbar Tests (2 hours)

**Files**:
- `src/components/diff/__tests__/DiffToolbar.test.tsx`
- `src/components/diff/__tests__/DiffSearchInput.test.tsx`
- `src/components/diff/DiffToolbar.tsx` (component fix)

**Approach Option 1** (Fix component):
```typescript
// Add missing aria-label
<button
  onClick={onClear}
  aria-label="Clear search"  // ADD THIS
>
  Clear
</button>
```

**Approach Option 2** (Fix test):
```typescript
// Use alternative selector
const clearButton = screen.getByRole('button', { name: /clear/i });
```

##### Phase 4: OAuth Tests (4 hours)

**See TEST-ISSUE-001** - This is critical and has its own issue.

##### Phase 5: Message & Chat Tests (3 hours)

**Files**:
- `src/__tests__/components/chat/Message.test.tsx`
- `src/components/chat/__tests__/MessageActions.test.tsx`
- `src/__tests__/components/chat/ChatHistory.test.tsx`

**Approach**:
1. Review component props and setup
2. Add missing mocks (API calls, user context)
3. Fix timing issues with waitFor
4. Update snapshots if needed

##### Phase 6: Remaining Tests (4-6 hours)

- admin-prompts-compare.test.tsx
- EditorToolbar.test.tsx
- UploadQueueItem.test.tsx
- UploadQueue.test.tsx
- CommentThread.test.tsx
- analytics.test.tsx (low priority)

#### Acceptance Criteria

- [ ] All 125 failing tests fixed
- [ ] 100% test pass rate (3,567/3,567)
- [ ] No act() warnings in test output
- [ ] All tests complete within reasonable time (<2min per suite)
- [ ] CI/CD pipeline green
- [ ] No regression in passing tests

#### Success Metrics

- Test pass rate: 96.5% → 100%
- Failed test suites: 17 → 0
- CI/CD reliability: Restored
- Deployment confidence: High

---

### TEST-ISSUE-003: Test Infrastructure

**Priority**: 🟡 HIGH
**Current Coverage**: 49-70% (should be 90%+)
**Estimated Effort**: 6-8 hours

#### Problem Statement

Test utilities and shared fixtures have low coverage:
- `__tests__/pages/chat/shared`: 49.33% statements, 0% branches
- `__tests__/utils`: 70.58% statements, 0% branches

Test infrastructure should have very high coverage (90%+) as it supports all other tests.

#### Affected Files

1. `src/__tests__/pages/chat/shared/` - Chat test fixtures
2. `src/__tests__/utils/` - General test utilities
3. `src/lib/__tests__/test-utils.tsx` - 57% coverage

#### Issues

- **0% branch coverage** in shared utilities (CRITICAL)
- Untested error paths in test helpers
- Missing documentation for test utilities
- Inconsistent test fixture usage

#### Implementation Tasks

1. **Chat Shared Utilities** (3 hours)
   - Add tests for `setupFullChatEnvironment`
   - Add tests for chat fixtures
   - Add tests for mock data generators
   - Test all conditional branches

2. **General Test Utilities** (2 hours)
   - Add tests for custom render functions
   - Add tests for test helpers
   - Test error scenarios
   - Test edge cases

3. **Documentation** (1-2 hours)
   - Document test utility API
   - Add usage examples
   - Create test writing guide
   - Document best practices

#### Acceptance Criteria

- [ ] 90%+ coverage for all test utilities
- [ ] 80%+ branch coverage
- [ ] All conditional paths tested
- [ ] Documentation complete
- [ ] Examples for each utility function

#### Files to Create/Modify

- `src/__tests__/pages/chat/shared/__tests__/fixtures.test.ts` (new)
- `src/__tests__/utils/__tests__/test-helpers.test.ts` (new)
- `src/lib/__tests__/test-utils.test.tsx` (enhance existing)
- `docs/testing/test-utilities-guide.md` (new)

---

### TEST-ISSUE-004: Chat Coverage

**Priority**: 🟡 HIGH
**Current Coverage**: 73.89% statements, 75% functions
**Target Coverage**: 85%+ across all metrics
**Estimated Effort**: 10-12 hours

#### Problem Statement

Chat components are core features but have lower coverage (73.89%) than target (85%+). Missing tests for edge cases, error scenarios, and complex interactions.

#### Affected Components

- `components/chat/` - Main chat components (383 statements)
- ChatHistory - Loading and pagination
- MessageActions - User interactions
- Message rendering - Various message types
- Chat state management

#### Coverage Gaps

- **100 uncovered statements** (283/383 covered)
- **51 untested branches** (212/263 covered)
- **25 untested functions** (75/100 covered)

#### Implementation Tasks

1. **ChatHistory Component** (3 hours)
   - Test history loading states
   - Test pagination
   - Test infinite scroll
   - Test empty state
   - Test error handling

2. **MessageActions Component** (3 hours)
   - Test edit action
   - Test delete action
   - Test copy action
   - Test share action
   - Test permission checks

3. **Message Rendering** (2-3 hours)
   - Test different message types
   - Test message formatting
   - Test code blocks
   - Test markdown rendering
   - Test link handling

4. **Chat State Management** (2-3 hours)
   - Test message sending
   - Test message updates
   - Test message deletion
   - Test optimistic updates
   - Test error recovery

#### Acceptance Criteria

- [ ] 85%+ statement coverage
- [ ] 85%+ function coverage
- [ ] 80%+ branch coverage
- [ ] All user interactions tested
- [ ] Error scenarios covered
- [ ] Edge cases tested

---

### TEST-ISSUE-005: E2E Tests

**Priority**: 🟢 MEDIUM
**Test Files**: 28 E2E specs not executed
**Estimated Effort**: 8-10 hours (including setup)

#### Problem Statement

28 E2E test files exist but were not executed in current analysis due to infrastructure requirements. E2E tests validate complete user workflows and system integration.

#### E2E Test Inventory

**Authentication** (3 files):
- authenticated.spec.ts
- demo-user-login.spec.ts
- OAuth flows (partially covered)

**Chat Features** (5 files):
- chat.spec.ts
- chat-animations.spec.ts
- chat-context-switching.spec.ts
- chat-edit-delete.spec.ts
- chat-streaming.spec.ts

**Editor** (3 files):
- editor.spec.ts
- editor-advanced.spec.ts
- editor-rich-text.spec.ts

**Admin** (4 files):
- admin.spec.ts
- admin-analytics.spec.ts
- admin-configuration.spec.ts
- admin-users.spec.ts

**PDF Processing** (3 files):
- pdf-preview.spec.ts
- pdf-processing-progress.spec.ts
- pdf-upload-journey.spec.ts

**Other** (10 files):
- accessibility.spec.ts
- ai04-qa-snippets.spec.ts
- chess-registration.spec.ts
- comments-enhanced.spec.ts
- error-handling.spec.ts
- home.spec.ts
- n8n.spec.ts
- session-expiration.spec.ts
- setup.spec.ts
- timeline.spec.ts
- versions.spec.ts

#### Infrastructure Requirements

1. **Backend Services**:
   - API server on port 8080
   - PostgreSQL database
   - Qdrant vector database
   - Redis cache

2. **Frontend**:
   - Next.js dev server on port 3000

3. **Test Environment**:
   - Playwright browsers installed
   - Test data seeded
   - Environment variables configured

#### Implementation Tasks

1. **Environment Setup** (2-3 hours)
   - Set up Docker Compose for services
   - Configure test environment variables
   - Seed test database
   - Verify all services healthy

2. **E2E Execution** (4-5 hours)
   - Run full E2E suite
   - Document any failures
   - Fix critical failures
   - Update test configurations

3. **CI Integration** (2 hours)
   - Add E2E tests to CI pipeline
   - Configure test parallelization
   - Set up test artifacts
   - Configure failure notifications

#### Acceptance Criteria

- [ ] All 28 E2E tests executed
- [ ] E2E test results documented
- [ ] Critical failures fixed
- [ ] E2E tests in CI pipeline
- [ ] E2E test documentation updated

#### Success Metrics

- E2E pass rate > 95%
- Critical user flows validated
- Full system integration tested
- Deployment confidence increased

---

### TEST-ISSUE-006: Component Gaps

**Priority**: 🟢 MEDIUM
**Components**: diff, admin, editor
**Estimated Effort**: 8-12 hours

#### Problem Statement

Several component groups are close to but below 85% coverage target:
- components/diff: 84.25%
- components/admin: 84.61%
- components/editor: Various coverage levels

#### Affected Components

1. **Diff Components** (84.25% coverage)
   - 20 uncovered statements
   - 11 untested branches
   - 7 untested functions
   - Need: +1% to reach 85% target

2. **Admin Components** (84.61% coverage)
   - 14 uncovered statements
   - 15 untested branches
   - 3 untested functions
   - Need: +0.4% to reach 85% target

3. **Editor Components** (analyze individually)
   - EditorToolbar - tests currently failing
   - RichTextEditor - good coverage
   - Editor utilities - need analysis

#### Implementation Tasks

1. **Diff Components** (3-4 hours)
   - Add tests for uncovered diff algorithms
   - Test edge cases in diff navigation
   - Test search functionality edge cases
   - Test diff rendering edge cases

2. **Admin Components** (3-4 hours)
   - Test admin form validations
   - Test admin permissions
   - Test admin data mutations
   - Test admin error handling

3. **Editor Components** (2-4 hours)
   - Fix EditorToolbar tests (see ISSUE-002)
   - Add tests for editor commands
   - Test editor state management
   - Test editor plugins

#### Acceptance Criteria

- [ ] diff components: 84.25% → 85%+
- [ ] admin components: 84.61% → 85%+
- [ ] editor components: 85%+ (after fixes)
- [ ] All edge cases covered
- [ ] Error scenarios tested

---

### TEST-ISSUE-007: Documentation

**Priority**: 🔵 LOW
**Type**: Documentation and tooling
**Estimated Effort**: 4-6 hours

#### Problem Statement

While coverage is good, test maintainability and onboarding could be improved with better documentation and tooling.

#### Current State

- ✅ Coverage reports generated
- ✅ CI enforcement in place
- ⚠️ Test writing guide missing
- ⚠️ Test patterns not documented
- ⚠️ Coverage trends not tracked
- ⚠️ No coverage badges

#### Implementation Tasks

1. **Test Writing Guide** (2 hours)
   - Document test structure
   - Provide component test examples
   - Provide hook test examples
   - Document common patterns
   - Document mock strategies

2. **Coverage Monitoring** (1-2 hours)
   - Create coverage trend script
   - Set up coverage dashboard
   - Add coverage badges to README
   - Configure coverage notifications

3. **Maintenance Scripts** (1-2 hours)
   - Create test cleanup scripts
   - Create coverage diff tool
   - Create test generator templates
   - Update CLAUDE.md

#### Deliverables

- `docs/testing/test-writing-guide.md`
- `docs/testing/test-patterns.md`
- `tools/coverage-trends.sh`
- Coverage badges in README.md
- Updated CLAUDE.md

#### Acceptance Criteria

- [ ] Test writing guide complete
- [ ] Test patterns documented
- [ ] Coverage trends tracked
- [ ] Coverage badges added
- [ ] CLAUDE.md updated

---

## Implementation Strategy

### Week 1 (Critical Issues)

**Days 1-2**: TEST-ISSUE-002 (Fix failing tests)
- Focus on act() warnings and timeouts first
- Fix DiffToolbar and Message tests
- Target: 50% of failures fixed

**Days 3-4**: TEST-ISSUE-001 (Auth coverage)
- Fix OAuthButtons tests
- Add OAuth flow tests
- Target: 80%+ auth coverage

**Day 5**: TEST-ISSUE-002 (Remaining)
- Fix remaining test failures
- Target: 100% pass rate

### Week 2 (High Priority)

**Days 1-2**: TEST-ISSUE-003 (Test infrastructure)
- Add tests for shared utilities
- Add tests for test-utils
- Target: 90%+ coverage

**Days 3-5**: TEST-ISSUE-004 (Chat coverage)
- Add ChatHistory tests
- Add MessageActions tests
- Target: 85%+ coverage

### Week 3-4 (Medium Priority)

**Week 3**: TEST-ISSUE-005 (E2E tests)
- Set up E2E environment
- Run E2E suite
- Fix critical failures

**Week 4**: TEST-ISSUE-006 (Component gaps)
- Fix diff component gaps
- Fix admin component gaps
- Fix editor component gaps

### Ongoing (Low Priority)

**Anytime**: TEST-ISSUE-007 (Documentation)
- Update documentation as you work
- Add examples from fixed tests
- Create guides from learnings

---

## Success Criteria

### Overall Project Success

- [ ] All 125 test failures fixed (100% pass rate)
- [ ] Auth coverage increased to 80%+
- [ ] Test infrastructure at 90%+
- [ ] Chat coverage at 85%+
- [ ] E2E tests executed and documented
- [ ] All component coverage gaps closed
- [ ] Documentation updated

### Metrics

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| **Test Pass Rate** | 96.5% | 100% | 🔴 |
| **Auth Coverage** | 30% | 80%+ | 🔴 |
| **Test Infra Coverage** | 49-70% | 90%+ | 🟡 |
| **Chat Coverage** | 73.89% | 85%+ | 🟡 |
| **E2E Execution** | 0/28 | 28/28 | 🔴 |
| **Diff Coverage** | 84.25% | 85%+ | 🟡 |
| **Admin Coverage** | 84.61% | 85%+ | 🟡 |

---

## Next Steps

1. **Review this document** with the team
2. **Prioritize issues** based on business needs
3. **Assign issues** to team members
4. **Track progress** using GitHub Issues
5. **Update** this document as issues are resolved

---

## GitHub Issues Creation

To create GitHub issues from this document:

```bash
# Create issues using gh CLI
gh issue create --title "TEST-001: Increase Auth Component Coverage to 80%+" \
  --body-file issues/TEST-ISSUE-001.md \
  --label "testing,critical,security" \
  --assignee "@me"

# Repeat for other issues...
```

Or manually create issues in GitHub with labels:
- 🔴 CRITICAL: `critical`, `testing`, `bug`
- 🟡 HIGH: `high-priority`, `testing`, `enhancement`
- 🟢 MEDIUM: `medium-priority`, `testing`, `enhancement`
- 🔵 LOW: `low-priority`, `documentation`, `testing`

---

**Document Version**: 1.0
**Last Updated**: 2025-11-05
**Status**: Ready for Implementation
