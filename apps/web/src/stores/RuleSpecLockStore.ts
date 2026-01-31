/**
 * RuleSpec Lock Store (Issue #2055)
 *
 * Centralized editor lock management with:
 * - Lock acquisition/release lifecycle
 * - Auto-refresh with TTL extension
 * - Conflict detection and resolution
 * - Presence tracking for other editors
 * - SSR-safe implementation
 *
 * Usage:
 * ```tsx
 * const { acquireLock, releaseLock, lockStatus } = useRuleSpecLockStore();
 * ```
 */

import { create } from 'zustand';
import { devtools, subscribeWithSelector } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';

import type { EditorLock, RuleSpec, RuleSpecConflict } from '@/lib/api/schemas';

// ============================================================================
// Types
// ============================================================================

export type LockAcquisitionStatus = 'idle' | 'acquiring' | 'acquired' | 'failed' | 'conflict';

export interface RuleSpecLockState {
  /** Current game ID being edited */
  gameId: string | null;

  /** Current lock status from server */
  lockStatus: EditorLock | null;

  /** Lock acquisition state */
  acquisitionStatus: LockAcquisitionStatus;

  /** Error message if lock acquisition failed */
  lockError: string | null;

  /** Current ETag for optimistic concurrency */
  currentETag: string | null;

  /** Conflict information when save fails due to concurrent edit */
  conflict: RuleSpecConflict | null;

  /** Whether conflict modal should be shown */
  showConflictModal: boolean;

  /** Interval ID for lock refresh (stored for cleanup) */
  refreshIntervalId: NodeJS.Timeout | null;

  /** Timestamp of last successful lock refresh */
  lastRefreshAt: number | null;

  /** Whether the lock is being refreshed */
  isRefreshing: boolean;

  /** Local version of RuleSpec for conflict resolution */
  localVersion: RuleSpec | null;

  /** Remote version from server for conflict resolution */
  remoteVersion: RuleSpec | null;
}

export interface RuleSpecLockActions {
  /** Initialize lock for a game (acquire lock and start refresh) */
  initializeLock: (gameId: string, api: LockApiClient) => Promise<boolean>;

  /** Release lock and cleanup */
  releaseLock: (api: LockApiClient) => Promise<void>;

  /** Refresh lock to extend TTL */
  refreshLock: (api: LockApiClient) => Promise<boolean>;

  /** Check lock status without acquiring */
  checkLockStatus: (gameId: string, api: LockApiClient) => Promise<EditorLock | null>;

  /** Set current ETag for concurrency tracking */
  setCurrentETag: (etag: string | null) => void;

  /** Handle conflict detected during save */
  handleConflict: (localVersion: RuleSpec, remoteVersion: RuleSpec, reason: string) => void;

  /** Resolve conflict by choosing local or remote version */
  resolveConflict: (choice: 'local' | 'remote' | 'merge') => RuleSpec | null;

  /** Close conflict modal */
  closeConflictModal: () => void;

  /** Reset store state */
  reset: () => void;

  /** Cleanup intervals and state */
  cleanup: () => void;
}

export type RuleSpecLockStore = RuleSpecLockState & RuleSpecLockActions;

/** API client interface for lock operations */
export interface LockApiClient {
  acquireEditorLock: (
    gameId: string
  ) => Promise<{ success: boolean; lock: EditorLock | null; message?: string | null }>;
  releaseEditorLock: (gameId: string) => Promise<void>;
  refreshEditorLock: (
    gameId: string
  ) => Promise<{ success: boolean; lock: EditorLock | null; message?: string | null }>;
  getEditorLockStatus: (gameId: string) => Promise<EditorLock | null>;
}

// ============================================================================
// Constants
// ============================================================================

/** Lock refresh interval (2 minutes - less than 5 min TTL) */
const LOCK_REFRESH_INTERVAL_MS = 2 * 60 * 1000;

// ============================================================================
// Initial State
// ============================================================================

const initialState: RuleSpecLockState = {
  gameId: null,
  lockStatus: null,
  acquisitionStatus: 'idle',
  lockError: null,
  currentETag: null,
  conflict: null,
  showConflictModal: false,
  refreshIntervalId: null,
  lastRefreshAt: null,
  isRefreshing: false,
  localVersion: null,
  remoteVersion: null,
};

// ============================================================================
// Store Creation
// ============================================================================

export const useRuleSpecLockStore = create<RuleSpecLockStore>()(
  devtools(
    subscribeWithSelector(
      immer((set, get) => ({
        ...initialState,

        initializeLock: async (gameId: string, api: LockApiClient): Promise<boolean> => {
          // Cleanup any existing lock first
          const currentState = get();
          if (currentState.refreshIntervalId) {
            clearInterval(currentState.refreshIntervalId);
          }

          set(state => {
            state.gameId = gameId;
            state.acquisitionStatus = 'acquiring';
            state.lockError = null;
            state.refreshIntervalId = null;
          });

          try {
            const result = await api.acquireEditorLock(gameId);

            if (result.success && result.lock) {
              // Start refresh interval
              const intervalId = setInterval(() => {
                void get().refreshLock(api);
              }, LOCK_REFRESH_INTERVAL_MS);

              set(state => {
                state.lockStatus = result.lock;
                state.acquisitionStatus = 'acquired';
                state.refreshIntervalId = intervalId;
                state.lastRefreshAt = Date.now();
              });

              return true;
            } else {
              set(state => {
                state.lockStatus = result.lock;
                state.acquisitionStatus = result.lock?.isLocked ? 'conflict' : 'failed';
                state.lockError = result.message ?? 'Failed to acquire lock';
              });

              return false;
            }
          } catch (error) {
            set(state => {
              state.acquisitionStatus = 'failed';
              state.lockError =
                error instanceof Error ? error.message : 'Unknown error acquiring lock';
            });

            return false;
          }
        },

        releaseLock: async (api: LockApiClient): Promise<void> => {
          const { gameId, refreshIntervalId } = get();

          // Clear refresh interval
          if (refreshIntervalId) {
            clearInterval(refreshIntervalId);
          }

          if (gameId) {
            try {
              await api.releaseEditorLock(gameId);
            } catch (error) {
              // Log but don't throw - lock will expire anyway
              console.warn('Failed to release lock:', error);
            }
          }

          set(state => {
            state.lockStatus = null;
            state.acquisitionStatus = 'idle';
            state.refreshIntervalId = null;
            state.lastRefreshAt = null;
          });
        },

        refreshLock: async (api: LockApiClient): Promise<boolean> => {
          const { gameId, acquisitionStatus } = get();

          if (!gameId || acquisitionStatus !== 'acquired') {
            return false;
          }

          set(state => {
            state.isRefreshing = true;
          });

          try {
            const result = await api.refreshEditorLock(gameId);

            if (result.success && result.lock) {
              set(state => {
                state.lockStatus = result.lock;
                state.lastRefreshAt = Date.now();
                state.isRefreshing = false;
              });

              return true;
            } else {
              // Lock was lost (possibly expired or taken by another user)
              set(state => {
                state.lockStatus = result.lock;
                state.acquisitionStatus = 'conflict';
                state.lockError = result.message ?? 'Lock refresh failed';
                state.isRefreshing = false;
              });

              return false;
            }
          } catch (error) {
            set(state => {
              state.isRefreshing = false;
              state.lockError = error instanceof Error ? error.message : 'Lock refresh error';
            });

            return false;
          }
        },

        checkLockStatus: async (gameId: string, api: LockApiClient): Promise<EditorLock | null> => {
          try {
            const status = await api.getEditorLockStatus(gameId);
            return status;
          } catch (error) {
            console.warn('Failed to check lock status:', error);
            return null;
          }
        },

        setCurrentETag: (etag: string | null) => {
          set(state => {
            state.currentETag = etag;
          });
        },

        handleConflict: (localVersion: RuleSpec, remoteVersion: RuleSpec, reason: string) => {
          set(state => {
            state.conflict = {
              localVersion,
              remoteVersion,
              conflictReason: reason,
            };
            state.localVersion = localVersion;
            state.remoteVersion = remoteVersion;
            state.showConflictModal = true;
            state.acquisitionStatus = 'conflict';
          });
        },

        resolveConflict: (choice: 'local' | 'remote' | 'merge'): RuleSpec | null => {
          const { localVersion, remoteVersion } = get();

          if (!localVersion || !remoteVersion) {
            return null;
          }

          let resolvedVersion: RuleSpec | null = null;

          switch (choice) {
            case 'local':
              resolvedVersion = localVersion;
              break;
            case 'remote':
              resolvedVersion = remoteVersion;
              break;
            case 'merge': {
              // For merge, combine atoms from both versions (simple strategy)
              // In a real implementation, you'd want a more sophisticated merge
              const localAtomIds = new Set(localVersion.atoms.map(a => a.id));
              const mergedAtoms = [
                ...localVersion.atoms,
                ...remoteVersion.atoms.filter(a => !localAtomIds.has(a.id)),
              ];
              resolvedVersion = {
                ...remoteVersion, // Use remote metadata (version, timestamps)
                atoms: mergedAtoms,
              };
              break;
            }
          }

          set(state => {
            state.conflict = null;
            state.showConflictModal = false;
            state.localVersion = null;
            state.remoteVersion = null;
            state.acquisitionStatus = 'acquired';
          });

          return resolvedVersion;
        },

        closeConflictModal: () => {
          set(state => {
            state.showConflictModal = false;
          });
        },

        reset: () => {
          const { refreshIntervalId } = get();
          if (refreshIntervalId) {
            clearInterval(refreshIntervalId);
          }

          set(() => ({ ...initialState }));
        },

        cleanup: () => {
          const { refreshIntervalId } = get();
          if (refreshIntervalId) {
            clearInterval(refreshIntervalId);
          }

          set(state => {
            state.refreshIntervalId = null;
          });
        },
      }))
    ),
    {
      name: 'RuleSpecLockStore',
      enabled: process.env.NODE_ENV === 'development',
    }
  )
);

// ============================================================================
// Selectors
// ============================================================================

export const selectGameId = (state: RuleSpecLockStore) => state.gameId;
export const selectLockStatus = (state: RuleSpecLockStore) => state.lockStatus;
export const selectAcquisitionStatus = (state: RuleSpecLockStore) => state.acquisitionStatus;
export const selectHasLock = (state: RuleSpecLockStore) =>
  state.acquisitionStatus === 'acquired' && state.lockStatus?.isCurrentUserLock === true;
export const selectIsLockedByOther = (state: RuleSpecLockStore) =>
  state.lockStatus?.isLocked === true && state.lockStatus?.isCurrentUserLock === false;
export const selectLockHolder = (state: RuleSpecLockStore) =>
  state.lockStatus?.lockedByUserEmail ?? null;
export const selectLockError = (state: RuleSpecLockStore) => state.lockError;
export const selectShowConflictModal = (state: RuleSpecLockStore) => state.showConflictModal;
export const selectConflict = (state: RuleSpecLockStore) => state.conflict;
export const selectCurrentETag = (state: RuleSpecLockStore) => state.currentETag;
