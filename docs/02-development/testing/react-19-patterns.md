# React 19 Testing Patterns - Quick Reference

**Context**: React 19 introduced stricter `act()` requirements for tests involving timer-based state updates.

---

## Problem

Tests that use `jest.advanceTimersByTime()` to simulate timers (intervals, timeouts) now fail with:

```
An update to Component inside a test was not wrapped in act(...).
```

---

## Solution Pattern

### ❌ Old Pattern (React 18)
```typescript
it('auto-refreshes data every 30 seconds', async () => {
  jest.useFakeTimers();

  render(<MyComponent />);

  // This triggers state updates outside act() ❌
  jest.advanceTimersByTime(30000);

  expect(myMock).toHaveBeenCalledTimes(2);

  jest.useRealTimers();
});
```

### ✅ New Pattern (React 19)
```typescript
it('auto-refreshes data every 30 seconds', async () => {
  jest.useFakeTimers();

  render(<MyComponent />);

  // Wrap timer advance in act() ✅
  await act(async () => {
    jest.advanceTimersByTime(30000);
  });

  // Use waitFor for async state updates ✅
  await waitFor(() => {
    expect(myMock).toHaveBeenCalledTimes(2);
  });

  jest.useRealTimers();
});
```

---

## Common Scenarios

### 1. Auto-Refresh Intervals
```typescript
// Component uses setInterval for auto-refresh
await act(async () => {
  jest.advanceTimersByTime(REFRESH_INTERVAL);
});
await waitFor(() => {
  expect(fetchMock).toHaveBeenCalled();
});
```

### 2. Toast/Notification Auto-Dismiss
```typescript
// Component auto-dismisses toast after 5 seconds
await act(async () => {
  jest.advanceTimersByTime(5000);
});
await waitFor(() => {
  expect(screen.queryByText(/toast message/)).not.toBeInTheDocument();
});
```

### 3. Debounced Input
```typescript
// Component debounces user input
fireEvent.change(input, { target: { value: 'search term' } });

await act(async () => {
  jest.advanceTimersByTime(DEBOUNCE_DELAY);
});

await waitFor(() => {
  expect(searchMock).toHaveBeenCalledWith('search term');
});
```

### 4. Polling/Status Checks
```typescript
// Component polls API every X seconds
await act(async () => {
  jest.advanceTimersByTime(POLL_INTERVAL * 3); // 3 polls
});

await waitFor(() => {
  expect(pollMock).toHaveBeenCalledTimes(3);
});
```

---

## Required Imports

```typescript
import { render, screen, waitFor, act } from '@testing-library/react';
//                                      ^^^ Add act import
```

---

## Template

```typescript
import { render, screen, waitFor, fireEvent, act } from '@testing-library/react';

describe('Component with timers', () => {
  it('does something after time passes', async () => {
    jest.useFakeTimers();

    // Setup
    const mockFn = jest.fn();
    render(<ComponentWithTimer onInterval={mockFn} />);

    // Initial state
    await waitFor(() => {
      expect(screen.getByText(/initial state/)).toBeInTheDocument();
    });

    // Advance time with act()
    await act(async () => {
      jest.advanceTimersByTime(TIME_IN_MS);
    });

    // Assert new state
    await waitFor(() => {
      expect(mockFn).toHaveBeenCalled();
      expect(screen.getByText(/new state/)).toBeInTheDocument();
    });

    // Cleanup
    jest.useRealTimers();
  });
});
```

---

## Debugging Tips

### 1. Check for Timer Usage
Look for these in components:
- `setTimeout`
- `setInterval`
- Auto-refresh logic
- Toast/notification timeouts
- Debouncing/throttling

### 2. Console Error Stack
The error shows where the unwrapped state update happens:
```
at scheduleUpdateOnFiber (...react-dom...)
at dispatchSetState (...react-dom...)
at setToasts (src/pages/analytics.tsx:57:7)
                                     ^^^
at callTimer (fake-timers:806:24)
```

### 3. Common Mistakes

**❌ Synchronous assertions after timer advance**
```typescript
await act(async () => {
  jest.advanceTimersByTime(5000);
});
expect(mockFn).toHaveBeenCalled(); // ❌ Might be too early
```

**✅ Use waitFor for async state**
```typescript
await act(async () => {
  jest.advanceTimersByTime(5000);
});
await waitFor(() => {
  expect(mockFn).toHaveBeenCalled(); // ✅ Waits for state update
});
```

---

## Migration Checklist

When migrating tests to React 19:

- [ ] Add `act` to imports from `@testing-library/react`
- [ ] Find all `jest.advanceTimersByTime()` calls
- [ ] Wrap each in `await act(async () => { ... })`
- [ ] Add `await waitFor(() => { ... })` for assertions
- [ ] Test that warnings are gone
- [ ] Verify test still validates correct behavior

---

## References

- React 19 Testing docs: https://react.dev/learn/testing
- Testing Library act guide: https://testing-library.com/docs/react-testing-library/api#act
- React act() warning: https://react.dev/link/wrap-tests-with-act

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Applied To**: Shadcn/UI migration test fixes

