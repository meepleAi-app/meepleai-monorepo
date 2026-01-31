/**
 * IndexedDB Utility (Issue #2054)
 *
 * Simple IndexedDB wrapper for offline data persistence.
 * More reliable than localStorage for large data and survives tab refresh.
 *
 * Usage:
 * ```typescript
 * const db = new IndexedDBStore('uploads', 'uploadQueue');
 * await db.init();
 * await db.set('item1', { ... });
 * const item = await db.get('item1');
 * ```
 */

// ============================================================================
// Types
// ============================================================================

export interface IndexedDBStoreOptions {
  /** Database version (increment to trigger upgrade) */
  version?: number;

  /** Key path for object store (default: 'id') */
  keyPath?: string;

  /** Additional indexes to create */
  indexes?: Array<{
    name: string;
    keyPath: string | string[];
    options?: IDBIndexParameters;
  }>;
}

// ============================================================================
// IndexedDB Store Class
// ============================================================================

export class IndexedDBStore<T> {
  private dbName: string;
  private storeName: string;
  private db: IDBDatabase | null = null;
  private options: IndexedDBStoreOptions;
  private initPromise: Promise<void> | null = null;

  constructor(dbName: string, storeName: string, options: IndexedDBStoreOptions = {}) {
    this.dbName = dbName;
    this.storeName = storeName;
    this.options = {
      version: 1,
      keyPath: 'id',
      indexes: [],
      ...options,
    };
  }

  /**
   * Initialize the database connection
   */
  async init(): Promise<void> {
    // Return existing promise if already initializing
    if (this.initPromise) {
      return this.initPromise;
    }

    // Already initialized
    if (this.db) {
      return;
    }

    // SSR guard
    if (typeof indexedDB === 'undefined') {
      console.warn('[IndexedDBStore] IndexedDB not available (SSR)');
      return;
    }

    this.initPromise = new Promise((resolve, reject) => {
      const request = indexedDB.open(this.dbName, this.options.version);

      request.onerror = () => {
        console.error('[IndexedDBStore] Failed to open database:', request.error);
        reject(request.error);
      };

      request.onsuccess = () => {
        this.db = request.result;
        resolve();
      };

      request.onupgradeneeded = event => {
        const db = (event.target as IDBOpenDBRequest).result;

        // Create object store if it doesn't exist
        if (!db.objectStoreNames.contains(this.storeName)) {
          const store = db.createObjectStore(this.storeName, {
            keyPath: this.options.keyPath,
          });

          // Create indexes
          this.options.indexes?.forEach(index => {
            store.createIndex(index.name, index.keyPath, index.options);
          });
        }
      };
    });

    return this.initPromise;
  }

  /**
   * Get a value by key
   */
  async get(key: string): Promise<T | undefined> {
    await this.ensureInit();
    const db = this.db;
    if (!db) return undefined;

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.get(key);

      request.onsuccess = () => resolve(request.result as T | undefined);
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Get all values
   */
  async getAll(): Promise<T[]> {
    await this.ensureInit();
    const db = this.db;
    if (!db) return [];

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.getAll();

      request.onsuccess = () => resolve(request.result as T[]);
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Set a value
   */
  async set(key: string, value: T): Promise<void> {
    await this.ensureInit();
    const db = this.db;
    const keyPath = this.options.keyPath ?? 'id';
    if (!db) return;

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.put({ ...value, [keyPath]: key });

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Delete a value by key
   */
  async delete(key: string): Promise<void> {
    await this.ensureInit();
    const db = this.db;
    if (!db) return;

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.delete(key);

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Clear all values
   */
  async clear(): Promise<void> {
    await this.ensureInit();
    const db = this.db;
    if (!db) return;

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readwrite');
      const store = transaction.objectStore(this.storeName);
      const request = store.clear();

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Count items in the store
   */
  async count(): Promise<number> {
    await this.ensureInit();
    const db = this.db;
    if (!db) return 0;

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readonly');
      const store = transaction.objectStore(this.storeName);
      const request = store.count();

      request.onsuccess = () => resolve(request.result);
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Query by index
   */
  async getByIndex(indexName: string, value: IDBValidKey): Promise<T[]> {
    await this.ensureInit();
    const db = this.db;
    if (!db) return [];

    return new Promise((resolve, reject) => {
      const transaction = db.transaction(this.storeName, 'readonly');
      const store = transaction.objectStore(this.storeName);
      const index = store.index(indexName);
      const request = index.getAll(value);

      request.onsuccess = () => resolve(request.result as T[]);
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Close the database connection
   */
  close(): void {
    if (this.db) {
      this.db.close();
      this.db = null;
      this.initPromise = null;
    }
  }

  /**
   * Delete the entire database
   */
  static async deleteDatabase(dbName: string): Promise<void> {
    if (typeof indexedDB === 'undefined') return;

    return new Promise((resolve, reject) => {
      const request = indexedDB.deleteDatabase(dbName);
      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Check if IndexedDB is available
   */
  static isAvailable(): boolean {
    try {
      return typeof indexedDB !== 'undefined' && indexedDB !== null;
    } catch {
      return false;
    }
  }

  private async ensureInit(): Promise<void> {
    if (!this.db && !this.initPromise) {
      await this.init();
    } else if (this.initPromise) {
      await this.initPromise;
    }
  }
}

// ============================================================================
// Singleton Instances for Common Stores
// ============================================================================

/**
 * Upload queue persistence store
 * Stores upload progress that survives tab refresh
 */
export const uploadQueueDB = new IndexedDBStore<{
  id: string;
  fileName: string;
  fileSize: number;
  gameId: string;
  language: string;
  status: string;
  progress: number;
  pdfId?: string;
  error?: string;
  uploadedBytes: number;
  createdAt: number;
  updatedAt: number;
}>('meepleai-offline', 'uploadQueue', {
  version: 1,
  keyPath: 'id',
  indexes: [
    { name: 'status', keyPath: 'status' },
    { name: 'gameId', keyPath: 'gameId' },
  ],
});

/**
 * Message cache store
 * Caches last N messages for offline viewing
 */
export const messageCacheDB = new IndexedDBStore<{
  id: string;
  chatId: string;
  role: 'user' | 'assistant';
  content: string;
  timestamp: number;
  gameId: string;
}>('meepleai-offline', 'messageCache', {
  version: 1,
  keyPath: 'id',
  indexes: [
    { name: 'chatId', keyPath: 'chatId' },
    { name: 'gameId', keyPath: 'gameId' },
    { name: 'timestamp', keyPath: 'timestamp' },
  ],
});
