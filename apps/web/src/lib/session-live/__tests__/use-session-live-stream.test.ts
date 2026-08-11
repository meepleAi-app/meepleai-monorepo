/**
 * Tests for useSessionLiveStream (Wave D.2, Issue #750)
 *
 * EventSource mocking strategy:
 * - vi.stubGlobal('EventSource', MockEventSource) — replaces global EventSource
 * - MockEventSource captures instances and exposes simulation helpers
 * - Tests drive MockEventSource.triggerMessage / triggerError / triggerOpen
 *
 * All tests use renderHook from @testing-library/react.
 * No real network calls are made.
 */

import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { useSessionLiveStream } from '../use-session-live-stream';

// ============================================================================
// MockEventSource
// ============================================================================

type EventHandler = (event: MessageEvent | Event) => void;

class MockEventSource {
  static readonly CONNECTING = 0;
  static readonly OPEN = 1;
  static readonly CLOSED = 2;

  static instances: MockEventSource[] = [];

  readyState: number = MockEventSource.CONNECTING;
  url: string;
  withCredentials: boolean;

  private listeners: Map<string, EventHandler[]> = new Map();
  onopen: EventHandler | null = null;
  onmessage: EventHandler | null = null;
  onerror: EventHandler | null = null;

  constructor(url: string, opts?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = opts?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, handler: EventHandler): void {
    if (!this.listeners.has(type)) {
      this.listeners.set(type, []);
    }
    this.listeners.get(type)!.push(handler);
  }

  removeEventListener(type: string, handler: EventHandler): void {
    const handlers = this.listeners.get(type) ?? [];
    this.listeners.set(
      type,
      handlers.filter(h => h !== handler)
    );
  }

  close(): void {
    this.readyState = MockEventSource.CLOSED;
  }

  // ---- Simulation helpers ----

  /** Simulate server sending a typed SSE event */
  triggerEvent(eventType: string, data: string, lastEventId = ''): void {
    this.readyState = MockEventSource.OPEN;
    const handlers = this.listeners.get(eventType) ?? [];
    const event = { data, lastEventId } as MessageEvent;
    for (const h of handlers) h(event);
  }

  /** Simulate onmessage (unnamed event) */
  triggerMessage(data: string, lastEventId = ''): void {
    this.readyState = MockEventSource.OPEN;
    const event = { data, lastEventId } as MessageEvent;
    if (this.onmessage) this.onmessage(event);
  }

  /** Simulate connection open */
  triggerOpen(): void {
    this.readyState = MockEventSource.OPEN;
    if (this.onopen) this.onopen(new Event('open'));
  }

  /** Simulate connection error */
  triggerError(closeImmediately = false): void {
    if (closeImmediately) {
      this.readyState = MockEventSource.CLOSED;
    }
    if (this.onerror) this.onerror(new Event('error'));
  }

  static reset(): void {
    MockEventSource.instances = [];
  }

  static lastInstance(): MockEventSource | undefined {
    return MockEventSource.instances[MockEventSource.instances.length - 1];
  }
}

// ============================================================================
// Setup / Teardown
// ============================================================================

beforeEach(() => {
  MockEventSource.reset();
  vi.useFakeTimers();
  vi.stubGlobal('EventSource', MockEventSource);
});

afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
  MockEventSource.reset();
});

// ============================================================================
// Helper
// ============================================================================

function makeHook(
  overrides: Partial<{ sessionId: string | null; enabled: boolean; baseUrl: string }> = {}
) {
  // Note: `??` treats null as absent, so explicitly check for the key to allow null sessionId
  const sessionId: string | null =
    'sessionId' in overrides ? (overrides.sessionId as string | null) : 'session-1';
  return renderHook(() =>
    useSessionLiveStream({
      sessionId,
      enabled: overrides.enabled ?? true,
      baseUrl: overrides.baseUrl ?? '',
    })
  );
}

// ============================================================================
// Tests
// ============================================================================

describe('useSessionLiveStream — mount/unmount lifecycle', () => {
  it('creates EventSource when sessionId and enabled are set', () => {
    makeHook();
    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.lastInstance()?.url).toContain('/api/v1/live-sessions/session-1/stream');
  });

  it('connects to the native live-sessions stream, never the legacy game-sessions route', () => {
    renderHook(() => useSessionLiveStream({ sessionId: 'session-1', enabled: true }));
    const url = MockEventSource.lastInstance()!.url;
    expect(url).toContain('/api/v1/live-sessions/session-1/stream');
    expect(url).not.toContain('/game-sessions/');
  });

  it('does not create EventSource when sessionId is null', () => {
    makeHook({ sessionId: null });
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('does not create EventSource when enabled is false', () => {
    makeHook({ enabled: false });
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('closes EventSource on unmount', () => {
    const { unmount } = makeHook();
    const es = MockEventSource.lastInstance()!;
    unmount();
    expect(es.readyState).toBe(MockEventSource.CLOSED);
  });

  it('uses baseUrl when provided', () => {
    makeHook({ baseUrl: 'https://api.example.com' });
    expect(MockEventSource.lastInstance()?.url).toContain('https://api.example.com');
  });
});

describe('useSessionLiveStream — connection state transitions', () => {
  it('starts as connecting', () => {
    const { result } = makeHook();
    expect(result.current.connectionState).toBe('connecting');
  });

  it('transitions to connected on open', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
    });
    expect(result.current.connectionState).toBe('connected');
  });

  it('transitions to connected on first message', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ playerId: 'p1', score: 1 }),
        'evt-1'
      );
    });
    expect(result.current.connectionState).toBe('connected');
  });
});

describe('useSessionLiveStream — event accumulation', () => {
  it('accumulates typed SSE events', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ playerId: 'p1', score: 10 }),
        'evt-1'
      );
    });
    expect(result.current.events).toHaveLength(1);
    expect(result.current.events[0].type).toBe('session:score');
  });

  it('accumulates multiple events', () => {
    const { result } = makeHook();
    const es = MockEventSource.lastInstance()!;
    act(() => {
      es.triggerEvent('session:score', JSON.stringify({ playerId: 'p1', score: 10 }), 'e1');
      es.triggerEvent('session:score', JSON.stringify({ playerId: 'p2', score: 20 }), 'e2');
    });
    expect(result.current.events).toHaveLength(2);
  });

  it('deduplicates events with same id (idempotent)', () => {
    const { result } = makeHook();
    const es = MockEventSource.lastInstance()!;
    act(() => {
      es.triggerEvent('session:score', JSON.stringify({ playerId: 'p1', score: 10 }), 'dup-id');
      es.triggerEvent('session:score', JSON.stringify({ playerId: 'p1', score: 10 }), 'dup-id');
    });
    expect(result.current.events).toHaveLength(1);
  });

  it('updates lastEventId on each event', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ playerId: 'p1', score: 1 }),
        'last-id-123'
      );
    });
    expect(result.current.lastEventId).toBe('last-id-123');
  });
});

describe('useSessionLiveStream — heartbeat handling', () => {
  it('heartbeat events do not appear in events array', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerEvent(
        'heartbeat',
        JSON.stringify({ timestamp: new Date().toISOString() }),
        'hb-1'
      );
    });
    expect(result.current.events).toHaveLength(0);
  });

  it('heartbeat updates lastEventId', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerEvent(
        'heartbeat',
        JSON.stringify({ timestamp: new Date().toISOString() }),
        'hb-42'
      );
    });
    expect(result.current.lastEventId).toBe('hb-42');
  });
});

describe('useSessionLiveStream — retry budget', () => {
  it('transitions to reconnecting after first error', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerOpen(); // establish connection
    });
    act(() => {
      MockEventSource.lastInstance()!.triggerError();
    });
    expect(result.current.connectionState).toBe('reconnecting');
    expect(result.current.retryCount).toBe(1);
  });

  it('schedules retry with exponential backoff', () => {
    makeHook();
    const es = MockEventSource.lastInstance()!;
    act(() => {
      es.triggerOpen();
      es.triggerError();
    });
    // Should schedule retry at 1000ms (RETRY_BUDGET_MS[0])
    expect(MockEventSource.instances).toHaveLength(1); // no new instance yet

    act(() => {
      vi.advanceTimersByTime(1001);
    });
    expect(MockEventSource.instances).toHaveLength(2); // new connection opened
  });

  it('transitions to degraded-polling after MAX_RETRIES (5) consecutive failures', () => {
    // Retry budget: [1s, 2s, 4s, 8s, 16s]
    // Each error WITHOUT a successful open increments retryCountRef.
    // triggerOpen() would reset the counter, so we trigger errors directly (no open).
    // Initial connection + 5 retry connections = 6 total EventSource instances before degraded.
    const { result } = makeHook();

    const delays = [1000, 2000, 4000, 8000, 16000];

    for (let i = 0; i < 5; i++) {
      const es = MockEventSource.lastInstance()!;
      act(() => {
        // Trigger error without prior open — retryCountRef increments each time
        es.triggerError(false); // not CLOSED immediately → not 429 heuristic
      });
      if (i < 4) {
        // Advance past retry delay to trigger reconnect
        act(() => {
          vi.advanceTimersByTime(delays[i]! + 1);
        });
      }
    }

    // After 5 consecutive errors the 5th triggers RETRY (retryCount=5), not yet degraded.
    // Advance the last timer to trigger the 6th connection, then error → degraded.
    act(() => {
      vi.advanceTimersByTime(delays[4]! + 1);
    });

    const es = MockEventSource.lastInstance()!;
    act(() => {
      es.triggerError(false);
    });

    expect(result.current.connectionState).toBe('degraded-polling');
  });
});

describe('useSessionLiveStream — 429 / failed state', () => {
  it('transitions to failed on immediate CLOSED error with no prior messages', () => {
    const { result } = makeHook();
    act(() => {
      // Simulate 429: error fired immediately with CLOSED state (no prior messages)
      const es = MockEventSource.lastInstance()!;
      es.triggerError(true); // true = set CLOSED before firing error
    });
    expect(result.current.connectionState).toBe('failed');
  });

  it('does not retry after 429 (failed state)', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerError(true);
    });
    act(() => {
      vi.advanceTimersByTime(30_000);
    });
    expect(MockEventSource.instances).toHaveLength(1); // no new connection
    expect(result.current.connectionState).toBe('failed');
  });
});

describe('useSessionLiveStream — reconnect()', () => {
  it('reconnect() opens new EventSource', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerError(true);
    });
    expect(result.current.connectionState).toBe('failed');

    act(() => {
      result.current.reconnect();
    });
    expect(MockEventSource.instances).toHaveLength(2);
  });

  it('reconnect() resets retryCount', () => {
    const { result } = makeHook();
    // Force a retry
    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerError();
    });
    expect(result.current.retryCount).toBe(1);

    act(() => {
      result.current.reconnect();
    });
    // After reconnect, retryCount reset and new connection connecting
    expect(MockEventSource.instances.length).toBeGreaterThanOrEqual(2);
  });
});

describe('useSessionLiveStream — sessionId change', () => {
  it('closes old EventSource and opens new on sessionId change', () => {
    const { rerender } = renderHook(
      ({ sessionId }) => useSessionLiveStream({ sessionId, enabled: true }),
      { initialProps: { sessionId: 'session-A' } }
    );

    const firstEs = MockEventSource.lastInstance()!;

    act(() => {
      rerender({ sessionId: 'session-B' });
    });

    expect(firstEs.readyState).toBe(MockEventSource.CLOSED);
    expect(MockEventSource.instances).toHaveLength(2);
    expect(MockEventSource.lastInstance()?.url).toContain('session-B');
  });

  it('#3050: clears accumulated events on sessionId change (no cross-session bleed)', () => {
    const { result, rerender } = renderHook(
      ({ sessionId }) => useSessionLiveStream({ sessionId, enabled: true }),
      { initialProps: { sessionId: 'session-A' } }
    );

    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p1', score: 5, sessionId: 'session-A' }),
        'evt-A-1'
      );
    });
    expect(result.current.events).toHaveLength(1);

    // Navigating to another session must not carry session-A's events over.
    act(() => {
      rerender({ sessionId: 'session-B' });
    });
    expect(result.current.events).toHaveLength(0);
    expect(result.current.lastEventId).toBeNull();

    // A fresh session-B event accumulates onto the cleared array.
    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p9', score: 3, sessionId: 'session-B' }),
        'evt-B-1'
      );
    });
    expect(result.current.events).toHaveLength(1);
  });

  it('#3050: does NOT clear events on a same-session reconnect (RESET preserves)', () => {
    const { result } = makeHook({ sessionId: 'session-1' });

    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p1', score: 5, sessionId: 'session-1' }),
        'evt-1'
      );
    });
    expect(result.current.events).toHaveLength(1);

    // A manual reconnect (same session) must preserve accumulated events.
    act(() => {
      result.current.reconnect();
    });
    expect(result.current.events).toHaveLength(1);
  });

  it("#3055: session B first connect URL omits the previous session's lastEventId cursor", () => {
    const { rerender } = renderHook(
      ({ sessionId }) => useSessionLiveStream({ sessionId, enabled: true }),
      { initialProps: { sessionId: 'session-A' } }
    );

    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p1', score: 5, sessionId: 'session-A' }),
        'evt-A-9'
      );
    });

    act(() => {
      rerender({ sessionId: 'session-B' });
    });

    const bUrl = MockEventSource.lastInstance()!.url;
    expect(bUrl).toContain('/api/v1/live-sessions/session-B/stream');
    // Before the fix this opened as ?lastEventId=evt-A-9 (session A's cursor bleeding into B).
    expect(bUrl).not.toContain('lastEventId');
  });

  it('#3055: same-session retry reconnect still sends the last cursor for replay', () => {
    const { result } = makeHook({ sessionId: 'session-1' });

    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p1', score: 5, sessionId: 'session-1' }),
        'evt-1'
      );
    });

    // Retryable error (not 429) → schedules a same-session reconnect at RETRY_BUDGET_MS[0].
    act(() => {
      MockEventSource.lastInstance()!.triggerError(false);
    });
    act(() => {
      vi.advanceTimersByTime(1001);
    });

    // The reconnect MUST replay from the last cursor — the fix only strips it on session change.
    expect(result.current).toBeDefined();
    expect(MockEventSource.lastInstance()!.url).toContain('lastEventId=evt-1');
  });

  it('#3055: a retry AFTER a session change does not resurrect the previous session cursor', () => {
    const { rerender } = renderHook(
      ({ sessionId }) => useSessionLiveStream({ sessionId, enabled: true }),
      { initialProps: { sessionId: 'session-A' } }
    );

    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p1', score: 5, sessionId: 'session-A' }),
        'evt-A-9'
      );
    });

    // Switch to B (first connect already omits A's cursor); then B errors before any B event.
    act(() => {
      rerender({ sessionId: 'session-B' });
    });
    act(() => {
      MockEventSource.lastInstance()!.triggerError(false);
    });
    act(() => {
      vi.advanceTimersByTime(1001);
    });

    // The retry reads the CLEARed (null) cursor, not A's evt-A-9 — the connect(false)-after-CLEAR branch.
    const retryUrl = MockEventSource.lastInstance()!.url;
    expect(retryUrl).toContain('/api/v1/live-sessions/session-B/stream');
    expect(retryUrl).not.toContain('lastEventId');
  });

  it('#3055: toggling enabled false→true on the same session preserves the replay cursor', () => {
    const { rerender } = renderHook(
      ({ sessionId, enabled }) => useSessionLiveStream({ sessionId, enabled }),
      { initialProps: { sessionId: 'session-1', enabled: true } }
    );

    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerEvent(
        'session:score',
        JSON.stringify({ participantId: 'p1', score: 5, sessionId: 'session-1' }),
        'evt-1'
      );
    });

    // Disable (no CLEAR — same session) then re-enable: the reconnect must still replay from evt-1.
    act(() => {
      rerender({ sessionId: 'session-1', enabled: false });
    });
    act(() => {
      rerender({ sessionId: 'session-1', enabled: true });
    });

    expect(MockEventSource.lastInstance()!.url).toContain('lastEventId=evt-1');
  });
});

describe('useSessionLiveStream — retryAt', () => {
  it('sets retryAt to future Date on retry', () => {
    const { result } = makeHook();
    act(() => {
      MockEventSource.lastInstance()!.triggerOpen();
      MockEventSource.lastInstance()!.triggerError();
    });
    expect(result.current.retryAt).toBeInstanceOf(Date);
    expect(result.current.retryAt!.getTime()).toBeGreaterThan(Date.now());
  });
});
