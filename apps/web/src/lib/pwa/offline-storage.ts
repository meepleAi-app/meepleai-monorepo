import { logger } from '@/lib/logger';

/**
 * IndexedDB Offline Storage for MeepleAI PWA (Issue #3346)
 *
 * Provides persistent offline storage for:
 * - Session data
 * - Pending actions queue
 * - Cached game data
 */

// ============================================================================
// Types
// ============================================================================

export interface OfflineSession {
  id: string;
  data: SessionData;
  pendingActions: OfflineAction[];
  lastSynced: number;
  lastModified: number;
}

export interface SessionData {
  id: string;
  name: string;
  gameId?: string;
  gameName?: string;
  participants: Participant[];
  status: 'active' | 'paused' | 'completed';
  createdAt: string;
  [key: string]: unknown;
}

export interface Participant {
  id: string;
  name: string;
  color?: string;
  isHost: boolean;
}

export interface OfflineAction {
  id: string;
  sessionId: string;
  type: ActionType;
  payload: unknown;
  timestamp: number;
  retryCount: number;
}

export type ActionType =
  | 'DICE_ROLL'
  | 'CARD_DRAW'
  | 'CARD_SHUFFLE'
  | 'TIMER_START'
  | 'TIMER_PAUSE'
  | 'TIMER_RESUME'
  | 'TIMER_RESET'
  | 'COIN_FLIP'
  | 'WHEEL_SPIN'
  | 'NOTE_CREATE'
  | 'NOTE_UPDATE'
  | 'NOTE_DELETE';

export interface CachedGame {
  id: string;
  name: string;
  description?: string;
  imageUrl?: string;
  cachedAt: number;
}

// ============================================================================
// Database Configuration
// ============================================================================

const DB_NAME = 'meepleai-offline';
const DB_VERSION = 1;

const STORES = {
  SESSIONS: 'sessions',
  ACTIONS: 'actions',
  GAMES: 'games',
  METADATA: 'metadata',
} as const;

// ============================================================================
// Database Initialization
// ============================================================================

let dbInstance: IDBDatabase | null = null;

export async function initOfflineStorage(): Promise<IDBDatabase> {
  if (dbInstance) {
    return dbInstance;
  }

  return new Promise((resolve, reject) => {
    const request = indexedDB.open(DB_NAME, DB_VERSION);

    request.onerror = () => {
      logger.error('[OfflineStorage] Failed to open database:', request.error);
      reject(request.error);
    };

    request.onsuccess = () => {
      dbInstance = request.result;
      if (process.env.NODE_ENV !== 'production') {
        logger.debug('[OfflineStorage] Database opened successfully');
      }
      resolve(dbInstance);
    };

    request.onupgradeneeded = event => {
      const db = (event.target as IDBOpenDBRequest).result;

      // Sessions store
      if (!db.objectStoreNames.contains(STORES.SESSIONS)) {
        const sessionsStore = db.createObjectStore(STORES.SESSIONS, { keyPath: 'id' });
        sessionsStore.createIndex('lastModified', 'lastModified', { unique: false });
        sessionsStore.createIndex('lastSynced', 'lastSynced', { unique: false });
      }

      // Actions store (pending offline actions)
      if (!db.objectStoreNames.contains(STORES.ACTIONS)) {
        const actionsStore = db.createObjectStore(STORES.ACTIONS, { keyPath: 'id' });
        actionsStore.createIndex('sessionId', 'sessionId', { unique: false });
        actionsStore.createIndex('timestamp', 'timestamp', { unique: false });
        actionsStore.createIndex('type', 'type', { unique: false });
      }

      // Games cache store
      if (!db.objectStoreNames.contains(STORES.GAMES)) {
        const gamesStore = db.createObjectStore(STORES.GAMES, { keyPath: 'id' });
        gamesStore.createIndex('cachedAt', 'cachedAt', { unique: false });
      }

      // Metadata store
      if (!db.objectStoreNames.contains(STORES.METADATA)) {
        db.createObjectStore(STORES.METADATA, { keyPath: 'key' });
      }

      if (process.env.NODE_ENV !== 'production') {
        logger.debug('[OfflineStorage] Database schema created/upgraded');
      }
    };
  });
}

// ============================================================================
// Sessions CRUD
// ============================================================================

export async function saveSession(session: OfflineSession): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.SESSIONS, 'readwrite');
    const store = transaction.objectStore(STORES.SESSIONS);

    const request = store.put({
      ...session,
      lastModified: Date.now(),
    });

    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      if (process.env.NODE_ENV !== 'production') {
        logger.debug(`[OfflineStorage] Session saved: ${session.id}`);
      }
      resolve();
    };
  });
}

export async function getSession(id: string): Promise<OfflineSession | null> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.SESSIONS, 'readonly');
    const store = transaction.objectStore(STORES.SESSIONS);

    const request = store.get(id);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result || null);
  });
}

// ============================================================================
// Pending Actions Queue
// ============================================================================

export async function getPendingActions(sessionId?: string): Promise<OfflineAction[]> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.ACTIONS, 'readonly');
    const store = transaction.objectStore(STORES.ACTIONS);

    let request: IDBRequest;

    if (sessionId) {
      const index = store.index('sessionId');
      request = index.getAll(IDBKeyRange.only(sessionId));
    } else {
      request = store.getAll();
    }

    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      // Sort by timestamp ascending
      const actions = request.result as OfflineAction[];
      actions.sort((a, b) => a.timestamp - b.timestamp);
      resolve(actions);
    };
  });
}

export async function removeAction(id: string): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.ACTIONS, 'readwrite');
    const store = transaction.objectStore(STORES.ACTIONS);

    const request = store.delete(id);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      if (process.env.NODE_ENV !== 'production') {
        logger.debug(`[OfflineStorage] Action removed: ${id}`);
      }
      resolve();
    };
  });
}

export async function incrementActionRetry(id: string): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.ACTIONS, 'readwrite');
    const store = transaction.objectStore(STORES.ACTIONS);

    const getRequest = store.get(id);

    getRequest.onsuccess = () => {
      const action = getRequest.result as OfflineAction;
      if (action) {
        action.retryCount += 1;
        store.put(action);
      }
      resolve();
    };

    getRequest.onerror = () => reject(getRequest.error);
  });
}

// ============================================================================
// Utility Functions
// ============================================================================

export async function getStorageStats(): Promise<{
  sessions: number;
  pendingActions: number;
  cachedGames: number;
}> {
  const db = await initOfflineStorage();

  const counts = await Promise.all([
    new Promise<number>(resolve => {
      const transaction = db.transaction(STORES.SESSIONS, 'readonly');
      const store = transaction.objectStore(STORES.SESSIONS);
      const request = store.count();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => resolve(0);
    }),
    new Promise<number>(resolve => {
      const transaction = db.transaction(STORES.ACTIONS, 'readonly');
      const store = transaction.objectStore(STORES.ACTIONS);
      const request = store.count();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => resolve(0);
    }),
    new Promise<number>(resolve => {
      const transaction = db.transaction(STORES.GAMES, 'readonly');
      const store = transaction.objectStore(STORES.GAMES);
      const request = store.count();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => resolve(0);
    }),
  ]);

  return {
    sessions: counts[0],
    pendingActions: counts[1],
    cachedGames: counts[2],
  };
}

export async function clearAllData(): Promise<void> {
  const db = await initOfflineStorage();

  const storeNames = Object.values(STORES);

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(storeNames, 'readwrite');

    for (const storeName of storeNames) {
      transaction.objectStore(storeName).clear();
    }

    transaction.oncomplete = () => {
      if (process.env.NODE_ENV !== 'production') {
        logger.debug('[OfflineStorage] All data cleared');
      }
      resolve();
    };

    transaction.onerror = () => reject(transaction.error);
  });
}
