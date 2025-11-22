# Issue: Replace Global Fetch Mocks with MSW

**ID**: TEST-005
**Category**: Frontend Testing - Reliability
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-20

---

## 📋 Summary

Replace global `fetch` mocks with Mock Service Worker (MSW) to eliminate side effects, improve test isolation, and enable more realistic API testing. Currently, `global.fetch = mockFetch` creates shared state issues across test files.

---

## 🎯 Problem Statement

### Current Anti-Pattern

Da `useChatStreaming.test.ts:9-11`:
```typescript
// ❌ PROBLEM: Global mock
const mockFetch = jest.fn();
global.fetch = mockFetch; // Pollutes global scope
```

### Issues
- ⚠️ **Global state pollution**: All tests share same mock
- ⚠️ **Side effects**: One test can affect another
- ⚠️ **Difficult debugging**: Hard to trace which test set what mock
- ⚠️ **Order dependency**: Tests might fail if run in different order
- ⚠️ **Not realistic**: Doesn't test actual fetch calls
- ⚠️ **Hard to maintain**: Mock setup scattered across files

### Impact Examples

```typescript
// Test file A
global.fetch = jest.fn().mockResolvedValue({ ok: true });

// Test file B (runs after A)
// ⚠️ Unexpectedly inherits mock from A
const result = await fetch('/api/data'); // Uses A's mock!
```

---

## 🔧 Solution: Adopt MSW (Mock Service Worker)

### Why MSW?

✅ **Network-level mocking**: Intercepts requests before they reach fetch
✅ **Realistic**: Same code path as production
✅ **Isolated**: Each test defines its own handlers
✅ **Type-safe**: TypeScript support
✅ **Framework-agnostic**: Works with fetch, axios, etc.
✅ **Easy debugging**: Clear request/response logs

### Recommended Implementation

#### Phase 1: Setup MSW

**File**: `apps/web/src/__tests__/setup/msw.ts`

```typescript
import { setupServer } from 'msw/node';
import { rest } from 'msw';

// Default handlers (can be overridden per test)
export const handlers = [
  // Health check endpoint
  rest.get('/api/health', (req, res, ctx) => {
    return res(ctx.status(200), ctx.json({ status: 'ok' }));
  }),
];

// Setup server with default handlers
export const server = setupServer(...handlers);

// Establish API mocking before all tests
beforeAll(() => {
  server.listen({ onUnhandledRequest: 'warn' });
});

// Reset handlers after each test (important for test isolation)
afterEach(() => {
  server.resetHandlers();
});

// Clean up after all tests
afterAll(() => {
  server.close();
});
```

**File**: `apps/web/jest.setup.ts` (add MSW setup)

```typescript
import '@testing-library/jest-dom';
import { TextEncoder, TextDecoder } from 'util';

// Polyfills
global.TextEncoder = TextEncoder as any;
global.TextDecoder = TextDecoder as any;

// ✅ Import MSW setup (replaces global.fetch mocks)
import './src/__tests__/setup/msw';
```

#### Phase 2: SSE Handler for Streaming

**File**: `apps/web/src/__tests__/helpers/msw-sse-handler.ts`

```typescript
import { rest, RestContext } from 'msw';
import { SSEMockBuilder } from './sse-mock-builder';

/**
 * MSW handler for SSE streaming endpoints
 */
export function createSSEHandler(
  url: string,
  builderFn: (builder: SSEMockBuilder) => SSEMockBuilder
) {
  return rest.post(url, async (req, res, ctx) => {
    const mockResponse = builderFn(new SSEMockBuilder()).build();

    // Return the stream body
    return res(
      ctx.status(200),
      ctx.set('Content-Type', 'text/event-stream'),
      ctx.set('Cache-Control', 'no-cache'),
      ctx.set('Connection', 'keep-alive'),
      ctx.body(mockResponse.body)
    );
  });
}

/**
 * Quick helper for common SSE patterns
 */
export const sseHandlers = {
  /**
   * Handler that returns simple text tokens
   */
  simpleTokens: (url: string, ...tokens: string[]) =>
    createSSEHandler(url, (builder) => {
      tokens.forEach((token) => builder.addToken(token));
      builder.addComplete({ totalTokens: tokens.length, confidence: 0.9, snippets: [] });
      return builder;
    }),

  /**
   * Handler that returns error
   */
  error: (url: string, message: string, code?: string) =>
    createSSEHandler(url, (builder) =>
      builder.addError(message, code)
    ),

  /**
   * Handler that returns complete stream with citations
   */
  withCitations: (url: string, snippets: any[], answer: string) =>
    createSSEHandler(url, (builder) =>
      builder
        .addCitations(snippets)
        .addToken(answer)
        .addComplete({ totalTokens: 10, confidence: 0.95, snippets })
    ),
};
```

### Usage Examples

#### Before (Global Mock - Brittle)

```typescript
describe('useChatStreaming', () => {
  const mockFetch = jest.fn();
  global.fetch = mockFetch; // ❌ Global pollution

  beforeEach(() => {
    mockFetch.mockReset();
  });

  it('should stream tokens', async () => {
    // Manual SSE stream creation
    const encoder = new TextEncoder();
    const stream = new ReadableStream({
      start(controller) {
        controller.enqueue(encoder.encode('event: token\ndata: {"token":"test"}\n\n'));
        controller.close();
      },
    });

    mockFetch.mockResolvedValue({
      ok: true,
      body: stream,
    });

    // Test code...
  });
});
```

#### After (MSW - Robust)

```typescript
import { server } from '@/__tests__/setup/msw';
import { sseHandlers } from '@/__tests__/helpers/msw-sse-handler';

describe('useChatStreaming', () => {
  it('should stream tokens', async () => {
    // ✅ Isolated, network-level mock
    server.use(
      sseHandlers.simpleTokens('/api/v1/agents/qa/stream', 'Hello', ' ', 'world')
    );

    const { result } = renderHook(() => useChatStreaming());
    const [, controls] = result.current;

    act(() => {
      controls.startStreaming('game-1', 'test query');
    });

    await waitFor(() => {
      const [state] = result.current;
      expect(state.currentAnswer).toBe('Hello world');
    });
  });

  it('should handle errors', async () => {
    // ✅ Override handler for this test only
    server.use(
      sseHandlers.error('/api/v1/agents/qa/stream', 'Network error', 'ERR_NETWORK')
    );

    // Test error handling...
  });

  it('should handle citations', async () => {
    const snippets = [
      { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
    ];

    server.use(
      sseHandlers.withCitations('/api/v1/agents/qa/stream', snippets, 'Based on the rules...')
    );

    // Test citation handling...
  });
});
```

---

## 📝 Implementation Checklist

### Phase 1: Setup MSW (2 hours)
- [ ] Install MSW: `pnpm add -D msw@latest`
- [ ] Create `apps/web/src/__tests__/setup/msw.ts`
- [ ] Update `jest.setup.ts` to import MSW
- [ ] Create base handlers for common endpoints
- [ ] Create SSE handler helpers
- [ ] Verify basic MSW functionality

### Phase 2: Migrate High-Impact Files (3-4 hours)
Priority files:
- [ ] `useChatStreaming.test.ts` (highest impact - 1,234 lines)
- [ ] `authClient.test.ts` (593 lines)
- [ ] `useGames.test.tsx` (274 lines)
- [ ] `httpClient.test.ts`
- [ ] Other files using `global.fetch` mocks

**Migration steps per file**:
1. Remove `global.fetch = mockFetch`
2. Remove `mockFetch` variable
3. Add MSW handlers using `server.use()`
4. Update test assertions if needed
5. Verify all tests pass

### Phase 3: Add Advanced Features (1-2 hours)
- [ ] Request matching (verify request body)
- [ ] Conditional responses (based on request data)
- [ ] Delay simulation (network latency)
- [ ] Error scenarios (network failures, timeouts)
- [ ] Response helpers (pagination, filtering)

### Phase 4: Documentation (1 hour)
- [ ] Update testing guide with MSW usage
- [ ] Add examples for common patterns
- [ ] Document how to override handlers
- [ ] Add troubleshooting guide

---

## ✅ Acceptance Criteria

- [ ] MSW installed and configured
- [ ] Zero global fetch mocks remaining
- [ ] All tests using `server.use()` for HTTP mocking
- [ ] SSE streaming works with MSW
- [ ] Test isolation verified (tests pass in any order)
- [ ] Documentation complete with examples
- [ ] All tests pass with 90%+ coverage maintained

---

## 📊 Impact Analysis

### Before
- **Global mocks**: 10+ files
- **Side effects**: High risk
- **Debugging**: Difficult
- **Realism**: Low (not testing actual fetch)

### After
- **MSW handlers**: Network-level, isolated
- **Side effects**: Zero
- **Debugging**: Easy (request/response logs)
- **Realism**: High (same code path as production)

### Benefits

| Metric | Before | After | Improvement |
|--------|--------|-------|-------------|
| Test isolation | ⚠️ Poor | ✅ Perfect | 100% |
| Debugging time | 10-30 min | 2-5 min | 75% faster |
| Flaky tests | 5-10% | <1% | 90% reduction |
| Maintenance | High | Low | -60% |

---

## 🔗 Related Issues

- [TEST-004](./frontend-sse-mock-helper.md) - SSE Mock Helper (prerequisite)
- [TEST-006](./frontend-split-large-files.md) - Split Large Files
- [TEST-012](./frontend-api-integration-tests.md) - API Integration Tests (uses MSW)

---

## 📚 References

- [MSW Documentation](https://mswjs.io/)
- [MSW with Jest](https://mswjs.io/docs/integrations/node)
- [Testing Library + MSW](https://kentcdodds.com/blog/stop-mocking-fetch)

---

## 📈 Effort Estimate

**Total: 6-8 hours**

| Phase | Effort | Description |
|-------|--------|-------------|
| Setup MSW | 2h | Install, configure, basic handlers |
| Migrate useChatStreaming | 2h | Largest file, complex SSE |
| Migrate other files | 2-3h | authClient, useGames, httpClient, etc. |
| Advanced features | 1-2h | Request matching, delays, errors |
| Documentation | 1h | Update guides, examples |

---

## 🧪 Testing Strategy

### Validation Steps

1. **Verify MSW setup**:
   ```typescript
   it('should intercept requests', async () => {
     server.use(
       rest.get('/api/test', (req, res, ctx) =>
         res(ctx.json({ message: 'MSW works' }))
       )
     );

     const response = await fetch('/api/test');
     const data = await response.json();

     expect(data.message).toBe('MSW works');
   });
   ```

2. **Verify test isolation**:
   ```bash
   # Run tests in random order
   pnpm test --randomize

   # Run tests in parallel
   pnpm test --maxWorkers=4
   ```

3. **Verify SSE streaming**:
   ```typescript
   it('should stream via MSW', async () => {
     server.use(
       sseHandlers.simpleTokens('/api/stream', 'test')
     );

     // Verify streaming works
   });
   ```

### Success Metrics
- [ ] All tests pass in random order
- [ ] All tests pass in parallel
- [ ] Zero global.fetch usage
- [ ] Coverage maintained at 90%+

---

**Last Updated**: 2025-11-20
**Status**: 🔴 Open - Ready for Implementation
**Assignee**: TBD
**Depends On**: TEST-004 (SSE Mock Helper)
