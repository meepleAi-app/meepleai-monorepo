/**
 * useNotificationSSE Hook Tests (Issue #4425)
 *
 * Tests for SSE real-time notification updates:
 * - Connection lifecycle (connect, close, cleanup)
 * - Message handling (valid JSON → addNotification)
 * - Error handling (reconnect with exponential backoff)
 * - Configuration (enabled flag)
 *
 * Coverage target: ≥85%
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useNotificationSSE } from '../useNotificationSSE';

// ============================================================================
// Mocks
// ============================================================================

const mockAddNotification = vi.fn();
const mockFetchUnreadCount = vi.fn();

vi.mock('@/store/notification/store', () => ({
  useNotificationStore: vi.fn((selector: (state: Record<string, unknown>) => unknown) => {
    const state = {
      addNotification: mockAddNotification,
      fetchUnreadCount: mockFetchUnreadCount,
    };
    return typeof selector === 'function' ? selector(state) : state;
  }),
}));

// EventSource mock
class MockEventSource {
  static instances: MockEventSource[] = [];
  url: string;
  withCredentials: boolean;
  readyState: number = 0; // CONNECTING
  onopen: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  closed = false;

  constructor(url: string, config?: EventSourceInit) {
    this.url = url;
    this.withCredentials = config?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  close() {
    this.closed = true;
    this.readyState = 2; // CLOSED
  }

  // Test helpers
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

describe('useNotificationSSE', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
    MockEventSource.instances = [];
    // @ts-expect-error -- mock global EventSource
    globalThis.EventSource = MockEventSource;
  });

  afterEach(() => {
    vi.useRealTimers();
    // @ts-expect-error -- cleanup mock
    delete globalThis.EventSource;
  });

  it('should connect to /api/v1/notifications/stream on mount', () => {
    renderHook(() => useNotificationSSE());

    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toBe('/api/v1/notifications/stream');
    expect(MockEventSource.instances[0].withCredentials).toBe(true);
  });

  it('should call addNotification() on incoming SSE message', () => {
    renderHook(() => useNotificationSSE());

    const es = MockEventSource.instances[0];
    es.simulateOpen();

    const notification = {
      id: '123e4567-e89b-12d3-a456-426614174000',
      userId: '123e4567-e89b-12d3-a456-426614174001',
      type: 'pdf_upload_completed',
      severity: 'success',
      title: 'PDF Ready',
      message: 'Your PDF has been processed',
      link: null,
      metadata: null,
      isRead: false,
      createdAt: '2026-02-15T10:00:00Z',
      readAt: null,
    };

    es.simulateMessage(JSON.stringify(notification));
    expect(mockAddNotification).toHaveBeenCalledWith(notification);
  });

  it('should close EventSource on unmount (cleanup)', () => {
    const { unmount } = renderHook(() => useNotificationSSE());

    const es = MockEventSource.instances[0];
    expect(es.closed).toBe(false);

    unmount();
    expect(es.closed).toBe(true);
  });

  it('should reconnect with exponential backoff on error (1s, 2s, 4s...)', () => {
    renderHook(() => useNotificationSSE());

    // First connection
    expect(MockEventSource.instances).toHaveLength(1);
    const es1 = MockEventSource.instances[0];
    es1.simulateOpen();

    // Error → should schedule reconnect after 1s
    es1.simulateError();
    expect(es1.closed).toBe(true);

    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(2);

    // Second error → reconnect after 2s
    const es2 = MockEventSource.instances[1];
    es2.simulateError();

    act(() => { vi.advanceTimersByTime(1999); });
    expect(MockEventSource.instances).toHaveLength(2); // Not yet
    act(() => { vi.advanceTimersByTime(1); });
    expect(MockEventSource.instances).toHaveLength(3);

    // Third error → reconnect after 4s
    const es3 = MockEventSource.instances[2];
    es3.simulateError();

    act(() => { vi.advanceTimersByTime(4000); });
    expect(MockEventSource.instances).toHaveLength(4);
  });

  it('should stop reconnecting after 5 attempts', () => {
    renderHook(() => useNotificationSSE());

    // Trigger 5 errors + reconnects (attempt 0..4)
    for (let i = 0; i < 5; i++) {
      const es = MockEventSource.instances[MockEventSource.instances.length - 1];
      es.simulateError();
      act(() => { vi.advanceTimersByTime(30000); }); // Advance past any backoff
    }

    // 1 initial + 5 reconnects = 6
    expect(MockEventSource.instances).toHaveLength(6);

    // 6th error → should NOT reconnect
    const lastEs = MockEventSource.instances[5];
    lastEs.simulateError();
    act(() => { vi.advanceTimersByTime(60000); });

    // Still 6 instances (no new connection)
    expect(MockEventSource.instances).toHaveLength(6);
  });

  it('should not connect when enabled: false', () => {
    renderHook(() => useNotificationSSE({ enabled: false }));

    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('should ignore malformed JSON messages', () => {
    renderHook(() => useNotificationSSE());

    const es = MockEventSource.instances[0];
    es.simulateOpen();

    // Send invalid JSON
    es.simulateMessage('not valid json {{{');
    expect(mockAddNotification).not.toHaveBeenCalled();

    // Send empty string
    es.simulateMessage('');
    expect(mockAddNotification).not.toHaveBeenCalled();
  });

  it('should reset reconnect attempts after successful connection', () => {
    renderHook(() => useNotificationSSE());

    // First error + reconnect
    const es1 = MockEventSource.instances[0];
    es1.simulateError();
    act(() => { vi.advanceTimersByTime(1000); });

    // Second connection succeeds
    const es2 = MockEventSource.instances[1];
    es2.simulateOpen();

    // Error again → should use 1s delay (reset), not 4s
    es2.simulateError();
    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(3);
  });

  it('should clean up reconnect timer on unmount', () => {
    const { unmount } = renderHook(() => useNotificationSSE());

    const es = MockEventSource.instances[0];
    es.simulateError(); // schedules reconnect

    unmount();

    // After unmount, timer shouldn't create new connection
    act(() => { vi.advanceTimersByTime(30000); });
    // Only 1 instance (no reconnect after unmount)
    expect(MockEventSource.instances).toHaveLength(1);
  });
});
