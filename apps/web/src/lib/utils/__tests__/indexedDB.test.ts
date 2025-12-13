/**
 * IndexedDB Utility Tests (Issue #2054)
 *
 * Tests for IndexedDB wrapper:
 * - Database initialization
 * - CRUD operations (get, set, delete, clear)
 * - Index queries
 * - SSR safety
 * - Static methods
 *
 * Coverage target: 90%+
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';
import { IndexedDBStore } from '../indexedDB';

describe('IndexedDBStore', () => {
  let originalIndexedDB: typeof indexedDB | undefined;
  let mockObjectStore: ReturnType<typeof createMockObjectStore>;
  let mockTransaction: { objectStore: ReturnType<typeof vi.fn> };
  let mockDB: {
    transaction: ReturnType<typeof vi.fn>;
    close: ReturnType<typeof vi.fn>;
    objectStoreNames: { contains: ReturnType<typeof vi.fn> };
    createObjectStore: ReturnType<typeof vi.fn>;
  };

  function createMockObjectStore() {
    return {
      get: vi.fn(),
      getAll: vi.fn(),
      put: vi.fn(),
      delete: vi.fn(),
      clear: vi.fn(),
      count: vi.fn(),
      index: vi.fn(),
      createIndex: vi.fn(),
    };
  }

  function createMockIndexedDB(
    options: {
      triggerError?: boolean;
      triggerUpgrade?: boolean;
    } = {}
  ) {
    return {
      open: vi.fn().mockImplementation(() => {
        const request = {
          result: mockDB,
          error: options.triggerError ? new Error('Database error') : null,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
          onupgradeneeded: null as ((ev: IDBVersionChangeEvent) => void) | null,
        };

        // Schedule callbacks asynchronously
        queueMicrotask(() => {
          if (options.triggerUpgrade && request.onupgradeneeded) {
            const event = {
              target: { result: mockDB },
            } as unknown as IDBVersionChangeEvent;
            request.onupgradeneeded(event);
          }
          if (options.triggerError && request.onerror) {
            request.onerror();
          } else if (request.onsuccess) {
            request.onsuccess();
          }
        });

        return request;
      }),
      deleteDatabase: vi.fn().mockImplementation(() => {
        const deleteRequest = {
          result: undefined,
          error: null,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => {
          if (deleteRequest.onsuccess) {
            deleteRequest.onsuccess();
          }
        });
        return deleteRequest;
      }),
    } as unknown as IDBFactory;
  }

  beforeEach(() => {
    originalIndexedDB = global.indexedDB;

    // Create fresh mocks for each test
    mockObjectStore = createMockObjectStore();
    mockTransaction = {
      objectStore: vi.fn().mockReturnValue(mockObjectStore),
    };
    mockDB = {
      transaction: vi.fn().mockReturnValue(mockTransaction),
      close: vi.fn(),
      objectStoreNames: {
        contains: vi.fn().mockReturnValue(false),
      },
      createObjectStore: vi.fn().mockReturnValue(mockObjectStore),
    };

    // Default mock
    global.indexedDB = createMockIndexedDB();

    vi.clearAllMocks();
  });

  afterEach(() => {
    if (originalIndexedDB !== undefined) {
      global.indexedDB = originalIndexedDB;
    }
    vi.restoreAllMocks();
  });

  describe('Constructor', () => {
    it('should create instance with default options', () => {
      const store = new IndexedDBStore('testDB', 'testStore');

      expect(store).toBeInstanceOf(IndexedDBStore);
    });

    it('should accept custom options', () => {
      const store = new IndexedDBStore('testDB', 'testStore', {
        version: 2,
        keyPath: 'customId',
        indexes: [{ name: 'status', keyPath: 'status' }],
      });

      expect(store).toBeInstanceOf(IndexedDBStore);
    });
  });

  describe('init', () => {
    it('should initialize database connection', async () => {
      const store = new IndexedDBStore('testDB', 'testStore');

      await store.init();

      expect(indexedDB.open).toHaveBeenCalledWith('testDB', 1);
    });

    it('should only initialize once', async () => {
      const store = new IndexedDBStore('testDB', 'testStore');

      await store.init();
      await store.init();

      expect(indexedDB.open).toHaveBeenCalledTimes(1);
    });

    it('should handle initialization errors', async () => {
      global.indexedDB = createMockIndexedDB({ triggerError: true });

      const store = new IndexedDBStore('testDB', 'testStore');

      await expect(store.init()).rejects.toThrow();
    });

    it('should handle SSR environment gracefully', async () => {
      // Remove indexedDB to simulate SSR
      // @ts-expect-error - Deliberately removing indexedDB
      delete global.indexedDB;

      const store = new IndexedDBStore('testDB', 'testStore');

      // Should not throw in SSR
      await store.init();
    });

    it('should create object store on upgrade', async () => {
      global.indexedDB = createMockIndexedDB({ triggerUpgrade: true });

      const store = new IndexedDBStore('testDB', 'testStore', {
        indexes: [{ name: 'status', keyPath: 'status' }],
      });

      await store.init();

      expect(mockDB.createObjectStore).toHaveBeenCalledWith('testStore', { keyPath: 'id' });
    });
  });

  describe('get', () => {
    it('should get value by key', async () => {
      const mockValue = { id: 'test-1', name: 'Test' };
      mockObjectStore.get.mockImplementation(() => {
        const request = {
          result: mockValue,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const result = await store.get('test-1');

      expect(mockObjectStore.get).toHaveBeenCalledWith('test-1');
      expect(result).toEqual(mockValue);
    });

    it('should return undefined for non-existent key', async () => {
      mockObjectStore.get.mockImplementation(() => {
        const request = {
          result: undefined,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const result = await store.get('non-existent');

      expect(result).toBeUndefined();
    });

    it('should return undefined when db not initialized', async () => {
      // Don't initialize - simulate SSR
      // @ts-expect-error - Deliberately removing indexedDB
      delete global.indexedDB;

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init(); // Will skip in SSR

      const result = await store.get('test-1');

      expect(result).toBeUndefined();
    });
  });

  describe('getAll', () => {
    it('should get all values', async () => {
      const mockValues = [
        { id: 'test-1', name: 'Test 1' },
        { id: 'test-2', name: 'Test 2' },
      ];
      mockObjectStore.getAll.mockImplementation(() => {
        const request = {
          result: mockValues,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const result = await store.getAll();

      expect(result).toEqual(mockValues);
    });

    it('should return empty array when db not initialized', async () => {
      // @ts-expect-error - Deliberately removing indexedDB
      delete global.indexedDB;

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const result = await store.getAll();

      expect(result).toEqual([]);
    });
  });

  describe('set', () => {
    it('should set value with key', async () => {
      mockObjectStore.put.mockImplementation(() => {
        const request = {
          result: undefined,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const value = { name: 'Test' };
      await store.set('test-1', value);

      expect(mockObjectStore.put).toHaveBeenCalledWith({ ...value, id: 'test-1' });
    });
  });

  describe('delete', () => {
    it('should delete value by key', async () => {
      mockObjectStore.delete.mockImplementation(() => {
        const request = {
          result: undefined,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      await store.delete('test-1');

      expect(mockObjectStore.delete).toHaveBeenCalledWith('test-1');
    });
  });

  describe('clear', () => {
    it('should clear all values', async () => {
      mockObjectStore.clear.mockImplementation(() => {
        const request = {
          result: undefined,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      await store.clear();

      expect(mockObjectStore.clear).toHaveBeenCalled();
    });
  });

  describe('count', () => {
    it('should return item count', async () => {
      mockObjectStore.count.mockImplementation(() => {
        const request = {
          result: 5,
          onsuccess: null as (() => void) | null,
          onerror: null as (() => void) | null,
        };
        queueMicrotask(() => request.onsuccess?.());
        return request;
      });

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const count = await store.count();

      expect(count).toBe(5);
    });

    it('should return 0 when db not initialized', async () => {
      // @ts-expect-error - Deliberately removing indexedDB
      delete global.indexedDB;

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const count = await store.count();

      expect(count).toBe(0);
    });
  });

  describe('getByIndex', () => {
    it('should query by index', async () => {
      const mockResults = [{ id: 'test-1', status: 'active' }];
      const mockIndex = {
        getAll: vi.fn().mockImplementation(() => {
          const request = {
            result: mockResults,
            onsuccess: null as (() => void) | null,
            onerror: null as (() => void) | null,
          };
          queueMicrotask(() => request.onsuccess?.());
          return request;
        }),
      };
      mockObjectStore.index.mockReturnValue(mockIndex);

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const results = await store.getByIndex('status', 'active');

      expect(mockObjectStore.index).toHaveBeenCalledWith('status');
      expect(mockIndex.getAll).toHaveBeenCalledWith('active');
      expect(results).toEqual(mockResults);
    });

    it('should return empty array when db not initialized', async () => {
      // @ts-expect-error - Deliberately removing indexedDB
      delete global.indexedDB;

      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      const results = await store.getByIndex('status', 'active');

      expect(results).toEqual([]);
    });
  });

  describe('close', () => {
    it('should close database connection', async () => {
      const store = new IndexedDBStore('testDB', 'testStore');
      await store.init();

      store.close();

      expect(mockDB.close).toHaveBeenCalled();
    });
  });

  describe('Static Methods', () => {
    describe('deleteDatabase', () => {
      it('should delete database', async () => {
        await IndexedDBStore.deleteDatabase('testDB');

        expect(indexedDB.deleteDatabase).toHaveBeenCalledWith('testDB');
      });

      it('should handle SSR environment', async () => {
        // @ts-expect-error - Deliberately removing indexedDB
        delete global.indexedDB;

        // Should not throw
        await IndexedDBStore.deleteDatabase('testDB');
      });
    });

    describe('isAvailable', () => {
      it('should return true when indexedDB is available', () => {
        expect(IndexedDBStore.isAvailable()).toBe(true);
      });

      it('should return false when indexedDB is not available', () => {
        // @ts-expect-error - Deliberately removing indexedDB
        delete global.indexedDB;

        expect(IndexedDBStore.isAvailable()).toBe(false);
      });
    });
  });
});
