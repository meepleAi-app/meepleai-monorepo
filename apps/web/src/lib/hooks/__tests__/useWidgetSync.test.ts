/**
 * Unit tests for useWidgetSync hook.
 * Phase P2-3/P2-6 — Widget Real-Time Sync.
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';

import { useWidgetSync } from '../useWidgetSync';

// ---- EventSource mock ----

type ESListener = (event: MessageEvent) => void;

class MockEventSource {
  static instances: MockEventSource[] = [];

  url: string;
  withCredentials: boolean;
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  readyState = 0;

  private listeners = new Map<string, ESListener[]>();

  constructor(url: string, opts?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = opts?.withCredentials ?? false;
    MockEventSource.instances.push(this);

    // Simulate async open
    queueMicrotask(() => {
      this.readyState = 1;
      this.onopen?.();
    });
  }

  addEventListener(type: string, listener: ESListener) {
    const existing = this.listeners.get(type) ?? [];
    existing.push(listener);
    this.listeners.set(type, existing);
  }

  removeEventListener(type: string, listener: ESListener) {
    const existing = this.listeners.get(type) ?? [];
    this.listeners.set(
      type,
      existing.filter(l => l !== listener)
    );
  }

  /** Test helper: dispatch a typed event */
  _emit(type: string, data: unknown) {
    const listeners = this.listeners.get(type) ?? [];
    const event = { data: JSON.stringify(data) } as MessageEvent;
    for (const listener of listeners) {
      listener(event);
    }
  }

  close() {
    this.readyState = 2;
  }
}

// ---- Test setup ----

const originalEventSource = globalThis.EventSource;

beforeEach(() => {
  MockEventSource.instances = [];
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  globalThis.EventSource = MockEventSource as any;
  vi.useFakeTimers();
  globalThis.fetch = vi.fn().mockResolvedValue({ ok: true });
});

afterEach(() => {
  globalThis.EventSource = originalEventSource;
  vi.useRealTimers();
  vi.restoreAllMocks();
});

// ---- Helpers ----

const defaultOpts = {
  sessionId: 'session-1',
  toolkitId: 'toolkit-1',
  widgetType: 'ScoreTracker',
  onRemoteUpdate: vi.fn(),
};

describe('useWidgetSync', () => {
  it('connects to SSE when sessionId is provided', async () => {
    renderHook(() => useWidgetSync(defaultOpts));
    await vi.advanceTimersByTimeAsync(0); // flush microtask
    expect(MockEventSource.instances).toHaveLength(1);
    expect(MockEventSource.instances[0].url).toContain('/stream/v2');
  });

  it('does not connect when sessionId is undefined', () => {
    renderHook(() => useWidgetSync({ ...defaultOpts, sessionId: undefined }));
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('does not connect when enabled is false', () => {
    renderHook(() => useWidgetSync({ ...defaultOpts, enabled: false }));
    expect(MockEventSource.instances).toHaveLength(0);
  });

  it('tracks connection status', async () => {
    const { result } = renderHook(() => useWidgetSync(defaultOpts));
    expect(result.current.isConnected).toBe(false);

    // Trigger onopen via microtask + re-render
    await act(async () => {
      await new Promise(resolve => queueMicrotask(resolve));
    });
    expect(result.current.isConnected).toBe(true);
  });

  it('calls onRemoteUpdate when matching widget event arrives', async () => {
    const onRemoteUpdate = vi.fn();
    renderHook(() => useWidgetSync({ ...defaultOpts, onRemoteUpdate }));
    await vi.advanceTimersByTimeAsync(0);

    const es = MockEventSource.instances[0];
    act(() => {
      es._emit('session:toolkit', {
        sessionId: 'session-1',
        toolkitId: 'toolkit-1',
        widgetType: 'ScoreTracker',
        stateJson: '{"entries":[]}',
        updatedByUserId: 'user-2',
        timestamp: new Date().toISOString(),
      });
    });

    expect(onRemoteUpdate).toHaveBeenCalledWith('{"entries":[]}', 'user-2');
  });

  it('ignores events for different widget type', async () => {
    const onRemoteUpdate = vi.fn();
    renderHook(() => useWidgetSync({ ...defaultOpts, onRemoteUpdate }));
    await vi.advanceTimersByTimeAsync(0);

    const es = MockEventSource.instances[0];
    act(() => {
      es._emit('session:toolkit', {
        widgetType: 'TurnManager',
        stateJson: '{}',
        updatedByUserId: 'user-2',
        timestamp: new Date().toISOString(),
      });
    });

    expect(onRemoteUpdate).not.toHaveBeenCalled();
  });

  it('echo prevention: ignores remote event matching last broadcast', async () => {
    const onRemoteUpdate = vi.fn();
    const { result } = renderHook(() =>
      useWidgetSync({ ...defaultOpts, onRemoteUpdate, debounceMs: 100 })
    );
    await vi.advanceTimersByTimeAsync(0);

    // Broadcast state
    const stateJson = '{"entries":[{"score":10}]}';
    act(() => {
      result.current.broadcastState(stateJson);
    });

    // Flush debounce
    await act(async () => {
      await vi.advanceTimersByTimeAsync(100);
    });

    // Now simulate the same state coming back from SSE
    const es = MockEventSource.instances[0];
    act(() => {
      es._emit('session:toolkit', {
        widgetType: 'ScoreTracker',
        stateJson,
        updatedByUserId: 'user-2',
        timestamp: new Date().toISOString(),
      });
    });

    // Should be ignored (echo prevention)
    expect(onRemoteUpdate).not.toHaveBeenCalled();
  });

  it('broadcasts state via debounced PATCH', async () => {
    const { result } = renderHook(() => useWidgetSync({ ...defaultOpts, debounceMs: 200 }));
    await vi.advanceTimersByTimeAsync(0);

    act(() => {
      result.current.broadcastState('{"state":1}');
    });

    // Not yet called (debounced)
    expect(globalThis.fetch).not.toHaveBeenCalled();

    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });

    expect(globalThis.fetch).toHaveBeenCalledOnce();
    const [url, opts] = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0];
    expect(url).toContain('/toolkit-state/ScoreTracker');
    expect(opts.method).toBe('PATCH');
    expect(opts.body).toBe(JSON.stringify({ stateJson: '{"state":1}' }));
  });

  it('debounces multiple rapid broadcasts', async () => {
    const { result } = renderHook(() => useWidgetSync({ ...defaultOpts, debounceMs: 200 }));
    await vi.advanceTimersByTimeAsync(0);

    act(() => {
      result.current.broadcastState('{"v":1}');
    });
    await vi.advanceTimersByTimeAsync(50);
    act(() => {
      result.current.broadcastState('{"v":2}');
    });
    await vi.advanceTimersByTimeAsync(50);
    act(() => {
      result.current.broadcastState('{"v":3}');
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(200);
    });

    // Only the last value should be sent
    expect(globalThis.fetch).toHaveBeenCalledOnce();
    const body = (globalThis.fetch as ReturnType<typeof vi.fn>).mock.calls[0][1].body;
    expect(body).toBe(JSON.stringify({ stateJson: '{"v":3}' }));
  });

  it('does not broadcast when toolkitId is undefined', async () => {
    const { result } = renderHook(() => useWidgetSync({ ...defaultOpts, toolkitId: undefined }));
    await vi.advanceTimersByTimeAsync(0);

    act(() => {
      result.current.broadcastState('{"state":1}');
    });

    await act(async () => {
      await vi.advanceTimersByTimeAsync(300);
    });

    expect(globalThis.fetch).not.toHaveBeenCalled();
  });

  it('closes EventSource on unmount', async () => {
    const { unmount } = renderHook(() => useWidgetSync(defaultOpts));
    await vi.advanceTimersByTimeAsync(0);

    const es = MockEventSource.instances[0];
    expect(es.readyState).toBe(1);

    unmount();
    expect(es.readyState).toBe(2); // closed
  });
});
