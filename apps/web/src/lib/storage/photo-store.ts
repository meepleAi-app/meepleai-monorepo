import { openDB, type IDBPDatabase } from 'idb';

const DB_NAME = 'meepleai-photos';
const DB_VERSION = 1;
const STORE = 'photos';

export interface StoredPhoto {
  id: string;
  sessionId: string;
  filename: string;
  timestamp: number;
  blob: Blob;
}

interface PhotoSchema {
  photos: {
    key: string;
    value: StoredPhoto;
    indexes: {
      'by-session': string;
    };
  };
}

let dbPromise: Promise<IDBPDatabase<PhotoSchema>> | null = null;

function getDb(): Promise<IDBPDatabase<PhotoSchema>> {
  if (!dbPromise) {
    dbPromise = openDB<PhotoSchema>(DB_NAME, DB_VERSION, {
      upgrade(db) {
        const store = db.createObjectStore(STORE, { keyPath: 'id' });
        store.createIndex('by-session', 'sessionId', { unique: false });
      },
    });
  }
  return dbPromise;
}

function generateId(sessionId: string): string {
  const random =
    globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  return `photo:${sessionId}:${random}`;
}

/**
 * Persists a captured photo for a given session.
 * Returns the StoredPhoto record (including the original Blob).
 */
export async function addPhoto(
  sessionId: string,
  blob: Blob,
  filename: string
): Promise<StoredPhoto> {
  const db = await getDb();
  const photo: StoredPhoto = {
    id: generateId(sessionId),
    sessionId,
    filename,
    timestamp: Date.now(),
    blob,
  };
  await db.put(STORE, photo);
  return photo;
}

/**
 * Lists all photos belonging to the given session, sorted by timestamp asc.
 */
export async function listPhotos(sessionId: string): Promise<StoredPhoto[]> {
  const db = await getDb();
  const all = await db.getAllFromIndex(STORE, 'by-session', sessionId);
  return all.sort((a, b) => a.timestamp - b.timestamp);
}

/**
 * Deletes a single photo by id.
 */
export async function deletePhoto(id: string): Promise<void> {
  const db = await getDb();
  await db.delete(STORE, id);
}

/**
 * Clears every photo across all sessions. Intended for tests / reset.
 */
export async function clearAllPhotos(): Promise<void> {
  const db = await getDb();
  await db.clear(STORE);
}
