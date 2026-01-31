/**
 * Network Status Store (Issue #2054)
 *
 * Centralized network status management with:
 * - Browser online/offline event detection
 * - Connection quality tracking
 * - Automatic reconnection coordination
 * - SSR-safe implementation
 *
 * Usage:
 * ```tsx
 * const { isOnline, connectionQuality } = useNetworkStatus();
 * ```
 */

import { create } from 'zustand';
import { devtools, subscribeWithSelector } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

// ============================================================================
// Types
// ============================================================================

export type ConnectionQuality = 'excellent' | 'good' | 'poor' | 'offline';

export interface NetworkStatusState {
  /** Whether browser reports online status */
  isOnline: boolean;

  /** Connection quality based on Navigator.connection (if available) */
  connectionQuality: ConnectionQuality;

  /** Timestamp of last online status change */
  lastOnlineAt: number | null;

  /** Timestamp of last offline status change */
  lastOfflineAt: number | null;

  /** Whether we're in the process of reconnecting */
  isReconnecting: boolean;

  /** Number of reconnection attempts since last offline */
  reconnectAttempts: number;

  /** Effective connection type from Navigator.connection */
  effectiveType: string | null;

  /** Downlink speed in Mbps (if available) */
  downlink: number | null;

  /** Round-trip time in ms (if available) */
  rtt: number | null;
}

export interface NetworkStatusActions {
  /** Update online status (called by event listeners) */
  setOnline: (online: boolean) => void;

  /** Update connection info from Navigator.connection */
  updateConnectionInfo: () => void;

  /** Set reconnecting state */
  setReconnecting: (reconnecting: boolean) => void;

  /** Increment reconnect attempts */
  incrementReconnectAttempts: () => void;

  /** Reset reconnect attempts (on successful connection) */
  resetReconnectAttempts: () => void;

  /** Initialize store (sets up event listeners) */
  initialize: () => () => void;
}

export type NetworkStatusStore = NetworkStatusState & NetworkStatusActions;

// ============================================================================
// Initial State
// ============================================================================

const initialState: NetworkStatusState = {
  isOnline: typeof navigator !== 'undefined' ? navigator.onLine : true,
  connectionQuality: 'excellent',
  lastOnlineAt: null,
  lastOfflineAt: null,
  isReconnecting: false,
  reconnectAttempts: 0,
  effectiveType: null,
  downlink: null,
  rtt: null,
};

// ============================================================================
// Connection Quality Calculator
// ============================================================================

function calculateConnectionQuality(
  isOnline: boolean,
  effectiveType: string | null,
  downlink: number | null,
  rtt: number | null
): ConnectionQuality {
  if (!isOnline) return 'offline';

  // Use effective connection type if available
  if (effectiveType) {
    switch (effectiveType) {
      case '4g':
        return 'excellent';
      case '3g':
        return 'good';
      case '2g':
      case 'slow-2g':
        return 'poor';
      default:
        break;
    }
  }

  // Fallback to downlink/RTT analysis
  if (downlink !== null && rtt !== null) {
    if (downlink >= 10 && rtt < 100) return 'excellent';
    if (downlink >= 1 && rtt < 300) return 'good';
    return 'poor';
  }

  // Default to good if online but no connection info
  return 'good';
}

// ============================================================================
// Store Creation
// ============================================================================

export const useNetworkStatusStore = create<NetworkStatusStore>()(
  devtools(
    subscribeWithSelector(
      immer((set, get) => ({
        ...initialState,

        setOnline: (online: boolean) => {
          set(state => {
            const wasOnline = state.isOnline;
            state.isOnline = online;

            if (online && !wasOnline) {
              state.lastOnlineAt = Date.now();
              state.isReconnecting = false;
            } else if (!online && wasOnline) {
              state.lastOfflineAt = Date.now();
            }

            // Recalculate connection quality
            state.connectionQuality = calculateConnectionQuality(
              online,
              state.effectiveType,
              state.downlink,
              state.rtt
            );
          });
        },

        updateConnectionInfo: () => {
          if (typeof navigator === 'undefined') return;

          // Navigator.connection is not available in all browsers
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const connection = (navigator as any).connection;
          if (!connection) return;

          set(state => {
            state.effectiveType = connection.effectiveType ?? null;
            state.downlink = connection.downlink ?? null;
            state.rtt = connection.rtt ?? null;
            state.connectionQuality = calculateConnectionQuality(
              state.isOnline,
              state.effectiveType,
              state.downlink,
              state.rtt
            );
          });
        },

        setReconnecting: (reconnecting: boolean) => {
          set(state => {
            state.isReconnecting = reconnecting;
          });
        },

        incrementReconnectAttempts: () => {
          set(state => {
            state.reconnectAttempts += 1;
          });
        },

        resetReconnectAttempts: () => {
          set(state => {
            state.reconnectAttempts = 0;
            state.isReconnecting = false;
          });
        },

        initialize: () => {
          if (typeof window === 'undefined') {
            // SSR: return no-op cleanup
            return () => {};
          }

          const store = get();

          // Initial connection info update
          store.updateConnectionInfo();

          // Online/offline event handlers
          const handleOnline = () => store.setOnline(true);
          const handleOffline = () => store.setOnline(false);

          // Connection change handler (Navigator.connection API)
          const handleConnectionChange = () => store.updateConnectionInfo();

          // Add event listeners
          window.addEventListener('online', handleOnline);
          window.addEventListener('offline', handleOffline);

          // Navigator.connection events (if available)
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          const connection = (navigator as any).connection;
          if (connection) {
            connection.addEventListener('change', handleConnectionChange);
          }

          // Return cleanup function
          return () => {
            window.removeEventListener('online', handleOnline);
            window.removeEventListener('offline', handleOffline);
            if (connection) {
              connection.removeEventListener('change', handleConnectionChange);
            }
          };
        },
      }))
    ),
    {
      name: 'NetworkStatusStore',
      enabled: process.env.NODE_ENV === 'development',
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

export const selectIsOnline = (state: NetworkStatusStore) => state.isOnline;
export const selectConnectionQuality = (state: NetworkStatusStore) => state.connectionQuality;
export const selectIsReconnecting = (state: NetworkStatusStore) => state.isReconnecting;
export const selectCanAttemptReconnect = (state: NetworkStatusStore) =>
  state.isOnline && !state.isReconnecting;
