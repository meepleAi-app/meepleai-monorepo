# Frontend Testing Patterns - MeepleAI

> **Target**: 85% | **Current**: 69.45% | **Gap**: -15.55% | **Last Updated**: 2026-01-21

## Philosophy

✅ **DO**: MSW network intercept, real stores/queries, user interactions
❌ **DON'T**: Mock internal functions, skip tests, test implementation details

---

## Quick Setup

```bash
pnpm add -D msw@latest
npx msw init public/
```

**Test Utils** (`src/__tests__/utils/`):
- `createTestQueryClient.ts`, `createTestStore.ts`, `mockEventSource.ts`
- `renderWithProviders.tsx`, `handlers/*.ts`

---

## Pattern 1: Query Hooks (MSW)

### Template

```typescript
// __tests__/hooks/queries/useLibrary.test.ts
import { renderHook, waitFor } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';

const handlers = [
  http.get('/api/v1/library', ({ request }) => {
    const url = new URL(request.url);
    return HttpResponse.json({
      items: [{ id: '1', gameId: 'catan-1', name: 'Catan', status: 'owned' }],
      totalCount: 2, page: parseInt(url.searchParams.get('page') || '1'), pageSize: 20
    });
  })
];

const server = setupServer(...handlers);
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('useLibrary', () => {
  it('fetches library items', async () => {
    const { result } = renderHook(() => useLibrary(), { wrapper: createWrapper() });
    expect(result.current.isLoading).toBe(true);
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.items).toHaveLength(2);
  });
});
```

### Priority Hooks

| Hook | LOC | Target |
|------|-----|--------|
| useLibrary.ts | 394 | 90% |
| useChats.ts | 248 | 90% |
| useDashboardData.ts | 162 | 85% |
| useGames.ts | 112 | 90% |

---

## Pattern 2: Zustand Store Integration

### Template

```typescript
// __tests__/store/chat/integration.test.ts
import { act, renderHook } from '@testing-library/react';
import { createChatStore } from '@/store/chat/store';

describe('Chat Store', () => {
  it('handles send → stream → complete flow', () => {
    const { result } = renderHook(() => useStore());

    act(() => result.current.createThread({ gameId: 'catan-1', title: 'Q' }));
    const threadId = result.current.threads[0].id;

    act(() => result.current.sendMessage({ threadId, content: 'How?', role: 'user' }));
    expect(result.current.messages).toHaveLength(1);

    act(() => result.current.startStreaming(threadId));
    expect(result.current.isStreaming).toBe(true);

    act(() => result.current.appendStreamingContent('Answer...'));
    expect(result.current.streamingContent).toBe('Answer...');

    act(() => result.current.completeStreaming({ messageId: 'msg-1', content: 'Answer...' }));
    expect(result.current.isStreaming).toBe(false);
    expect(result.current.messages).toHaveLength(2);
  });
});
```

---

## Pattern 3: SSE/Streaming (MockEventSource)

### Mock Setup

```typescript
// src/__tests__/utils/mockEventSource.ts
export class MockEventSource {
  static instances: MockEventSource[] = [];
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  readyState = 0; // 0=CONNECTING, 1=OPEN, 2=CLOSED

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
    MockEventSource.instances.push(this);
    queueMicrotask(() => { this.readyState = 1; this.onopen?.(); });
  }

  close() { this.readyState = 2; }
  simulateMessage(data: string) { this.onmessage?.(new MessageEvent('message', { data })); }
  simulateSSEChunk(chunk: object) { this.simulateMessage(`data: ${JSON.stringify(chunk)}\n\n`); }
  simulateDone() { this.simulateMessage('data: [DONE]\n\n'); }

  static reset() { this.instances.forEach(es => es.close()); this.instances = []; }
}

export const setupMockEventSource = () => {
  beforeAll(() => { global.EventSource = MockEventSource as unknown as typeof EventSource; });
  afterEach(() => MockEventSource.reset());
};
```

### Test Template

```typescript
// __tests__/lib/hooks/useChunkStreaming.test.ts
setupMockEventSource();

it('receives chunks and completes', async () => {
  const onChunk = vi.fn(), onComplete = vi.fn();
  const { result } = renderHook(() => useChunkStreaming({ url: '/api/stream', onChunk, onComplete }));

  await waitFor(() => expect(result.current.isConnected).toBe(true));

  act(() => MockEventSource.getLatest()!.simulateSSEChunk({ chunk: 'Hello ' }));
  expect(onChunk).toHaveBeenCalledWith('Hello ');

  act(() => MockEventSource.getLatest()!.simulateDone());
  expect(onComplete).toHaveBeenCalledWith('Hello ');
});
```

---

## Pattern 4: Component Integration

### Template

```typescript
// __tests__/components/catalog/CatalogFilters.integration.test.tsx
const server = setupServer(
  http.get('/api/v1/games/categories', () => HttpResponse.json([
    { id: 'strategy', name: 'Strategy', count: 150 }
  ]))
);

it('loads and applies filters', async () => {
  const onFilterChange = vi.fn();
  renderWithProviders(<CatalogFilters onFilterChange={onFilterChange} />);

  await waitFor(() => expect(screen.getByText('Strategy')).toBeInTheDocument());
  await user.click(screen.getByText('Strategy'));
  await user.click(screen.getByRole('button', { name: /apply/i }));

  expect(onFilterChange).toHaveBeenCalledWith(expect.objectContaining({ categories: ['strategy'] }));
});
```

---

## Shared Utilities

### renderWithProviders

```typescript
// src/__tests__/utils/renderWithProviders.tsx
export const createTestQueryClient = () => new QueryClient({
  defaultOptions: { queries: { retry: false, gcTime: 0, staleTime: 0 } }
});

export function renderWithProviders(ui, { queryClient = createTestQueryClient(), ...opts } = {}) {
  return render(ui, {
    wrapper: ({ children }) => (
      <QueryClientProvider client={queryClient}>
        <ChatStoreProvider>{children}</ChatStoreProvider>
      </QueryClientProvider>
    ),
    ...opts
  });
}
```

### Common Handlers

```typescript
// src/__tests__/utils/handlers/index.ts
export const authHandlers = [
  http.get('/api/v1/auth/me', () => HttpResponse.json({ id: 'u1', email: 't@e.com', role: 'user' }))
];

export const gamesHandlers = [
  http.get('/api/v1/games', () => HttpResponse.json({ items: [...], totalCount: 100 }))
];
```

---

## Coverage Roadmap

### Sprint 1: Query Hooks (+5%)
- useLibrary.ts (394 LOC)
- useChats.ts (248 LOC)
- useDashboardData.ts (162 LOC)

### Sprint 2: Store Integration (+3%)
- Chat slices integration
- StoreProvider, HookContext

### Sprint 3: Catalog & Shared Games (+4%)
- CatalogFilters.tsx (352 LOC)
- SharedGameSearch.tsx (432 LOC)

### Sprint 4: Documents & Upload (+3%)
- MultiDocumentCollectionUpload.tsx (342 LOC)
- FileUploadList.tsx (268 LOC)

### Sprint 5: Streaming (+2%)
- useChunkStreaming.ts, useStatefulStreaming.ts
- useStreamingChatWithReconnect.ts (641 LOC)

### Sprint 6: Polish (+1-2%)
- GameContext.tsx, Versioning components
- Edge cases & error handling

---

## Troubleshooting

**Windows: Tests hang**
```bash
node node_modules/vitest/vitest.mjs run --coverage
```

See [windows-vitest-troubleshooting.md](./windows-vitest-troubleshooting.md)

---

**Related**: [E2E Guide](./e2e/e2-e-test-guide.md) | [Backend Testing](./backend/testcontainers-best-practices.md) | [Week 4 Plan](./week4-frontend-component-test-plan.md)
