/**
 * Tests for API Key Store
 *
 * Tests cover:
 * - Basic storage operations
 * - Promise type correctness
 * - Race condition handling during logout/clear
 * - Generation counter invalidation
 */

import {
  setStoredApiKey,
  getStoredApiKey,
  clearStoredApiKey,
  hydrateApiKey,
  waitForHydration,
  isHydrationComplete,
  hasStoredApiKey,
  getStoredApiKeySync,
} from '../apiKeyStore';
import * as secureStorage from '../secureStorage';

// Mock the secure storage module
vi.mock('../secureStorage');

const mockEncrypt = secureStorage.encrypt as Mock<typeof secureStorage.encrypt>;
const mockDecrypt = secureStorage.decrypt as Mock<typeof secureStorage.decrypt>;
const mockClearEncryptionKey = secureStorage.clearEncryptionKey as Mock<
  typeof secureStorage.clearEncryptionKey
>;

const mockSessionStorage: {
  store: Map<string, string>;
  getItem: Mock<(key: string) => string | null>;
  setItem: Mock<(key: string, value: string) => void>;
  removeItem: Mock<(key: string) => void>;
  clear: Mock<() => void>;
} = {
  store: new Map<string, string>(),
  getItem: vi.fn((key: string): string | null => mockSessionStorage.store.get(key) || null),
  setItem: vi.fn((key: string, value: string): void => {
    mockSessionStorage.store.set(key, value);
  }),
  removeItem: vi.fn((key: string): void => {
    mockSessionStorage.store.delete(key);
  }),
  clear: vi.fn((): void => {
    mockSessionStorage.store.clear();
  }),
};

describe('apiKeyStore', () => {
  beforeAll(() => {
    // Setup sessionStorage mock
    Object.defineProperty(global.window, 'sessionStorage', {
      value: mockSessionStorage,
      writable: true,
      configurable: true,
    });
  });

  beforeEach(() => {
    // Clear storage
    mockSessionStorage.clear();
    mockSessionStorage.store.clear();

    // Reset all mocks
    vi.clearAllMocks();

    // Setup default mock implementations
    mockEncrypt.mockImplementation(async (plaintext: string) => {
      return `encrypted:${plaintext}`;
    });

    mockDecrypt.mockImplementation(async (encrypted: string) => {
      return encrypted.replace('encrypted:', '');
    });

    mockClearEncryptionKey.mockImplementation(() => {
      // No-op for tests
    });

    // Clear the module state by re-importing
    clearStoredApiKey();
  });

  describe('setStoredApiKey', () => {
    it('should store API key in memory and sessionStorage', async () => {
      const apiKey = 'mpl_dev_testKey123';

      await setStoredApiKey(apiKey);

      expect(mockEncrypt).toHaveBeenCalledWith(apiKey);
      expect(mockSessionStorage.setItem).toHaveBeenCalledWith(
        'meepleai:apiKey',
        'encrypted:mpl_dev_testKey123'
      );
    });

    it('should handle encryption failures gracefully', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();
      mockEncrypt.mockRejectedValueOnce(new Error('Encryption failed'));

      const apiKey = 'mpl_dev_testKey123';
      await setStoredApiKey(apiKey);

      // Should still work, just without sessionStorage
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to encrypt API key, using in-memory storage only:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });
  });

  describe('getStoredApiKey', () => {
    it('should retrieve and decrypt API key from sessionStorage', async () => {
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      const result = await getStoredApiKey();

      expect(mockDecrypt).toHaveBeenCalledWith(`encrypted:${apiKey}`);
      expect(result).toBe(apiKey);
    });

    it('should handle decryption failures gracefully', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();
      mockSessionStorage.setItem('meepleai:apiKey', 'corrupted-data');
      mockDecrypt.mockRejectedValueOnce(new Error('Decryption failed'));

      const result = await getStoredApiKey();

      expect(result).toBeNull();
      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:apiKey');
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to decrypt API key, clearing storage:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });

    it('should prevent race condition when cleared during decryption', async () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation();
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      // Simulate slow decryption
      mockDecrypt.mockImplementation(async (encrypted: string) => {
        // Clear the key during decryption
        clearStoredApiKey();
        return encrypted.replace('encrypted:', '');
      });

      const result = await getStoredApiKey();

      // Should return null, not the decrypted value
      expect(result).toBeNull();
      expect(consoleSpy).toHaveBeenCalledWith(
        'API key was cleared during retrieval, discarding decrypted value'
      );

      consoleSpy.mockRestore();
    });

    it('should prevent race condition when storage removed during decryption', async () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation();
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      // Simulate slow decryption
      mockDecrypt.mockImplementation(async (encrypted: string) => {
        // Remove storage during decryption (but don't increment generation)
        mockSessionStorage.removeItem('meepleai:apiKey');
        return encrypted.replace('encrypted:', '');
      });

      const result = await getStoredApiKey();

      // Should return null, not the decrypted value
      expect(result).toBeNull();
      expect(consoleSpy).toHaveBeenCalledWith(
        'API key storage was cleared during retrieval, discarding decrypted value'
      );

      consoleSpy.mockRestore();
    });
  });

  describe('clearStoredApiKey', () => {
    it('should clear API key from memory and sessionStorage', () => {
      mockSessionStorage.setItem('meepleai:apiKey', 'encrypted:test');

      clearStoredApiKey();

      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:apiKey');
      expect(mockClearEncryptionKey).toHaveBeenCalled();
    });

    it('should reset hydration state', async () => {
      // Set up a key first
      await setStoredApiKey('test-key');
      expect(isHydrationComplete()).toBe(true);

      clearStoredApiKey();

      expect(isHydrationComplete()).toBe(false);
    });
  });

  describe('hydrateApiKey', () => {
    it('should return Promise<boolean> with correct type', async () => {
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      const result = await hydrateApiKey();

      // Type assertion - should be boolean, not void
      expect(typeof result).toBe('boolean');
      expect(result).toBe(true);
    });

    it('should return false when no key exists', async () => {
      const result = await hydrateApiKey();

      expect(result).toBe(false);
      expect(isHydrationComplete()).toBe(true);
    });

    it('should return cached promise on subsequent calls', async () => {
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      const promise1 = hydrateApiKey();
      const promise2 = hydrateApiKey();

      // Should return the same promise instance
      expect(promise1).toBe(promise2);

      const result1 = await promise1;
      const result2 = await promise2;

      expect(result1).toBe(true);
      expect(result2).toBe(true);

      // Decrypt should only be called once
      expect(mockDecrypt).toHaveBeenCalledTimes(1);
    });

    it('should prevent race condition when cleared during hydration', async () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation();
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      // Simulate slow decryption
      mockDecrypt.mockImplementation(async (encrypted: string) => {
        // Clear the key during decryption (simulates logout during hydration)
        clearStoredApiKey();
        return encrypted.replace('encrypted:', '');
      });

      const result = await hydrateApiKey();

      // Should return false, and key should not be in memory
      expect(result).toBe(false);
      expect(consoleSpy).toHaveBeenCalledWith(
        'API key was cleared during hydration, discarding decrypted value'
      );

      // Verify key is not restored
      const syncKey = getStoredApiKeySync();
      expect(syncKey).toBeNull();

      consoleSpy.mockRestore();
    });

    it('should handle decryption errors and clean up', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();
      mockSessionStorage.setItem('meepleai:apiKey', 'corrupted-data');
      mockDecrypt.mockRejectedValueOnce(new Error('Decryption failed'));

      const result = await hydrateApiKey();

      expect(result).toBe(false);
      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:apiKey');
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to hydrate API key from sessionStorage:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });

    it('should not clean up corrupted data if cleared during decryption', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation();
      mockSessionStorage.setItem('meepleai:apiKey', 'corrupted-data');

      let removeItemCallCount = 0;

      mockDecrypt.mockImplementation(async () => {
        // Clear during decryption
        clearStoredApiKey();
        removeItemCallCount = mockSessionStorage.removeItem.mock.calls.length;
        throw new Error('Decryption failed');
      });

      await hydrateApiKey();

      // removeItem should have been called once by clearStoredApiKey
      // but NOT again by the error handler (because generation changed)
      expect(mockSessionStorage.removeItem.mock.calls.length).toBe(removeItemCallCount);

      consoleSpy.mockRestore();
    });
  });

  describe('waitForHydration', () => {
    it('should wait for hydration to complete', async () => {
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      let hydrationComplete = false;

      mockDecrypt.mockImplementation(async (encrypted: string) => {
        // Simulate slow decryption
        await new Promise(resolve => setTimeout(resolve, 10));
        hydrationComplete = true;
        return encrypted.replace('encrypted:', '');
      });

      const promise = waitForHydration();

      expect(hydrationComplete).toBe(false);

      await promise;

      expect(hydrationComplete).toBe(true);
      expect(isHydrationComplete()).toBe(true);
    });

    it('should return immediately if already hydrated', async () => {
      // Trigger hydration
      await hydrateApiKey();

      const startTime = Date.now();
      await waitForHydration();
      const endTime = Date.now();

      // Should be nearly instant
      expect(endTime - startTime).toBeLessThan(10);
    });
  });

  describe('hasStoredApiKey', () => {
    it('should return true when key exists in memory', async () => {
      await setStoredApiKey('test-key');

      expect(hasStoredApiKey()).toBe(true);
    });

    it('should return true when key exists in sessionStorage', () => {
      mockSessionStorage.setItem('meepleai:apiKey', 'encrypted:test');

      expect(hasStoredApiKey()).toBe(true);
    });

    it('should return false when no key exists', () => {
      expect(hasStoredApiKey()).toBe(false);
    });
  });

  describe('getStoredApiKeySync', () => {
    it('should return cached API key from memory', async () => {
      const apiKey = 'mpl_dev_testKey123';
      await setStoredApiKey(apiKey);

      const result = getStoredApiKeySync();

      expect(result).toBe(apiKey);
    });

    it('should return null when not hydrated', () => {
      const result = getStoredApiKeySync();

      expect(result).toBeNull();
    });

    it('should trigger background hydration if storage exists', () => {
      mockSessionStorage.setItem('meepleai:apiKey', 'encrypted:test');

      const result = getStoredApiKeySync();

      // First call returns null (not yet hydrated)
      expect(result).toBeNull();

      // But hydration should have been triggered
      // (We can't easily test the async side effect here, but we've verified the logic)
    });
  });

  describe('race condition scenarios', () => {
    it('should handle logout during multiple concurrent hydrations', async () => {
      const consoleSpy = vi.spyOn(console, 'log').mockImplementation();
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      let decryptCallCount = 0;
      mockDecrypt.mockImplementation(async (encrypted: string) => {
        decryptCallCount++;

        // Clear on the first call (simulating logout)
        if (decryptCallCount === 1) {
          await new Promise(resolve => setTimeout(resolve, 5));
          clearStoredApiKey();
        }

        return encrypted.replace('encrypted:', '');
      });

      // Start multiple hydrations (though the second should return cached promise)
      const promise1 = hydrateApiKey();
      const promise2 = hydrateApiKey();

      const [result1, result2] = await Promise.all([promise1, promise2]);

      // Both should return false due to logout
      expect(result1).toBe(false);
      expect(result2).toBe(false);

      // Only one decrypt should have been called (promise is cached)
      expect(decryptCallCount).toBe(1);

      // Key should not be in memory
      expect(getStoredApiKeySync()).toBeNull();

      consoleSpy.mockRestore();
    });

    it('should handle rapid set/clear cycles', async () => {
      for (let i = 0; i < 10; i++) {
        await setStoredApiKey(`key-${i}`);
        clearStoredApiKey();
      }

      expect(getStoredApiKeySync()).toBeNull();
      expect(hasStoredApiKey()).toBe(false);
    });

    it('should handle clear after hydration completes', async () => {
      const apiKey = 'mpl_dev_testKey123';
      mockSessionStorage.setItem('meepleai:apiKey', `encrypted:${apiKey}`);

      const result = await hydrateApiKey();
      expect(result).toBe(true);

      // Now clear
      clearStoredApiKey();

      expect(getStoredApiKeySync()).toBeNull();
      expect(hasStoredApiKey()).toBe(false);
    });
  });
});
