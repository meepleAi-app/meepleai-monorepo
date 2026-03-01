/**
 * Tests for useSessionStream hook
 * Issue #4767 - SSE Client + Player/Spectator Mode UI
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useSessionStream } from '@/lib/hooks/useSessionStream';
import type { ConnectionStatus } from '@/lib/hooks/useSessionStream';

// Mock EventSource
class MockEventSource {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;

  readyState = MockEventSource.CONNECTING;
  url: string;
  withCredentials: boolean;
  onopen: (() => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  onerror: (() => void) | null = null;
  private listeners: Record<string, EventListener[]> = {};

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
    MockEventSource._instances.push(this);
  }

  addEventListener(type: string, listener: EventListener) {
    if (!this.listeners[type]) this.listeners[type] = [];
    this.listeners[type].push(listener);
  }

  removeEventListener(type: string, listener: EventListener) {
    if (this.listeners[type]) {
      this.listeners[type] = this.listeners[type].filter(l => l !== listener);
    }
  }

  close() {
    this.readyState = MockEventSource.CLOSED;
  }

  // Test helpers
  simulateOpen() {
    this.readyState = MockEventSource.OPEN;
    this.onopen?.();
  }

  simulateMessage(data: unknown) {
    const event = new MessageEvent('message', {
      data: JSON.stringify(data),
    });
    this.onmessage?.(event);
  }

  simulateError() {
    this.onerror?.();
  }

  static _instances: MockEventSource[] = [];
  static reset() {
    MockEventSource._instances = [];
  }
  static get latest() {
    return MockEventSource._instances[MockEventSource._instances.length - 1];
  }
}

// Patch global
const originalEventSource = globalThis.EventSource;

beforeEach(() => {
  MockEventSource.reset();
  (globalThis as unknown as Record<string, unknown>).EventSource = MockEventSource as unknown as typeof EventSource;
  vi.useFakeTimers();
});

afterEach(() => {
  (globalThis as unknown as Record<string, unknown>).EventSource = originalEventSource;
  vi.useRealTimers();
});

describe('useSessionStream', () => {
  it('should start disconnected when no sessionId', () => {
    const { result } = renderHook(() => useSessionStream(null));

    expect(result.current.connectionStatus).toBe('disconnected');
    expect(MockEventSource._instances).toHaveLength(0);
  });

  it('should connect when sessionId is provided', () => {
    const { result } = renderHook(() => useSessionStream('session-123'));

    expect(result.current.connectionStatus).toBe('connecting');
    expect(MockEventSource._instances).toHaveLength(1);
    expect(MockEventSource.latest.url).toContain('/api/v1/game-sessions/session-123/stream');
  });

  it('should transition to connected on open', async () => {
    const { result } = renderHook(() => useSessionStream('session-123'));

    act(() => {
      MockEventSource.latest.simulateOpen();
    });

    expect(result.current.connectionStatus).toBe('connected');
    expect(result.current.reconnectCount).toBe(0);
  });

  it('should attempt reconnection on error', async () => {
    const { result } = renderHook(() =>
      useSessionStream('session-123', { maxReconnectAttempts: 3 })
    );

    act(() => {
      MockEventSource.latest.simulateOpen();
    });
    expect(result.current.connectionStatus).toBe('connected');

    act(() => {
      MockEventSource.latest.simulateError();
    });

    expect(result.current.connectionStatus).toBe('reconnecting');
    expect(result.current.reconnectCount).toBe(1);
  });

  it('should set failed after max reconnect attempts', async () => {
    const onError = vi.fn();
    const { result } = renderHook(() =>
      useSessionStream('session-123', { maxReconnectAttempts: 1, onError })
    );

    // First error: triggers reconnect
    act(() => {
      MockEventSource.latest.simulateError();
    });
    expect(result.current.connectionStatus).toBe('reconnecting');

    // After timeout, reconnects
    act(() => {
      vi.advanceTimersByTime(1000);
    });

    // Second error on the new connection: exceeds max
    act(() => {
      MockEventSource.latest.simulateError();
    });
    expect(result.current.connectionStatus).toBe('failed');
    expect(onError).toHaveBeenCalledWith(expect.any(Error));
  });

  it('should disconnect when enabled becomes false', () => {
    const { result, rerender } = renderHook(
      ({ enabled }) => useSessionStream('session-123', { enabled }),
      { initialProps: { enabled: true } }
    );

    expect(MockEventSource._instances.length).toBeGreaterThan(0);

    rerender({ enabled: false });
    expect(result.current.connectionStatus).toBe('disconnected');
  });

  it('should call onPlayerJoined for player join events', () => {
    const onPlayerJoined = vi.fn();
    renderHook(() =>
      useSessionStream('session-123', { onPlayerJoined })
    );

    act(() => {
      MockEventSource.latest.simulateOpen();
    });

    act(() => {
      MockEventSource.latest.simulateMessage({
        type: 'session:player',
        data: {
          sessionId: 'session-123',
          participantId: 'p-1',
          displayName: 'Alice',
          isOwner: false,
          joinOrder: 2,
        },
        timestamp: new Date().toISOString(),
      });
    });

    expect(onPlayerJoined).toHaveBeenCalledWith(
      expect.objectContaining({
        displayName: 'Alice',
        joinOrder: 2,
      })
    );
  });

  it('should call onScoreUpdated for score events', () => {
    const onScoreUpdated = vi.fn();
    renderHook(() =>
      useSessionStream('session-123', { onScoreUpdated })
    );

    act(() => {
      MockEventSource.latest.simulateOpen();
    });

    act(() => {
      MockEventSource.latest.simulateMessage({
        type: 'session:score',
        data: {
          sessionId: 'session-123',
          participantId: 'p-1',
          scoreEntryId: 'se-1',
          newScore: 42,
        },
        timestamp: new Date().toISOString(),
      });
    });

    expect(onScoreUpdated).toHaveBeenCalledWith(
      expect.objectContaining({ newScore: 42 })
    );
  });

  it('should call onRoleChanged for role change events', () => {
    const onRoleChanged = vi.fn();
    renderHook(() =>
      useSessionStream('session-123', { onRoleChanged })
    );

    act(() => {
      MockEventSource.latest.simulateOpen();
    });

    act(() => {
      MockEventSource.latest.simulateMessage({
        type: 'session:player',
        data: {
          sessionId: 'session-123',
          participantId: 'p-1',
          displayName: 'Bob',
          previousRole: 'Player',
          newRole: 'Host',
          changedBy: 'host-id',
        },
        timestamp: new Date().toISOString(),
      });
    });

    expect(onRoleChanged).toHaveBeenCalledWith(
      expect.objectContaining({ previousRole: 'Player', newRole: 'Host' })
    );
  });

  it('should support manual disconnect and reconnect', () => {
    const { result } = renderHook(() => useSessionStream('session-123'));

    act(() => {
      MockEventSource.latest.simulateOpen();
    });
    expect(result.current.connectionStatus).toBe('connected');

    act(() => {
      result.current.disconnect();
    });
    expect(result.current.connectionStatus).toBe('disconnected');

    act(() => {
      result.current.reconnect();
    });
    // Should create a new connection
    expect(result.current.connectionStatus).toBe('connecting');
  });

  it('should use credentials for auth', () => {
    renderHook(() => useSessionStream('session-123'));

    expect(MockEventSource.latest.withCredentials).toBe(true);
  });
});
