/**
 * Network Status Store Tests (Issue #2054)
 *
 * Tests for network status Zustand store:
 * - Online/offline state detection
 * - Connection quality assessment
 * - Reconnection tracking
 * - Event listener initialization
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useNetworkStatusStore } from '../NetworkStatusStore';

// Mock navigator
const mockNavigator = {
  onLine: true,
  connection: {
    effectiveType: '4g',
    downlink: 10,
    rtt: 50,
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
  },
};

describe('useNetworkStatusStore', () => {
  let originalNavigator: typeof navigator;
  let originalWindow: typeof window;
  let onlineHandler: EventListener | null = null;
  let offlineHandler: EventListener | null = null;

  beforeEach(() => {
    // Store original values
    originalNavigator = global.navigator;
    originalWindow = global.window;

    // Mock navigator
    Object.defineProperty(global, 'navigator', {
      value: mockNavigator,
      writable: true,
      configurable: true,
    });

    // Mock window event listeners
    vi.spyOn(window, 'addEventListener').mockImplementation((event, handler) => {
      if (event === 'online') onlineHandler = handler as EventListener;
      if (event === 'offline') offlineHandler = handler as EventListener;
    });
    vi.spyOn(window, 'removeEventListener');

    // Clear mocks
    vi.clearAllMocks();
    onlineHandler = null;
    offlineHandler = null;

    // Reset store state manually via setState
    const store = useNetworkStatusStore.getState();
    useNetworkStatusStore.setState({
      isOnline: true,
      connectionQuality: 'excellent',
      lastOnlineAt: null,
      lastOfflineAt: null,
      isReconnecting: false,
      reconnectAttempts: 0,
      effectiveType: null,
      downlink: null,
      rtt: null,
    });
  });

  afterEach(() => {
    // Restore original values
    Object.defineProperty(global, 'navigator', {
      value: originalNavigator,
      writable: true,
      configurable: true,
    });
    vi.restoreAllMocks();
  });

  describe('Initial State', () => {
    it('should initialize with default values', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      expect(result.current.isOnline).toBe(true);
      expect(result.current.connectionQuality).toBe('excellent');
      expect(result.current.lastOnlineAt).toBeNull();
      expect(result.current.lastOfflineAt).toBeNull();
      expect(result.current.isReconnecting).toBe(false);
      expect(result.current.reconnectAttempts).toBe(0);
    });
  });

  describe('setOnline', () => {
    it('should set online state to true', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.setOnline(false); // First go offline
      });

      expect(result.current.isOnline).toBe(false);

      act(() => {
        result.current.setOnline(true);
      });

      expect(result.current.isOnline).toBe(true);
      expect(result.current.lastOnlineAt).toBeTruthy();
      expect(result.current.lastOnlineAt).toBeGreaterThan(0);
    });

    it('should set online state to false and update lastOfflineAt', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      expect(result.current.isOnline).toBe(true);

      act(() => {
        result.current.setOnline(false);
      });

      expect(result.current.isOnline).toBe(false);
      expect(result.current.connectionQuality).toBe('offline');
      expect(result.current.lastOfflineAt).toBeTruthy();
    });

    it('should reset reconnecting state when going online', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.setOnline(false);
        result.current.setReconnecting(true);
        result.current.incrementReconnectAttempts();
      });

      expect(result.current.isReconnecting).toBe(true);
      expect(result.current.reconnectAttempts).toBe(1);

      act(() => {
        result.current.setOnline(true);
      });

      expect(result.current.isReconnecting).toBe(false);
    });
  });

  describe('setReconnecting', () => {
    it('should set reconnecting state', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      expect(result.current.isReconnecting).toBe(false);

      act(() => {
        result.current.setReconnecting(true);
      });

      expect(result.current.isReconnecting).toBe(true);
    });

    it('should clear reconnecting state', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.setReconnecting(true);
      });

      expect(result.current.isReconnecting).toBe(true);

      act(() => {
        result.current.setReconnecting(false);
      });

      expect(result.current.isReconnecting).toBe(false);
    });
  });

  describe('incrementReconnectAttempts', () => {
    it('should increment attempt counter', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      expect(result.current.reconnectAttempts).toBe(0);

      act(() => {
        result.current.incrementReconnectAttempts();
      });

      expect(result.current.reconnectAttempts).toBe(1);

      act(() => {
        result.current.incrementReconnectAttempts();
      });

      expect(result.current.reconnectAttempts).toBe(2);
    });
  });

  describe('resetReconnectAttempts', () => {
    it('should reset attempt counter to zero', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.incrementReconnectAttempts();
        result.current.incrementReconnectAttempts();
        result.current.incrementReconnectAttempts();
      });

      expect(result.current.reconnectAttempts).toBe(3);

      act(() => {
        result.current.resetReconnectAttempts();
      });

      expect(result.current.reconnectAttempts).toBe(0);
    });

    it('should also reset isReconnecting to false', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.setReconnecting(true);
        result.current.incrementReconnectAttempts();
      });

      expect(result.current.isReconnecting).toBe(true);

      act(() => {
        result.current.resetReconnectAttempts();
      });

      expect(result.current.isReconnecting).toBe(false);
    });
  });

  describe('initialize', () => {
    it('should add event listeners for online/offline', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.initialize();
      });

      expect(window.addEventListener).toHaveBeenCalledWith('online', expect.any(Function));
      expect(window.addEventListener).toHaveBeenCalledWith('offline', expect.any(Function));
    });

    it('should return cleanup function', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      let cleanup: (() => void) | undefined;
      act(() => {
        cleanup = result.current.initialize();
      });

      expect(cleanup).toBeInstanceOf(Function);

      // Call cleanup
      cleanup!();

      expect(window.removeEventListener).toHaveBeenCalledWith('online', expect.any(Function));
      expect(window.removeEventListener).toHaveBeenCalledWith('offline', expect.any(Function));
    });

    it('should trigger setOnline(true) when online event fires', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      // First go offline
      act(() => {
        result.current.setOnline(false);
      });

      expect(result.current.isOnline).toBe(false);

      // Initialize listeners
      act(() => {
        result.current.initialize();
      });

      // Simulate online event
      if (onlineHandler) {
        act(() => {
          onlineHandler!(new Event('online'));
        });
      }

      expect(result.current.isOnline).toBe(true);
    });

    it('should trigger setOnline(false) when offline event fires', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      expect(result.current.isOnline).toBe(true);

      // Initialize listeners
      act(() => {
        result.current.initialize();
      });

      // Simulate offline event
      if (offlineHandler) {
        act(() => {
          offlineHandler!(new Event('offline'));
        });
      }

      expect(result.current.isOnline).toBe(false);
    });
  });

  describe('Connection Quality Assessment', () => {
    it('should set offline quality when going offline', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.setOnline(false);
      });

      expect(result.current.connectionQuality).toBe('offline');
    });

    it('should update connection info when updateConnectionInfo is called', () => {
      const { result } = renderHook(() => useNetworkStatusStore());

      act(() => {
        result.current.updateConnectionInfo();
      });

      // With our mock navigator.connection, should get effective type info
      expect(result.current.effectiveType).toBe('4g');
      expect(result.current.downlink).toBe(10);
      expect(result.current.rtt).toBe(50);
    });
  });
});
