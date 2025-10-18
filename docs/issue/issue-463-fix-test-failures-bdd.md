# Issue #463: Fix Pre-Existing Test Failures - BDD Specification

## Feature Overview

**Feature**: Fix Pre-Existing Test Failures in Main Branch
**Issue**: #463
**Priority**: High (Blocking CI)
**Type**: Bug Fix / Test Maintenance

**Problem Statement**:
> The main branch contains 58 failing frontend tests (12 in admin-cache.test.tsx, 46 in chat.test.tsx) that are blocking CI/CD pipeline reliability and preventing accurate regression detection.

**Success Metrics**:
- All 58 tests pass consistently
- CI pipeline shows 0 test failures on main branch
- Test execution time remains under 60 seconds
- No flaky tests (100% pass rate across 10+ runs)
- Test coverage maintained at 90%+

---

## Background Context

### Current State
- **admin-cache.test.tsx**: 12/19 tests failing (63% failure rate)
- **chat.test.tsx**: 46/59 tests failing (78% failure rate)
- **Total Impact**: 58 failing tests on main branch
- **CI Status**: Blocked due to test failures

### Root Cause Analysis Summary

**admin-cache.test.tsx** (12 failures):
1. Module cache clearing pattern breaks mocks (lines 20-30)
2. Environment variable timing race (env set AFTER module loads, lines 86, 99, 159, etc.)
3. Multiple renders with cleanup() without module reload (lines 166, 178, 586)
4. Fetch mock sequencing assumptions (lines 169-172, 589-592)
5. waitFor() racing conditions (lines 105-117, 225-234)
6. Timer mock issues with jest.useFakeTimers() (lines 502-534)

**chat.test.tsx** (46 failures):
1. Infinite promise hangs (promises never resolve, widespread)
2. Mock sequencing after mockClear() without proper reset (widespread)
3. Async callback timing with setTimeout(..., 0) (widespread)
4. Component dependency array issues (chatStatesByGame causes extra renders)
5. Missing act() wrappers (50+ locations)
6. setupAuthenticatedState() helper sequencing issues
7. beforeEach() reset issues with mock overrides

---

## BDD Scenarios

### Scenario 1: Fix admin-cache Environment Variable Timing

```gherkin
Feature: admin-cache Environment Variable Configuration
  As a test suite
  I need environment variables set BEFORE module loading
  So that the API client reads the correct configuration

Scenario: Environment variable set before module import
  Given process.env.NEXT_PUBLIC_API_BASE is undefined
  When I set process.env.NEXT_PUBLIC_API_BASE = "https://api.example.com"
  And I call loadCacheDashboard() to import the module
  Then the API client should use "https://api.example.com" as the base URL
  And fetch calls should be made to "https://api.example.com/api/v1/*"

Scenario: Environment variable fallback when unset
  Given process.env.NEXT_PUBLIC_API_BASE is undefined
  When I import the CacheDashboard module without setting env
  Then the API client should use API_BASE_FALLBACK
  And fetch calls should be made to "http://localhost:8080/api/v1/*"

Acceptance Criteria:
  - Test "renders loading state while data is being fetched" passes
  - Test "renders cache statistics and top questions successfully" passes
  - Test "falls back to localhost API base when NEXT_PUBLIC_API_BASE is unset" passes
  - All 19 tests use consistent env variable pattern
  - No timing race conditions between env setup and module load
```

### Scenario 2: Fix admin-cache Module Cache Pattern

```gherkin
Feature: admin-cache Module Cache Management
  As a test suite with dynamic module loading
  I need proper module cache clearing between tests
  So that mocks remain stable and environment changes take effect

Scenario: Module reload preserves mock stability
  Given I have configured global.fetch mock in beforeAll
  And I have set process.env.NEXT_PUBLIC_API_BASE
  When I call loadCacheDashboard() to clear require.cache
  Then the fetch mock should remain active and stable
  And the new module should use the configured environment variable
  And subsequent mock calls should be tracked correctly

Scenario: Multiple renders with cleanup do not require reload
  Given I have rendered CacheDashboard with initial test data
  When I call cleanup() to unmount the component
  And I reset fetchMock with mockReset()
  And I configure new mock return values
  And I render CacheDashboard again
  Then the component should render with new data
  And I should NOT need to call loadCacheDashboard() again

Acceptance Criteria:
  - Test "displays hit rate with appropriate color coding" passes (lines 149-189)
  - Test "formats cache size correctly for different units" passes (lines 570-597)
  - No module reload needed between cleanup() and re-render
  - Fetch mock remains stable across all 19 tests
  - loadCacheDashboard() only called when env variable changes
```

### Scenario 3: Fix admin-cache Fetch Mock Sequencing

```gherkin
Feature: admin-cache Fetch Mock Call Order
  As a test suite with multiple async API calls
  I need predictable fetch mock sequencing
  So that each API call receives the correct mocked response

Scenario: Sequential fetch calls with proper ordering
  Given I configure fetchMock with chained responses:
    | Call # | Endpoint             | Response          |
    | 1      | /api/v1/games        | mockGamesResponse |
    | 2      | /api/v1/cache/stats  | mockStatsResponse |
  When I render CacheDashboard
  Then the first fetch call should receive mockGamesResponse
  And the second fetch call should receive mockStatsResponse
  And waitFor assertions should wait for both calls to complete

Scenario: Mock reset between subtests maintains sequencing
  Given I have completed a subtest with 2 fetch calls
  And I call cleanup() to unmount
  And I call fetchMock.mockReset() to clear history
  When I configure new mock responses with mockResolvedValueOnce()
  And I render CacheDashboard again
  Then the mock call sequence should restart from 1
  And each fetch call should receive the correct response in order

Acceptance Criteria:
  - Test "displays hit rate with appropriate color coding" passes (lines 149-189)
  - Test "formats cache size correctly for different units" passes (lines 570-597)
  - No fetch calls receive wrong response due to sequencing
  - waitFor() assertions do not race with async fetch completion
  - Mock reset properly clears call history without breaking sequencing
```

### Scenario 4: Fix admin-cache Timer Mocks

```gherkin
Feature: admin-cache Toast Auto-Dismissal Timer
  As a test suite testing auto-dismiss behavior
  I need proper fake timer management
  So that timeouts can be controlled deterministically

Scenario: Auto-dismiss toast after 5 seconds
  Given I call jest.useFakeTimers() before test
  And I render CacheDashboard with successful API responses
  When I trigger a refresh action that shows a toast
  And I wait for the toast to appear in the DOM
  And I advance fake timers by 5000ms
  Then the toast should be removed from the DOM
  And I call jest.useRealTimers() to restore normal timers

Scenario: Fake timers do not interfere with async operations
  Given I call jest.useFakeTimers()
  When I configure fetch mocks with mockResolvedValueOnce()
  And I render CacheDashboard
  Then async fetch promises should still resolve
  And waitFor() should successfully wait for async operations
  And I should NOT need to manually flush promises

Acceptance Criteria:
  - Test "automatically dismisses toast notifications after 5 seconds" passes (lines 501-535)
  - jest.useFakeTimers() scoped to specific test only
  - jest.useRealTimers() called in afterEach or test cleanup
  - Fake timers do not break waitFor() or async assertions
  - No "act() warning" errors related to timer advancement
```

### Scenario 5: Fix chat.test Infinite Promise Hangs

```gherkin
Feature: chat.test Promise Resolution
  As a test suite with async API calls
  I need all promises to resolve or reject
  So that tests complete instead of timing out

Scenario: API mocks resolve immediately
  Given I mock api.get('/api/v1/users/me') with mockResolvedValue()
  And I mock api.get('/api/v1/games') with mockResolvedValue()
  And I mock api.get('/api/v1/agents') with mockResolvedValue()
  When I render ChatPage
  Then all API promises should resolve within 100ms
  And the component should transition from loading to loaded state
  And the test should complete within 5 seconds

Scenario: Replace infinite promises with resolved promises
  Given I find code using "() => new Promise(() => {})" for loading state
  When I replace with "() => new Promise(resolve => setTimeout(resolve, 1000))"
  Or I replace with waitFor assertions that check loading state
  Then tests should not hang indefinitely
  And timeouts should be eliminated

Acceptance Criteria:
  - All 46 failing tests complete without timeout
  - No test uses "new Promise(() => {})" for loading state testing
  - All api.* mocks use mockResolvedValue() or mockRejectedValue()
  - Test execution time for chat.test.tsx under 30 seconds
  - No "Exceeded timeout of 5000ms" errors
```

### Scenario 6: Fix chat.test Mock Sequencing

```gherkin
Feature: chat.test Mock State Management
  As a test suite with complex mock dependencies
  I need proper mock reset between tests
  So that previous test state does not leak into subsequent tests

Scenario: Clean mock state in beforeEach
  Given I have completed a test that called api.get() 5 times
  When beforeEach() runs for the next test
  Then mockApi.get.mockClear() should clear call history
  And mockApi.post.mockClear() should clear call history
  And I should reconfigure mocks with fresh mockResolvedValue() calls
  And the new test should start with clean mock state

Scenario: Mock configuration after mockClear is stable
  Given I call mockApi.get.mockClear() in beforeEach
  When I configure mockApi.get.mockResolvedValueOnce(mockAuthResponse)
  And I configure mockApi.get.mockResolvedValueOnce(mockGames)
  And I render ChatPage
  Then the first api.get call should receive mockAuthResponse
  And the second api.get call should receive mockGames
  And no calls should return undefined due to cleared mocks

Acceptance Criteria:
  - All tests use proper beforeEach() cleanup
  - Mock state isolated between tests
  - No test failures due to stale mock return values
  - mockClear() called for all API methods in beforeEach
  - Fresh mockResolvedValue() configuration per test
```

### Scenario 7: Fix chat.test act() Wrapper Coverage

```gherkin
Feature: chat.test React State Update Wrapping
  As a test suite triggering async state updates
  I need proper act() wrappers around state changes
  So that React warnings are eliminated and assertions are reliable

Scenario: Wrap user interactions in act()
  Given I have a user interaction that triggers state updates
  When I call await user.click(button)
  Or I call await user.type(input, 'text')
  Then the interaction should be wrapped in act() automatically by userEvent
  And I should wait for state updates with waitFor()
  And no "act() warning" should appear in console

Scenario: Wrap async operations in waitFor()
  Given I trigger an async operation (API call, timer, promise)
  When I need to assert on the resulting state change
  Then I should use await waitFor(() => expect(...))
  And I should NOT use bare await for timing
  And I should NOT assume immediate state updates

Scenario: Identify missing act() wrappers
  Given a test that shows "act() warning" in console
  When I find the state-changing operation causing the warning
  Then I should wrap it in await waitFor(() => ...)
  Or I should ensure userEvent.setup() is used correctly
  Or I should add act() wrapper around the operation

Acceptance Criteria:
  - Zero "act() warning" messages in test output
  - All user interactions use await user.*() from userEvent.setup()
  - All async assertions use waitFor()
  - All state-changing operations properly wrapped
  - Test output clean with no React warnings
```

### Scenario 8: Fix chat.test setupAuthenticatedState Helper

```gherkin
Feature: chat.test Authentication Helper Reliability
  As a test suite with repeated authentication setup
  I need a stable setupAuthenticatedState() helper
  So that authentication state is consistent across tests

Scenario: setupAuthenticatedState configures mocks correctly
  Given I call setupAuthenticatedState() helper
  Then mockApi.get should be configured to return mockAuthResponse for '/api/v1/users/me'
  And mockApi.get should be configured to return mockGames for '/api/v1/games'
  And mockApi.get should be configured to return mockAgents for '/api/v1/agents'
  And the mock configuration should be stable for the duration of the test

Scenario: setupAuthenticatedState is idempotent
  Given I call setupAuthenticatedState() in beforeEach
  When a test calls it again explicitly
  Then the second call should not break existing mock configuration
  Or the helper should be designed to only be called once
  And tests should not need to call it multiple times

Scenario: Helper mock sequencing matches component expectations
  Given ChatPage component calls APIs in this order:
    | Order | Endpoint            | Expected Response |
    | 1     | /api/v1/users/me    | mockAuthResponse  |
    | 2     | /api/v1/games       | mockGames         |
    | 3     | /api/v1/agents      | mockAgents        |
  When I call setupAuthenticatedState()
  Then mocks should be configured in the same order
  And component API calls should match mock sequence exactly

Acceptance Criteria:
  - setupAuthenticatedState() helper works consistently
  - All tests using the helper pass
  - Helper does not cause mock sequencing issues
  - Helper can be called once per test safely
  - Component rendering succeeds after helper call
```

### Scenario 9: Fix chat.test Component Dependency Arrays

```gherkin
Feature: chat.test Infinite Re-Render Prevention
  As a test suite with complex component state
  I need proper dependency array management
  So that useEffect does not cause infinite re-render loops

Scenario: chatStatesByGame dependency causes extra renders
  Given ChatPage component has useEffect with [chatStatesByGame] dependency
  And chatStatesByGame is an object that changes reference on every render
  When the component renders
  Then useEffect should not trigger infinite re-renders
  And the object reference should be stable
  Or the dependency should be removed/changed to primitive values

Scenario: Memoize complex objects in dependencies
  Given I have a useEffect that depends on an object
  When the object is recreated on every render
  Then I should use useMemo() to stabilize the reference
  Or I should use specific primitive properties as dependencies
  Or I should restructure state to avoid object dependencies

Acceptance Criteria:
  - No infinite re-render loops in tests
  - useEffect dependencies use stable references
  - Complex objects in dependencies are memoized
  - Component renders predictable number of times
  - Tests complete without render loop timeouts
```

### Scenario 10: Fix chat.test Async Callback Timing

```gherkin
Feature: chat.test Async Callback Reliability
  As a test suite with async callbacks
  I need proper async/await handling
  So that callbacks execute before assertions

Scenario: Replace setTimeout(..., 0) with proper async
  Given I have code using setTimeout(() => callback(), 0)
  When I need to wait for the callback to execute
  Then I should use await waitFor(() => expect(...))
  And I should NOT rely on setTimeout for async coordination
  And I should NOT assume callback execution order

Scenario: Wait for useChatStreaming callbacks
  Given useChatStreaming hook captures onComplete callback
  And onComplete is called asynchronously after streaming
  When I test streaming completion
  Then I should trigger mockOnComplete explicitly in test
  Or I should use waitFor() to wait for state changes from callback
  And I should NOT assume immediate callback execution

Acceptance Criteria:
  - Zero setTimeout(..., 0) patterns for async testing
  - All async callbacks waited with waitFor()
  - useChatStreaming callbacks testable and reliable
  - No race conditions between callbacks and assertions
  - Tests deterministic and non-flaky
```

---

## Acceptance Criteria

### admin-cache.test.tsx (12 tests)
- [ ] All 19 tests pass consistently (100% pass rate)
- [ ] Environment variable set before module import in all tests
- [ ] loadCacheDashboard() only called when necessary (env changes)
- [ ] cleanup() does not require module reload for re-render
- [ ] Fetch mock sequencing reliable across all subtests
- [ ] Timer mocks properly scoped with jest.useFakeTimers()/useRealTimers()
- [ ] No timing race conditions or flaky failures
- [ ] Test execution time under 15 seconds

### chat.test.tsx (46 tests)
- [ ] All 59 tests pass consistently (100% pass rate)
- [ ] Zero infinite promise hangs (all promises resolve/reject)
- [ ] Zero "act() warning" messages in test output
- [ ] setupAuthenticatedState() helper works reliably
- [ ] Mock state properly isolated between tests (clean beforeEach)
- [ ] No infinite re-render loops from dependency arrays
- [ ] Zero setTimeout(..., 0) patterns for async coordination
- [ ] Test execution time under 45 seconds

### Overall Quality Gates
- [ ] 0 test failures in CI pipeline
- [ ] 100% pass rate across 10+ consecutive CI runs (no flaky tests)
- [ ] Test coverage maintained at 90%+ (no regression)
- [ ] No console warnings (React, act(), timer, etc.)
- [ ] Test execution time for both suites under 60 seconds total
- [ ] CI pipeline returns to green status on main branch

---

## Implementation Strategy

### Phase 1: admin-cache.test.tsx (Day 1)

**Step 1.1: Fix Environment Variable Timing**
- Move `process.env.NEXT_PUBLIC_API_BASE = apiBase` BEFORE `loadCacheDashboard()` call
- Pattern: Always set env → then load module
- Files: Lines 86, 99, 159, 213, 245, 303, 353, 378, 411, 442, 475, 491, 510, 544, 579, 630, 654, 675

**Step 1.2: Eliminate Unnecessary Module Reloads**
- Remove `loadCacheDashboard()` call after cleanup() in subtests
- Use direct `render(<CacheDashboard />)` after cleanup
- Use `CacheDashboard = loadCacheDashboard()` once per test, not per subtest
- Files: Lines 166-189 (hit rate test), 586-597 (cache size test)

**Step 1.3: Fix Fetch Mock Sequencing**
- Add explicit waitFor() for each fetch call before asserting
- Ensure mockReset() clears call history properly
- Verify mock chain order matches API call order
- Files: Lines 169-172, 589-592

**Step 1.4: Fix Timer Mocks**
- Scope jest.useFakeTimers() to single test only (line 502)
- Add jest.useRealTimers() in afterEach or test cleanup (line 534)
- Ensure timers don't interfere with waitFor() async assertions
- Files: Lines 501-535

**Expected Outcome**: All 19 admin-cache tests pass

### Phase 2: chat.test.tsx Infinite Promises (Day 2)

**Step 2.1: Audit Promise Usage**
- Find all instances of `new Promise(() => {})` (infinite promises)
- Replace with `mockResolvedValue()` or `mockRejectedValue()`
- Remove loading state tests that rely on infinite promises
- Alternative: Use `waitFor(() => expect(screen.getByText('Loading')))` instead

**Step 2.2: Configure API Mocks to Resolve**
- Ensure all api.get/post/put/delete mocks return resolved promises
- Pattern: `mockApi.get.mockResolvedValue(response)` not `mockImplementation(() => new Promise(() => {}))`
- Verify setupAuthenticatedState() returns resolved promises

**Expected Outcome**: Zero timeout errors, tests complete in under 45s

### Phase 3: chat.test.tsx Mock Management (Day 3)

**Step 3.1: Strengthen beforeEach Cleanup**
- Add mockApi.get.mockClear()
- Add mockApi.post.mockClear()
- Add mockApi.put.mockClear()
- Add mockApi.delete.mockClear()
- Add mockStartStreaming.mockClear()
- Add mockStopStreaming.mockClear()

**Step 3.2: Verify Mock Configuration After Clear**
- Ensure each test reconfigures mocks after mockClear()
- Pattern: `beforeEach(() => { mockApi.get.mockClear(); mockApi.get.mockResolvedValue(...); })`
- Avoid relying on mocks from previous tests

**Expected Outcome**: No stale mock state, isolated test execution

### Phase 4: chat.test.tsx act() Wrappers (Day 4)

**Step 4.1: Audit Console Warnings**
- Run tests with console.warn enabled
- Identify all "act() warning" sources
- Document line numbers and state-changing operations

**Step 4.2: Add waitFor() Wrappers**
- Wrap all async assertions in `await waitFor(() => expect(...))`
- Ensure userEvent calls use await: `await user.click(button)`
- Pattern: Any state change → wrap in waitFor or act()

**Step 4.3: Review useChatStreaming Mock**
- Ensure mock updates do not trigger sync state changes
- Verify callbacks are async-safe
- Add act() around manual callback triggers if needed

**Expected Outcome**: Zero "act() warning" messages

### Phase 5: chat.test.tsx Edge Cases (Day 5)

**Step 5.1: Fix setupAuthenticatedState Helper**
- Review helper implementation for mock sequencing
- Ensure it matches component API call order
- Make idempotent (safe to call multiple times) or enforce single call
- Add JSDoc comment documenting usage

**Step 5.2: Fix Dependency Arrays**
- Find useEffect with chatStatesByGame dependency
- Refactor to use primitive dependencies or useMemo
- Test for infinite re-render loops
- Verify render counts are predictable

**Step 5.3: Eliminate setTimeout Patterns**
- Find all setTimeout(..., 0) for async coordination
- Replace with waitFor(() => expect(...))
- Remove timing assumptions

**Expected Outcome**: Stable, deterministic tests with no race conditions

### Phase 6: Validation & CI (Day 6)

**Step 6.1: Local Testing**
- Run `pnpm test admin-cache.test.tsx` → 19/19 pass
- Run `pnpm test chat.test.tsx` → 59/59 pass
- Run 10 times locally to verify no flakiness

**Step 6.2: CI Testing**
- Push to branch, verify CI green
- Merge to main
- Monitor CI for 10+ builds to ensure stability

**Step 6.3: Coverage Verification**
- Run `pnpm test:coverage`
- Verify coverage ≥90%
- Ensure no regression from fixes

**Expected Outcome**: Green CI, no flaky tests, coverage maintained

---

## Risk Assessment & Mitigation

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| Fixing one test breaks another | Medium | High | Fix tests iteratively, run full suite after each change |
| Module cache pattern too fragile | Medium | Medium | Simplify loadCacheDashboard(), minimize module reloading |
| act() wrappers cause new timing issues | Low | Medium | Use waitFor() consistently, avoid manual act() calls |
| Mock sequencing still unreliable | Medium | High | Document expected call order, add explicit assertions |
| Timer mocks interfere with async | Low | Medium | Scope jest.useFakeTimers() narrowly, restore in cleanup |
| setupAuthenticatedState helper breaks | Medium | High | Make helper idempotent, add tests for helper itself |
| Infinite re-renders not fixed | Low | High | Profile component renders, use React DevTools, add render count assertions |
| CI still flaky after fixes | Medium | Critical | Run CI 20+ times, add retry logic, investigate CI-specific issues |

---

## Definition of Done

### Code Quality
- [ ] All 58 failing tests now pass
- [ ] Zero console warnings (act, timer, React, etc.)
- [ ] Zero flaky tests (100% pass rate over 10 runs)
- [ ] No setTimeout patterns for async coordination
- [ ] No infinite promises in test code
- [ ] Proper act() wrapper coverage

### Testing
- [ ] Local test run: `pnpm test admin-cache.test.tsx` → 19/19 pass
- [ ] Local test run: `pnpm test chat.test.tsx` → 59/59 pass
- [ ] 10 consecutive local runs: 0 failures
- [ ] CI pipeline: 10+ consecutive builds with 0 failures
- [ ] Coverage report: ≥90% maintained (no regression)

### Documentation
- [ ] This BDD spec document created
- [ ] Code comments added to complex test patterns
- [ ] CLAUDE.md updated if testing patterns change
- [ ] Implementation summary document created post-fix

### CI/CD
- [ ] CI pipeline green on main branch
- [ ] No test-related warnings in CI logs
- [ ] Test execution time under 60 seconds total
- [ ] Coverage report uploads successfully

### Observability
- [ ] Test failure rate metric: 0%
- [ ] Test flakiness metric: 0%
- [ ] Test execution time metric: <60s
- [ ] CI build success rate: 100%

---

## Success Validation

### Immediate Validation (Post-Implementation)
- [ ] `pnpm test` runs with 0 failures
- [ ] No timeout errors in test output
- [ ] No "act() warning" in console
- [ ] Test execution time ≤60 seconds

### Short-Term Validation (Week 1)
- [ ] 20+ CI builds with 0 test failures
- [ ] No developer reports of flaky tests
- [ ] No rollback or hotfix needed
- [ ] Test coverage report stable at ≥90%

### Long-Term Validation (Month 1)
- [ ] 100+ CI builds with 0 test failures
- [ ] Zero test-related incidents
- [ ] Developer confidence in test suite restored
- [ ] New tests follow established reliable patterns

---

## Technical Debt Notes

### Future Improvements (Post-#463)
1. **loadCacheDashboard() pattern**: Consider refactoring to avoid module cache manipulation entirely. Explore React Testing Library best practices for environment variable testing.

2. **setupAuthenticatedState() helper**: Extract to shared test utility file, add comprehensive JSDoc, consider making it a custom render function.

3. **useChatStreaming mock**: Complex mock with callback capture. Consider creating a test helper that wraps hook mock setup.

4. **Dependency array stability**: Review ChatPage component for potential useMemo/useCallback optimization opportunities to reduce re-renders in production.

5. **Test data fixtures**: Consolidate mockAuthResponse, mockGames, mockAgents, mockChats into shared test fixtures file for reusability.

6. **Timer testing**: Evaluate modern alternatives to jest.useFakeTimers() that integrate better with async operations.

---

## Related Documentation

- **Testing Guide**: `docs/code-coverage.md` - Coverage measurement standards
- **CI Pipeline**: `.github/workflows/ci.yml` - Frontend test execution
- **Component Under Test**:
  - `apps/web/src/pages/admin/cache.tsx` - Cache dashboard component
  - `apps/web/src/pages/chat.tsx` - Chat page component
- **API Client**: `apps/web/src/lib/api.ts` - API base URL configuration
- **Streaming Hook**: `apps/web/src/lib/hooks/useChatStreaming.ts` - Chat streaming logic

---

**Document Version**: 1.0
**Created**: 2025-10-18
**Author**: Claude Code (MeepleAI Development Team)
**Status**: Ready for Implementation
