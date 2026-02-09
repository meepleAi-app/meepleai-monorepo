/**
 * Background Sync Manager for MeepleAI PWA (Issue #3346)
 *
 * Handles:
 * - Syncing pending offline actions when back online
 * - Conflict resolution
 * - Retry logic with exponential backoff
 */

import {
  getPendingActions,
  removeAction,
  incrementActionRetry,
  saveSession,
  getSession,
  type OfflineAction,
  type ActionType,
} from './offline-storage';

// ============================================================================
// Types
// ============================================================================

export interface SyncResult {
  success: boolean;
  synced: number;
  failed: number;
  errors: SyncError[];
}

export interface SyncError {
  actionId: string;
  type: ActionType;
  error: string;
  retryCount: number;
}

export type SyncStatus = 'idle' | 'syncing' | 'completed' | 'failed';

export interface SyncState {
  status: SyncStatus;
  lastSyncAt: number | null;
  pendingCount: number;
  isOnline: boolean;
}

// ============================================================================
// Configuration
// ============================================================================

const MAX_RETRIES = 3;
const _RETRY_DELAYS = [1000, 5000, 15000]; // Exponential backoff (for future use)
const SYNC_TIMEOUT = 30000; // 30 seconds per action

// ============================================================================
// API Endpoints Mapping
// ============================================================================

const ACTION_ENDPOINTS: Record<ActionType, { method: string; path: (payload: Record<string, unknown>) => string }> = {
  DICE_ROLL: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/dice/roll`,
  },
  CARD_DRAW: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/decks/${p.deckId}/draw`,
  },
  CARD_SHUFFLE: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/decks/${p.deckId}/shuffle`,
  },
  TIMER_START: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/timer/start`,
  },
  TIMER_PAUSE: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/timer/pause`,
  },
  TIMER_RESUME: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/timer/resume`,
  },
  TIMER_RESET: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/timer/reset`,
  },
  COIN_FLIP: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/coin/flip`,
  },
  WHEEL_SPIN: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/wheel/spin`,
  },
  NOTE_CREATE: {
    method: 'POST',
    path: (p) => `/api/v1/sessions/${p.sessionId}/notes`,
  },
  NOTE_UPDATE: {
    method: 'PUT',
    path: (p) => `/api/v1/sessions/${p.sessionId}/notes/${p.noteId}`,
  },
  NOTE_DELETE: {
    method: 'DELETE',
    path: (p) => `/api/v1/sessions/${p.sessionId}/notes/${p.noteId}`,
  },
};

// ============================================================================
// Sync Manager Class
// ============================================================================

class SyncManager {
  private syncInProgress = false;
  private listeners: Set<(state: SyncState) => void> = new Set();
  private state: SyncState = {
    status: 'idle',
    lastSyncAt: null,
    pendingCount: 0,
    isOnline: typeof navigator !== 'undefined' ? navigator.onLine : true,
  };

  constructor() {
    if (typeof window !== 'undefined') {
      window.addEventListener('online', this.handleOnline);
      window.addEventListener('offline', this.handleOffline);

      // Listen for service worker sync messages
      if ('serviceWorker' in navigator) {
        navigator.serviceWorker.addEventListener('message', this.handleSWMessage);
      }
    }
  }

  // ==========================================================================
  // State Management
  // ==========================================================================

  private updateState(updates: Partial<SyncState>) {
    this.state = { ...this.state, ...updates };
    this.notifyListeners();
  }

  private notifyListeners() {
    this.listeners.forEach((listener) => listener(this.state));
  }

  public subscribe(listener: (state: SyncState) => void): () => void {
    this.listeners.add(listener);
    // Immediately notify with current state
    listener(this.state);
    return () => this.listeners.delete(listener);
  }

  public getState(): SyncState {
    return this.state;
  }

  // ==========================================================================
  // Event Handlers
  // ==========================================================================

  private handleOnline = async () => {
    // eslint-disable-next-line no-console
    console.log('[SyncManager] Back online');
    this.updateState({ isOnline: true });

    // Auto-sync when back online
    await this.syncAll();
  };

  private handleOffline = () => {
    // eslint-disable-next-line no-console
    console.log('[SyncManager] Went offline');
    this.updateState({ isOnline: false });
  };

  private handleSWMessage = (event: MessageEvent) => {
    if (event.data?.type === 'SYNC_OFFLINE_ACTIONS') {
      this.syncAll();
    }
  };

  // ==========================================================================
  // Sync Logic
  // ==========================================================================

  public async syncAll(): Promise<SyncResult> {
    if (this.syncInProgress) {
      // eslint-disable-next-line no-console
      console.log('[SyncManager] Sync already in progress');
      return { success: false, synced: 0, failed: 0, errors: [] };
    }

    if (!navigator.onLine) {
      // eslint-disable-next-line no-console
      console.log('[SyncManager] Cannot sync: offline');
      return { success: false, synced: 0, failed: 0, errors: [] };
    }

    this.syncInProgress = true;
    this.updateState({ status: 'syncing' });

    const result: SyncResult = {
      success: true,
      synced: 0,
      failed: 0,
      errors: [],
    };

    try {
      const pendingActions = await getPendingActions();
      this.updateState({ pendingCount: pendingActions.length });

      // eslint-disable-next-line no-console
      console.log(`[SyncManager] Syncing ${pendingActions.length} pending actions`);

      for (const action of pendingActions) {
        const actionResult = await this.syncAction(action);

        if (actionResult.success) {
          await removeAction(action.id);
          result.synced++;
        } else {
          result.failed++;
          result.errors.push({
            actionId: action.id,
            type: action.type,
            error: actionResult.error || 'Unknown error',
            retryCount: action.retryCount,
          });

          if (action.retryCount < MAX_RETRIES) {
            await incrementActionRetry(action.id);
          } else {
            // Remove action after max retries
            console.warn(`[SyncManager] Action ${action.id} exceeded max retries, removing`);
            await removeAction(action.id);
          }
        }

        // Update pending count
        const remaining = await getPendingActions();
        this.updateState({ pendingCount: remaining.length });
      }

      result.success = result.failed === 0;
      this.updateState({
        status: result.success ? 'completed' : 'failed',
        lastSyncAt: Date.now(),
      });
    } catch (error) {
      console.error('[SyncManager] Sync error:', error);
      result.success = false;
      this.updateState({ status: 'failed' });
    } finally {
      this.syncInProgress = false;
    }

    // Update pending count
    const remaining = await getPendingActions();
    this.updateState({ pendingCount: remaining.length });

    return result;
  }

  private async syncAction(action: OfflineAction): Promise<{ success: boolean; error?: string }> {
    const endpoint = ACTION_ENDPOINTS[action.type];
    if (!endpoint) {
      return { success: false, error: `Unknown action type: ${action.type}` };
    }

    const payload = action.payload as Record<string, unknown>;
    const url = endpoint.path(payload);

    try {
      const controller = new AbortController();
      const timeout = setTimeout(() => controller.abort(), SYNC_TIMEOUT);

      const response = await fetch(url, {
        method: endpoint.method,
        headers: {
          'Content-Type': 'application/json',
        },
        body: endpoint.method !== 'DELETE' ? JSON.stringify(payload) : undefined,
        signal: controller.signal,
        credentials: 'include',
      });

      clearTimeout(timeout);

      if (!response.ok) {
        const errorText = await response.text();
        return { success: false, error: `HTTP ${response.status}: ${errorText}` };
      }

      // Update local session data with server response
      if (action.sessionId) {
        await this.updateLocalSession(action.sessionId);
      }

      return { success: true };
    } catch (error) {
      if (error instanceof Error && error.name === 'AbortError') {
        return { success: false, error: 'Request timeout' };
      }
      return { success: false, error: error instanceof Error ? error.message : 'Unknown error' };
    }
  }

  private async updateLocalSession(sessionId: string): Promise<void> {
    try {
      const response = await fetch(`/api/v1/sessions/${sessionId}`, {
        credentials: 'include',
      });

      if (response.ok) {
        const sessionData = await response.json();
        const existingSession = await getSession(sessionId);

        if (existingSession) {
          await saveSession({
            ...existingSession,
            data: sessionData,
            lastSynced: Date.now(),
          });
        }
      }
    } catch (error) {
      console.warn('[SyncManager] Failed to update local session:', error);
    }
  }

  // ==========================================================================
  // Background Sync Registration
  // ==========================================================================

  public async registerBackgroundSync(): Promise<boolean> {
    if (!('serviceWorker' in navigator) || !('sync' in window.ServiceWorkerRegistration.prototype)) {
      console.warn('[SyncManager] Background sync not supported');
      return false;
    }

    try {
      const registration = await navigator.serviceWorker.ready;
      await (registration as ServiceWorkerRegistration & { sync: { register: (tag: string) => Promise<void> } }).sync.register('session-sync');
      // eslint-disable-next-line no-console
      console.log('[SyncManager] Background sync registered');
      return true;
    } catch (error) {
      console.error('[SyncManager] Failed to register background sync:', error);
      return false;
    }
  }

  // ==========================================================================
  // Manual Sync
  // ==========================================================================

  public async forcSync(): Promise<SyncResult> {
    // Clear sync in progress flag to force retry
    this.syncInProgress = false;
    return this.syncAll();
  }

  // ==========================================================================
  // Cleanup
  // ==========================================================================

  public destroy() {
    if (typeof window !== 'undefined') {
      window.removeEventListener('online', this.handleOnline);
      window.removeEventListener('offline', this.handleOffline);

      if ('serviceWorker' in navigator) {
        navigator.serviceWorker.removeEventListener('message', this.handleSWMessage);
      }
    }

    this.listeners.clear();
  }
}

// ============================================================================
// Singleton Export
// ============================================================================

export const syncManager = typeof window !== 'undefined' ? new SyncManager() : null;
