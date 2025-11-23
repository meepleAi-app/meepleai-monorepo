import { encrypt, decrypt, clearEncryptionKey } from './secureStorage';

const STORAGE_KEY = 'meepleai:apiKey';

let memoryApiKey: string | null = null;
let isHydrating = false;

// Track hydration status to prevent race conditions
let hydrationPromise: Promise<boolean> | null = null;
let isHydrated = false;

// Generation counter to detect cleared/invalidated keys during async operations
let hydrationGeneration = 0;

const isBrowser = () => typeof window !== 'undefined' && typeof window.sessionStorage !== 'undefined';

/**
 * Store API key securely with encryption
 * Falls back to in-memory storage if encryption fails
 */
export async function setStoredApiKey(apiKey: string): Promise<void> {
  memoryApiKey = apiKey;
  isHydrated = true; // Mark as hydrated since we're setting a key

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
    // Capture current generation before async operation
    const startGeneration = hydrationGeneration;

    const encryptedValue = window.sessionStorage.getItem(STORAGE_KEY);

    if (encryptedValue) {
      try {
        const decrypted = await decrypt(encryptedValue);

        // Check if key was cleared during decryption (generation changed)
        if (hydrationGeneration !== startGeneration) {
          console.log('API key was cleared during retrieval, discarding decrypted value');
          return null;
        }

        // Verify storage still exists before writing to memory
        const currentEncryptedValue = window.sessionStorage.getItem(STORAGE_KEY);
        if (!currentEncryptedValue) {
          console.log('API key storage was cleared during retrieval, discarding decrypted value');
          return null;
        }

        memoryApiKey = decrypted;
        return decrypted;
      } catch (error) {
        console.error('Failed to decrypt API key, clearing storage:', error);
        // If decryption fails, clear the corrupted data only if generation hasn't changed
        if (hydrationGeneration === startGeneration) {
          window.sessionStorage.removeItem(STORAGE_KEY);
          memoryApiKey = null;
        }
        return null;
      }
    }
  }

  return memoryApiKey;
}

/**
 * Clear stored API key and encryption keys
 * Resets hydration state to allow re-hydration
 * Increments generation counter to invalidate any in-flight hydrations
 */
export function clearStoredApiKey(): void {
  memoryApiKey = null;
  hydrationPromise = null;
  isHydrated = false;

  // Increment generation to invalidate any in-flight hydrations
  hydrationGeneration++;

  if (isBrowser()) {
    window.sessionStorage.removeItem(STORAGE_KEY);
    clearEncryptionKey();
  }
}

/**
 * Hydrate API key from sessionStorage into memory cache
 * Should be called on app startup to restore API key after page reload
 * Returns true if a key was found and hydrated, false otherwise
 *
 * This function is idempotent - calling it multiple times will return
 * the same promise without re-executing the hydration logic.
 */
export function hydrateApiKey(): Promise<boolean> {
  // Return existing hydration promise if already in progress
  if (hydrationPromise !== null) {
    return hydrationPromise;
  }

  // Create and cache the hydration promise
  hydrationPromise = (async () => {
    if (!isBrowser()) {
      isHydrated = true;
      return false;
    }

    // Capture current generation before async operation
    const startGeneration = hydrationGeneration;

    const encryptedValue = window.sessionStorage.getItem(STORAGE_KEY);
    if (!encryptedValue) {
      isHydrated = true;
      return false;
    }

    try {
      const decrypted = await decrypt(encryptedValue);

      // Check if key was cleared during decryption (generation changed)
      if (hydrationGeneration !== startGeneration) {
        console.log('API key was cleared during hydration, discarding decrypted value');
        return false;
      }

      // Verify storage still exists before writing to memory
      const currentEncryptedValue = window.sessionStorage.getItem(STORAGE_KEY);
      if (!currentEncryptedValue) {
        console.log('API key storage was cleared during hydration, discarding decrypted value');
        return false;
      }

      memoryApiKey = decrypted;
      isHydrated = true;
      return true;
    } catch (error) {
      console.error('Failed to hydrate API key from sessionStorage:', error);
      // Clear corrupted data only if generation hasn't changed
      if (hydrationGeneration === startGeneration) {
        window.sessionStorage.removeItem(STORAGE_KEY);
        memoryApiKey = null;
      }
      isHydrated = true;
      return false;
    }
  })();

  return hydrationPromise;
}

/**
 * Wait for hydration to complete before proceeding
 * This can be used to ensure the API key is loaded before making requests
 *
 * @returns Promise that resolves when hydration is complete
 */
export async function waitForHydration(): Promise<void> {
  if (isHydrated) {
    return;
  }
  await hydrateApiKey();
}

/**
 * Check if hydration has completed
 * @returns true if hydration has finished (successfully or not)
 */
export function isHydrationComplete(): boolean {
  return isHydrated;
}

/**
 * Check if API key exists
 * First checks memory cache, then falls back to checking sessionStorage
 * Note: This only checks for presence, use hydrateApiKey() to load into memory
 */
export function hasStoredApiKey(): boolean {
  if (memoryApiKey !== null) {
    return true;
  }

  // Check if key exists in sessionStorage (without decrypting)
  if (isBrowser()) {
    return window.sessionStorage.getItem(STORAGE_KEY) !== null;
  }

  return false;
}

/**
 * Synchronous version of getStoredApiKey that returns cached value
 * Used by httpClient for header generation (already has key in memory)
 * Note: Returns null if not hydrated. Call hydrateApiKey() on app startup.
 */
export function getStoredApiKeySync(): string | null {
  // Return memory cache if available
  if (memoryApiKey !== null) {
    return memoryApiKey;
  }

  // If we have encrypted storage but haven't hydrated yet, trigger hydration
  // (This can happen if the sync method is called before hydration completes)
  if (isBrowser() && !isHydrating && hydrationPromise === null) {
    const hasEncrypted = window.sessionStorage.getItem(STORAGE_KEY) !== null;
    if (hasEncrypted) {
      // Trigger async hydration in background
      // The next call will have the decrypted value
      hydrationPromise = hydrateApiKey();
    }
  }

  return memoryApiKey;
}
