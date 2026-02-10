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
      console.error('[OfflineStorage] Failed to open database:', request.error);
      reject(request.error);
    };

    request.onsuccess = () => {
      dbInstance = request.result;
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] Database opened successfully');
      resolve(dbInstance);
    };

    request.onupgradeneeded = (event) => {
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

      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] Database schema created/upgraded');
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
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] Session saved:', session.id);
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

export async function getAllSessions(): Promise<OfflineSession[]> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.SESSIONS, 'readonly');
    const store = transaction.objectStore(STORES.SESSIONS);

    const request = store.getAll();

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result);
  });
}

export async function deleteSession(id: string): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction([STORES.SESSIONS, STORES.ACTIONS], 'readwrite');
    const sessionsStore = transaction.objectStore(STORES.SESSIONS);
    const actionsStore = transaction.objectStore(STORES.ACTIONS);

    // Delete session
    sessionsStore.delete(id);

    // Delete associated actions
    const actionIndex = actionsStore.index('sessionId');
    const actionRequest = actionIndex.openCursor(IDBKeyRange.only(id));

    actionRequest.onsuccess = (event) => {
      const cursor = (event.target as IDBRequest).result;
      if (cursor) {
        cursor.delete();
        cursor.continue();
      }
    };

    transaction.oncomplete = () => {
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] Session deleted:', id);
      resolve();
    };

    transaction.onerror = () => reject(transaction.error);
  });
}

// ============================================================================
// Pending Actions Queue
// ============================================================================

export async function queueAction(action: Omit<OfflineAction, 'id' | 'timestamp' | 'retryCount'>): Promise<string> {
  const db = await initOfflineStorage();
  const id = crypto.randomUUID();

  const fullAction: OfflineAction = {
    ...action,
    id,
    timestamp: Date.now(),
    retryCount: 0,
  };

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.ACTIONS, 'readwrite');
    const store = transaction.objectStore(STORES.ACTIONS);

    const request = store.add(fullAction);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] Action queued:', fullAction.type, id);
      resolve(id);
    };
  });
}

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
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] Action removed:', id);
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

export async function clearAllActions(): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.ACTIONS, 'readwrite');
    const store = transaction.objectStore(STORES.ACTIONS);

    const request = store.clear();

    request.onerror = () => reject(request.error);
    request.onsuccess = () => {
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] All actions cleared');
      resolve();
    };
  });
}

// ============================================================================
// Games Cache
// ============================================================================

export async function cacheGame(game: CachedGame): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.GAMES, 'readwrite');
    const store = transaction.objectStore(STORES.GAMES);

    const request = store.put({
      ...game,
      cachedAt: Date.now(),
    });

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve();
  });
}

export async function getCachedGame(id: string): Promise<CachedGame | null> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.GAMES, 'readonly');
    const store = transaction.objectStore(STORES.GAMES);

    const request = store.get(id);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result || null);
  });
}

export async function clearOldGamesCache(maxAge: number = 7 * 24 * 60 * 60 * 1000): Promise<void> {
  const db = await initOfflineStorage();
  const cutoff = Date.now() - maxAge;

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.GAMES, 'readwrite');
    const store = transaction.objectStore(STORES.GAMES);
    const index = store.index('cachedAt');

    const request = index.openCursor(IDBKeyRange.upperBound(cutoff));

    request.onsuccess = (event) => {
      const cursor = (event.target as IDBRequest).result;
      if (cursor) {
        cursor.delete();
        cursor.continue();
      }
    };

    transaction.oncomplete = () => resolve();
    transaction.onerror = () => reject(transaction.error);
  });
}

// ============================================================================
// Metadata
// ============================================================================

export async function setMetadata(key: string, value: unknown): Promise<void> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.METADATA, 'readwrite');
    const store = transaction.objectStore(STORES.METADATA);

    const request = store.put({ key, value, updatedAt: Date.now() });

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve();
  });
}

export async function getMetadata<T>(key: string): Promise<T | null> {
  const db = await initOfflineStorage();

  return new Promise((resolve, reject) => {
    const transaction = db.transaction(STORES.METADATA, 'readonly');
    const store = transaction.objectStore(STORES.METADATA);

    const request = store.get(key);

    request.onerror = () => reject(request.error);
    request.onsuccess = () => resolve(request.result?.value ?? null);
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
    new Promise<number>((resolve) => {
      const transaction = db.transaction(STORES.SESSIONS, 'readonly');
      const store = transaction.objectStore(STORES.SESSIONS);
      const request = store.count();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => resolve(0);
    }),
    new Promise<number>((resolve) => {
      const transaction = db.transaction(STORES.ACTIONS, 'readonly');
      const store = transaction.objectStore(STORES.ACTIONS);
      const request = store.count();
      request.onsuccess = () => resolve(request.result);
      request.onerror = () => resolve(0);
    }),
    new Promise<number>((resolve) => {
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
      // eslint-disable-next-line no-console
      console.log('[OfflineStorage] All data cleared');
      resolve();
    };

    transaction.onerror = () => reject(transaction.error);
  });
}
