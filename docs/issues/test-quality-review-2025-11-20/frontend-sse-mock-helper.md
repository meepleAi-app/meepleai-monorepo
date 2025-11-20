# Issue: Extract Reusable SSE Mock Helper

**ID**: TEST-004
**Category**: Frontend Testing - Code Quality
**Priority**: 🔴 **CRITICAL**
**Status**: 🔴 Open
**Created**: 2025-11-20

---

## 📋 Summary

Extract and centralize SSE (Server-Sent Events) mock helper from `useChatStreaming.test.ts`. Currently, the `createMockStream()` function is duplicated 10+ times across the test file, creating 100+ lines of boilerplate code.

---

## 🎯 Problem Statement

### Current Duplication

Da `useChatStreaming.test.ts` - Duplicated **10+ times**:
```typescript
const createMockStream = (events: string[]) => {
  const encoder = new TextEncoder();
  const stream = new ReadableStream({
    start(controller) {
      events.forEach((event) => {
        controller.enqueue(encoder.encode(event));
      });
      controller.close();
    },
  });

  return {
    ok: true,
    body: stream,
  };
};
```

**Usage** (repeated pattern):
```typescript
// ❌ Manual SSE event construction - error-prone
const sseEvents = [
  'event: token\ndata: {"token":"Hello"}\n\n',
  'event: token\ndata: {"token":" "}\n\n',
  'event: token\ndata: {"token":"world"}\n\n',
  'event: complete\ndata: {"totalTokens":3,"confidence":0.95,"snippets":[]}\n\n',
];

mockFetch.mockResolvedValue(createMockStream(sseEvents));
```

### Issues
- ⚠️ **100+ lines** of duplicated code
- ⚠️ **Error-prone**: Manual SSE format (`event: ...\ndata: ...\n\n`)
- ⚠️ **Typos**: Easy to forget double newline `\n\n`
- ⚠️ **JSON escaping**: Complex data requires manual JSON.stringify
- ⚠️ **Not reusable**: Can't use in other test files
- ⚠️ **Difficult maintenance**: Changes require updating 10+ locations

### Files Affected
- `apps/web/src/lib/hooks/__tests__/useChatStreaming.test.ts` (1,234 lines, 10+ duplications)
- Future SSE tests (currently blocked by lack of helper)

---

## 🔧 Solution: Create SSEMockBuilder

### Recommended Implementation

**File**: `apps/web/src/__tests__/helpers/sse-mock-builder.ts`

```typescript
/**
 * Builder for creating mock SSE (Server-Sent Events) streams for testing.
 *
 * Usage:
 * ```ts
 * const mockStream = new SSEMockBuilder()
 *   .addStateUpdate('Processing...')
 *   .addToken('Hello')
 *   .addToken(' world')
 *   .addComplete({ totalTokens: 2, confidence: 0.9, snippets: [] })
 *   .build();
 *
 * mockFetch.mockResolvedValue(mockStream);
 * ```
 */
export class SSEMockBuilder {
  private events: string[] = [];

  /**
   * Add a state update event
   */
  addStateUpdate(state: string): this {
    this.events.push(`event: stateUpdate\ndata: ${JSON.stringify({ state })}\n\n`);
    return this;
  }

  /**
   * Add a token event (for streaming text)
   */
  addToken(token: string): this {
    this.events.push(`event: token\ndata: ${JSON.stringify({ token })}\n\n`);
    return this;
  }

  /**
   * Add citations event
   */
  addCitations(snippets: Array<{ text: string; source: string; page: number; line: number | null }>): this {
    this.events.push(`event: citations\ndata: ${JSON.stringify({ snippets })}\n\n`);
    return this;
  }

  /**
   * Add completion event (marks end of stream)
   */
  addComplete(metadata: { totalTokens: number; confidence: number; snippets: any[] }): this {
    this.events.push(`event: complete\ndata: ${JSON.stringify(metadata)}\n\n`);
    return this;
  }

  /**
   * Add error event
   */
  addError(message: string, code?: string): this {
    const data = code ? { message, code } : { message };
    this.events.push(`event: error\ndata: ${JSON.stringify(data)}\n\n`);
    return this;
  }

  /**
   * Add heartbeat event (keep-alive)
   */
  addHeartbeat(): this {
    this.events.push('event: heartbeat\ndata: null\n\n');
    return this;
  }

  /**
   * Add custom event (for edge cases)
   */
  addCustomEvent(eventType: string, data: any): this {
    this.events.push(`event: ${eventType}\ndata: ${JSON.stringify(data)}\n\n`);
    return this;
  }

  /**
   * Add raw SSE string (for malformed event testing)
   */
  addRaw(raw: string): this {
    this.events.push(raw);
    return this;
  }

  /**
   * Build the mock Response object with ReadableStream
   */
  build(): Response {
    const encoder = new TextEncoder();
    const events = this.events;

    const stream = new ReadableStream({
      start(controller) {
        events.forEach((event) => {
          controller.enqueue(encoder.encode(event));
        });
        controller.close();
      },
    });

    return {
      ok: true,
      body: stream,
      status: 200,
      statusText: 'OK',
      headers: new Headers(),
    } as any;
  }

  /**
   * Build with delay between events (simulate slow network)
   */
  buildWithDelay(delayMs: number): Response {
    const encoder = new TextEncoder();
    const events = this.events;

    const stream = new ReadableStream({
      async start(controller) {
        for (const event of events) {
          controller.enqueue(encoder.encode(event));
          if (delayMs > 0) {
            await new Promise((resolve) => setTimeout(resolve, delayMs));
          }
        }
        controller.close();
      },
    });

    return {
      ok: true,
      body: stream,
      status: 200,
      statusText: 'OK',
      headers: new Headers(),
    } as any;
  }

  /**
   * Build error response (HTTP error, not SSE error)
   */
  buildError(status: number, statusText: string): Response {
    return {
      ok: false,
      body: null,
      status,
      statusText,
      headers: new Headers(),
    } as any;
  }

  /**
   * Build response with partial chunks (test buffering)
   */
  buildPartialChunks(chunkSize: number): Response {
    const encoder = new TextEncoder();
    const fullData = this.events.join('');

    const stream = new ReadableStream({
      start(controller) {
        for (let i = 0; i < fullData.length; i += chunkSize) {
          const chunk = fullData.substring(i, i + chunkSize);
          controller.enqueue(encoder.encode(chunk));
        }
        controller.close();
      },
    });

    return {
      ok: true,
      body: stream,
      status: 200,
      statusText: 'OK',
      headers: new Headers(),
    } as any;
  }
}

/**
 * Factory function for quick mock creation
 */
export function createSSEMock(
  fn: (builder: SSEMockBuilder) => SSEMockBuilder
): Response {
  return fn(new SSEMockBuilder()).build();
}
```

### Usage Examples

#### Before (Current - Verbose)
```typescript
const sseEvents = [
  'event: stateUpdate\ndata: {"state":"Processing..."}\n\n',
  'event: token\ndata: {"token":"Hello"}\n\n',
  'event: token\ndata: {"token":" "}\n\n',
  'event: token\ndata: {"token":"world"}\n\n',
  'event: complete\ndata: {"totalTokens":3,"confidence":0.95,"snippets":[]}\n\n',
];

const encoder = new TextEncoder();
const stream = new ReadableStream({
  start(controller) {
    sseEvents.forEach((event) => {
      controller.enqueue(encoder.encode(event));
    });
    controller.close();
  },
});

mockFetch.mockResolvedValue({ ok: true, body: stream });
```

#### After (With Builder - Clean)
```typescript
const mockStream = new SSEMockBuilder()
  .addStateUpdate('Processing...')
  .addToken('Hello')
  .addToken(' ')
  .addToken('world')
  .addComplete({ totalTokens: 3, confidence: 0.95, snippets: [] })
  .build();

mockFetch.mockResolvedValue(mockStream);
```

#### Advanced Usage
```typescript
// Error scenario
const errorStream = new SSEMockBuilder()
  .addStateUpdate('Generating...')
  .addToken('Partial')
  .addError('Something went wrong', 'ERR_500')
  .build();

// With citations
const withCitations = new SSEMockBuilder()
  .addCitations([
    { text: 'Rule 1', source: 'rules.pdf', page: 1, line: null },
    { text: 'Rule 2', source: 'rules.pdf', page: 2, line: null },
  ])
  .addToken('Based on the rules...')
  .addComplete({ totalTokens: 5, confidence: 0.9, snippets: [] })
  .build();

// Test malformed events
const malformed = new SSEMockBuilder()
  .addToken('Valid')
  .addRaw('event: token\ndata: {invalid json}\n\n') // Malformed JSON
  .addToken('Still works')
  .addComplete({ totalTokens: 2, confidence: 0.8, snippets: [] })
  .build();

// Test slow network
const slowStream = new SSEMockBuilder()
  .addToken('Slow')
  .addToken('Stream')
  .addComplete({ totalTokens: 2, confidence: 0.9, snippets: [] })
  .buildWithDelay(100); // 100ms between events

// Test HTTP error
const httpError = new SSEMockBuilder().buildError(401, 'Unauthorized');
```

---

## 📝 Implementation Checklist

### Phase 1: Create Helper (1 hour)
- [ ] Create `apps/web/src/__tests__/helpers/sse-mock-builder.ts`
- [ ] Implement SSEMockBuilder class with all methods
- [ ] Add JSDoc documentation
- [ ] Create unit tests for the builder itself
- [ ] Export from `apps/web/src/__tests__/helpers/index.ts`

### Phase 2: Migrate useChatStreaming.test.ts (1-2 hours)
- [ ] Import SSEMockBuilder in test file
- [ ] Replace first `createMockStream` usage with builder
- [ ] Run tests to verify behavior
- [ ] Migrate remaining usages (10+ locations)
- [ ] Remove duplicated `createMockStream` function
- [ ] Update imports

### Phase 3: Documentation & Examples (<1 hour)
- [ ] Add usage examples to testing guide
- [ ] Document in CONTRIBUTING.md
- [ ] Create Storybook story (optional, for visual docs)
- [ ] Add to test utils documentation

---

## ✅ Acceptance Criteria

- [ ] SSEMockBuilder class created and fully documented
- [ ] Builder covers all SSE event types (token, citations, complete, error, heartbeat)
- [ ] All usages in useChatStreaming.test.ts migrated
- [ ] Zero code duplication of stream creation
- [ ] All tests pass with new builder
- [ ] File size reduced by ~100 lines (boilerplate removed)
- [ ] Documentation includes usage examples
- [ ] Ready for reuse in other test files

---

## 📊 Impact Analysis

### Before
- 1,234 lines in useChatStreaming.test.ts
- ~100 lines of duplicated code (10+ duplications)
- Error-prone manual SSE formatting
- Not reusable across files

### After
- ~1,134 lines in useChatStreaming.test.ts (-8%)
- 0 duplications
- Type-safe builder with IntelliSense
- Reusable across entire codebase
- Improved test readability

### Code Reduction
```typescript
// Before: 12 lines for simple stream
const sseEvents = ['event: token\ndata: {"token":"Hello"}\n\n'];
const encoder = new TextEncoder();
const stream = new ReadableStream({
  start(controller) {
    sseEvents.forEach((event) => {
      controller.enqueue(encoder.encode(event));
    });
    controller.close();
  },
});
mockFetch.mockResolvedValue({ ok: true, body: stream });

// After: 3 lines
const mock = new SSEMockBuilder().addToken('Hello').build();
mockFetch.mockResolvedValue(mock);

// 75% reduction in boilerplate
```

---

## 🔗 Related Issues

- [TEST-005](./frontend-replace-global-mocks.md) - Replace Global Fetch Mocks (uses this helper)
- [TEST-006](./frontend-split-large-files.md) - Split Large Files (will be easier after this)
- [TEST-011](./frontend-performance-tests.md) - Performance Tests (can use this helper)

---

## 📚 References

- [Server-Sent Events Spec](https://html.spec.whatwg.org/multipage/server-sent-events.html)
- [ReadableStream API](https://developer.mozilla.org/en-US/docs/Web/API/ReadableStream)
- [Builder Pattern](https://refactoring.guru/design-patterns/builder)

---

## 📈 Effort Estimate

**Total: 2-3 hours**

| Phase | Effort | Description |
|-------|--------|-------------|
| Create helper | 1h | SSEMockBuilder class + docs |
| Migrate usages | 1-2h | Update 10+ locations in test file |
| Documentation | <1h | Update testing guide |

---

## 🧪 Testing Strategy

### Validation Steps
1. **Builder tests**:
   ```typescript
   describe('SSEMockBuilder', () => {
     it('should create valid SSE stream', () => {
       const mock = new SSEMockBuilder()
         .addToken('test')
         .build();

       expect(mock.ok).toBe(true);
       expect(mock.body).toBeDefined();
     });
   });
   ```

2. **Integration tests**: All useChatStreaming tests must pass
3. **Edge cases**: Test malformed events, errors, delays

### Success Metrics
- [ ] All existing tests pass unchanged
- [ ] Builder is type-safe (TypeScript errors for invalid usage)
- [ ] IntelliSense works for all builder methods

---

**Last Updated**: 2025-11-20
**Status**: 🔴 Open - Ready for Implementation
**Assignee**: TBD
