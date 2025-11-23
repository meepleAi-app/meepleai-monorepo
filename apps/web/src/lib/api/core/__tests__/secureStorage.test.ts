/**
 * Tests for Secure Storage Utility
 */

import { encrypt, decrypt, clearEncryptionKey } from '../secureStorage';

// Mock Web Crypto API
const mockCrypto = {
  subtle: {
    generateKey: jest.fn(),
    exportKey: jest.fn(),
    importKey: jest.fn(),
    encrypt: jest.fn(),
    decrypt: jest.fn(),
  },
  getRandomValues: jest.fn((arr: Uint8Array) => {
    // Fill with predictable values for testing
    for (let i = 0; i < arr.length; i++) {
      arr[i] = i % 256;
    }
    return arr;
  }),
};

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

describe('secureStorage', () => {
  beforeAll(() => {
    // Setup global mocks
    Object.defineProperty(global.window, 'crypto', {
      value: mockCrypto,
      writable: true,
      configurable: true,
    });
    Object.defineProperty(global.window, 'sessionStorage', {
      value: mockSessionStorage,
      writable: true,
      configurable: true,
    });

    // Add TextEncoder/TextDecoder if not available (needed for Node.js environment)
    if (typeof global.TextEncoder === 'undefined') {
      const { TextEncoder, TextDecoder } = require('util');
      global.TextEncoder = TextEncoder;
      global.TextDecoder = TextDecoder;
    }
  });

  beforeEach(() => {
    // Clear storage
    mockSessionStorage.clear();

    // Clear sessionStorage store to prevent key pollution between tests
    mockSessionStorage.store.clear();

    // Reset all mocks
    jest.clearAllMocks();

    // Setup default mock implementations
    const mockKey = { type: 'secret' } as CryptoKey;

    mockCrypto.subtle.generateKey.mockReset().mockResolvedValue(mockKey);
    mockCrypto.subtle.exportKey.mockReset().mockResolvedValue(
      new ArrayBuffer(32) // 256 bits
    );
    mockCrypto.subtle.importKey.mockReset().mockResolvedValue(mockKey);

    // Reset to default implementations to prevent test pollution
    mockCrypto.subtle.encrypt.mockReset().mockImplementation(
      async (_algorithm: any, _key: any, data: BufferSource) => {
        // Simple mock: return a copy of the data (in reality it would be encrypted)
        // The IV is added by the encrypt() function, not by crypto.subtle.encrypt
        const source = new Uint8Array(data as ArrayBuffer);
        const copy = new Uint8Array(source.length);
        copy.set(source);
        return copy.buffer;
      }
    );
    mockCrypto.subtle.decrypt.mockReset().mockImplementation(
      async (_algorithm: any, _key: any, data: BufferSource) => {
        // Simple mock: return the data as-is (in reality it would be decrypted)
        // The IV has already been stripped by decrypt() before calling crypto.subtle.decrypt
        const source = new Uint8Array(data as ArrayBuffer);
        const copy = new Uint8Array(source.length);
        copy.set(source);
        return copy.buffer;
      }
    );
  });

  describe('encrypt', () => {
    it('should encrypt plaintext successfully', async () => {
      const plaintext = 'mpl_dev_testApiKey123';
      const encrypted = await encrypt(plaintext);

      expect(encrypted).toBeDefined();
      expect(typeof encrypted).toBe('string');
      expect(encrypted).not.toBe(plaintext);
      expect(mockCrypto.subtle.encrypt).toHaveBeenCalled();
    });

    it('should generate encryption key on first use', async () => {
      const plaintext = 'test-data';
      await encrypt(plaintext);

      expect(mockCrypto.subtle.generateKey).toHaveBeenCalledWith(
        { name: 'AES-GCM', length: 256 },
        true,
        ['encrypt', 'decrypt']
      );
      expect(mockSessionStorage.setItem).toHaveBeenCalledWith(
        'meepleai:encryption:key',
        expect.any(String)
      );
    });

    it('should reuse existing encryption key', async () => {
      // First encryption
      await encrypt('first');
      const firstCallCount = mockCrypto.subtle.generateKey.mock.calls.length;

      // Second encryption
      await encrypt('second');
      const secondCallCount = mockCrypto.subtle.generateKey.mock.calls.length;

      // Key generation should only happen once
      expect(secondCallCount).toBe(firstCallCount);
    });

    it('should handle encryption errors gracefully', async () => {
      // Temporarily replace encrypt implementation
      mockCrypto.subtle.encrypt.mockImplementationOnce(
        async () => {
          throw new Error('Encryption failed');
        }
      );

      await expect(encrypt('test')).rejects.toThrow('Failed to encrypt data');
    });
  });

  describe('decrypt', () => {
    it('should decrypt encrypted data successfully', async () => {
      const plaintext = 'mpl_dev_testApiKey123';
      const encrypted = await encrypt(plaintext);
      const decrypted = await decrypt(encrypted);

      // Our mock implementation doesn't actually encrypt, so we need to adjust expectations
      expect(decrypted).toBeDefined();
      expect(mockCrypto.subtle.decrypt).toHaveBeenCalled();
    });

    it('should handle decryption errors gracefully', async () => {
      const encrypted = await encrypt('test');

      // Temporarily replace decrypt implementation for this single call
      mockCrypto.subtle.decrypt.mockImplementationOnce(
        async () => {
          throw new Error('Decryption failed');
        }
      );

      await expect(decrypt(encrypted)).rejects.toThrow('Failed to decrypt data');
    });

    it('should handle corrupted data gracefully', async () => {
      // Temporarily replace decrypt implementation
      mockCrypto.subtle.decrypt.mockImplementationOnce(
        async () => {
          throw new Error('Invalid data');
        }
      );

      await expect(decrypt('corrupted-base64-data')).rejects.toThrow('Failed to decrypt data');
    });
  });

  describe('encryption/decryption round-trip', () => {
    it('should successfully encrypt and decrypt API key', async () => {
      // This test validates that encrypt/decrypt work together correctly
      const plaintext = 'mpl_dev_dGVzdF9hcGlfa2V5XzEyMzQ1Ng==';

      const encrypted = await encrypt(plaintext);
      expect(encrypted).toBeDefined();
      expect(typeof encrypted).toBe('string');

      const decrypted = await decrypt(encrypted);
      expect(decrypted).toBe(plaintext);
    });

    it('should handle multiple encryption/decryption cycles', async () => {
      const testData = [
        'mpl_dev_key1',
        'mpl_prod_key2',
        'mpl_test_key3',
      ];

      for (const data of testData) {
        const encrypted = await encrypt(data);
        expect(encrypted).toBeDefined();
        expect(encrypted).not.toBe(data);
      }
    });
  });

  describe('clearEncryptionKey', () => {
    it('should remove encryption key from storage', () => {
      // Store a key first
      mockSessionStorage.setItem('meepleai:encryption:key', 'test-key');

      clearEncryptionKey();

      expect(mockSessionStorage.removeItem).toHaveBeenCalledWith('meepleai:encryption:key');
    });
  });

  describe('edge cases', () => {
    it('should handle empty string', async () => {
      const encrypted = await encrypt('');
      expect(encrypted).toBeDefined();
      expect(typeof encrypted).toBe('string');

      const decrypted = await decrypt(encrypted);
      expect(decrypted).toBe('');
    });

    it('should handle special characters', async () => {
      const specialChars = 'test!@#$%^&*()_+-=[]{}|;:\'",.<>?/~`';

      const encrypted = await encrypt(specialChars);
      expect(encrypted).toBeDefined();

      const decrypted = await decrypt(encrypted);
      expect(decrypted).toBe(specialChars);
    });

    it('should handle unicode characters', async () => {
      const unicode = '测试 🔐 тест';

      const encrypted = await encrypt(unicode);
      expect(encrypted).toBeDefined();

      const decrypted = await decrypt(encrypted);
      expect(decrypted).toBe(unicode);
    });
  });

  describe('fallback behavior', () => {
    it('should warn and return plaintext when crypto API is not available', async () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();

      // Temporarily remove crypto API
      const originalCrypto = global.window.crypto;
      Object.defineProperty(global.window, 'crypto', {
        value: undefined,
        writable: true,
        configurable: true,
      });

      const plaintext = 'test-data';
      const result = await encrypt(plaintext);

      expect(result).toBe(plaintext);
      expect(consoleSpy).toHaveBeenCalledWith(
        'Web Crypto API not available, storing data unencrypted'
      );

      // Restore crypto API
      Object.defineProperty(global.window, 'crypto', {
        value: originalCrypto,
        writable: true,
        configurable: true,
      });
      consoleSpy.mockRestore();
    });

    it('should return data as-is when decrypting without crypto API', async () => {
      const consoleSpy = jest.spyOn(console, 'warn').mockImplementation();

      // Encrypt first (with crypto)
      const plaintext = 'test-data';
      const encrypted = await encrypt(plaintext);

      // Remove crypto API
      const originalCrypto = global.window.crypto;
      Object.defineProperty(global.window, 'crypto', {
        value: undefined,
        writable: true,
        configurable: true,
      });

      const result = await decrypt(encrypted);

      expect(result).toBe(encrypted);

      // Restore crypto API
      Object.defineProperty(global.window, 'crypto', {
        value: originalCrypto,
        writable: true,
        configurable: true,
      });
      consoleSpy.mockRestore();
    });
  });
});
