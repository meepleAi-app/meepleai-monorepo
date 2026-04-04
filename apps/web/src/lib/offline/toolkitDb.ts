import { openDB, type IDBPDatabase } from 'idb';

export type ToolkitOperationType = 'dice_roll' | 'score_update' | 'add_note' | 'card_draw';

export interface PendingOperation {
  id: string;
  sessionId: string;
  type: ToolkitOperationType;
  payload: Record<string, unknown>;
  timestamp: string;
  synced: boolean;
}

const DB_NAME = 'meepleai-offline';
const STORE_NAME = 'pending-ops';
const DB_VERSION = 1;

let dbInstance: IDBPDatabase | null = null;
let dbPromise: Promise<IDBPDatabase> | null = null;

export function openToolkitDb(): Promise<IDBPDatabase> {
  if (dbInstance) return Promise.resolve(dbInstance);
  if (!dbPromise) {
    dbPromise = openDB(DB_NAME, DB_VERSION, {
      upgrade(db) {
        if (!db.objectStoreNames.contains(STORE_NAME)) {
          const store = db.createObjectStore(STORE_NAME, { keyPath: 'id' });
          store.createIndex('by-session', 'sessionId');
          store.createIndex('by-synced', 'synced');
        }
      },
    }).then(db => {
      dbInstance = db;
      return db;
    });
  }
  return dbPromise;
}

/** Reset the singleton — used in tests to isolate state between test cases. */
export function resetDbInstance(): void {
  if (dbInstance && typeof dbInstance.close === 'function') {
    dbInstance.close();
  }
  dbInstance = null;
  dbPromise = null;
}

export async function queueOperation(op: PendingOperation): Promise<void> {
  const db = await openToolkitDb();
  await db.put(STORE_NAME, op);
}

export async function getPendingOperations(sessionId: string): Promise<PendingOperation[]> {
  const db = await openToolkitDb();
  return db.getAllFromIndex(STORE_NAME, 'by-session', sessionId) as Promise<PendingOperation[]>;
}

export async function clearOperation(id: string): Promise<void> {
  const db = await openToolkitDb();
  await db.delete(STORE_NAME, id);
}

export async function syncPendingOperations(
  sessionId: string,
  syncFn: (op: PendingOperation) => Promise<void>
): Promise<void> {
  const pending = await getPendingOperations(sessionId);
  const unsynced = pending.filter(op => !op.synced);

  for (const op of unsynced) {
    try {
      await syncFn(op);
      await clearOperation(op.id);
    } catch {
      // Leave in queue for next sync attempt
    }
  }
}
