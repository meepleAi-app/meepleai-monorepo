/**
 * useDashboardStream Hook Tests (Issue #3324)
 *
 * Tests for SSE-based dashboard streaming hook
 *
 * Note: Full SSE behavior is tested in E2E tests
 * These unit tests verify hook initialization, state management, and callbacks.
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import {
  useDashboardStream,
  DashboardEventType,
  type DashboardStatsData,
  type DashboardActivityData,
  type ConnectionState,
} from '../useDashboardStream';

// Mock EventSource
class MockEventSource {
  static CONNECTING = 0;
  static OPEN = 1;
  static CLOSED = 2;

  url: string;
  readyState: number = MockEventSource.CONNECTING;
  withCredentials: boolean;
  onopen: ((event: Event) => void) | null = null;
  onerror: ((event: Event) => void) | null = null;
  onmessage: ((event: MessageEvent) => void) | null = null;
  private listeners: Map<string, ((event: MessageEvent) => void)[]> = new Map();

  constructor(url: string, options?: { withCredentials?: boolean }) {
    this.url = url;
    this.withCredentials = options?.withCredentials ?? false;
  }

  addEventListener(type: string, listener: (event: MessageEvent) => void): void {
    if (!this.listeners.has(type)) {
      this.listeners.set(type, []);
    }
    this.listeners.get(type)!.push(listener);
  }

  removeEventListener(type: string, listener: (event: MessageEvent) => void): void {
    const eventListeners = this.listeners.get(type);
    if (eventListeners) {
      const index = eventListeners.indexOf(listener);
      if (index > -1) {
        eventListeners.splice(index, 1);
      }
    }
  }

  close(): void {
    this.readyState = MockEventSource.CLOSED;
  }

  // Test helpers
  simulateOpen(): void {
    this.readyState = MockEventSource.OPEN;
    if (this.onopen) {
      this.onopen(new Event('open'));
    }
  }

  simulateError(): void {
    if (this.onerror) {
      this.onerror(new Event('error'));
    }
  }

  simulateMessage(type: string, data: unknown): void {
    const event = new MessageEvent(type, {
      data: JSON.stringify(data),
    });
    const listeners = this.listeners.get(type);
    if (listeners) {
      listeners.forEach(listener => listener(event));
    }
  }
}

describe('useDashboardStream', () => {
  let mockEventSourceInstance: MockEventSource | null = null;

  beforeEach(() => {
    vi.useFakeTimers();
    mockEventSourceInstance = null;

    // Mock EventSource constructor
    global.EventSource = vi.fn().mockImplementation((url: string, options?: { withCredentials?: boolean }) => {
      mockEventSourceInstance = new MockEventSource(url, options);
      return mockEventSourceInstance;
    }) as unknown as typeof EventSource;

    // Add static properties
    (global.EventSource as unknown as typeof MockEventSource).CONNECTING = MockEventSource.CONNECTING;
    (global.EventSource as unknown as typeof MockEventSource).OPEN = MockEventSource.OPEN;
    (global.EventSource as unknown as typeof MockEventSource).CLOSED = MockEventSource.CLOSED;
  });

  afterEach(() => {
    vi.clearAllTimers();
    vi.useRealTimers();
    vi.clearAllMocks();
  });

  // ============================================================================
  // Initialization Tests
  // ============================================================================

  describe('Initialization', () => {
    it('should initialize with default disconnected state', () => {
      const { result } = renderHook(() => useDashboardStream({ autoConnect: false }));

      expect(result.current.isConnected).toBe(false);
      expect(result.current.connectionState).toBe('disconnected');
      expect(result.current.error).toBeNull();
      expect(result.current.lastEvent).toBeNull();
      expect(result.current.reconnectAttempts).toBe(0);
    });

    it('should auto-connect by default', () => {
      renderHook(() => useDashboardStream());

      expect(global.EventSource).toHaveBeenCalled();
    });

    it('should not auto-connect when autoConnect is false', () => {
      renderHook(() => useDashboardStream({ autoConnect: false }));

      expect(global.EventSource).not.toHaveBeenCalled();
    });

    it('should return connect and disconnect functions', () => {
      const { result } = renderHook(() => useDashboardStream({ autoConnect: false }));

      expect(typeof result.current.connect).toBe('function');
      expect(typeof result.current.disconnect).toBe('function');
    });
  });

  // ============================================================================
  // Connection Tests
  // ============================================================================

  describe('Connection', () => {
    it('should create EventSource with correct URL', () => {
      renderHook(() => useDashboardStream());

      expect(global.EventSource).toHaveBeenCalledWith(
        expect.stringContaining('/api/v1/dashboard/stream'),
        expect.objectContaining({ withCredentials: true })
      );
    });

    it('should use custom API base URL if provided', () => {
      const customBaseUrl = 'http://localhost:8080';

      renderHook(() =>
        useDashboardStream({
          apiBaseUrl: customBaseUrl,
        })
      );

      expect(global.EventSource).toHaveBeenCalledWith(
        expect.stringContaining(customBaseUrl),
        expect.any(Object)
      );
    });

    it('should update state to connecting when connect is called', () => {
      const { result } = renderHook(() => useDashboardStream({ autoConnect: false }));

      expect(result.current.connectionState).toBe('disconnected');

      act(() => {
        result.current.connect();
      });

      expect(result.current.connectionState).toBe('connecting');
    });

    it('should update state to connected on successful connection', async () => {
      const onConnectionChange = vi.fn();
      const { result } = renderHook(() =>
        useDashboardStream({
          onConnectionChange,
        })
      );

      // Simulate successful connection
      act(() => {
        mockEventSourceInstance?.simulateOpen();
      });

      expect(result.current.isConnected).toBe(true);
      expect(result.current.connectionState).toBe('connected');
      expect(onConnectionChange).toHaveBeenCalledWith('connected');
    });

    it('should cleanup EventSource on unmount', () => {
      const { unmount } = renderHook(() => useDashboardStream());

      const closeSpy = vi.spyOn(mockEventSourceInstance!, 'close');

      unmount();

      expect(closeSpy).toHaveBeenCalled();
    });

    it('should cleanup EventSource when disconnect is called', () => {
      const { result } = renderHook(() => useDashboardStream());

      const closeSpy = vi.spyOn(mockEventSourceInstance!, 'close');

      act(() => {
        result.current.disconnect();
      });

      expect(closeSpy).toHaveBeenCalled();
      expect(result.current.connectionState).toBe('disconnected');
    });
  });

  // ============================================================================
  // Event Handling Tests
  // ============================================================================

  describe('Event Handling', () => {
    it('should call onStatsUpdate when stats event is received', async () => {
      const onStatsUpdate = vi.fn();
      renderHook(() =>
        useDashboardStream({
          onStatsUpdate,
        })
      );

      const statsData: DashboardStatsData = {
        collectionCount: 10,
        playedCount: 5,
        activeSessionCount: 2,
        onlineUserCount: 3,
        timestamp: new Date().toISOString(),
      };

      act(() => {
        mockEventSourceInstance?.simulateOpen();
        mockEventSourceInstance?.simulateMessage(DashboardEventType.StatsUpdated, statsData);
      });

      expect(onStatsUpdate).toHaveBeenCalledWith(statsData);
    });

    it('should call onActivity when activity event is received', async () => {
      const onActivity = vi.fn();
      renderHook(() =>
        useDashboardStream({
          onActivity,
        })
      );

      const activityData: DashboardActivityData = {
        activityType: 'game_added',
        title: 'Wingspan added to collection',
        timestamp: new Date().toISOString(),
      };

      act(() => {
        mockEventSourceInstance?.simulateOpen();
        mockEventSourceInstance?.simulateMessage(DashboardEventType.Activity, activityData);
      });

      expect(onActivity).toHaveBeenCalledWith(activityData);
    });

    it('should update lastEvent when any event is received', async () => {
      const { result } = renderHook(() => useDashboardStream());

      const statsData = {
        collectionCount: 10,
        playedCount: 5,
        activeSessionCount: 2,
        onlineUserCount: 3,
        timestamp: new Date().toISOString(),
      };

      act(() => {
        mockEventSourceInstance?.simulateOpen();
        mockEventSourceInstance?.simulateMessage(DashboardEventType.StatsUpdated, statsData);
      });

      expect(result.current.lastEvent).not.toBeNull();
      expect(result.current.lastEvent?.type).toBe(DashboardEventType.StatsUpdated);
      expect(result.current.lastEvent?.data).toEqual(statsData);
    });

    it('should handle heartbeat events without calling callbacks', async () => {
      const onStatsUpdate = vi.fn();
      const { result } = renderHook(() =>
        useDashboardStream({
          onStatsUpdate,
        })
      );

      act(() => {
        mockEventSourceInstance?.simulateOpen();
        mockEventSourceInstance?.simulateMessage(DashboardEventType.Heartbeat, { timestamp: Date.now() });
      });

      expect(onStatsUpdate).not.toHaveBeenCalled();
      expect(result.current.lastEvent?.type).toBe(DashboardEventType.Heartbeat);
    });
  });

  // ============================================================================
  // Error Handling Tests
  // ============================================================================

  describe('Error Handling', () => {
    it('should call onError when connection fails', async () => {
      const onError = vi.fn();
      renderHook(() =>
        useDashboardStream({
          onError,
        })
      );

      act(() => {
        mockEventSourceInstance?.simulateError();
      });

      expect(onError).toHaveBeenCalled();
    });

    it('should set error state when connection fails', async () => {
      const { result } = renderHook(() => useDashboardStream());

      act(() => {
        mockEventSourceInstance?.simulateError();
      });

      expect(result.current.error).not.toBeNull();
      expect(result.current.error?.message).toBe('SSE connection failed');
    });

    it('should attempt reconnection after error', async () => {
      const onConnectionChange = vi.fn();
      renderHook(() =>
        useDashboardStream({
          onConnectionChange,
          maxReconnectAttempts: 5,
        })
      );

      act(() => {
        mockEventSourceInstance?.simulateError();
      });

      expect(onConnectionChange).toHaveBeenCalledWith('reconnecting');
    });

    it('should stop reconnecting after max attempts', async () => {
      const onConnectionChange = vi.fn();
      const maxAttempts = 2;

      renderHook(() =>
        useDashboardStream({
          onConnectionChange,
          maxReconnectAttempts: maxAttempts,
        })
      );

      // Simulate failures until max attempts
      for (let i = 0; i <= maxAttempts; i++) {
        act(() => {
          mockEventSourceInstance?.simulateError();
          vi.advanceTimersByTime(Math.pow(2, i) * 1000);
        });
      }

      expect(onConnectionChange).toHaveBeenCalledWith('error');
    });

    it('should reset reconnect attempts on successful connection', async () => {
      const { result } = renderHook(() => useDashboardStream());

      // Simulate error
      act(() => {
        mockEventSourceInstance?.simulateError();
      });

      // Advance timer for reconnect
      act(() => {
        vi.advanceTimersByTime(1000);
      });

      // Simulate successful reconnection
      act(() => {
        mockEventSourceInstance?.simulateOpen();
      });

      expect(result.current.reconnectAttempts).toBe(0);
      expect(result.current.error).toBeNull();
    });
  });

  // ============================================================================
  // Connection State Tests
  // ============================================================================

  describe('Connection State Management', () => {
    it('should track all connection states', async () => {
      const states: ConnectionState[] = [];
      const onConnectionChange = vi.fn((state: ConnectionState) => {
        states.push(state);
      });

      const { result, unmount } = renderHook(() =>
        useDashboardStream({
          onConnectionChange,
        })
      );

      // Initial connecting state
      expect(states).toContain('connecting');

      // Successful connection
      act(() => {
        mockEventSourceInstance?.simulateOpen();
      });
      expect(states).toContain('connected');

      // Error occurs
      act(() => {
        mockEventSourceInstance?.simulateError();
      });
      expect(states).toContain('reconnecting');

      // Manual disconnect
      act(() => {
        result.current.disconnect();
      });
      expect(states).toContain('disconnected');
    });

    it('should prevent multiple concurrent connections', async () => {
      const { result } = renderHook(() => useDashboardStream({ autoConnect: false }));

      act(() => {
        result.current.connect();
        result.current.connect();
        result.current.connect();
      });

      // Should only create one EventSource
      expect(global.EventSource).toHaveBeenCalledTimes(1);
    });
  });

  // ============================================================================
  // Callback Props Tests
  // ============================================================================

  describe('Callback Props', () => {
    it('should accept all callback props', () => {
      const onStatsUpdate = vi.fn();
      const onActivity = vi.fn();
      const onSessionUpdate = vi.fn();
      const onNotification = vi.fn();
      const onError = vi.fn();
      const onConnectionChange = vi.fn();

      const { result } = renderHook(() =>
        useDashboardStream({
          onStatsUpdate,
          onActivity,
          onSessionUpdate,
          onNotification,
          onError,
          onConnectionChange,
        })
      );

      // Verify hook returns expected structure
      expect(result.current).toHaveProperty('isConnected');
      expect(result.current).toHaveProperty('connectionState');
      expect(result.current).toHaveProperty('error');
      expect(result.current).toHaveProperty('lastEvent');
      expect(result.current).toHaveProperty('reconnectAttempts');
      expect(result.current).toHaveProperty('connect');
      expect(result.current).toHaveProperty('disconnect');
    });
  });
});
