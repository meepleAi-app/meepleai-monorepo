/**
 * useNetworkStatus Hook (Issue #2054)
 *
 * React hook for network status detection with:
 * - Browser online/offline events
 * - Connection quality monitoring
 * - Reconnection state coordination
 * - SSR-safe with automatic initialization
 *
 * @example
 * ```tsx
 * function MyComponent() {
 *   const { isOnline, connectionQuality, isReconnecting } = useNetworkStatus();
 *
 *   if (!isOnline) {
 *     return <OfflineBanner />;
 *   }
 *
 *   return <Content />;
 * }
 * ```
 */

import { useEffect, useMemo } from 'react';

import {
  useNetworkStatusStore,
  selectIsOnline,
  selectConnectionQuality,
  selectIsReconnecting,
  selectCanAttemptReconnect,
  type ConnectionQuality,
} from '@/stores/NetworkStatusStore';

// ============================================================================
// Hook Interface
// ============================================================================

export interface UseNetworkStatusReturn {
  /** Whether the browser is online */
  isOnline: boolean;

  /** Whether the browser is offline */
  isOffline: boolean;

  /** Connection quality assessment */
  connectionQuality: ConnectionQuality;

  /** Whether we're currently reconnecting */
  isReconnecting: boolean;

  /** Whether we can attempt a reconnection */
  canAttemptReconnect: boolean;

  /** Time since last online (ms), or null if always online */
  timeSinceOnline: number | null;

  /** Time since last offline (ms), or null if never offline */
  timeSinceOffline: number | null;

  /** Number of reconnection attempts */
  reconnectAttempts: number;

  /** Mark reconnection as in progress */
  startReconnecting: () => void;

  /** Mark reconnection as complete */
  stopReconnecting: () => void;

  /** Increment reconnect attempt counter */
  incrementReconnectAttempts: () => void;

  /** Reset reconnect attempt counter */
  resetReconnectAttempts: () => void;
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useNetworkStatus(): UseNetworkStatusReturn {
  const store = useNetworkStatusStore();

  // Use selectors for optimized subscriptions
  const isOnline = useNetworkStatusStore(selectIsOnline);
  const connectionQuality = useNetworkStatusStore(selectConnectionQuality);
  const isReconnecting = useNetworkStatusStore(selectIsReconnecting);
  const canAttemptReconnect = useNetworkStatusStore(selectCanAttemptReconnect);

  // Initialize event listeners on mount
  useEffect(() => {
    const cleanup = store.initialize();
    return cleanup;
  }, [store]);

  // Compute derived values
  const timeSinceOnline = useMemo(() => {
    if (!store.lastOnlineAt) return null;
    return Date.now() - store.lastOnlineAt;
  }, [store.lastOnlineAt]);

  const timeSinceOffline = useMemo(() => {
    if (!store.lastOfflineAt) return null;
    return Date.now() - store.lastOfflineAt;
  }, [store.lastOfflineAt]);

  return {
    isOnline,
    isOffline: !isOnline,
    connectionQuality,
    isReconnecting,
    canAttemptReconnect,
    timeSinceOnline,
    timeSinceOffline,
    reconnectAttempts: store.reconnectAttempts,
    startReconnecting: () => store.setReconnecting(true),
    stopReconnecting: () => store.setReconnecting(false),
    incrementReconnectAttempts: store.incrementReconnectAttempts,
    resetReconnectAttempts: store.resetReconnectAttempts,
  };
}

// ============================================================================
// Utility Hook: useOnlineCallback
// ============================================================================

/**
 * Execute a callback when coming back online
 *
 * @example
 * ```tsx
 * useOnlineCallback(() => {
 *   refetchData();
 * });
 * ```
 */
export function useOnlineCallback(callback: () => void): void {
  const isOnline = useNetworkStatusStore(selectIsOnline);
  const lastOnlineAt = useNetworkStatusStore(state => state.lastOnlineAt);

  useEffect(() => {
    // Only trigger when transitioning from offline to online
    if (isOnline && lastOnlineAt && Date.now() - lastOnlineAt < 1000) {
      callback();
    }
  }, [isOnline, lastOnlineAt, callback]);
}

// ============================================================================
// Utility Hook: useOfflineCallback
// ============================================================================

/**
 * Execute a callback when going offline
 *
 * @example
 * ```tsx
 * useOfflineCallback(() => {
 *   saveLocalDraft();
 * });
 * ```
 */
export function useOfflineCallback(callback: () => void): void {
  const isOnline = useNetworkStatusStore(selectIsOnline);
  const lastOfflineAt = useNetworkStatusStore(state => state.lastOfflineAt);

  useEffect(() => {
    // Only trigger when transitioning from online to offline
    if (!isOnline && lastOfflineAt && Date.now() - lastOfflineAt < 1000) {
      callback();
    }
  }, [isOnline, lastOfflineAt, callback]);
}
