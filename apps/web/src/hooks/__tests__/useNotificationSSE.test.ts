/**
 * useNotificationSSE Hook Tests (Issue #4425)
 *
 * Tests for SSE real-time notification hook:
 * - Connection lifecycle (mount/unmount)
 * - Message handling (addNotification on SSE message)
 * - Reconnection with exponential backoff
 * - Max reconnect attempts (5)
 * - Disabled state
 * - Malformed JSON handling
 *
 * Coverage target: 85%+
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useNotificationSSE } from '../useNotificationSSE';

// ============================================================================
// EventSource Mock
// ============================================================================

class MockEventSource {
  static instances: MockEventSource[] = [];

  url: string;
  withCredentials: boolean;
  readyState: number = 0; // CONNECTING
  onopen: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  close = vi.fn();

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  // Helper to simulate events
  simulateOpen() {
    this.readyState = 1; // OPEN
    this.onopen?.(new Event('open'));
  }

  simulateMessage(data: string) {
    this.onmessage?.(new MessageEvent('message', { data }));
  }

  simulateError() {
    this.onerror?.(new Event('error'));
  }
}

// ============================================================================
// Store Mock
// ============================================================================

const mockAddNotification = vi.fn();
const mockFetchUnreadCount = vi.fn();

vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn((selector: (state: unknown) => unknown) => {
    const state = {
      addNotification: mockAddNotification,
      fetchUnreadCount: mockFetchUnreadCount,
    };
    return selector(state);
  }),
}));

// ============================================================================
// Tests
// ============================================================================

describe('useNotificationSSE', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    MockEventSource.instances = [];
    // @ts-expect-error - mock global EventSource
    globalThis.EventSource = MockEventSource;
  });

  afterEach(() => {
    vi.useRealTimers();
    // @ts-expect-error - restore global
    delete globalThis.EventSource;
  });

  it('connects to /api/v1/notifications/stream on mount', () => {
    renderHook(() => useNotificationSSE());

    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe('/api/v1/notifications/stream');
    expect(MockEventSource.instances[0].withCredentials).toBe(true);
  });

  it('calls addNotification on incoming SSE message', () => {
    renderHook(() => useNotificationSSE());

    const eventSource = MockEventSource.instances[0];
    eventSource.simulateOpen();

    const notification = {
      id: '550e8400-e29b-41d4-a716-446655440000',
      userId: '550e8400-e29b-41d4-a716-446655440001',
      type: 'pdf_upload_completed',
      severity: 'success',
      title: 'PDF Ready',
      message: 'Your document is ready',
      isRead: false,
      createdAt: '2026-02-15T10:00:00Z',
    };

    eventSource.simulateMessage(JSON.stringify(notification));

    expect(mockAddNotification).toHaveBeenCalledWith(notification);
  });

  it('closes EventSource on unmount', () => {
    const { unmount } = renderHook(() => useNotificationSSE());

    const eventSource = MockEventSource.instances[0];
    eventSource.simulateOpen();

    unmount();

    expect(eventSource.close).toHaveBeenCalled();
  });

  it('reconnects with exponential backoff on error (1s, 2s, 4s...)', () => {
    renderHook(() => useNotificationSSE());

    const firstInstance = MockEventSource.instances[0];
    firstInstance.simulateOpen();

    // First error → reconnect after 1s
    firstInstance.simulateError();
    expect(MockEventSource.instances).toHaveLength(1); // Not yet reconnected

    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(2); // Reconnected

    // Second error → reconnect after 2s
    MockEventSource.instances[1].simulateError();
    act(() => { vi.advanceTimersByTime(1999); });
    expect(MockEventSource.instances).toHaveLength(2); // Not yet
    act(() => { vi.advanceTimersByTime(1); });
    expect(MockEventSource.instances).toHaveLength(3); // Reconnected at 2s

    // Third error → reconnect after 4s
    MockEventSource.instances[2].simulateError();
    act(() => { vi.advanceTimersByTime(3999); });
    expect(MockEventSource.instances).toHaveLength(3);
    act(() => { vi.advanceTimersByTime(1); });
    expect(MockEventSource.instances).toHaveLength(4); // Reconnected at 4s
  });

  it('stops reconnecting after 5 attempts', () => {
    renderHook(() => useNotificationSSE());

    // Trigger 5 errors with reconnections
    for (let attempt = 0; attempt < 5; attempt++) {
      const instance = MockEventSource.instances[attempt];
      instance.simulateError();
      const delay = 1000 * Math.pow(2, attempt);
      act(() => { vi.advanceTimersByTime(delay); });
    }

    // 5 reconnect attempts = 6 total instances (1 initial + 5 reconnects)
    expect(MockEventSource.instances).toHaveLength(6);

    // 6th error → no more reconnection
    MockEventSource.instances[5].simulateError();
    act(() => { vi.advanceTimersByTime(60000); }); // Wait a long time
    expect(MockEventSource.instances).toHaveLength(6); // No new instance
  });

  it('does not connect when enabled: false', () => {
    renderHook(() => useNotificationSSE({ enabled: false }));

    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('ignores malformed JSON messages', () => {
    renderHook(() => useNotificationSSE());

    const eventSource = MockEventSource.instances[0];
    eventSource.simulateOpen();

    // Send malformed JSON - should not throw or call addNotification
    eventSource.simulateMessage('not valid json {{{');

    expect(mockAddNotification).not.toHaveBeenCalled();
  });

  it('resets reconnect counter on successful connection', () => {
    renderHook(() => useNotificationSSE());

    // First error → reconnect
    MockEventSource.instances[0].simulateError();
    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(2);

    // Successfully connect
    MockEventSource.instances[1].simulateOpen();

    // Another error → should restart backoff at 1s (not 2s)
    MockEventSource.instances[1].simulateError();
    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(3); // Reconnected at 1s
  });
});
