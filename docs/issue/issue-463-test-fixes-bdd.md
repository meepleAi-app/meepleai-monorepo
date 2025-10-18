# BDD Specification: Fix Pre-Existing Test Failures (Issue #463)

**Issue:** #463
**Type:** Bug Fix - Testing
**Priority:** High
**Status:** In Progress

---

## Executive Summary

Fix 49 pre-existing test failures in `admin-cache.test.tsx` and `chat.test.tsx` while establishing reusable testing patterns to prevent future failures. Implements hybrid pragmatic approach: immediate fixes + utility extraction + documentation.

---

## Problem Statement

### Current State
- **admin-cache.test.tsx:** 3 failures (number localization, dialog duplication, fake timer misuse)
- **chat.test.tsx:** 46 failures (missing `window.matchMedia` polyfill)
- CI pipeline blocked for all PRs
- Difficult to identify real regressions vs pre-existing failures
- No documented testing best practices

### Root Causes
1. **Missing Browser API Polyfills:** `window.matchMedia` not mocked in jest.setup.js
2. **Fake Timer Anti-patterns:** `jest.advanceTimersByTime()` called inside `waitFor()` callbacks
3. **Locale Formatting:** Tests expect specific number formats (e.g., "1,000") but `toLocaleString()` varies by environment
4. **Query Ambiguity:** Multiple elements with same text causing "Found multiple elements" errors

### Impact
- ❌ All PRs fail CI due to pre-existing failures
- ❌ Developer productivity degraded
- ❌ No regression detection capability

---

## BDD Features & Scenarios

### Feature 1: Browser API Polyfills for Jest Environment

```gherkin
Feature: Browser API Polyfills for Jest Environment
  As a developer writing tests
  I need complete browser API polyfills in the test environment
  So that components using modern browser APIs don't fail in jsdom

  Background:
    Given I am using jsdom as the test environment
    And modern React components use browser APIs unavailable in jsdom

  Scenario: Component uses matchMedia for responsive behavior
    Given a component imports and uses the useReducedMotion hook
    And useReducedMotion calls window.matchMedia()
    When I render the component in a test
    Then window.matchMedia should be available
    And the test should not throw "matchMedia is not a function"
    And I should be able to test the component behavior

  Scenario: Polyfill provides realistic browser API behavior
    Given window.matchMedia is polyfilled in jest.setup.js
    When a component calls window.matchMedia('(prefers-reduced-motion: reduce)')
    Then it should return an object with matches, media, and event listener methods
    And the default matches value should be false
    And components should render without errors

  Scenario: ChatPage renders with SkeletonLoader using matchMedia
    Given ChatPage uses SkeletonLoader component
    And SkeletonLoader uses useReducedMotion hook
    And useReducedMotion calls window.matchMedia()
    When I render ChatPage in any test
    Then the component should mount successfully
    And no TypeError should be thrown
    And all 46 chat.test.tsx tests should reach their assertions

  Acceptance Criteria:
    ✅ jest.setup.js includes window.matchMedia polyfill
    ✅ Polyfill mocks all required properties and methods
    ✅ All 46 chat.test.tsx tests pass
    ✅ No components throw matchMedia errors during render
```

### Feature 2: Fake Timer Usage in Async Tests

```gherkin
Feature: Fake Timer Usage in Async Tests
  As a developer testing async time-dependent behavior
  I need to safely manipulate fake timers
  So that tests are reliable and follow React Testing Library best practices

  Background:
    Given I am testing a component with auto-dismiss toast (5 second timeout)
    And I want to verify the toast disappears after the timeout

  Scenario: Advancing timers before async assertions (CORRECT)
    Given I have called jest.useFakeTimers() before rendering
    And a toast notification is displayed
    When I advance timers by 5000ms using jest.advanceTimersByTime(5000)
    And then I use waitFor() to assert the toast is removed
    Then the test should pass reliably
    And React should not throw act() warnings

  Scenario: Advancing timers inside waitFor callback (INCORRECT - Anti-pattern)
    Given I have called jest.useFakeTimers() before rendering
    And a toast notification is displayed
    When I call waitFor(() => { jest.advanceTimersByTime(5000); expect(...) })
    Then the test should timeout or fail
    Because waitFor() polls the callback but timers don't advance between polls
    And this is a documented anti-pattern in RTL

  Scenario: Cleaning up timers after each test
    Given I have used jest.useFakeTimers() in a test
    When the test completes (afterEach hook runs)
    Then jest.runOnlyPendingTimers() should be called
    And jest.useRealTimers() should be called
    So that fake timers don't leak into subsequent tests

  Scenario: Fix admin-cache timer test
    Given the test "automatically dismisses toast notifications after 5 seconds"
    And it currently calls jest.advanceTimersByTime() inside waitFor()
    When I refactor to advance timers before waitFor()
    Then the test should pass without timeouts
    And the toast should be properly validated as removed

  Acceptance Criteria:
    ✅ jest.advanceTimersByTime() called BEFORE waitFor(), never inside
    ✅ afterEach includes jest.runOnlyPendingTimers() + jest.useRealTimers()
    ✅ Timer test in admin-cache.test.tsx passes
    ✅ No act() warnings in test output
```

### Feature 3: Testing Localized Content

```gherkin
Feature: Testing Localized Content
  As a developer testing internationalized components
  I need to query localized content flexibly
  So that tests work across different locale settings (local vs CI)

  Background:
    Given a component displays cache size using toLocaleString()
    And the format varies by system locale
    And CI may use a different locale than developer machines

  Scenario: Query localized numbers with flexible patterns
    Given a component displays the number 1000 using toLocaleString()
    And it may render as "1,000" (en-US) or "1000" (other locales)
    When I query for this number in a test
    Then I should use a regex pattern that matches both formats
    Or use a custom query that handles locale variants
    And the test should pass in all environments

  Scenario: Fix admin-cache localized number assertions
    Given the test expects to find "1,000" with getByText('1,000')
    And the component may render "1000" without comma in some locales
    When I change the query to use /1,?000/ regex pattern
    Or use getByText((content) => content.includes('1000'))
    Then the test should pass in both en-US and other locales
    And CI should no longer fail due to locale mismatches

  Acceptance Criteria:
    ✅ Number queries use regex patterns for format flexibility
    ✅ Tests pass with both "1,000" and "1000" formats
    ✅ No hard-coded locale assumptions in assertions
    ✅ admin-cache localization test passes in CI and locally
```

### Feature 4: Accessible Dialog Testing

```gherkin
Feature: Accessible Dialog Testing
  As a developer testing dialog components
  I need to query dialog content correctly
  So that tests are accessible and avoid false "multiple elements" errors

  Background:
    Given a page may render multiple elements with the same text
    And I need to query content within a specific dialog

  Scenario: Query content scoped to specific dialog
    Given multiple dialogs or elements may have text "Invalidate Game Cache"
    When I need to find the dialog title
    Then I should first query the dialog by role using getByRole('dialog')
    And then use within(dialog) to scope subsequent queries
    So that I avoid ambiguous global text queries
    And tests are more robust and accessible

  Scenario: Fix admin-cache dialog title duplication
    Given the test expects to find "Invalidate Game Cache"
    And screen.getByText() throws "Found multiple elements"
    When I refactor to use:
      const dialog = screen.getByRole('dialog')
      within(dialog).getByText('Invalidate Game Cache')
    Then the test should find the title within the correct dialog
    And no "multiple elements" error should occur

  Acceptance Criteria:
    ✅ Dialog queries use getByRole('dialog') first
    ✅ Content queries use within(dialog) for scoping
    ✅ No "Found multiple elements" errors
    ✅ admin-cache dialog test passes
```

---

## Implementation Plan

### Phase 1: Critical Path (Get CI Green) - 3 hours

#### Hour 1: Fix chat.test.tsx (46 tests)
**Changes:**
- Add `window.matchMedia` polyfill to `jest.setup.js`
- Polyfill includes all required properties: matches, media, addEventListener, etc.
- Default behavior: matches = false (no reduced motion)

**Expected Outcome:**
- All 46 chat.test.tsx tests pass
- No matchMedia TypeErrors

#### Hour 2: Fix admin-cache locale & dialog (2 tests)
**Changes:**
- Number localization: Use regex `/1,?000/` instead of exact string "1,000"
- Dialog queries: Use `getByRole('dialog')` then `within(dialog).getByText()`

**Expected Outcome:**
- Locale-dependent number tests pass in all environments
- Dialog title test passes without "multiple elements" error

#### Hour 3: Fix admin-cache timer test (1 test)
**Changes:**
- Move `jest.advanceTimersByTime(5000)` BEFORE `waitFor()` block
- Ensure `jest.useFakeTimers()` called before render
- Ensure `jest.useRealTimers()` in afterEach (already added)

**Expected Outcome:**
- Timer test passes without timeout
- No act() warnings

### Phase 2: Extract Utilities - 1-2 hours

#### Hour 4: Create Test Utilities

**File 1: `test-utils/browser-polyfills.ts`**
```typescript
/**
 * Comprehensive browser API polyfills for jsdom test environment.
 * Import and call setupBrowserPolyfills() in jest.setup.js
 */
export function setupBrowserPolyfills() {
  setupMatchMedia();
  // Future: setupResizeObserver(), setupIntersectionObserver()
}

function setupMatchMedia() {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: jest.fn().mockImplementation(query => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: jest.fn(),
      removeListener: jest.fn(),
      addEventListener: jest.fn(),
      removeEventListener: jest.fn(),
      dispatchEvent: jest.fn(),
    })),
  });
}
```

**File 2: `test-utils/timer-test-helpers.ts`**
```typescript
import { waitFor } from '@testing-library/react';

/**
 * Safe fake timer utilities for Jest tests.
 *
 * ⚠️ WARNING: Never call timer functions inside waitFor() callbacks.
 * ❌ BAD:  await waitFor(() => { jest.advanceTimersByTime(1000); ... })
 * ✅ GOOD: await advanceTimersAndWaitFor(1000, () => expect(...))
 */

/**
 * Advances fake timers by ms, then waits for assertion to pass.
 * Use this instead of manually calling jest.advanceTimersByTime() inside waitFor().
 */
export async function advanceTimersAndWaitFor(
  ms: number,
  assertion: () => void | Promise<void>
) {
  jest.advanceTimersByTime(ms);
  return waitFor(assertion);
}

/**
 * Setup fake timers in beforeEach. Call this at the start of describe blocks
 * that need timer manipulation.
 */
export function setupFakeTimers() {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
    jest.useRealTimers();
  });
}
```

**File 3: `test-utils/locale-queries.ts`**
```typescript
import { screen, within as rtlWithin } from '@testing-library/react';

/**
 * Custom queries for localized content.
 * Handles variations in number formatting across different locales.
 */

/**
 * Gets element by localized number, supporting both comma-separated and plain formats.
 * Example: 1000 may render as "1,000" (en-US) or "1000" (other locales)
 */
export function getByLocalizedNumber(
  container: HTMLElement,
  value: number
): HTMLElement {
  // Create regex that matches both "1,000" and "1000"
  const withComma = value.toLocaleString('en-US');
  const withoutComma = value.toString();
  const pattern = new RegExp(`${withComma}|${withoutComma}`.replace(/,/g, ',?'));

  return rtlWithin(container).getByText(pattern);
}

/**
 * Gets element by text within a specific dialog.
 * Prevents "Found multiple elements" errors when querying dialog content.
 */
export function getByTextInDialog(text: string | RegExp): HTMLElement {
  const dialog = screen.getByRole('dialog');
  return rtlWithin(dialog).getByText(text);
}
```

### Phase 3: Documentation - 1 hour

#### Hour 5: Create Testing Best Practices Doc

**File: `docs/guide/testing-best-practices.md`**
```markdown
# Testing Best Practices

## Browser API Polyfills

Our `jest.setup.js` includes polyfills for browser APIs not available in jsdom:
- `window.matchMedia` - For responsive behavior and media queries
- (Future: ResizeObserver, IntersectionObserver as needed)

**Implementation:** See `test-utils/browser-polyfills.ts`

## Fake Timers

⚠️ **Critical Anti-Pattern to Avoid:**

❌ **NEVER** advance timers inside `waitFor()` callbacks:
```typescript
// DON'T DO THIS
await waitFor(() => {
  jest.advanceTimersByTime(5000); // ❌ WRONG
  expect(element).not.toBeInTheDocument();
});
```

✅ **ALWAYS** advance timers BEFORE `waitFor()`:
```typescript
// DO THIS INSTEAD
jest.advanceTimersByTime(5000);
await waitFor(() => {
  expect(element).not.toBeInTheDocument();
});
```

**Why?** `waitFor()` polls the callback repeatedly, but fake timers don't advance between polls, causing timeouts.

**Utilities:** See `test-utils/timer-test-helpers.ts` for helper functions.

## Localized Content

When testing components that use `toLocaleString()` for number formatting:

✅ **Use regex patterns** for format flexibility:
```typescript
// Matches both "1,000" and "1000"
expect(screen.getByText(/1,?000/)).toBeInTheDocument();
```

✅ **Or use custom queries** from `test-utils/locale-queries.ts`:
```typescript
import { getByLocalizedNumber } from '@/test-utils/locale-queries';
expect(getByLocalizedNumber(container, 1000)).toBeInTheDocument();
```

## Accessible Dialog Testing

When querying content within dialogs:

✅ **Scope queries to specific dialog**:
```typescript
const dialog = screen.getByRole('dialog');
expect(within(dialog).getByText('Dialog Title')).toBeInTheDocument();
```

✅ **Or use helper**:
```typescript
import { getByTextInDialog } from '@/test-utils/locale-queries';
expect(getByTextInDialog('Dialog Title')).toBeInTheDocument();
```

**Why?** Prevents "Found multiple elements" errors when multiple elements have the same text.

## TODO: Future Improvements

- [ ] ESLint rule to catch timer anti-patterns
- [ ] Comprehensive audit of all test files for missing polyfills
- [ ] Custom render function with common providers
- [ ] Shared mock factories for API responses
```

---

## Success Criteria (Definition of Done)

### Functional Requirements
- ✅ All 49 tests passing (3 admin-cache + 46 chat.test)
- ✅ CI pipeline green on feature branch
- ✅ No test warnings (act(), console.error, etc.)
- ✅ Tests run in <30s total

### Code Quality
- ✅ Reusable test utilities extracted
- ✅ No duplicate polyfill code
- ✅ JSDoc comments on all utilities
- ✅ TypeScript types for all utilities

### Documentation
- ✅ Testing best practices documented
- ✅ Anti-patterns clearly explained with examples
- ✅ TODO items for future improvements

### Regression Prevention
- ✅ Polyfills prevent future browser API errors
- ✅ Timer utilities guide correct usage
- ✅ Locale queries handle internationalization
- ✅ Patterns established for future tests

---

## Risk Assessment

### Low Risk
- ✅ Polyfill addition (well-understood, isolated change)
- ✅ Regex pattern changes (backward compatible)
- ✅ Documentation (no code impact)

### Medium Risk
- ⚠️ Timer refactoring (could introduce new timing issues)
  - **Mitigation:** Test locally with fake timers before committing
- ⚠️ Utility extraction (could miss edge cases)
  - **Mitigation:** Keep utilities simple, add tests if time permits

### High Risk
- None identified

---

## Testing Strategy

### Unit Tests (Existing)
- admin-cache.test.tsx: 19 tests (all should pass)
- chat.test.tsx: 46+ tests (all should pass)

### Integration Tests
- Verify polyfills work across multiple test files
- Ensure timer utilities integrate with React Testing Library

### Manual Testing
- Run full test suite: `pnpm test`
- Run specific files: `pnpm test admin-cache.test.tsx`
- Verify CI passes on feature branch

---

## Timeline

| Phase | Duration | Deliverable |
|-------|----------|-------------|
| Phase 1 Hour 1 | 1 hour | chat.test.tsx passing (46 tests) |
| Phase 1 Hour 2 | 1 hour | admin-cache locale/dialog fixed (2 tests) |
| Phase 1 Hour 3 | 1 hour | admin-cache timer fixed (1 test) |
| **Phase 1 Total** | **3 hours** | **All 49 tests passing, CI green** |
| Phase 2 Hour 4 | 1-2 hours | Test utilities extracted |
| Phase 3 Hour 5 | 1 hour | Documentation complete |
| **Total** | **5-6 hours** | **Complete solution** |

---

## Decisions & Trade-offs

### Decision 1: Hybrid Approach (Fix + Utilities + Docs)
- **Rationale:** Balances speed with long-term value
- **Alternative Considered:** Quick fix only (2 hours)
- **Trade-off:** 3 extra hours for lasting infrastructure

### Decision 2: Polyfill in jest.setup.js (Not Per-Test)
- **Rationale:** Global polyfill simpler, matches browser reality
- **Alternative Considered:** Mock in each test file
- **Trade-off:** Less flexibility, but less duplication

### Decision 3: matchMedia Default matches=false
- **Rationale:** Most tests don't need responsive behavior
- **Alternative Considered:** Configurable per test
- **Trade-off:** Add configuration later if needed (YAGNI)

### Decision 4: Extract Utilities Now (Not Later)
- **Rationale:** Issue affects 2 files, pattern will recur
- **Alternative Considered:** Fix in-place, defer utilities
- **Trade-off:** 1-2 extra hours now saves time later

### Decision 5: New Doc File (Not CLAUDE.md Extension)
- **Rationale:** CLAUDE.md already 300+ lines, testing deserves space
- **Alternative Considered:** Add to CLAUDE.md
- **Trade-off:** One more file to maintain

---

## Validation Plan

### Pre-Commit Checklist
- [ ] All 19 admin-cache tests pass locally
- [ ] All 46+ chat tests pass locally
- [ ] Full test suite passes (`pnpm test`)
- [ ] No act() warnings in output
- [ ] Test utilities have JSDoc comments
- [ ] Documentation reviewed for clarity

### CI Validation
- [ ] CI passes on feature branch
- [ ] No flaky test failures (run 3x to confirm)
- [ ] Coverage threshold maintained (90%)

### Code Review Checklist
- [ ] Polyfill implementation matches MDN spec
- [ ] Timer utilities prevent anti-pattern
- [ ] Locale queries handle edge cases
- [ ] Documentation accurate and helpful

---

## References

- **Issue:** #463
- **Related PRs:** #462 (partial fix attempt)
- **RTL Docs:** https://testing-library.com/docs/dom-testing-library/api-async
- **Jest Fake Timers:** https://jestjs.io/docs/timer-mocks
- **Context7 Research:** React Testing Library best practices (fetched in discovery)

---

**Author:** Claude Code
**Created:** 2025-10-18
**Status:** In Progress
**Next Steps:** Commit uncommitted changes → Implement Phase 1 fixes
