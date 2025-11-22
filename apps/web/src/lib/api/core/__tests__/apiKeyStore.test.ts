/**
 * Tests for API Key Store with Encryption
 */

import {
  setStoredApiKey,
  getStoredApiKey,
  getStoredApiKeySync,
  clearStoredApiKey,
  hasStoredApiKey,
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

  describe('hasStoredApiKey', () => {
    it('should return true when API key is in memory', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);

      expect(hasStoredApiKey()).toBe(true);
    });

    it('should return false when no API key in memory', () => {
      expect(hasStoredApiKey()).toBe(false);
    });

    it('should return false after clearing', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      await setStoredApiKey(apiKey);
      expect(hasStoredApiKey()).toBe(true);

      clearStoredApiKey();
      expect(hasStoredApiKey()).toBe(false);
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

    it('should support page refresh simulation', async () => {
      const apiKey = 'mpl_dev_testApiKey123';

      // Initial login
      await setStoredApiKey(apiKey);

      // Simulate page refresh: clear memory but keep storage
      mockSessionStorage.store.set('meepleai:apiKey', `encrypted_${apiKey}`);

      // Retrieve from storage (simulating app initialization)
      const retrieved = await getStoredApiKey();
      expect(retrieved).toBe(apiKey);

      // Memory cache should be restored
      expect(getStoredApiKeySync()).toBe(apiKey);
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
  });
});
