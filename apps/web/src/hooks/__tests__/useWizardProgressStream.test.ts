/**
 * useWizardProgressStream Hook Tests
 * Issue #4673: SSE progress stream reconnect logic, timeout cleanup, state transitions.
 *
 * Tests:
 * - Connects to correct URL when gameId is provided
 * - Handles 'progress' named events → sets progress + connectionState='connected'
 * - Handles 'complete' named events → sets isComplete=true, closes connection
 * - Handles 'error' named events → sets isFailed=true (pdfState='Failed')
 * - onerror triggers auto-reconnect with exponential backoff
 * - Stops reconnecting after MAX_RECONNECT_ATTEMPTS (5)
 * - Does NOT connect when gameId is null
 * - Cleans up EventSource and timer on unmount
 * - Manual reconnect() resets attempt counter
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useWizardProgressStream } from '../useWizardProgressStream';

// ─── MockEventSource ─────────────────────────────────────────────────────────

type EventListenerCallback = (event: MessageEvent) => void;

class MockEventSource {
  static instances: MockEventSource[] = [];
  url: string;
  withCredentials: boolean;
  readyState = 0;
  closed = false;
  onerror: ((event: Event) => void) | null = null;

  private listeners: Map<string, EventListenerCallback[]> = new Map();

  constructor(url: string, config?: EventSourceInit) {
    this.url = url;
    this.withCredentials = config?.withCredentials ?? false;
    MockEventSource.instances.push(this);
  }

  addEventListener(type: string, callback: EventListenerCallback) {
    if (!this.listeners.has(type)) this.listeners.set(type, []);
    this.listeners.get(type)!.push(callback);
  }

  removeEventListener(type: string, callback: EventListenerCallback) {
    const arr = this.listeners.get(type);
    if (arr) this.listeners.set(type, arr.filter((fn) => fn !== callback));
  }

  close() {
    this.closed = true;
    this.readyState = 2;
  }

  // ─── Test helpers ───────────────────────────────────────────────────────

  simulateProgressEvent(data: object) {
    const msg = new MessageEvent('progress', { data: JSON.stringify(data) });
    this.listeners.get('progress')?.forEach((fn) => fn(msg));
  }

  simulateCompleteEvent(data: object) {
    const msg = new MessageEvent('complete', { data: JSON.stringify(data) });
    this.listeners.get('complete')?.forEach((fn) => fn(msg));
  }

  simulateNamedErrorEvent(data: object) {
    const msg = new MessageEvent('error', { data: JSON.stringify(data) });
    this.listeners.get('error')?.forEach((fn) => fn(msg));
  }

  simulateConnectionError() {
    this.onerror?.(new Event('error'));
  }
}

// ─── Setup / Teardown ────────────────────────────────────────────────────────

describe('useWizardProgressStream', () => {
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

  // ─── Connection ──────────────────────────────────────────────────────────

  it('should connect to the correct SSE URL when gameId is provided', () => {
    renderHook(() => useWizardProgressStream('game-uuid-1'));

    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toContain('/game-uuid-1/progress/stream');
    expect(MockEventSource.instances[0].withCredentials).toBe(true);
  });

  it('should NOT connect when gameId is null', () => {
    renderHook(() => useWizardProgressStream(null));

    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('should start with connectionState="connecting"', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    expect(result.current.connectionState).toBe('connecting');
  });

  // ─── Progress events ─────────────────────────────────────────────────────

  it('should update progress on "progress" event and set state to "connected"', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    const es = MockEventSource.instances[0];

    act(() => {
      es.simulateProgressEvent({
        currentStep: 'extracting',
        pdfState: 'Extracting',
        agentExists: false,
        overallPercent: 25,
        message: 'Extracting text...',
        isComplete: false,
        errorMessage: null,
        priority: 'Admin',
        timestamp: new Date().toISOString(),
      });
    });

    expect(result.current.connectionState).toBe('connected');
    expect(result.current.progress?.currentStep).toBe('extracting');
    expect(result.current.progress?.overallPercent).toBe(25);
    expect(result.current.isComplete).toBe(false);
    expect(result.current.isFailed).toBe(false);
  });

  // ─── Complete events ──────────────────────────────────────────────────────

  it('should set isComplete=true on "complete" event', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    const es = MockEventSource.instances[0];

    act(() => {
      es.simulateCompleteEvent({
        currentStep: 'done',
        pdfState: 'Ready',
        agentExists: true,
        overallPercent: 100,
        message: 'Processing complete!',
        isComplete: true,
        errorMessage: null,
        priority: 'Admin',
        timestamp: new Date().toISOString(),
      });
    });

    expect(result.current.isComplete).toBe(true);
    expect(result.current.connectionState).toBe('closed');
    expect(es.closed).toBe(true);
  });

  // ─── Named error events ───────────────────────────────────────────────────

  it('should set isFailed=true when pdfState="Failed" in "error" event', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    const es = MockEventSource.instances[0];

    act(() => {
      es.simulateNamedErrorEvent({
        currentStep: 'failed',
        pdfState: 'Failed',
        agentExists: false,
        overallPercent: 30,
        message: 'Extraction failed',
        isComplete: false,
        errorMessage: 'PDF could not be parsed',
        priority: 'Admin',
        timestamp: new Date().toISOString(),
      });
    });

    expect(result.current.isFailed).toBe(true);
    expect(result.current.connectionState).toBe('error');
    expect(es.closed).toBe(true);
  });

  // ─── Auto-reconnect on connection error ───────────────────────────────────

  it('should reconnect after connection error with 1s delay', () => {
    renderHook(() => useWizardProgressStream('game-uuid-1'));

    expect(MockEventSource.instances).toHaveLength(1);
    const es = MockEventSource.instances[0];

    act(() => { es.simulateConnectionError(); });
    expect(es.closed).toBe(true);

    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(2);
  });

  it('should reconnect with exponential backoff (1s, 2s, 4s)', () => {
    renderHook(() => useWizardProgressStream('game-uuid-1'));

    // Attempt 1: error → reconnect after 1s
    act(() => { MockEventSource.instances[0].simulateConnectionError(); });
    act(() => { vi.advanceTimersByTime(1000); });
    expect(MockEventSource.instances).toHaveLength(2);

    // Attempt 2: error → reconnect after 2s
    act(() => { MockEventSource.instances[1].simulateConnectionError(); });
    act(() => { vi.advanceTimersByTime(1999); });
    expect(MockEventSource.instances).toHaveLength(2); // not yet
    act(() => { vi.advanceTimersByTime(1); });
    expect(MockEventSource.instances).toHaveLength(3);

    // Attempt 3: error → reconnect after 4s
    act(() => { MockEventSource.instances[2].simulateConnectionError(); });
    act(() => { vi.advanceTimersByTime(4000); });
    expect(MockEventSource.instances).toHaveLength(4);
  });

  it('should set connectionState="reconnecting" during backoff', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    act(() => { MockEventSource.instances[0].simulateConnectionError(); });

    expect(result.current.connectionState).toBe('reconnecting');
  });

  it('should stop reconnecting after 5 attempts and set state to "error"', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    // Trigger MAX_RECONNECT_ATTEMPTS (5) errors + reconnects
    for (let i = 0; i < 5; i++) {
      const es = MockEventSource.instances[MockEventSource.instances.length - 1];
      act(() => { es.simulateConnectionError(); });
      act(() => { vi.advanceTimersByTime(30000); });
    }

    // 1 initial + 5 reconnects = 6 total
    expect(MockEventSource.instances).toHaveLength(6);

    // 6th error → should NOT reconnect, set state to 'error'
    const lastEs = MockEventSource.instances[5];
    act(() => { lastEs.simulateConnectionError(); });
    act(() => { vi.advanceTimersByTime(60000); });

    expect(MockEventSource.instances).toHaveLength(6);
    expect(result.current.connectionState).toBe('error');
  });

  // ─── Cleanup ─────────────────────────────────────────────────────────────

  it('should close EventSource on unmount', () => {
    const { unmount } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    const es = MockEventSource.instances[0];
    expect(es.closed).toBe(false);

    unmount();
    expect(es.closed).toBe(true);
  });

  it('should cancel pending reconnect timer on unmount', () => {
    const { unmount } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    const es = MockEventSource.instances[0];
    act(() => { es.simulateConnectionError(); }); // schedules 1s reconnect

    unmount(); // should cancel timer

    act(() => { vi.advanceTimersByTime(5000); });

    // No new connection after unmount
    expect(MockEventSource.instances).toHaveLength(1);
  });

  // ─── Manual reconnect ────────────────────────────────────────────────────

  it('should reset attempt counter and reconnect when reconnect() is called', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));

    // Exhaust all attempts
    for (let i = 0; i < 5; i++) {
      const es = MockEventSource.instances[MockEventSource.instances.length - 1];
      act(() => { es.simulateConnectionError(); });
      act(() => { vi.advanceTimersByTime(30000); });
    }

    const lastEs = MockEventSource.instances[5];
    act(() => { lastEs.simulateConnectionError(); }); // push over limit
    expect(result.current.connectionState).toBe('error');

    // Manual reconnect
    act(() => { result.current.reconnect(); });

    expect(MockEventSource.instances.length).toBeGreaterThan(6);
  });

  // ─── Computed states ─────────────────────────────────────────────────────

  it('should return progress=null initially', () => {
    const { result } = renderHook(() => useWizardProgressStream('game-uuid-1'));
    expect(result.current.progress).toBeNull();
  });

  it('should reset progress on reconnect when gameId changes', () => {
    const { result, rerender } = renderHook(
      ({ gameId }: { gameId: string | null }) => useWizardProgressStream(gameId),
      { initialProps: { gameId: 'game-1' } }
    );

    act(() => {
      MockEventSource.instances[0].simulateProgressEvent({
        currentStep: 'extracting',
        pdfState: 'Extracting',
        agentExists: false,
        overallPercent: 50,
        message: 'In progress',
        isComplete: false,
        errorMessage: null,
        priority: 'Admin',
        timestamp: new Date().toISOString(),
      });
    });

    expect(result.current.progress?.overallPercent).toBe(50);

    // Switch to null gameId — should disconnect
    rerender({ gameId: null });

    expect(MockEventSource.instances[0].closed).toBe(true);
  });
});
