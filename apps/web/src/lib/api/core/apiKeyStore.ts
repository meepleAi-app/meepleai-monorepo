import { encrypt, decrypt, clearEncryptionKey } from './secureStorage';

const STORAGE_KEY = 'meepleai:apiKey';

let memoryApiKey: string | null = null;

const isBrowser = () => typeof window !== 'undefined' && typeof window.sessionStorage !== 'undefined';

/**
 * Store API key securely with encryption
 * Falls back to in-memory storage if encryption fails
 */
export async function setStoredApiKey(apiKey: string): Promise<void> {
  memoryApiKey = apiKey;

  if (isBrowser()) {
    try {
      const encrypted = await encrypt(apiKey);
      window.sessionStorage.setItem(STORAGE_KEY, encrypted);
    } catch (error) {
      console.error('Failed to encrypt API key, using in-memory storage only:', error);
      // Keep in-memory copy but don't persist to storage if encryption fails
    }
  }
}

/**
 * Retrieve and decrypt stored API key
 * Falls back to in-memory cache if decryption fails
 */
export async function getStoredApiKey(): Promise<string | null> {
  if (isBrowser()) {
    const encryptedValue = window.sessionStorage.getItem(STORAGE_KEY);

    if (encryptedValue) {
      try {
        const decrypted = await decrypt(encryptedValue);
        memoryApiKey = decrypted;
        return decrypted;
      } catch (error) {
        console.error('Failed to decrypt API key, clearing storage:', error);
        // If decryption fails, clear the corrupted data
        window.sessionStorage.removeItem(STORAGE_KEY);
        memoryApiKey = null;
        return null;
      }
    }
  }

  return memoryApiKey;
}

/**
 * Clear stored API key and encryption keys
 */
export function clearStoredApiKey(): void {
  memoryApiKey = null;

  if (isBrowser()) {
    window.sessionStorage.removeItem(STORAGE_KEY);
    clearEncryptionKey();
  }
}

/**
 * Check if API key exists (synchronous check using memory cache)
 * Note: This checks the in-memory cache only for performance
 */
export function hasStoredApiKey(): boolean {
  return memoryApiKey !== null;
}

/**
 * Synchronous version of getStoredApiKey that returns cached value
 * Used by httpClient for header generation (already has key in memory)
 */
export function getStoredApiKeySync(): string | null {
  return memoryApiKey;
}
