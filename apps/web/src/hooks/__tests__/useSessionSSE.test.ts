import { renderHook, act } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach, afterEach } from 'vitest';

import { useSessionStore } from '@/store/session';

import { useSessionSSE } from '../useSessionSSE';

// ============================================================================
// Mock EventSource
// ============================================================================

class MockEventSource {
  url: string;
  withCredentials: boolean;
  onopen: ((ev: Event) => void) | null = null;
  onmessage: ((ev: MessageEvent) => void) | null = null;
  onerror: ((ev: Event) => void) | null = null;
  readyState = 0;
  closed = false;

  constructor(url: string, init?: EventSourceInit) {
    this.url = url;
    this.withCredentials = init?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  close() {
    this.closed = true;
    this.readyState = 2;
  }

  // Track instances for assertions
  static instances: MockEventSource[] = [];
  static clear() {
    MockEventSource.instances = [];
  }
}

// ============================================================================
// Setup
// ============================================================================

type AddEventFn = ReturnType<typeof useSessionStore.getState>['addEvent'];
let originalAddEvent: AddEventFn;

beforeEach(() => {
  MockEventSource.clear();
  vi.stubGlobal('EventSource', MockEventSource);
  // Reset store and capture original addEvent
  useSessionStore.getState().reset();
  originalAddEvent = useSessionStore.getState().addEvent;
});

afterEach(() => {
  // Restore original addEvent if it was replaced by a test
  useSessionStore.setState({ addEvent: originalAddEvent } as never);
  vi.restoreAllMocks();
  vi.useRealTimers();
});

// ============================================================================
// Tests
// ============================================================================

describe('useSessionSSE', () => {
  it('does nothing when sessionId is null', () => {
    renderHook(() => useSessionSSE(null));
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('creates EventSource with correct URL', () => {
    renderHook(() => useSessionSSE('session-123'));

    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe('/api/v1/sessions/session-123/events/stream');
    expect(MockEventSource.instances[0].withCredentials).toBe(true);
  });

  it('dispatches parsed events to store via addEvent', () => {
    const mockAddEvent = vi.fn(originalAddEvent);
    useSessionStore.setState({ addEvent: mockAddEvent } as never);

    renderHook(() => useSessionSSE('session-123'));

    const es = MockEventSource.instances[0];

    // Simulate onopen
    act(() => {
      es.onopen?.(new Event('open'));
    });

    // Simulate incoming message
    const event = {
      id: 'evt-1',
      type: 'note' as const,
      playerId: 'p1',
      data: { playerName: 'Alice', text: 'Hello' },
      timestamp: '2026-03-11T10:00:00.000Z',
    };

    act(() => {
      es.onmessage?.({
        data: JSON.stringify(event),
      } as MessageEvent);
    });

    const events = useSessionStore.getState().events;
    expect(events).toHaveLength(1);
    expect(events[0].id).toBe('evt-1');
    expect(events[0].type).toBe('note');
  });

  it('deduplicates events with the same ID', () => {
    renderHook(() => useSessionSSE('session-123'));

    const es = MockEventSource.instances[0];

    act(() => {
      es.onopen?.(new Event('open'));
    });

    const event = {
      id: 'evt-dup',
      type: 'note' as const,
      playerId: 'p1',
      data: { text: 'Dup test' },
      timestamp: '2026-03-11T10:00:00.000Z',
    };

    // Send same event twice
    act(() => {
      es.onmessage?.({ data: JSON.stringify(event) } as MessageEvent);
    });
    act(() => {
      es.onmessage?.({ data: JSON.stringify(event) } as MessageEvent);
    });

    const events = useSessionStore.getState().events;
    expect(events).toHaveLength(1);
  });

  it('cleans up EventSource on unmount', () => {
    const { unmount } = renderHook(() => useSessionSSE('session-123'));

    expect(MockEventSource.instances).toHaveLength(1);
    const es = MockEventSource.instances[0];
    expect(es.closed).toBe(false);

    unmount();

    expect(es.closed).toBe(true);
  });

  it('reconnects with exponential backoff on error', () => {
    vi.useFakeTimers();

    renderHook(() => useSessionSSE('session-123'));

    const es = MockEventSource.instances[0];

    // Trigger error
    act(() => {
      es.onerror?.(new Event('error'));
    });

    expect(MockEventSource.instances).toHaveLength(1); // still 1 — timer pending

    // Advance 1s (initial backoff)
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(MockEventSource.instances).toHaveLength(2); // reconnected

    // Trigger error again
    const es2 = MockEventSource.instances[1];
    act(() => {
      es2.onerror?.(new Event('error'));
    });

    // Advance 2s (doubled backoff)
    act(() => {
      vi.advanceTimersByTime(2000);
    });

    expect(MockEventSource.instances).toHaveLength(3); // reconnected again
  });

  it('stops reconnecting after 5-minute offline threshold', () => {
    vi.useFakeTimers();

    renderHook(() => useSessionSSE('session-123'));

    const es = MockEventSource.instances[0];

    // Trigger error — sets disconnectedAt
    act(() => {
      es.onerror?.(new Event('error'));
    });

    // Advance past initial backoff to trigger reconnect
    act(() => {
      vi.advanceTimersByTime(1000);
    });
    expect(MockEventSource.instances).toHaveLength(2);

    // Advance system clock past the 5-minute threshold (5min + 1ms)
    vi.setSystemTime(Date.now() + 5 * 60 * 1000 + 1);

    // Error on the reconnected source
    const es2 = MockEventSource.instances[1];
    act(() => {
      es2.onerror?.(new Event('error'));
    });

    // Should NOT schedule another reconnect — threshold exceeded
    act(() => {
      vi.advanceTimersByTime(60000);
    });
    expect(MockEventSource.instances).toHaveLength(2); // no new instance
  });
});
