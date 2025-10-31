# React Testing Patterns - Fixing act() Warnings

## The Problem

When components have immediate async effects (useEffect that runs on mount and updates state), you may see warnings like:

```
Warning: An update to Component inside a test was not wrapped in act(...).
```

## Root Cause

React state updates happen asynchronously. When a component mounts and immediately:
1. Calls an async API in useEffect
2. Updates state when the promise resolves
3. Uses setInterval/setTimeout for polling

...the state updates happen OUTSIDE the test's normal synchronous flow.

## The Solution

### Pattern 1: Use waitFor for ALL Assertions

**❌ Bad**:
```typescript
render(<Component />);
expect(screen.getByText('Loaded')).toBeInTheDocument(); // May fail or cause act() warning
```

**✅ Good**:
```typescript
render(<Component />);
await waitFor(() => {
  expect(screen.getByText('Loaded')).toBeInTheDocument();
});
```

### Pattern 2: Use findBy Queries for Async Elements

**❌ Bad**:
```typescript
render(<Component />);
const element = screen.getByText('Async Content'); // Synchronous query
```

**✅ Good**:
```typescript
render(<Component />);
const element = await screen.findByText('Async Content'); // Waits for element
```

### Pattern 3: Flush Promises When Advancing Fake Timers

**❌ Bad**:
```typescript
act(() => {
  jest.advanceTimersByTime(2000);
});
```

**✅ Good**:
```typescript
await act(async () => {
  jest.advanceTimersByTime(2000);
  await Promise.resolve(); // Flush microtask queue
});
```

### Pattern 4: Don't Wrap render() in act()

React Testing Library's `render()` already handles act() internally.

**❌ Bad**:
```typescript
await act(async () => {
  render(<Component />);
});
```

**✅ Good**:
```typescript
render(<Component />);
// Then use waitFor for assertions
```

### Pattern 5: Use userEvent instead of fireEvent

**❌ Bad**:
```typescript
fireEvent.click(button); // Doesn't handle act() automatically
```

**✅ Good**:
```typescript
const user = userEvent.setup();
await user.click(button); // Handles act() internally
```

## Complete Example

```typescript
describe('AsyncComponent', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    jest.useFakeTimers();
  });

  afterEach(async () => {
    // Flush all pending operations
    await act(async () => {
      jest.runOnlyPendingTimers();
      await Promise.resolve();
    });
    jest.useRealTimers();
  });

  it('should handle async loading', async () => {
    mockAPI.mockResolvedValue({ data: 'test' });

    // Render without wrapping in act()
    render(<AsyncComponent />);

    // Wait for async state updates
    await waitFor(() => {
      expect(mockAPI).toHaveBeenCalledTimes(1);
    });

    // Assert on async content
    await waitFor(() => {
      expect(screen.getByText('test')).toBeInTheDocument();
    });
  });

  it('should handle polling with timers', async () => {
    mockAPI.mockResolvedValue({ data: 'test' });

    render(<PollingComponent />);

    // Wait for initial call
    await waitFor(() => {
      expect(mockAPI).toHaveBeenCalledTimes(1);
    });

    // Advance timer and flush promises
    await act(async () => {
      jest.advanceTimersByTime(2000);
      await Promise.resolve();
    });

    // Check polling happened
    await waitFor(() => {
      expect(mockAPI).toHaveBeenCalledTimes(2);
    });
  });

  it('should handle user interactions', async () => {
    const user = userEvent.setup({ delay: null }); // For fake timers

    render(<InteractiveComponent />);

    // Wait for initial render
    await waitFor(() => {
      expect(screen.getByRole('button')).toBeInTheDocument();
    });

    // User interaction
    await user.click(screen.getByRole('button'));

    // Assert on result
    await waitFor(() => {
      expect(screen.getByText('Clicked')).toBeInTheDocument();
    });
  });
});
```

## Key Takeaways

1. **React Testing Library handles act() for you** - don't manually wrap render()
2. **Use waitFor for async assertions** - don't make synchronous assertions on async state
3. **Use findBy queries** - they wait automatically
4. **Flush promises after timer advances** - `await Promise.resolve()` in act()
5. **Use userEvent over fireEvent** - better act() handling
6. **Clean up async operations in afterEach** - prevent leaks

## When act() Warnings Persist

If you still see warnings after applying these patterns:
1. Check if assertions are outside waitFor blocks
2. Verify all async operations complete in afterEach
3. Look for state updates in cleanup functions
4. Consider if the component needs architectural changes (separate polling logic, AbortController)

## Testing Browser-Specific Behavior

Some behaviors cannot be tested in jsdom and require real browser E2E tests.

### Redirects via window.location

**Problem**: jsdom doesn't properly simulate `window.location.href = '/path'` assignments.

**Symptoms**:
- Tests that rely on `window.location.href` changes fail or behave unexpectedly
- Mocking `window.location` is complex and unreliable in jsdom

**Solution**: Use Playwright E2E tests for redirect behavior.

**When to use E2E vs Unit**:
- **Unit tests**: Hook logic, state management, API call triggering, conditional logic
- **E2E tests**: Actual redirects, browser navigation, URL changes, full user flows

**Example**: Session expiration redirects

**Unit Test** (tests hook logic only):
```typescript
it('should detect expired session (0 minutes) and trigger redirect logic', async () => {
  mockGetSessionStatus.mockResolvedValueOnce({
    expiresAt: new Date().toISOString(),
    lastSeenAt: new Date().toISOString(),
    remainingMinutes: 0,
  });

  const { result } = renderHook(() => useSessionCheck());

  // Verify hook state reflects expiration
  await waitFor(() => {
    expect(result.current.remainingMinutes).toBe(0);
    expect(result.current.isNearExpiry).toBe(true);
  });

  // Note: Cannot verify window.location.href redirect in jsdom
  // See e2e/session-expiration.spec.ts for actual redirect behavior
});
```

**E2E Test** (tests actual browser redirect):
```typescript
test('should redirect to login when session expires (0 minutes)', async ({ page }) => {
  // Mock session status API to return 0 minutes remaining
  await page.route(`${apiBase}/api/v1/auth/session/status`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        expiresAt: new Date().toISOString(),
        lastSeenAt: new Date().toISOString(),
        remainingMinutes: 0
      })
    });
  });

  // Navigate to protected page
  await page.goto('/chat');

  // Verify actual browser redirect occurred
  await page.waitForURL('/login?reason=session_expired', { timeout: 10000 });
  await expect(page).toHaveURL('/login?reason=session_expired');
});
```

**Coverage Strategy**:
- Unit tests: 100% of hook logic (state, API calls, conditions)
- E2E tests: Critical browser behaviors (redirects, navigation, URL changes)
- Combined coverage: Complete confidence in feature behavior

**Other Browser Behaviors Requiring E2E**:
- `window.open()` / `window.close()`
- Browser history manipulation (`history.pushState`, `history.back`)
- Cross-origin redirects
- Authentication flows with OAuth providers
- File downloads via blob URLs
- Clipboard API interactions
- Local/session storage across navigation

## References

- [React Testing Library - Async Methods](https://testing-library.com/docs/dom-testing-library/api-async/)
- [React Testing Library - User Event](https://testing-library.com/docs/user-event/intro)
- [React - act() API](https://react.dev/reference/react/act)
- [Playwright Documentation](https://playwright.dev/docs/intro)
- [jsdom Limitations](https://github.com/jsdom/jsdom#unimplemented-parts-of-the-web-platform)
