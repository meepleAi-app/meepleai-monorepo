# Frontend Testing Patterns - MeepleAI

> **Coverage Target**: 85% statements | **Current**: 69.45% | **Gap**: -15.55%
>
> **Last Updated**: 2026-01-21

## Overview

This guide documents the testing patterns for MeepleAI frontend, focusing on **minimal mocking** approach to ensure tests reflect real application behavior.

### Philosophy

```
✅ DO: Test real behavior with MSW network interception
✅ DO: Use actual stores and query clients
✅ DO: Test user interactions, not implementation details
❌ DON'T: Mock internal functions or modules
❌ DON'T: Skip tests to increase coverage
❌ DON'T: Test implementation details
```

---

## Prerequisites

### MSW Setup

```bash
# Install MSW
pnpm add -D msw@latest

# Initialize (creates public/mockServiceWorker.js)
npx msw init public/
```

### Test Utilities Location

```
src/
├── __tests__/
│   └── utils/
│       ├── createTestQueryClient.ts
│       ├── createTestStore.ts
│       ├── mockEventSource.ts
│       ├── renderWithProviders.tsx
│       └── handlers/
│           ├── authHandlers.ts
│           ├── chatHandlers.ts
│           ├── gamesHandlers.ts
│           └── libraryHandlers.ts
```

---

## Pattern 1: Query Hooks Testing

### Problem
Query hooks in `src/hooks/queries/` depend on API calls and are difficult to test without heavy mocking.

### Solution
Use MSW to intercept network requests at the fetch level, allowing real React Query behavior.

### Template

```typescript
// __tests__/hooks/queries/useLibrary.test.ts
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { useLibrary } from '@/hooks/queries/useLibrary';

// Define handlers that simulate real API
const handlers = [
  http.get('/api/v1/library', ({ request }) => {
    const url = new URL(request.url);
    const page = url.searchParams.get('page') || '1';

    return HttpResponse.json({
      items: [
        { id: '1', gameId: 'catan-1', name: 'Catan', status: 'owned' },
        { id: '2', gameId: 'ticket-1', name: 'Ticket to Ride', status: 'wishlist' }
      ],
      totalCount: 2,
      page: parseInt(page),
      pageSize: 20
    });
  }),

  http.get('/api/v1/library', ({ request }) => {
    const url = new URL(request.url);
    if (url.searchParams.get('status') === 'invalid') {
      return HttpResponse.json(
        { error: 'Invalid status filter' },
        { status: 400 }
      );
    }
  })
];

const server = setupServer(...handlers);

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

// Factory for isolated QueryClient per test
const createTestQueryClient = () => new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      gcTime: 0,
      staleTime: 0
    }
  }
});

const createWrapper = () => {
  const queryClient = createTestQueryClient();
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
};

describe('useLibrary', () => {
  it('fetches library items successfully', async () => {
    const { result } = renderHook(() => useLibrary(), {
      wrapper: createWrapper()
    });

    // Verify loading state
    expect(result.current.isLoading).toBe(true);
    expect(result.current.data).toBeUndefined();

    // Wait for success
    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    // Verify data
    expect(result.current.data?.items).toHaveLength(2);
    expect(result.current.data?.items[0]).toMatchObject({
      id: '1',
      name: 'Catan',
      status: 'owned'
    });
  });

  it('handles pagination correctly', async () => {
    const { result, rerender } = renderHook(
      ({ page }) => useLibrary({ page }),
      {
        wrapper: createWrapper(),
        initialProps: { page: 1 }
      }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.page).toBe(1);

    // Change page
    rerender({ page: 2 });

    await waitFor(() => {
      expect(result.current.data?.page).toBe(2);
    });
  });

  it('handles API errors gracefully', async () => {
    server.use(
      http.get('/api/v1/library', () => {
        return HttpResponse.json(
          { error: 'Unauthorized' },
          { status: 401 }
        );
      })
    );

    const { result } = renderHook(() => useLibrary(), {
      wrapper: createWrapper()
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error).toBeDefined();
  });

  it('refetches on window focus', async () => {
    const { result } = renderHook(() => useLibrary(), {
      wrapper: createWrapper()
    });

    await waitFor(() => expect(result.current.isSuccess).toBe(true));

    // Simulate window focus
    window.dispatchEvent(new Event('focus'));

    await waitFor(() => {
      expect(result.current.isFetching).toBe(true);
    });
  });
});
```

### Coverage Targets
| Hook | LOC | Priority | Target Coverage |
|------|-----|----------|-----------------|
| useLibrary.ts | 394 | High | 90% |
| useChats.ts | 248 | High | 90% |
| useDashboardData.ts | 162 | High | 85% |
| useGames.ts | 112 | Medium | 90% |
| useSharedGames.ts | 111 | Medium | 85% |

---

## Pattern 2: Zustand Store Integration Testing

### Problem
`store/chat/` has partial coverage but lacks integration testing between slices and providers.

### Solution
Create store factories for isolated testing and test real state transitions.

### Template

```typescript
// __tests__/store/chat/integration.test.ts
import { act, renderHook } from '@testing-library/react';
import { createStore } from 'zustand';
import { createChatStore } from '@/store/chat/store';
import type { ChatState, ChatMessage } from '@/store/chat/types';

// Factory for isolated store instances
const createTestChatStore = (initialState?: Partial<ChatState>) => {
  return createChatStore({
    messages: [],
    threads: [],
    activeThreadId: null,
    isStreaming: false,
    streamingContent: '',
    error: null,
    ...initialState
  });
};

describe('Chat Store Integration', () => {
  let useStore: ReturnType<typeof createTestChatStore>;

  beforeEach(() => {
    useStore = createTestChatStore();
  });

  describe('Message Flow', () => {
    it('handles complete send → stream → complete flow', () => {
      const { result } = renderHook(() => useStore());

      // 1. Create thread
      act(() => {
        result.current.createThread({
          gameId: 'catan-1',
          title: 'How to play?'
        });
      });

      const threadId = result.current.threads[0].id;
      expect(result.current.activeThreadId).toBe(threadId);

      // 2. Send message (triggers streaming)
      act(() => {
        result.current.sendMessage({
          threadId,
          content: 'How do I build a settlement?',
          role: 'user'
        });
      });

      expect(result.current.messages).toHaveLength(1);
      expect(result.current.messages[0].role).toBe('user');

      // 3. Start streaming response
      act(() => {
        result.current.startStreaming(threadId);
      });

      expect(result.current.isStreaming).toBe(true);

      // 4. Receive chunks
      act(() => {
        result.current.appendStreamingContent('To build ');
        result.current.appendStreamingContent('a settlement, ');
        result.current.appendStreamingContent('you need...');
      });

      expect(result.current.streamingContent).toBe('To build a settlement, you need...');

      // 5. Complete streaming
      act(() => {
        result.current.completeStreaming({
          messageId: 'msg-assistant-1',
          content: 'To build a settlement, you need...'
        });
      });

      expect(result.current.isStreaming).toBe(false);
      expect(result.current.streamingContent).toBe('');
      expect(result.current.messages).toHaveLength(2);
      expect(result.current.messages[1]).toMatchObject({
        id: 'msg-assistant-1',
        role: 'assistant',
        content: 'To build a settlement, you need...'
      });
    });

    it('handles streaming error with recovery', () => {
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.createThread({ gameId: 'catan-1', title: 'Test' });
        result.current.startStreaming(result.current.threads[0].id);
        result.current.appendStreamingContent('Partial content...');
      });

      // Simulate error during streaming
      act(() => {
        result.current.handleStreamingError({
          error: 'Connection lost',
          partialContent: result.current.streamingContent
        });
      });

      expect(result.current.isStreaming).toBe(false);
      expect(result.current.error).toBe('Connection lost');
      // Partial content should be preserved for retry
      expect(result.current.streamingContent).toBe('Partial content...');
    });
  });

  describe('Thread Management', () => {
    it('maintains thread limit per game', () => {
      const { result } = renderHook(() => useStore());

      // Create 5 threads for same game
      for (let i = 0; i < 5; i++) {
        act(() => {
          result.current.createThread({
            gameId: 'catan-1',
            title: `Thread ${i + 1}`
          });
        });
      }

      expect(result.current.threads.filter(t => t.gameId === 'catan-1')).toHaveLength(5);

      // 6th thread should fail or archive oldest
      act(() => {
        result.current.createThread({
          gameId: 'catan-1',
          title: 'Thread 6'
        });
      });

      const activeThreads = result.current.threads.filter(
        t => t.gameId === 'catan-1' && !t.isArchived
      );
      expect(activeThreads.length).toBeLessThanOrEqual(5);
    });

    it('switches context correctly between games', () => {
      const { result } = renderHook(() => useStore());

      // Create threads for different games
      act(() => {
        result.current.createThread({ gameId: 'catan-1', title: 'Catan Q' });
        result.current.sendMessage({
          threadId: result.current.threads[0].id,
          content: 'Catan question',
          role: 'user'
        });
      });

      act(() => {
        result.current.createThread({ gameId: 'ticket-1', title: 'Ticket Q' });
        result.current.sendMessage({
          threadId: result.current.threads[1].id,
          content: 'Ticket question',
          role: 'user'
        });
      });

      // Switch to Catan thread
      act(() => {
        result.current.setActiveThread(result.current.threads[0].id);
      });

      expect(result.current.activeThreadId).toBe(result.current.threads[0].id);
      // Verify game context matches
      const activeThread = result.current.threads.find(
        t => t.id === result.current.activeThreadId
      );
      expect(activeThread?.gameId).toBe('catan-1');
    });
  });

  describe('Slice Interactions', () => {
    it('syncs UI slice with messages slice', () => {
      const { result } = renderHook(() => useStore());

      act(() => {
        result.current.createThread({ gameId: 'catan-1', title: 'Test' });
        result.current.setInputValue('Draft message');
      });

      expect(result.current.inputValue).toBe('Draft message');

      // Sending should clear input
      act(() => {
        result.current.sendMessage({
          threadId: result.current.threads[0].id,
          content: result.current.inputValue,
          role: 'user'
        });
        result.current.clearInput();
      });

      expect(result.current.inputValue).toBe('');
      expect(result.current.messages[0].content).toBe('Draft message');
    });
  });
});
```

### Provider Integration Test

```typescript
// __tests__/store/chat/StoreProvider.test.tsx
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatStoreProvider } from '@/store/chat/StoreProvider';
import { useChatStore } from '@/store/chat/hooks';

const TestConsumer = () => {
  const { messages, sendMessage, createThread, threads } = useChatStore();

  return (
    <div>
      <span data-testid="message-count">{messages.length}</span>
      <span data-testid="thread-count">{threads.length}</span>
      <button
        onClick={() => createThread({ gameId: 'test', title: 'Test' })}
        data-testid="create-thread"
      >
        Create Thread
      </button>
      <button
        onClick={() => threads[0] && sendMessage({
          threadId: threads[0].id,
          content: 'Hello',
          role: 'user'
        })}
        data-testid="send-message"
      >
        Send
      </button>
    </div>
  );
};

describe('ChatStoreProvider', () => {
  const user = userEvent.setup();

  it('provides store context to consumers', async () => {
    render(
      <ChatStoreProvider>
        <TestConsumer />
      </ChatStoreProvider>
    );

    expect(screen.getByTestId('message-count')).toHaveTextContent('0');
    expect(screen.getByTestId('thread-count')).toHaveTextContent('0');

    await user.click(screen.getByTestId('create-thread'));
    expect(screen.getByTestId('thread-count')).toHaveTextContent('1');

    await user.click(screen.getByTestId('send-message'));
    expect(screen.getByTestId('message-count')).toHaveTextContent('1');
  });

  it('isolates state between provider instances', () => {
    const { unmount } = render(
      <ChatStoreProvider>
        <TestConsumer />
      </ChatStoreProvider>
    );

    // Modify state
    act(() => {
      screen.getByTestId('create-thread').click();
    });

    unmount();

    // New instance should have fresh state
    render(
      <ChatStoreProvider>
        <TestConsumer />
      </ChatStoreProvider>
    );

    expect(screen.getByTestId('thread-count')).toHaveTextContent('0');
  });
});
```

---

## Pattern 3: SSE/Streaming Testing

### Problem
`useChunkStreaming.ts` and `useStatefulStreaming.ts` are at 0% coverage because EventSource is hard to test.

### Solution
Create a minimal EventSource mock that simulates real SSE behavior.

### Mock Implementation

```typescript
// src/__tests__/utils/mockEventSource.ts
export class MockEventSource {
  static instances: MockEventSource[] = [];

  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  onopen: (() => void) | null = null;

  readyState = 0; // 0=CONNECTING, 1=OPEN, 2=CLOSED
  url: string;
  withCredentials: boolean;

  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
    MockEventSource.instances.push(this);

    // Simulate async connection (like real EventSource)
    queueMicrotask(() => {
      this.readyState = MockEventSource.OPEN;
      this.onopen?.();
    });
  }

  close() {
    this.readyState = MockEventSource.CLOSED;
  }

  // Test helpers
  simulateMessage(data: string, event?: string) {
    if (this.readyState !== MockEventSource.OPEN) return;

    const messageEvent = new MessageEvent(event || 'message', { data });
    this.onmessage?.(messageEvent);
  }

  simulateSSEChunk(chunk: object) {
    this.simulateMessage(`data: ${JSON.stringify(chunk)}\n\n`);
  }

  simulateDone() {
    this.simulateMessage('data: [DONE]\n\n');
  }

  simulateError(error?: string) {
    this.onerror?.(new Event('error'));
  }

  static reset() {
    MockEventSource.instances.forEach(es => es.close());
    MockEventSource.instances = [];
  }

  static getLatest(): MockEventSource | undefined {
    return MockEventSource.instances[MockEventSource.instances.length - 1];
  }
}

// Setup helper for tests
export const setupMockEventSource = () => {
  const original = global.EventSource;

  beforeAll(() => {
    global.EventSource = MockEventSource as unknown as typeof EventSource;
  });

  afterEach(() => {
    MockEventSource.reset();
  });

  afterAll(() => {
    global.EventSource = original;
  });
};
```

### Streaming Hook Test

```typescript
// __tests__/lib/hooks/useChunkStreaming.test.ts
import { renderHook, act, waitFor } from '@testing-library/react';
import { useChunkStreaming } from '@/lib/hooks/useChunkStreaming';
import { MockEventSource, setupMockEventSource } from '@/__tests__/utils/mockEventSource';

setupMockEventSource();

describe('useChunkStreaming', () => {
  it('connects and receives chunks', async () => {
    const onChunk = vi.fn();
    const onComplete = vi.fn();

    const { result } = renderHook(() =>
      useChunkStreaming({
        url: '/api/chat/stream?threadId=123',
        onChunk,
        onComplete
      })
    );

    // Wait for connection
    await waitFor(() => {
      expect(result.current.isConnected).toBe(true);
    });

    const eventSource = MockEventSource.getLatest()!;

    // Simulate SSE chunks
    act(() => {
      eventSource.simulateSSEChunk({ chunk: 'Hello ' });
    });
    expect(onChunk).toHaveBeenCalledWith('Hello ');
    expect(result.current.content).toBe('Hello ');

    act(() => {
      eventSource.simulateSSEChunk({ chunk: 'World!' });
    });
    expect(result.current.content).toBe('Hello World!');

    // Complete
    act(() => {
      eventSource.simulateDone();
    });

    expect(onComplete).toHaveBeenCalledWith('Hello World!');
    expect(result.current.isComplete).toBe(true);
    expect(result.current.isConnected).toBe(false);
  });

  it('handles connection errors with retry', async () => {
    const onError = vi.fn();

    const { result } = renderHook(() =>
      useChunkStreaming({
        url: '/api/chat/stream',
        onError,
        maxRetries: 3,
        retryDelayMs: 10 // Fast for tests
      })
    );

    await waitFor(() => expect(result.current.isConnected).toBe(true));

    // Simulate error
    act(() => {
      MockEventSource.getLatest()!.simulateError();
    });

    expect(result.current.retryCount).toBe(1);
    expect(onError).toHaveBeenCalled();

    // Should auto-reconnect
    await waitFor(() => {
      expect(MockEventSource.instances.length).toBe(2);
    });
  });

  it('aborts on unmount', async () => {
    const { result, unmount } = renderHook(() =>
      useChunkStreaming({ url: '/api/chat/stream' })
    );

    await waitFor(() => expect(result.current.isConnected).toBe(true));

    const eventSource = MockEventSource.getLatest()!;

    unmount();

    expect(eventSource.readyState).toBe(MockEventSource.CLOSED);
  });

  it('handles backpressure with buffering', async () => {
    const onChunk = vi.fn();

    renderHook(() =>
      useChunkStreaming({
        url: '/api/chat/stream',
        onChunk,
        bufferSize: 3
      })
    );

    await waitFor(() => expect(MockEventSource.instances.length).toBe(1));

    const eventSource = MockEventSource.getLatest()!;

    // Send rapid chunks
    act(() => {
      for (let i = 0; i < 10; i++) {
        eventSource.simulateSSEChunk({ chunk: `${i}` });
      }
    });

    // Verify buffering behavior
    expect(onChunk).toHaveBeenCalled();
  });
});
```

### Reconnection State Machine Test

```typescript
// __tests__/lib/hooks/useStreamingChatWithReconnect.test.ts
import { renderHook, act, waitFor } from '@testing-library/react';
import { useStreamingChatWithReconnect } from '@/lib/hooks/useStreamingChatWithReconnect';
import { MockEventSource, setupMockEventSource } from '@/__tests__/utils/mockEventSource';

setupMockEventSource();

describe('useStreamingChatWithReconnect', () => {
  it('follows state machine: idle → connecting → connected', async () => {
    const onStateChange = vi.fn();

    renderHook(() =>
      useStreamingChatWithReconnect({
        threadId: 'thread-1',
        onStateChange
      })
    );

    expect(onStateChange).toHaveBeenCalledWith('connecting');

    await waitFor(() => {
      expect(onStateChange).toHaveBeenCalledWith('connected');
    });
  });

  it('handles disconnect → reconnecting → connected', async () => {
    const onStateChange = vi.fn();

    renderHook(() =>
      useStreamingChatWithReconnect({
        threadId: 'thread-1',
        onStateChange,
        reconnectDelayMs: 10
      })
    );

    await waitFor(() => {
      expect(onStateChange).toHaveBeenCalledWith('connected');
    });

    // Simulate disconnect
    act(() => {
      MockEventSource.getLatest()!.simulateError();
    });

    expect(onStateChange).toHaveBeenCalledWith('reconnecting');

    // Wait for reconnection
    await waitFor(() => {
      expect(onStateChange).toHaveBeenLastCalledWith('connected');
    }, { timeout: 100 });
  });

  it('gives up after max retries', async () => {
    const onStateChange = vi.fn();
    const onError = vi.fn();

    renderHook(() =>
      useStreamingChatWithReconnect({
        threadId: 'thread-1',
        onStateChange,
        onError,
        maxRetries: 2,
        reconnectDelayMs: 5
      })
    );

    await waitFor(() => expect(onStateChange).toHaveBeenCalledWith('connected'));

    // Fail repeatedly
    for (let i = 0; i < 3; i++) {
      act(() => {
        MockEventSource.getLatest()?.simulateError();
      });
      await new Promise(r => setTimeout(r, 10));
    }

    expect(onStateChange).toHaveBeenCalledWith('failed');
    expect(onError).toHaveBeenCalledWith(expect.objectContaining({
      message: expect.stringContaining('Max retries')
    }));
  });

  it('preserves partial content on reconnect', async () => {
    const { result } = renderHook(() =>
      useStreamingChatWithReconnect({
        threadId: 'thread-1',
        reconnectDelayMs: 10
      })
    );

    await waitFor(() => expect(result.current.state).toBe('connected'));

    // Receive partial content
    act(() => {
      MockEventSource.getLatest()!.simulateSSEChunk({ chunk: 'Partial ' });
    });

    expect(result.current.content).toBe('Partial ');

    // Disconnect
    act(() => {
      MockEventSource.getLatest()!.simulateError();
    });

    // Content should be preserved
    expect(result.current.content).toBe('Partial ');

    // Continue after reconnect
    await waitFor(() => expect(result.current.state).toBe('connected'));

    act(() => {
      MockEventSource.getLatest()!.simulateSSEChunk({ chunk: 'continued' });
    });

    expect(result.current.content).toBe('Partial continued');
  });
});
```

---

## Pattern 4: Component Integration Testing

### Problem
Components like `CatalogFilters`, `SharedGameSearch` have 0% coverage because they depend on APIs.

### Solution
Combine MSW with real component rendering and user interactions.

### Template

```typescript
// __tests__/components/catalog/CatalogFilters.integration.test.tsx
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { setupServer } from 'msw/node';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CatalogFilters } from '@/components/catalog/CatalogFilters';

// Realistic API handlers
const handlers = [
  http.get('/api/v1/games/categories', () => {
    return HttpResponse.json([
      { id: 'strategy', name: 'Strategy', count: 150 },
      { id: 'party', name: 'Party Games', count: 80 },
      { id: 'family', name: 'Family', count: 120 }
    ]);
  }),

  http.get('/api/v1/games/mechanics', () => {
    return HttpResponse.json([
      { id: 'worker-placement', name: 'Worker Placement' },
      { id: 'deck-building', name: 'Deck Building' },
      { id: 'area-control', name: 'Area Control' }
    ]);
  }),

  http.get('/api/v1/games/complexity-ranges', () => {
    return HttpResponse.json([
      { id: 'light', label: 'Light (1-2)', min: 1, max: 2 },
      { id: 'medium', label: 'Medium (2-3)', min: 2, max: 3 },
      { id: 'heavy', label: 'Heavy (3-5)', min: 3, max: 5 }
    ]);
  })
];

const server = setupServer(...handlers);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

const renderWithProviders = (ui: React.ReactElement) => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  });

  return render(
    <QueryClientProvider client={queryClient}>
      {ui}
    </QueryClientProvider>
  );
};

describe('CatalogFilters Integration', () => {
  const user = userEvent.setup();

  it('loads and displays all filter options from API', async () => {
    const onFilterChange = vi.fn();

    renderWithProviders(
      <CatalogFilters onFilterChange={onFilterChange} />
    );

    // Wait for categories to load
    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    // Verify all filter sections
    expect(screen.getByText('Party Games')).toBeInTheDocument();
    expect(screen.getByText('Worker Placement')).toBeInTheDocument();
    expect(screen.getByText('Light (1-2)')).toBeInTheDocument();
  });

  it('applies single filter correctly', async () => {
    const onFilterChange = vi.fn();

    renderWithProviders(
      <CatalogFilters onFilterChange={onFilterChange} />
    );

    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Strategy'));
    await user.click(screen.getByRole('button', { name: /apply/i }));

    expect(onFilterChange).toHaveBeenCalledWith(
      expect.objectContaining({
        categories: ['strategy']
      })
    );
  });

  it('applies multiple filters across categories', async () => {
    const onFilterChange = vi.fn();

    renderWithProviders(
      <CatalogFilters onFilterChange={onFilterChange} />
    );

    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    // Select category
    await user.click(screen.getByText('Strategy'));

    // Select mechanic
    await user.click(screen.getByText('Worker Placement'));

    // Select complexity
    await user.click(screen.getByText('Medium (2-3)'));

    // Apply
    await user.click(screen.getByRole('button', { name: /apply/i }));

    expect(onFilterChange).toHaveBeenCalledWith({
      categories: ['strategy'],
      mechanics: ['worker-placement'],
      complexity: { min: 2, max: 3 },
      playerCount: null
    });
  });

  it('clears all filters on reset', async () => {
    const onFilterChange = vi.fn();

    renderWithProviders(
      <CatalogFilters
        onFilterChange={onFilterChange}
        initialFilters={{ categories: ['strategy'] }}
      />
    );

    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    // Strategy should be pre-selected
    expect(screen.getByText('Strategy').closest('button')).toHaveAttribute(
      'aria-pressed', 'true'
    );

    await user.click(screen.getByRole('button', { name: /reset/i }));

    expect(onFilterChange).toHaveBeenLastCalledWith({
      categories: [],
      mechanics: [],
      complexity: null,
      playerCount: null
    });
  });

  it('shows loading state while fetching', () => {
    renderWithProviders(<CatalogFilters onFilterChange={vi.fn()} />);

    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  it('shows error state when API fails', async () => {
    server.use(
      http.get('/api/v1/games/categories', () => {
        return HttpResponse.json(
          { error: 'Service unavailable' },
          { status: 503 }
        );
      })
    );

    renderWithProviders(<CatalogFilters onFilterChange={vi.fn()} />);

    await waitFor(() => {
      expect(screen.getByText(/failed to load/i)).toBeInTheDocument();
    });

    // Should show retry button
    expect(screen.getByRole('button', { name: /retry/i })).toBeInTheDocument();
  });

  it('preserves filter state across re-renders', async () => {
    const onFilterChange = vi.fn();

    const { rerender } = renderWithProviders(
      <CatalogFilters onFilterChange={onFilterChange} />
    );

    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Strategy'));

    // Re-render with same props
    rerender(
      <QueryClientProvider client={new QueryClient()}>
        <CatalogFilters onFilterChange={onFilterChange} />
      </QueryClientProvider>
    );

    // Selection should persist (via controlled state or URL params)
    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });
  });
});
```

### Shared Component Test

```typescript
// __tests__/components/shared-games/SharedGameSearch.integration.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse, delay } from 'msw';
import { setupServer } from 'msw/node';
import { SharedGameSearch } from '@/components/shared-games/SharedGameSearch';
import { renderWithProviders } from '@/__tests__/utils/renderWithProviders';

const mockGames = [
  { id: '1', name: 'Catan', year: 1995, rating: 7.2 },
  { id: '2', name: 'Carcassonne', year: 2000, rating: 7.4 },
  { id: '3', name: 'Ticket to Ride', year: 2004, rating: 7.5 }
];

const server = setupServer(
  http.get('/api/v1/shared-games/search', async ({ request }) => {
    const url = new URL(request.url);
    const query = url.searchParams.get('q') || '';

    // Simulate network delay
    await delay(100);

    const filtered = mockGames.filter(g =>
      g.name.toLowerCase().includes(query.toLowerCase())
    );

    return HttpResponse.json({
      items: filtered,
      totalCount: filtered.length
    });
  })
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('SharedGameSearch Integration', () => {
  const user = userEvent.setup();

  it('searches and displays results', async () => {
    const onSelect = vi.fn();

    renderWithProviders(
      <SharedGameSearch onSelect={onSelect} />
    );

    const searchInput = screen.getByPlaceholderText(/search games/i);

    await user.type(searchInput, 'Catan');

    // Wait for debounce + API
    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    }, { timeout: 500 });

    // Should not show other games
    expect(screen.queryByText('Carcassonne')).not.toBeInTheDocument();
  });

  it('shows loading indicator during search', async () => {
    renderWithProviders(<SharedGameSearch onSelect={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/search/i), 'test');

    expect(screen.getByRole('progressbar')).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });
  });

  it('shows empty state for no results', async () => {
    server.use(
      http.get('/api/v1/shared-games/search', () => {
        return HttpResponse.json({ items: [], totalCount: 0 });
      })
    );

    renderWithProviders(<SharedGameSearch onSelect={vi.fn()} />);

    await user.type(screen.getByPlaceholderText(/search/i), 'xyz');

    await waitFor(() => {
      expect(screen.getByText(/no games found/i)).toBeInTheDocument();
    });
  });

  it('calls onSelect when game is clicked', async () => {
    const onSelect = vi.fn();

    renderWithProviders(<SharedGameSearch onSelect={onSelect} />);

    await user.type(screen.getByPlaceholderText(/search/i), 'Cat');

    await waitFor(() => {
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Catan'));

    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({
        id: '1',
        name: 'Catan'
      })
    );
  });

  it('debounces search input', async () => {
    const searchSpy = vi.fn();

    server.use(
      http.get('/api/v1/shared-games/search', ({ request }) => {
        searchSpy(new URL(request.url).searchParams.get('q'));
        return HttpResponse.json({ items: [], totalCount: 0 });
      })
    );

    renderWithProviders(<SharedGameSearch onSelect={vi.fn()} />);

    // Type quickly
    await user.type(screen.getByPlaceholderText(/search/i), 'test');

    // Wait for debounce
    await waitFor(() => {
      expect(searchSpy).toHaveBeenCalled();
    }, { timeout: 500 });

    // Should only call once, not for each keystroke
    expect(searchSpy).toHaveBeenCalledTimes(1);
    expect(searchSpy).toHaveBeenCalledWith('test');
  });
});
```

---

## Shared Test Utilities

### Render With Providers

```typescript
// src/__tests__/utils/renderWithProviders.tsx
import { render, RenderOptions } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ChatStoreProvider } from '@/store/chat/StoreProvider';
import { IntlProvider } from '@/components/providers/IntlProvider';

interface ExtendedRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  queryClient?: QueryClient;
  initialChatState?: Partial<ChatState>;
  locale?: string;
}

export const createTestQueryClient = () => new QueryClient({
  defaultOptions: {
    queries: {
      retry: false,
      gcTime: 0,
      staleTime: 0
    },
    mutations: {
      retry: false
    }
  }
});

export function renderWithProviders(
  ui: React.ReactElement,
  {
    queryClient = createTestQueryClient(),
    initialChatState,
    locale = 'en',
    ...renderOptions
  }: ExtendedRenderOptions = {}
) {
  function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        <IntlProvider locale={locale}>
          <ChatStoreProvider initialState={initialChatState}>
            {children}
          </ChatStoreProvider>
        </IntlProvider>
      </QueryClientProvider>
    );
  }

  return {
    ...render(ui, { wrapper: Wrapper, ...renderOptions }),
    queryClient
  };
}
```

### Common API Handlers

```typescript
// src/__tests__/utils/handlers/index.ts
import { http, HttpResponse } from 'msw';

export const authHandlers = [
  http.get('/api/v1/auth/me', () => {
    return HttpResponse.json({
      id: 'user-1',
      email: 'test@example.com',
      name: 'Test User',
      role: 'user'
    });
  }),

  http.post('/api/v1/auth/logout', () => {
    return new HttpResponse(null, { status: 204 });
  })
];

export const gamesHandlers = [
  http.get('/api/v1/games', ({ request }) => {
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('page') || '1');

    return HttpResponse.json({
      items: [
        { id: '1', name: 'Catan', complexity: 2.3 },
        { id: '2', name: 'Ticket to Ride', complexity: 1.9 }
      ],
      totalCount: 100,
      page,
      pageSize: 20
    });
  }),

  http.get('/api/v1/games/:id', ({ params }) => {
    return HttpResponse.json({
      id: params.id,
      name: 'Catan',
      description: 'A game about trading and building',
      complexity: 2.3,
      minPlayers: 3,
      maxPlayers: 4
    });
  })
];

export const chatHandlers = [
  http.get('/api/v1/chat/threads', () => {
    return HttpResponse.json({
      items: [
        { id: 'thread-1', title: 'Catan Rules', gameId: 'catan-1' }
      ]
    });
  }),

  http.post('/api/v1/chat/messages', async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json({
      id: 'msg-1',
      threadId: body.threadId,
      content: body.content,
      role: 'user',
      createdAt: new Date().toISOString()
    });
  })
];

export const allHandlers = [
  ...authHandlers,
  ...gamesHandlers,
  ...chatHandlers
];
```

---

## Coverage Checklist

### Sprint 1: Query Hooks (+5%)
- [ ] useLibrary.ts - 394 LOC
- [ ] useChats.ts - 248 LOC
- [ ] useDashboardData.ts - 162 LOC
- [ ] useGames.ts - 112 LOC
- [ ] useSharedGames.ts - 111 LOC

### Sprint 2: Store Integration (+3%)
- [ ] store/chat slices integration
- [ ] StoreProvider.tsx
- [ ] HookContext.tsx
- [ ] Cross-slice interactions

### Sprint 3: Catalog & Shared Games (+4%)
- [ ] CatalogFilters.tsx - 352 LOC
- [ ] CatalogPagination.tsx - 147 LOC
- [ ] SharedGameSearch.tsx - 432 LOC
- [ ] SharedGameSearchFilters.tsx - 487 LOC

### Sprint 4: Documents & Upload (+3%)
- [ ] MultiDocumentCollectionUpload.tsx - 342 LOC
- [ ] FileUploadList.tsx - 268 LOC
- [ ] MultiFileUpload.tsx - 370 LOC
- [ ] UploadQueue.tsx - 118 LOC

### Sprint 5: Streaming (+2%)
- [ ] useChunkStreaming.ts
- [ ] useStatefulStreaming.ts
- [ ] useStreamingChatWithReconnect.ts - 641 LOC

### Sprint 6: Polish (+1-2%)
- [ ] GameContext.tsx
- [ ] Versioning components
- [ ] Admin-games store
- [ ] Edge cases & error handling

---

## Troubleshooting

### Windows: Tests Hang with No Output

If running `pnpm test` produces no output on Windows, use direct node execution:

```bash
node node_modules/vitest/vitest.mjs run --coverage
```

See [Windows Vitest Troubleshooting Guide](./windows-vitest-troubleshooting.md) for details.

## Related Documentation

- [E2E Test Guide](./e2e/e2-e-test-guide.md)
- [Playwright Best Practices](./e2e/e2e-selector-best-practices.md)
- [Backend Testing](./backend/testcontainers-best-practices.md)
- [Week 4 Component Plan](./frontend/week4-frontend-component-test-plan.md)
- [Windows Troubleshooting](./windows-vitest-troubleshooting.md)
