# Testing Best Practices

**Last Updated:** 2025-10-18 (Issue #463)
**Maintainers:** MeepleAI Development Team

---

## Table of Contents

1. [Browser API Polyfills](#browser-api-polyfills)
2. [Fake Timers](#fake-timers)
3. [Localized Content](#localized-content)
4. [Accessible Dialog Testing](#accessible-dialog-testing)
5. [Common Patterns](#common-patterns)
6. [Troubleshooting](#troubleshooting)

---

## Browser API Polyfills

### Overview

jsdom (our test environment) doesn't include all browser APIs. We provide polyfills in `test-utils/browser-polyfills.ts` to prevent "X is not a function" errors.

### Available Polyfills

| API | Purpose | Used By |
|-----|---------|---------|
| `window.matchMedia` | Responsive behavior, media queries | `useReducedMotion` hook, `SkeletonLoader` |
| `Element.prototype.scrollIntoView` | Auto-scroll features | `ChatPage` message scrolling |
| `ResizeObserver` | Element size change detection | Chart components (Recharts) |
| `IntersectionObserver` | Viewport intersection detection | `framer-motion` InView |
| `ReadableStream` | SSE streaming | Chat streaming responses |

### Setup

Polyfills are automatically configured in `jest.setup.js`. No action needed in individual test files.

```javascript
// jest.setup.js (already configured)
import { setupBrowserPolyfills } from './src/test-utils/browser-polyfills';
setupBrowserPolyfills();
```

### Testing Responsive Behavior

By default, `window.matchMedia().matches` returns `false`. To test media query matches:

```typescript
it('shows mobile layout when screen is narrow', () => {
  // Override matchMedia for this test
  window.matchMedia = jest.fn().mockImplementation(query => ({
    matches: query === '(max-width: 768px)', // Match narrow screens
    media: query,
    addEventListener: jest.fn(),
    removeEventListener: jest.fn(),
    dispatchEvent: jest.fn(),
  }));

  render(<ResponsiveComponent />);
  expect(screen.getByText('Mobile Menu')).toBeInTheDocument();
});
```

### Verifying Scroll Behavior

```typescript
it('scrolls to latest message on new message', async () => {
  const scrollSpy = jest.spyOn(Element.prototype, 'scrollIntoView');

  render(<ChatPage />);
  // ... trigger new message

  expect(scrollSpy).toHaveBeenCalled();
  scrollSpy.mockRestore(); // Clean up spy
});
```

---

## Fake Timers

### ⚠️ CRITICAL: Timer Anti-Pattern

**NEVER** call timer functions inside `waitFor()` callbacks!

```typescript
// ❌ WRONG - Causes timeouts
await waitFor(() => {
  jest.advanceTimersByTime(5000); // ❌ Don't do this!
  expect(element).not.toBeInTheDocument();
});

// ✅ CORRECT - Advance BEFORE waitFor
jest.advanceTimersByTime(5000);
await waitFor(() => {
  expect(element).not.toBeInTheDocument();
});
```

**Why?** `waitFor()` polls the callback repeatedly until it passes or times out. If you advance timers inside the callback, timers don't actually advance between polls, causing the test to timeout.

### Helper Functions

Use utilities from `test-utils/timer-test-helpers.ts`:

```typescript
import {
  advanceTimersAndWaitFor,
  setupFakeTimers,
  advanceTimers
} from '@/test-utils/timer-test-helpers';

describe('Auto-dismiss toast', () => {
  setupFakeTimers(); // Handles setup/cleanup automatically

  it('dismisses after 5 seconds', async () => {
    render(<Toast message="Success!" />);
    expect(screen.getByText('Success!')).toBeInTheDocument();

    // Helper ensures correct order: advance → wait
    await advanceTimersAndWaitFor(5000, () => {
      expect(screen.queryByText('Success!')).not.toBeInTheDocument();
    });
  });
});
```

### Manual Timer Setup

If you need finer control:

```typescript
describe('Manual timer setup', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });

  afterEach(() => {
    jest.runOnlyPendingTimers(); // Prevent timer leaks
    jest.useRealTimers(); // Restore for other tests
  });

  it('tests time-dependent behavior', async () => {
    render(<Component />);
    jest.advanceTimersByTime(1000);
    await waitFor(() => expect(...).toBeInTheDocument());
  });
});
```

---

## Localized Content

### Problem

Components using `toLocaleString()` may render numbers differently based on system locale:

- **en-US:** `1,000` (comma separator)
- **de-DE:** `1.000` (period separator)
- **Other:** `1000` (no separator)

Tests that expect exact strings fail in different environments (local vs CI).

### Solution 1: Regex Patterns

```typescript
// ❌ Fails in some environments
expect(screen.getByText('1,000')).toBeInTheDocument();

// ✅ Works in all environments
expect(screen.getByText(/1,?000/)).toBeInTheDocument(); // Matches "1,000" or "1000"
```

### Solution 2: Custom Queries

Use `getByLocalizedNumber` from `test-utils/locale-queries.ts`:

```typescript
import { getByLocalizedNumber } from '@/test-utils/locale-queries';

// Component: <div>Total: {total.toLocaleString()}</div>
expect(getByLocalizedNumber(document.body, 1000)).toBeInTheDocument();
```

### Flexible Pattern Matching

For complex locale-aware text:

```typescript
import { createLocaleFlexiblePattern } from '@/test-utils/locale-queries';

const pattern = createLocaleFlexiblePattern('Total:', { number: 1000 });
expect(screen.getByText(pattern)).toBeInTheDocument();
// Matches: "Total: 1,000", "Total: 1.000", "Total: 1000"
```

---

## Accessible Dialog Testing

### Problem

Querying text that appears in multiple places (e.g., dialog title that matches a button label) causes "Found multiple elements" errors:

```typescript
// ❌ Fails: "Found multiple elements with text 'Confirm'"
expect(screen.getByText('Confirm')).toBeInTheDocument();
```

### Solution: Scope to Dialog

```typescript
import { within } from '@testing-library/react';

// ✅ Query within specific dialog
const dialog = screen.getByRole('dialog');
expect(within(dialog).getByText('Confirm')).toBeInTheDocument();
```

### Helper Functions

Use `test-utils/locale-queries.ts` helpers:

```typescript
import {
  getByTextInDialog,
  getByRoleInDialog
} from '@/test-utils/locale-queries';

// Find text within dialog
expect(getByTextInDialog('Invalidate Game Cache')).toBeInTheDocument();

// Find button within dialog by accessible name
const confirmButton = getByRoleInDialog('button', { name: 'Confirm' });
await user.click(confirmButton);
```

### Why This is Better

1. **Accessibility:** Uses semantic role-based queries (`getByRole('dialog')`)
2. **Specificity:** Scopes queries to specific UI regions
3. **Robustness:** Avoids ambiguity when elements have duplicate text
4. **Maintainability:** Reflects actual user interaction (users see dialogs as distinct regions)

---

## Common Patterns

### Async Rendering

**Prefer `findBy` over `waitFor + getBy`:**

```typescript
// ❌ Verbose
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
});

// ✅ Concise (findBy automatically uses waitFor)
expect(await screen.findByText('Loaded')).toBeInTheDocument();
```

### User Interactions

Always await user interactions:

```typescript
import userEvent from '@testing-library/user-event';

const user = userEvent.setup();

// ✅ Await all interactions
await user.click(button);
await user.type(input, 'Hello');
await user.selectOptions(select, 'option-1');
```

### Cleanup

React Testing Library auto-cleans up after each test. No manual cleanup needed unless:

- Using fake timers (restore real timers in `afterEach`)
- Spying on prototypes (restore spies: `spy.mockRestore()`)
- Modifying global state (reset in `afterEach`)

---

## Troubleshooting

### "window.matchMedia is not a function"

**Cause:** Component uses media queries but polyfill not loaded.

**Fix:** Already handled by `jest.setup.js`. If still failing, verify `setupBrowserPolyfills()` is called.

### "scrollIntoView is not a function"

**Cause:** Component tries to scroll but jsdom doesn't implement `scrollIntoView`.

**Fix:** Already handled by `test-utils/browser-polyfills.ts`.

### Test timeouts with fake timers

**Cause:** Timer advancement inside `waitFor()` callback (anti-pattern).

**Fix:**
```typescript
// ❌ Causes timeout
await waitFor(() => { jest.advanceTimersByTime(1000); ... });

// ✅ Fix
jest.advanceTimersByTime(1000);
await waitFor(() => { ... });
```

### "Found multiple elements"

**Cause:** Text appears in multiple places (e.g., dialog title + button label).

**Fix:** Scope query to specific region:
```typescript
const dialog = screen.getByRole('dialog');
within(dialog).getByText('Confirm');
```

### Locale-specific number format failures

**Cause:** Test expects "1,000" but `toLocaleString()` renders "1000" in different locale.

**Fix:** Use regex or `getByLocalizedNumber`:
```typescript
expect(screen.getByText(/1,?000/)).toBeInTheDocument();
// or
expect(getByLocalizedNumber(document.body, 1000)).toBeInTheDocument();
```

### act() warnings

**Cause:** State updates not wrapped in React's act() (usually async operations).

**Fix:** Use `waitFor()` or `findBy` queries:
```typescript
// Wrap assertion in waitFor to handle async state updates
await waitFor(() => {
  expect(screen.getByText('Updated')).toBeInTheDocument();
});
```

---

## TODO: Future Improvements

- [ ] **ESLint Rule:** Detect timer anti-patterns (advanceTimers inside waitFor)
- [ ] **Comprehensive Audit:** Review all test files for missing polyfills
- [ ] **Custom Render:** Create wrapper with common providers (theme, auth, etc.)
- [ ] **Shared Mock Factories:** Centralize API response mocks
- [ ] **Visual Regression:** Consider adding screenshot testing (Playwright)

---

## References

- **React Testing Library Docs:** https://testing-library.com/docs/react-testing-library/intro
- **Jest Timer Mocks:** https://jestjs.io/docs/timer-mocks
- **Issue #463:** Pre-existing test failures fix (browser polyfills, timers, locale)
- **BDD Spec:** `docs/issue/issue-463-test-fixes-bdd.md`

---

**Questions or Improvements?**
Contact the development team or create an issue on GitHub.
