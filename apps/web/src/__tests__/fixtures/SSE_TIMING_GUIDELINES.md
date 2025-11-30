# SSE Test Timing Guidelines (Issue #1495)

## Purpose
Guidelines for choosing appropriate `eventDelay` values in SSE tests to balance **test speed** vs **race condition detection**.

---

## Quick Reference

| Test Type | eventDelay | Use When | Example |
|-----------|------------|----------|---------|
| **Fast** | `0ms` | UI rendering only, no timing dependencies | Message component displays tokens |
| **Realistic** | `10ms` (default) | State updates, message accumulation, integration | Chat message submission flow |
| **Slow** | `50-100ms` | Timeout scenarios, cancellation, error handling | AbortSignal cancellation, network timeouts |

---

## Decision Tree

```
Does test verify SSE event ORDERING or STATE ACCUMULATION?
├─ YES → Use eventDelay: 10ms (realistic)
│   Examples:
│   - Token accumulation into message
│   - Multiple state updates in sequence
│   - Citation ordering with messages
│
└─ NO → Does test mock SSE but only check final UI state?
    ├─ YES → Use eventDelay: 0ms (fast)
    │   Examples:
    │   - Component renders with mocked response
    │   - Button states during streaming
    │   - Loading indicators
    │
    └─ Testing error/timeout scenarios?
        └─ YES → Use eventDelay: 50-100ms (slow)
            Examples:
            - AbortSignal cancellation
            - Timeout handling
            - Network error recovery
```

---

## Implementation Examples

### Fast Mode (0ms) - UI Rendering Tests

```typescript
import { createSSEResponse, createTokenEvent } from '../fixtures/sse-test-helpers';

// ✅ GOOD: Just checking if component renders tokens, no timing dependency
it('renders streamed tokens correctly', async () => {
  const events = [
    createTokenEvent('Hello'),
    createTokenEvent(' World'),
    createCompleteEvent(2, 0.95),
  ];

  mockFetch(createSSEResponse(events, { eventDelay: 0 })); // Fast!

  render(<MessageComponent />);
  await waitFor(() => expect(screen.getByText('Hello World')).toBeInTheDocument());
});
```

### Realistic Mode (10ms) - Integration Tests

```typescript
import { createTokenStreamResponse } from '../fixtures/sse-test-helpers';

// ✅ GOOD: Testing state accumulation, needs realistic timing
it('accumulates tokens into complete message', async () => {
  const response = createTokenStreamResponse(
    ['Hello', ' ', 'World'],
    { totalTokens: 3, confidence: 0.95 },
    { eventDelay: 10 } // Realistic timing to catch race conditions
  );

  mockFetch(response);

  render(<ChatInterface />);

  // Verify intermediate states (requires sequential delivery)
  await waitFor(() => expect(screen.getByText('Hello')).toBeInTheDocument());
  await waitFor(() => expect(screen.getByText('Hello World')).toBeInTheDocument());
});
```

### Slow Mode (50-100ms) - Timeout/Error Tests

```typescript
import { createSSEResponse, SSEOptions } from '../fixtures/sse-test-helpers';

// ✅ GOOD: Testing timeout behavior, needs slow delivery
it('handles timeout during streaming', async () => {
  const controller = new AbortController();
  const events = [
    createTokenEvent('Slow'),
    createTokenEvent(' response'),
  ];

  const options: SSEOptions = {
    eventDelay: 100, // Slow delivery to test timeout
    signal: controller.signal,
  };

  mockFetch(createSSEResponse(events, options));

  render(<ChatInterface />);

  // Timeout after 50ms (before 2nd event at 100ms)
  setTimeout(() => controller.abort(), 50);

  await waitFor(() => expect(screen.getByText('Request timed out')).toBeInTheDocument());
});
```

---

## File Categorization

### Fast Mode (0ms) Files
- `MessageInput.rendering.test.tsx` - UI states, button visibility
- `Message.test.tsx` - Component rendering
- `AgentSelector-states.test.tsx` - Dropdown states
- `CitationCard.test.tsx` - Citation display
- `FollowUpQuestions.test.tsx` - Question list rendering

**Rationale**: These tests mock SSE responses but only verify final UI state, not event ordering or accumulation.

### Realistic Mode (10ms) Files
- `MessageInput.submission.test.tsx` - Message submission → SSE response
- `ChatHistory.loading.test.tsx` - Loading states during streaming
- `ChatHistory.messages.test.tsx` - Message list updates
- `MessageEditForm.test.tsx` - Edit → save → stream response
- `chat.handlers.ts` - MSW mock handlers (default for all tests)

**Rationale**: These tests verify state changes over time, token accumulation, or multi-step flows requiring sequential event delivery.

### Slow Mode (50-100ms) Files
- Future: Timeout/cancellation test files
- Future: Error recovery test files
- Future: Network resilience test files

**Rationale**: Testing edge cases that require delayed delivery to trigger timeout/error conditions.

---

## Common Pitfalls

### ❌ WRONG: Using 0ms when testing state accumulation
```typescript
// BUG: Race condition - events arrive instantly, state updates may not settle
const response = createSSEResponse(events, { eventDelay: 0 });
await waitFor(() => expect(message).toBe('Hello World')); // Flaky!
```

### ✅ RIGHT: Using 10ms default for state updates
```typescript
// STABLE: Events arrive sequentially, state updates settle between events
const response = createSSEResponse(events, { eventDelay: 10 });
await waitFor(() => expect(message).toBe('Hello World')); // Reliable!
```

### ❌ WRONG: Using 10ms for simple rendering tests
```typescript
// SLOW: Unnecessary 10ms delay per event when just checking final render
const response = createSSEResponse(events, { eventDelay: 10 });
render(<Component />);
// Test suite runs 30% slower for no stability gain
```

### ✅ RIGHT: Using 0ms for render-only tests
```typescript
// FAST: Instant delivery when order doesn't matter
const response = createSSEResponse(events, { eventDelay: 0 });
render(<Component />);
// Test runs instantly, no race conditions possible
```

---

## Performance Impact

| Mode | Events/Test | Time/Test | 100 Tests Impact |
|------|------------|-----------|------------------|
| Fast (0ms) | 10 events | ~5ms | +500ms |
| Realistic (10ms) | 10 events | ~100ms | +10s |
| Slow (100ms) | 10 events | ~1000ms | +100s |

**Recommendation**: Use Fast mode wherever safe, Realistic only when needed.

---

## Migration Checklist

When migrating existing SSE tests:

- [ ] Identify test purpose (rendering vs integration vs error handling)
- [ ] Choose appropriate eventDelay (0ms, 10ms, or 50-100ms)
- [ ] Replace manual SSE mocking with `createSSEResponse()`
- [ ] Run test to verify stability (no flakiness)
- [ ] Document decision in test comments if non-obvious

---

## Questions?

If unsure which mode to use:
1. **Start with 10ms (realistic)** - safe default
2. Run test 10 times: `pnpm test:watch --run <test-file> --reporter=verbose`
3. If 100% stable → try 0ms for speed
4. If flaky → keep 10ms or investigate test logic

**Last Updated**: 2025-11-30 (Issue #1495)
