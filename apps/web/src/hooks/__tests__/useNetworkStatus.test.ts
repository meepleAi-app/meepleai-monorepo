/**
 * useNetworkStatus Hook Tests (Issue #2054)
 *
 * Tests for network status hook:
 * - Hook initialization
 * - Network status callbacks
 * - Event listener management
 * - Derived state calculations
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useNetworkStatus, useOnlineCallback, useOfflineCallback } from '../useNetworkStatus';
import { useNetworkStatusStore } from '@/stores/NetworkStatusStore';

// Mock the store
vi.mock('@/stores/NetworkStatusStore', () => ({
  useNetworkStatusStore: vi.fn(),
  initializeNetworkListeners: vi.fn(),
  cleanupNetworkListeners: vi.fn(),
  // Selectors
  selectIsOnline: (state: { isOnline: boolean }) => state.isOnline,
  selectConnectionQuality: (state: { connectionQuality: string }) => state.connectionQuality,
  selectIsReconnecting: (state: { isReconnecting: boolean }) => state.isReconnecting,
  selectCanAttemptReconnect: (state: { reconnectAttempts: number }) => state.reconnectAttempts < 5,
}));

const mockStore = {
  isOnline: true,
  connectionQuality: 'excellent' as const,
  lastOnlineAt: null,
  lastOfflineAt: null,
  isReconnecting: false,
  reconnectAttempts: 0,
  effectiveType: '4g',
  downlink: 10,
  rtt: 50,
  setOnline: vi.fn(),
  setOffline: vi.fn(),
  updateConnectionQuality: vi.fn(),
  startReconnecting: vi.fn(),
  stopReconnecting: vi.fn(),
  setReconnecting: vi.fn(),
  incrementReconnectAttempts: vi.fn(),
  resetReconnectAttempts: vi.fn(),
  reset: vi.fn(),
  initialize: vi.fn().mockReturnValue(() => {}),
};

describe('useNetworkStatus', () => {
  beforeEach(() => {
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        if (selector) {
          return selector(mockStore);
        }
        return mockStore;
      }
    );
    vi.clearAllMocks();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  describe('Basic Hook Return', () => {
    it('should return network status state', () => {
      const { result } = renderHook(() => useNetworkStatus());

      expect(result.current.isOnline).toBe(true);
      expect(result.current.isOffline).toBe(false);
      expect(result.current.connectionQuality).toBe('excellent');
      expect(result.current.isReconnecting).toBe(false);
      expect(result.current.reconnectAttempts).toBe(0);
    });

    it('should return action functions', () => {
      const { result } = renderHook(() => useNetworkStatus());

      expect(result.current.startReconnecting).toBeInstanceOf(Function);
      expect(result.current.stopReconnecting).toBeInstanceOf(Function);
      expect(result.current.incrementReconnectAttempts).toBeInstanceOf(Function);
      expect(result.current.resetReconnectAttempts).toBeInstanceOf(Function);
    });
  });

  describe('Derived State', () => {
    it('should calculate isOffline correctly', () => {
      const offlineStore = { ...mockStore, isOnline: false };
      vi.mocked(useNetworkStatusStore).mockImplementation(
        (selector?: (state: typeof mockStore) => unknown) => {
          if (selector) {
            return selector(offlineStore);
          }
          return offlineStore;
        }
      );

      const { result } = renderHook(() => useNetworkStatus());

      expect(result.current.isOnline).toBe(false);
      expect(result.current.isOffline).toBe(true);
    });

    it('should calculate canAttemptReconnect correctly when offline and not reconnecting', () => {
      const offlineStore = { ...mockStore, isOnline: false, isReconnecting: false };
      vi.mocked(useNetworkStatusStore).mockImplementation(
        (selector?: (state: typeof mockStore) => unknown) => {
          if (selector) {
            return selector(offlineStore);
          }
          return offlineStore;
        }
      );

      const { result } = renderHook(() => useNetworkStatus());

      expect(result.current.canAttemptReconnect).toBe(true);
    });

    it('should return false for canAttemptReconnect when max attempts reached', () => {
      const maxAttemptsStore = { ...mockStore, reconnectAttempts: 5 };
      vi.mocked(useNetworkStatusStore).mockImplementation(
        (selector?: (state: typeof mockStore) => unknown) => {
          if (selector) {
            return selector(maxAttemptsStore);
          }
          return maxAttemptsStore;
        }
      );

      const { result } = renderHook(() => useNetworkStatus());

      expect(result.current.canAttemptReconnect).toBe(false);
    });

    it('should return true for canAttemptReconnect when under max attempts', () => {
      const underMaxStore = { ...mockStore, reconnectAttempts: 3 };
      vi.mocked(useNetworkStatusStore).mockImplementation(
        (selector?: (state: typeof mockStore) => unknown) => {
          if (selector) {
            return selector(underMaxStore);
          }
          return underMaxStore;
        }
      );

      const { result } = renderHook(() => useNetworkStatus());

      expect(result.current.canAttemptReconnect).toBe(true);
    });
  });

  describe('Action Functions', () => {
    it('should call setReconnecting(true) when startReconnecting is called', () => {
      const { result } = renderHook(() => useNetworkStatus());

      act(() => {
        result.current.startReconnecting();
      });

      expect(mockStore.setReconnecting).toHaveBeenCalledWith(true);
    });

    it('should call setReconnecting(false) when stopReconnecting is called', () => {
      const { result } = renderHook(() => useNetworkStatus());

      act(() => {
        result.current.stopReconnecting();
      });

      expect(mockStore.setReconnecting).toHaveBeenCalledWith(false);
    });

    it('should call incrementReconnectAttempts from store', () => {
      const { result } = renderHook(() => useNetworkStatus());

      act(() => {
        result.current.incrementReconnectAttempts();
      });

      expect(mockStore.incrementReconnectAttempts).toHaveBeenCalled();
    });

    it('should call resetReconnectAttempts from store', () => {
      const { result } = renderHook(() => useNetworkStatus());

      act(() => {
        result.current.resetReconnectAttempts();
      });

      expect(mockStore.resetReconnectAttempts).toHaveBeenCalled();
    });
  });
});

describe('useOnlineCallback', () => {
  beforeEach(() => {
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        if (selector) {
          return selector(mockStore);
        }
        return mockStore;
      }
    );
    vi.clearAllMocks();
  });

  it('should call callback when transitioning from offline to online', () => {
    const callback = vi.fn();

    // Start offline
    let currentOnline = false;
    let currentLastOnlineAt: number | null = null;
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        const state = { ...mockStore, isOnline: currentOnline, lastOnlineAt: currentLastOnlineAt };
        if (selector) {
          return selector(state);
        }
        return state;
      }
    );

    const { rerender } = renderHook(() => useOnlineCallback(callback));

    // Transition to online with recent lastOnlineAt
    currentOnline = true;
    currentLastOnlineAt = Date.now();
    rerender();

    expect(callback).toHaveBeenCalled();
  });

  it('should not call callback when staying online', () => {
    const callback = vi.fn();

    // Already online with old lastOnlineAt (not a recent transition)
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        const state = { ...mockStore, isOnline: true, lastOnlineAt: Date.now() - 5000 }; // 5 seconds ago
        if (selector) {
          return selector(state);
        }
        return state;
      }
    );

    const { rerender } = renderHook(() => useOnlineCallback(callback));
    rerender();

    // Should not be called since we were already online (lastOnlineAt > 1 second ago)
    expect(callback).not.toHaveBeenCalled();
  });
});

describe('useOfflineCallback', () => {
  beforeEach(() => {
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        if (selector) {
          return selector(mockStore);
        }
        return mockStore;
      }
    );
    vi.clearAllMocks();
  });

  it('should call callback when transitioning from online to offline', () => {
    const callback = vi.fn();

    // Start online
    let currentOnline = true;
    let currentLastOfflineAt: number | null = null;
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        const state = {
          ...mockStore,
          isOnline: currentOnline,
          lastOfflineAt: currentLastOfflineAt,
        };
        if (selector) {
          return selector(state);
        }
        return state;
      }
    );

    const { rerender } = renderHook(() => useOfflineCallback(callback));

    // Transition to offline with recent lastOfflineAt
    currentOnline = false;
    currentLastOfflineAt = Date.now();
    rerender();

    expect(callback).toHaveBeenCalled();
  });

  it('should not call callback when staying offline', () => {
    const callback = vi.fn();

    // Already offline with old lastOfflineAt (not a recent transition)
    vi.mocked(useNetworkStatusStore).mockImplementation(
      (selector?: (state: typeof mockStore) => unknown) => {
        const state = { ...mockStore, isOnline: false, lastOfflineAt: Date.now() - 5000 }; // 5 seconds ago
        if (selector) {
          return selector(state);
        }
        return state;
      }
    );

    const { rerender } = renderHook(() => useOfflineCallback(callback));
    rerender();

    // Should not be called since we were already offline (lastOfflineAt > 1 second ago)
    expect(callback).not.toHaveBeenCalled();
  });
});
