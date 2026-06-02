/**
 * useLiveEvents Hook Tests — Task 3.3
 * Issue #1718: F4.1 Monitor LiveEventLog
 *
 * TDD: 8 scenarios covering backfill, SSE lifecycle, buffer management,
 * exponential backoff, pause/resume, refetch, and cleanup.
 *
 * Coverage target: ≥85% of use-live-events.ts
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import type { DomainEventDto } from '../live-event-types';
import { useLiveEvents } from '../use-live-events';

// ============================================================================
// Mock EventSource
// ============================================================================

class MockEventSource {
  static instances: MockEventSource[] = [];

  url: string;
  withCredentials: boolean;
  readyState: number = 0; // CONNECTING
  onopen: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  closed = false;

  constructor(url: string, init?: EventSourceInit) {
    this.url = url;
    this.withCredentials = init?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  close() {
    this.closed = true;
    this.readyState = 2; // CLOSED
  }

  // --- Test helpers ---

  simulateOpen() {
    this.readyState = 1; // OPEN
    this.onopen?.(new Event('open'));
  }

  simulateMessage(data: string) {
    this.onmessage?.(new MessageEvent('message', { data }));
  }

  simulateError() {
    this.readyState = 2;
    this.onerror?.(new Event('error'));
  }

  static clear() {
    MockEventSource.instances = [];
  }
}

// ============================================================================
// Mock fetch helpers
// ============================================================================

function makeDomainEventDto(overrides: Partial<DomainEventDto> = {}): DomainEventDto {
  return {
    id: `id-${Math.random().toString(36).slice(2)}`,
    eventId: `eid-${Math.random().toString(36).slice(2)}`,
    eventType: 'agent.created',
    aggregateType: 'Agent',
    aggregateId: 'agg-001',
    userId: 'user-001',
    payloadJson: '{}',
    payloadVersion: 1,
    occurredAt: new Date().toISOString(),
    loggedAt: new Date().toISOString(),
    ...overrides,
  };
}

function mockFetchSuccess(events: DomainEventDto[]) {
  vi.mocked(global.fetch).mockResolvedValueOnce({
    ok: true,
    json: async () => ({ items: events, total: events.length }),
  } as unknown as Response);
}

function mockFetchFailure() {
  vi.mocked(global.fetch).mockRejectedValueOnce(new Error('Network error'));
}

// ============================================================================
// Setup / Teardown
// ============================================================================

beforeEach(() => {
  vi.clearAllMocks();
  // shouldAdvanceTime keeps real microtasks/promises responsive so `waitFor`
  // can observe React state transitions while still letting us advance timers
  // manually (vi.advanceTimersByTime) for backoff tests.
  vi.useFakeTimers({ shouldAdvanceTime: true });
  MockEventSource.clear();
  global.fetch = vi.fn();
  // @ts-expect-error -- mock global EventSource
  globalThis.EventSource = MockEventSource;
});

afterEach(() => {
  vi.useRealTimers();
  // @ts-expect-error -- cleanup mock
  delete globalThis.EventSource;
});

// ============================================================================
// Tests
// ============================================================================

describe('useLiveEvents', () => {
  /**
   * 1. Initial backfill populates events
   */
  it('useLiveEvents_Initial_BackfillPopulatesEvents', async () => {
    const backfillEvents = Array.from({ length: 5 }, () => makeDomainEventDto());
    mockFetchSuccess(backfillEvents);

    const { result } = renderHook(() => useLiveEvents());

    // Initially loading
    expect(result.current.isLoading).toBe(true);

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.events).toHaveLength(5);
    // No error
    expect(result.current.error).toBeNull();
  });

  /**
   * 2. SSE message prepends to top of events list
   */
  it('useLiveEvents_SseMessage_AppendsToTop', async () => {
    const backfillEvents = Array.from({ length: 3 }, () => makeDomainEventDto());
    mockFetchSuccess(backfillEvents);

    const { result } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.events).toHaveLength(3);

    // Simulate SSE open then a new event message
    const es = MockEventSource.instances[0];
    act(() => {
      es.simulateOpen();
    });

    const newEvent = makeDomainEventDto({ eventType: 'kb.doc.indexed', id: 'newest-id' });

    act(() => {
      es.simulateMessage(JSON.stringify(newEvent));
    });

    // Newest event at index 0
    expect(result.current.events).toHaveLength(4);
    expect(result.current.events[0].id).toBe('newest-id');
  });

  /**
   * 3. maxBuffer drops oldest events on overflow
   */
  it('useLiveEvents_MaxBuffer_DropsOldest', async () => {
    const backfillEvents = [
      makeDomainEventDto({ id: 'old-1' }),
      makeDomainEventDto({ id: 'old-2' }),
      makeDomainEventDto({ id: 'old-3' }),
    ];
    mockFetchSuccess(backfillEvents);

    const { result } = renderHook(() => useLiveEvents({ maxBuffer: 3 }));

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    expect(result.current.events).toHaveLength(3);

    const es = MockEventSource.instances[0];
    act(() => {
      es.simulateOpen();
    });

    // Add a 4th event — oldest (last in array) should be dropped
    const newEvent = makeDomainEventDto({ id: 'newest' });
    act(() => {
      es.simulateMessage(JSON.stringify(newEvent));
    });

    expect(result.current.events).toHaveLength(3);
    // Newest is at index 0
    expect(result.current.events[0].id).toBe('newest');
    // 'old-3' (was last in DESC list) is dropped
    expect(result.current.events.find(e => e.id === 'old-3')).toBeUndefined();
  });

  /**
   * 4. Last-Event-ID from backfill is included in SSE connection
   *    (either via URL param or the EventSource constructor URL)
   */
  it('useLiveEvents_LastEventId_SetOnReconnect', async () => {
    const latestEvent = makeDomainEventDto({ id: 'latest-guid' });
    const olderEvent = makeDomainEventDto({ id: 'older-guid' });
    // backfill returns newest-first (DESC): [latest, older]
    mockFetchSuccess([latestEvent, olderEvent]);

    renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(MockEventSource.instances).toHaveLength(1);
    });

    // After backfill the EventSource URL should include the latest event id
    // The hook passes it as a query param: ?lastEventId=latest-guid
    const esUrl = MockEventSource.instances[0].url;
    expect(esUrl).toContain('latest-guid');
  });

  /**
   * 5. Exponential backoff starts after BACKOFF_THRESHOLD (3) consecutive failures
   */
  it('useLiveEvents_ExponentialBackoff_AfterFailures', async () => {
    mockFetchSuccess([]);

    const { result } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    const es1 = MockEventSource.instances[0];

    // First failure — below BACKOFF_THRESHOLD, schedules 500 ms reconnect
    act(() => {
      es1.simulateError();
    });

    // Advance 500 ms to let the throttled reconnect fire → instance #2
    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(MockEventSource.instances.length).toBeGreaterThanOrEqual(2);

    // Second failure
    act(() => {
      MockEventSource.instances[MockEventSource.instances.length - 1].simulateError();
    });

    // Advance 500 ms to let the throttled reconnect fire → instance #3
    act(() => {
      vi.advanceTimersByTime(500);
    });
    expect(MockEventSource.instances.length).toBeGreaterThanOrEqual(3);

    // Third failure — consecutiveFailures reaches BACKOFF_THRESHOLD (3),
    // so next reconnect uses exponential backoff starting at 1000 ms
    act(() => {
      MockEventSource.instances[MockEventSource.instances.length - 1].simulateError();
    });

    // Record the instance count BEFORE advancing past the backoff delay
    const countBeforeBackoff = MockEventSource.instances.length;

    // Advancing less than 1 s should NOT create a new instance
    act(() => {
      vi.advanceTimersByTime(999);
    });
    expect(MockEventSource.instances.length).toBe(countBeforeBackoff);

    // Advancing past 1 s SHOULD create a new instance (first backoff fires)
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(MockEventSource.instances.length).toBeGreaterThan(countBeforeBackoff);
  });

  /**
   * 6. Cleanup on unmount: EventSource.close() is called
   */
  it('useLiveEvents_CleanupOnUnmount_ClosesEventSource', async () => {
    mockFetchSuccess([]);

    const { result: _result, unmount } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(MockEventSource.instances).toHaveLength(1);
    });

    const es = MockEventSource.instances[0];
    expect(es.closed).toBe(false);

    unmount();

    expect(es.closed).toBe(true);
  });

  /**
   * 7. Pause stops stream — new SSE events do not update state
   */
  it('useLiveEvents_Pause_StopsStream', async () => {
    const backfillEvents = [makeDomainEventDto({ id: 'initial' })];
    mockFetchSuccess(backfillEvents);

    const { result } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    const es = MockEventSource.instances[0];
    act(() => {
      es.simulateOpen();
    });

    expect(result.current.isStreaming).toBe(true);

    // Pause
    act(() => {
      result.current.pause();
    });

    expect(result.current.isStreaming).toBe(false);

    const snapshotLength = result.current.events.length;

    // Simulate an SSE message arriving after pause — should be ignored
    act(() => {
      es.simulateMessage(JSON.stringify(makeDomainEventDto({ id: 'after-pause' })));
    });

    expect(result.current.events).toHaveLength(snapshotLength);
    expect(result.current.events.find(e => e.id === 'after-pause')).toBeUndefined();
  });

  /**
   * 8. Reconnect below BACKOFF_THRESHOLD uses 500 ms throttle (not synchronous)
   */
  it('useLiveEvents_ImmediateReconnect_Uses500msThrottle', async () => {
    mockFetchSuccess([]);

    const { result } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    const es1 = MockEventSource.instances[0];
    const countBefore = MockEventSource.instances.length;

    // First failure — below BACKOFF_THRESHOLD, so should schedule at 500 ms
    act(() => {
      es1.simulateError();
    });

    // No new instance before 500 ms
    act(() => {
      vi.advanceTimersByTime(499);
    });
    expect(MockEventSource.instances.length).toBe(countBefore);

    // Instance appears after 500 ms
    act(() => {
      vi.advanceTimersByTime(1);
    });
    expect(MockEventSource.instances.length).toBeGreaterThan(countBefore);
  });

  /**
   * 9. Resume sets isStreaming=true synchronously (optimistic) before onopen fires
   */
  it('useLiveEvents_Resume_SetsIsStreamingOptimistically', async () => {
    const backfillEvents = [makeDomainEventDto({ id: 'initial' })];
    mockFetchSuccess(backfillEvents);

    const { result } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
    });

    const es = MockEventSource.instances[0];
    act(() => {
      es.simulateOpen();
    });

    // Pause first
    act(() => {
      result.current.pause();
    });
    expect(result.current.isStreaming).toBe(false);

    // Resume — isStreaming must flip to true immediately, before onopen fires
    act(() => {
      result.current.resume();
    });
    expect(result.current.isStreaming).toBe(true);
  });

  /**
   * 10. Refetch resets events and re-runs backfill + attaches a new SSE
   */
  it('useLiveEvents_Refetch_ResetsAndBackfills', async () => {
    // First backfill
    const firstBatch = [
      makeDomainEventDto({ id: 'first-1' }),
      makeDomainEventDto({ id: 'first-2' }),
    ];
    mockFetchSuccess(firstBatch);

    const { result } = renderHook(() => useLiveEvents());

    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
      expect(result.current.events).toHaveLength(2);
    });

    const initialEsCount = MockEventSource.instances.length;
    const initialFetchCount = vi.mocked(global.fetch).mock.calls.length;

    // Second backfill for refetch
    const secondBatch = [makeDomainEventDto({ id: 'refetched-1' })];
    mockFetchSuccess(secondBatch);

    act(() => {
      result.current.refetch();
    });

    // Events should be cleared immediately (or after refetch settles)
    await waitFor(() => {
      expect(result.current.isLoading).toBe(false);
      expect(result.current.events).toHaveLength(1);
      expect(result.current.events[0].id).toBe('refetched-1');
    });

    // A new fetch was issued
    expect(vi.mocked(global.fetch).mock.calls.length).toBeGreaterThan(initialFetchCount);

    // A new EventSource was created
    expect(MockEventSource.instances.length).toBeGreaterThan(initialEsCount);
  });
});
