/**
 * Tests for useNotificationsCounter hook.
 *
 * Mocks EventSource + fetch.
 *
 * Asse B (#1897) WP6 T6 DEC-3.
 */

import { renderHook, waitFor, act } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useNotificationsCounter } from '../use-notifications-counter';

// Mock getApiBase
vi.mock('@/lib/api', () => ({
  getApiBase: () => 'http://test',
}));

// Mock EventSource implementation
class MockEventSource {
  static instances: MockEventSource[] = [];

  url: string;
  withCredentials: boolean;
  readyState = 0;
  onopen: ((ev: Event) => void) | null = null;
  onerror: ((ev: Event) => void) | null = null;
  listeners: Record<string, EventListener[]> = {};

  constructor(url: string, init?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = init?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, listener: EventListener) {
    if (!this.listeners[type]) this.listeners[type] = [];
    this.listeners[type].push(listener);
  }

  removeEventListener(type: string, listener: EventListener) {
    this.listeners[type] = (this.listeners[type] ?? []).filter(l => l !== listener);
  }

  dispatchEvent(event: Event & { type: string }): boolean {
    (this.listeners[event.type] ?? []).forEach(l => l(event));
    return true;
  }

  close() {
    this.readyState = 2;
  }

  // Test helpers
  fireOpen() {
    this.readyState = 1;
    this.onopen?.(new Event('open'));
  }

  fireNotification() {
    this.dispatchEvent({ type: 'notification' } as Event);
  }

  fireError() {
    this.onerror?.(new Event('error'));
  }
}

beforeEach(() => {
  MockEventSource.instances = [];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  (globalThis as any).EventSource = MockEventSource;

  // Default fetch mock
  globalThis.fetch = vi.fn().mockResolvedValue({
    ok: true,
    json: async () => ({ count: 3 }),
  }) as typeof fetch;
});

afterEach(() => {
  vi.clearAllMocks();
});

describe('useNotificationsCounter', () => {
  it('fetches initial unread count and connects SSE', async () => {
    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(result.current.count).toBe(3);
    });

    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe('http://test/api/v1/notifications/stream');
  });

  it('increments count on notification event', async () => {
    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(result.current.count).toBe(3);
    });

    act(() => {
      MockEventSource.instances[0].fireNotification();
    });

    expect(result.current.count).toBe(4);
  });

  it('returns count=0 when disabled', () => {
    const { result } = renderHook(() => useNotificationsCounter({ enabled: false }));
    expect(result.current.count).toBe(0);
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('handles 401 silently (no SSE attempt)', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 401,
    }) as typeof fetch;

    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(result.current.connectionState).toBe('closed');
    });

    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('connects SSE after initial fetch success', async () => {
    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(MockEventSource.instances).toHaveLength(1);
    });

    act(() => {
      MockEventSource.instances[0].fireOpen();
    });

    expect(result.current.connectionState).toBe('open');
  });

  it('cleans up EventSource on unmount', async () => {
    const { unmount } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(MockEventSource.instances).toHaveLength(1);
    });

    unmount();

    expect(MockEventSource.instances[0].readyState).toBe(2); // closed
  });

  it('reconnects on SSE error with exponential backoff', async () => {
    vi.useFakeTimers();

    const { result } = renderHook(() => useNotificationsCounter());

    await vi.advanceTimersByTimeAsync(10);
    expect(MockEventSource.instances).toHaveLength(1);

    act(() => {
      MockEventSource.instances[0].fireError();
    });

    expect(result.current.connectionState).toBe('reconnecting');

    // Wait initial backoff (1s)
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    expect(MockEventSource.instances).toHaveLength(2); // reconnected

    vi.useRealTimers();
  });

  it('stops reconnecting after max attempts', async () => {
    vi.useFakeTimers();

    const { result } = renderHook(() => useNotificationsCounter());

    await vi.advanceTimersByTimeAsync(10);

    // Trigger 5 errors with full backoff between each
    for (let i = 0; i < 5; i++) {
      const currentInstance = MockEventSource.instances[MockEventSource.instances.length - 1];
      act(() => {
        currentInstance.fireError();
      });
      const backoff = Math.min(1000 * Math.pow(2, i), 16000);
      act(() => {
        vi.advanceTimersByTime(backoff);
      });
    }

    // 6th error → no more reconnect
    const lastInstance = MockEventSource.instances[MockEventSource.instances.length - 1];
    act(() => {
      lastInstance.fireError();
    });

    expect(result.current.connectionState).toBe('closed');
    expect(result.current.error?.message).toContain('max reconnect attempts');

    vi.useRealTimers();
  });

  it('error during fetch sets error state', async () => {
    globalThis.fetch = vi.fn().mockRejectedValue(new Error('Network down')) as typeof fetch;

    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(result.current.error?.message).toBe('Network down');
    });
  });

  it('non-401 fetch errors are captured as error state', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: false,
      status: 500,
    }) as typeof fetch;

    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(result.current.error?.message).toContain('500');
    });

    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('uses withCredentials on EventSource', async () => {
    renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(MockEventSource.instances).toHaveLength(1);
    });

    expect(MockEventSource.instances[0].withCredentials).toBe(true);
  });

  it('initial count of 0 is preserved', async () => {
    globalThis.fetch = vi.fn().mockResolvedValue({
      ok: true,
      json: async () => ({ count: 0 }),
    }) as typeof fetch;

    const { result } = renderHook(() => useNotificationsCounter());

    await waitFor(() => {
      expect(MockEventSource.instances).toHaveLength(1);
    });

    expect(result.current.count).toBe(0);
  });
});
