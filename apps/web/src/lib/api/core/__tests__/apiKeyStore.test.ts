/**
 * Tests for API Key Store with Encryption
 */

import {
  setStoredApiKey,
  getStoredApiKey,
  getStoredApiKeySync,
  clearStoredApiKey,
  hasStoredApiKey,
  hydrateApiKey,
  waitForHydration,
  isHydrationComplete,
} from '../apiKeyStore';
import * as secureStorage from '../secureStorage';

// Mock secureStorage module
jest.mock('../secureStorage', () => ({
  encrypt: jest.fn((data: string) => Promise.resolve(`encrypted_${data}`)),
  decrypt: jest.fn((data: string) => Promise.resolve(data.replace('encrypted_', ''))),
  clearEncryptionKey: jest.fn(),
}));

const mockSessionStorage = {
  store: new Map<string, string>(),
  getItem: jest.fn((key: string) => mockSessionStorage.store.get(key) || null),
  setItem: jest.fn((key: string, value: string) => {
    mockSessionStorage.store.set(key, value);
  }),
  removeItem: jest.fn((key: string) => {
    mockSessionStorage.store.delete(key);
  }),
  clear: jest.fn(() => {
    mockSessionStorage.store.clear();
  }),
};

describe('apiKeyStore', () => {
  beforeAll(() => {
    // Setup global mocks
    Object.defineProperty(global.window, 'sessionStorage', {
      value: mockSessionStorage,
      writable: true,
      configurable: true,
    });
  });

  beforeEach(() => {
    // Clear storage
    mockSessionStorage.clear();

    // Clear in-memory API key to prevent state pollution
    clearStoredApiKey();

    // Reset mocks AFTER clearing to avoid counting the cleanup calls
    jest.clearAllMocks();
  });

  describe('setStoredApiKey', () => {
    it('should encrypt and store API key', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);

      expect(secureStorage.encrypt).toHaveBeenCalledWith(apiKey);
      expect(mockSessionStorage.setItem).toHaveBeenCalledWith(
        'meepleai:apiKey',
        `encrypted_${apiKey}`
      );
    });

    it('should store in memory regardless of encryption success', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);

      const syncValue = getStoredApiKeySync();
      expect(syncValue).toBe(apiKey);
    });

    it('should handle encryption failure gracefully', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      const apiKey = 'mpl_dev_testApiKey123';

      // Mock encryption failure
      (secureStorage.encrypt as jest.Mock).mockRejectedValueOnce(
        new Error('Encryption failed')
      );

      await setStoredApiKey(apiKey);

      // Should still store in memory
      expect(getStoredApiKeySync()).toBe(apiKey);

      // Should not store in sessionStorage
      expect(mockSessionStorage.setItem).not.toHaveBeenCalled();

      // Should log error
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to encrypt API key, using in-memory storage only:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });
  });

  describe('getStoredApiKey', () => {
    it('should decrypt and return stored API key', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Store encrypted key
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      const result = await getStoredApiKey();

      expect(secureStorage.decrypt).toHaveBeenCalledWith(`encrypted_${apiKey}`);
      expect(result).toBe(apiKey);
    });

    it('should update memory cache when retrieving from storage', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Store encrypted key
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      await getStoredApiKey();

      // Memory cache should be updated
      expect(getStoredApiKeySync()).toBe(apiKey);
    });

    it('should return null if no key is stored', async () => {
      const result = await getStoredApiKey();

      expect(result).toBeNull();
      expect(secureStorage.decrypt).not.toHaveBeenCalled();
    });

    it('should handle decryption failure gracefully', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      // Store corrupted encrypted key
      mockSessionStorage.store.set('meepleai:apiKey', 'corrupted_data');

      // Mock decryption failure
      (secureStorage.decrypt as jest.Mock).mockRejectedValueOnce(
        new Error('Decryption failed')
      );

      const result = await getStoredApiKey();

      // Should return null
      expect(result).toBeNull();

      // Should clear corrupted data
      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:apiKey');

      // Should log error
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to decrypt API key, clearing storage:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });

    it('should return memory cache if no sessionStorage value', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Set in memory only
      await setStoredApiKey(apiKey);
      mockSessionStorage.store.delete('meepleai:apiKey');

      const result = await getStoredApiKey();

      expect(result).toBe(apiKey);
      expect(secureStorage.decrypt).not.toHaveBeenCalled();
    });
  });

  describe('getStoredApiKeySync', () => {
    it('should return memory cache synchronously', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);

      const result = getStoredApiKeySync();

      expect(result).toBe(apiKey);
    });

    it('should return null if no key in memory', () => {
      const result = getStoredApiKeySync();

      expect(result).toBeNull();
    });
  });

  describe('clearStoredApiKey', () => {
    it('should clear API key from memory and storage', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Store key
      await setStoredApiKey(apiKey);

      // Clear it
      clearStoredApiKey();

      // Memory should be cleared
      expect(getStoredApiKeySync()).toBeNull();

      // SessionStorage should be cleared
      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:apiKey');

      // Encryption key should be cleared
      expect(secureStorage.clearEncryptionKey).toHaveBeenCalled();
    });

    it('should be safe to call multiple times', () => {
      clearStoredApiKey();
      clearStoredApiKey();
      clearStoredApiKey();

      expect(mockSessionStorage.removeItem).toHaveBeenCalledTimes(3);
    });
  });

  describe('hydrateApiKey', () => {
    it('should hydrate API key from sessionStorage into memory', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Store encrypted key in sessionStorage
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Hydrate should restore it to memory
      const result = await hydrateApiKey();

      expect(result).toBe(true);
      expect(getStoredApiKeySync()).toBe(apiKey);
      expect(secureStorage.decrypt).toHaveBeenCalledWith(`encrypted_${apiKey}`);
    });

    it('should return false when no key in sessionStorage', async () => {
      const result = await hydrateApiKey();

      expect(result).toBe(false);
      expect(getStoredApiKeySync()).toBeNull();
      expect(secureStorage.decrypt).not.toHaveBeenCalled();
    });

    it('should handle decryption failure gracefully', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      // Store corrupted encrypted key
      mockSessionStorage.store.set('meepleai:apiKey', 'corrupted_data');

      // Mock decryption failure
      (secureStorage.decrypt as jest.Mock).mockRejectedValueOnce(
        new Error('Decryption failed')
      );

      const result = await hydrateApiKey();

      // Should return false
      expect(result).toBe(false);

      // Should clear memory
      expect(getStoredApiKeySync()).toBeNull();

      // Should clear corrupted data from sessionStorage
      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:apiKey');

      // Should log error
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to hydrate API key from sessionStorage:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });

    it('should work in page refresh scenario', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Initial login - store key
      await setStoredApiKey(apiKey);
      expect(getStoredApiKeySync()).toBe(apiKey);

      // Simulate page refresh - clear memory but keep sessionStorage
      // In real scenario, memory is cleared by browser reload
      // Here we manually clear it for testing
      const encryptedKey = mockSessionStorage.store.get('meepleai:apiKey');
      clearStoredApiKey();
      expect(getStoredApiKeySync()).toBeNull();

      // Restore sessionStorage entry (simulating what remains after reload)
      mockSessionStorage.store.set('meepleai:apiKey', encryptedKey!);

      // Hydrate on app startup
      const hydrated = await hydrateApiKey();

      expect(hydrated).toBe(true);
      expect(getStoredApiKeySync()).toBe(apiKey);
    });

    it('should be idempotent - multiple calls return same promise', async () => {
      const apiKey = 'mpl_dev_testApiKey123';
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Call hydrateApiKey multiple times
      const promise1 = hydrateApiKey();
      const promise2 = hydrateApiKey();
      const promise3 = hydrateApiKey();

      // All should be the same promise instance
      expect(promise1).toBe(promise2);
      expect(promise2).toBe(promise3);

      const result = await promise1;
      expect(result).toBe(true);

      // Decrypt should only be called once
      expect(secureStorage.decrypt).toHaveBeenCalledTimes(1);
    });

    it('should mark as hydrated after completion', async () => {
      const apiKey = 'mpl_dev_testApiKey123';
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      expect(isHydrationComplete()).toBe(false);

      await hydrateApiKey();

      expect(isHydrationComplete()).toBe(true);
    });
  });

  describe('waitForHydration', () => {
    it('should wait for hydration to complete', async () => {
      const apiKey = 'mpl_dev_testApiKey123';
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      expect(isHydrationComplete()).toBe(false);

      // Start hydration (don't await yet)
      const hydrationPromise = hydrateApiKey();

      // Wait for hydration should wait for the same promise
      await waitForHydration();

      expect(isHydrationComplete()).toBe(true);
      expect(getStoredApiKeySync()).toBe(apiKey);

      // Ensure original promise also resolved
      const result = await hydrationPromise;
      expect(result).toBe(true);
    });

    it('should return immediately if already hydrated', async () => {
      const apiKey = 'mpl_dev_testApiKey123';
      await setStoredApiKey(apiKey);

      expect(isHydrationComplete()).toBe(true);

      // Should return immediately
      await waitForHydration();

      expect(isHydrationComplete()).toBe(true);
    });

    it('should handle case when no key exists', async () => {
      expect(isHydrationComplete()).toBe(false);

      await waitForHydration();

      expect(isHydrationComplete()).toBe(true);
      expect(getStoredApiKeySync()).toBeNull();
    });
  });

  describe('isHydrationComplete', () => {
    it('should return false initially', () => {
      expect(isHydrationComplete()).toBe(false);
    });

    it('should return true after setStoredApiKey', async () => {
      await setStoredApiKey('test-key');
      expect(isHydrationComplete()).toBe(true);
    });

    it('should return true after hydrateApiKey completes', async () => {
      await hydrateApiKey();
      expect(isHydrationComplete()).toBe(true);
    });

    it('should return false after clearStoredApiKey', async () => {
      await setStoredApiKey('test-key');
      expect(isHydrationComplete()).toBe(true);

      clearStoredApiKey();
      expect(isHydrationComplete()).toBe(false);
    });
  });

  describe('hasStoredApiKey', () => {
    it('should return true when API key is in memory', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);

      expect(hasStoredApiKey()).toBe(true);
    });

    it('should return true when API key is in sessionStorage but not in memory', () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Store encrypted key in sessionStorage only
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Should detect key exists even though not hydrated yet
      expect(hasStoredApiKey()).toBe(true);
    });

    it('should return false when no API key in memory or sessionStorage', () => {
      expect(hasStoredApiKey()).toBe(false);
    });

    it('should return false after clearing', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);
      expect(hasStoredApiKey()).toBe(true);

      clearStoredApiKey();
      expect(hasStoredApiKey()).toBe(false);
    });

    it('should return true when encrypted key exists in storage but memory is null', () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Directly set encrypted key in storage (simulating page reload)
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Memory is null, but storage has encrypted key
      expect(hasStoredApiKey()).toBe(true);
    });

    it('should trigger background hydration when encrypted key exists but memory is null', () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Directly set encrypted key in storage
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Call hasStoredApiKey - should trigger hydration
      const result = hasStoredApiKey();

      expect(result).toBe(true);
      // Note: Hydration is async, so memory won't be populated immediately
      // But the next call to getStoredApiKeySync() after hydration completes will have it
    });
  });

  describe('SSR compatibility', () => {
    it('should handle missing window object gracefully', async () => {
      // Temporarily remove window
      const originalWindow = global.window;
      // @ts-ignore
      delete global.window;

      const apiKey = 'mpl_dev_testApiKey123';

      // Should not throw
      await setStoredApiKey(apiKey);

      // Memory cache should still work
      expect(getStoredApiKeySync()).toBe(apiKey);

      const retrieved = await getStoredApiKey();
      expect(retrieved).toBe(apiKey);

      clearStoredApiKey();
      expect(getStoredApiKeySync()).toBeNull();

      // Restore window
      // @ts-ignore
      global.window = originalWindow;
    });
  });

  describe('rehydrateApiKey', () => {
    it('should manually re-hydrate API key from storage', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Store encrypted key in storage
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Manually trigger hydration
      await rehydrateApiKey();

      // Memory should now have the decrypted key
      expect(getStoredApiKeySync()).toBe(apiKey);
    });

    it('should force re-hydration even if memory is already populated', async () => {
      const oldKey = 'mpl_dev_oldKey123';
      const newKey = 'mpl_dev_newKey456';

      // Set old key in memory
      await setStoredApiKey(oldKey);
      expect(getStoredApiKeySync()).toBe(oldKey);

      // Update storage with new key (simulating external update)
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${newKey}`);

      // Memory still has old key
      expect(getStoredApiKeySync()).toBe(oldKey);

      // Force re-hydration
      await rehydrateApiKey();

      // Memory should now have the new key
      expect(getStoredApiKeySync()).toBe(newKey);
    });

    it('should handle re-hydration with no storage gracefully', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Set key in memory
      await setStoredApiKey(apiKey);

      // Clear storage but not memory
      mockSessionStorage.store.delete('meepleai:apiKey');

      // Re-hydrate (should clear memory since storage is empty)
      await rehydrateApiKey();

      // Memory should be null since storage was empty
      expect(getStoredApiKeySync()).toBeNull();
    });

    it('should handle decryption failure during re-hydration', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();

      // Store corrupted data
      mockSessionStorage.store.set('meepleai:apiKey', 'corrupted_data');

      // Mock decryption failure
      (secureStorage.decrypt as jest.Mock).mockRejectedValueOnce(
        new Error('Decryption failed')
      );

      // Re-hydrate
      await rehydrateApiKey();

      // Memory should be null
      expect(getStoredApiKeySync()).toBeNull();

      // Storage should be cleared
      expect(mockSessionStorage.store.has('meepleai:apiKey')).toBe(false);

      // Error should be logged
      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to decrypt API key during hydration, clearing storage:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });
  });

  describe('integration scenarios', () => {
    it('should support full login/logout flow', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Login: store key
      await setStoredApiKey(apiKey);
      expect(hasStoredApiKey()).toBe(true);

      // Use key for requests (sync access)
      const keyForHeader = getStoredApiKeySync();
      expect(keyForHeader).toBe(apiKey);

      // Logout: clear key
      clearStoredApiKey();
      expect(hasStoredApiKey()).toBe(false);
      expect(getStoredApiKeySync()).toBeNull();
    });

    it('should support page refresh simulation with hydration', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Initial login
      await setStoredApiKey(apiKey);
      const encryptedKey = mockSessionStorage.store.get('meepleai:apiKey');

      // Simulate page refresh: clear memory (browser reload clears JS state)
      clearStoredApiKey();
      expect(getStoredApiKeySync()).toBeNull();

      // But sessionStorage persists
      mockSessionStorage.store.set('meepleai:apiKey', encryptedKey!);

      // App startup: hydrate from storage
      const hydrated = await hydrateApiKey();
      expect(hydrated).toBe(true);

      // Memory cache should be restored
      expect(getStoredApiKeySync()).toBe(apiKey);
      expect(hasStoredApiKey()).toBe(true);
    });

    it('should handle rapid successive calls', async () => {
      const apiKeys = ['key1', 'key2', 'key3', 'key4', 'key5'];

      // Rapidly set different keys
      for (const key of apiKeys) {
        await setStoredApiKey(key);
      }

      // Should have the last key
      expect(getStoredApiKeySync()).toBe('key5');
    });

    it('should support full page reload with auto-hydration', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // User logs in and API key is stored
      await setStoredApiKey(apiKey);
      expect(getStoredApiKeySync()).toBe(apiKey);

      // Simulate page reload: clear memory (via clearStoredApiKey without clearing storage)
      const encryptedValue = mockSessionStorage.store.get('meepleai:apiKey');
      clearStoredApiKey();

      // Restore the encrypted value (simulating it surviving page reload)
      mockSessionStorage.store.set('meepleai:apiKey', encryptedValue!);

      // Simulate auto-hydration that would happen on module load
      await rehydrateApiKey();

      // hasStoredApiKey should return true (checks storage)
      expect(hasStoredApiKey()).toBe(true);

      // Memory should be hydrated
      expect(getStoredApiKeySync()).toBe(apiKey);

      // API calls should include the API key
      const keyForHeader = getStoredApiKeySync();
      expect(keyForHeader).toBe(apiKey);
    });

    it('should handle first API call timing during hydration window', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Encrypted key exists in storage (page just loaded)
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // First call to getStoredApiKeySync before hydration completes
      const firstCall = getStoredApiKeySync();

      // Should return null (hydration not complete)
      expect(firstCall).toBeNull();

      // hasStoredApiKey should detect encrypted storage
      expect(hasStoredApiKey()).toBe(true);

      // Complete hydration manually
      await rehydrateApiKey();

      // Subsequent calls should have the key
      expect(getStoredApiKeySync()).toBe(apiKey);
    });

    it('should support dual-auth fallback pattern', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Encrypted key exists (page reload scenario)
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // User makes API call immediately after page load
      const keyForFirstRequest = getStoredApiKeySync();

      // First request has no API key (hydration not complete)
      expect(keyForFirstRequest).toBeNull();
      // In real app, request would fall back to cookie auth

      // Background hydration completes
      await rehydrateApiKey();

      // Subsequent requests have API key
      const keyForSecondRequest = getStoredApiKeySync();
      expect(keyForSecondRequest).toBe(apiKey);
    });
  });
});
