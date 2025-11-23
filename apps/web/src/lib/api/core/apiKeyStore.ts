import { encrypt, decrypt, clearEncryptionKey } from './secureStorage';

const STORAGE_KEY = 'meepleai:apiKey';

let memoryApiKey: string | null = null;
let isHydrating = false;
let hydrationPromise: Promise<void> | null = null;

// Track hydration status to prevent race conditions
let hydrationPromise: Promise<boolean> | null = null;
let isHydrated = false;

const isBrowser = () => typeof window !== 'undefined' && typeof window.sessionStorage !== 'undefined';

/**
 * Hydrate the in-memory API key cache from encrypted sessionStorage
 * This should be called on app startup to restore API key after page reload
 */
async function hydrateApiKey(): Promise<void> {
  // Skip if not in browser or already hydrating
  if (!isBrowser() || isHydrating || memoryApiKey !== null) {
    return;
  }

  isHydrating = true;

  try {
    const encryptedValue = window.sessionStorage.getItem(STORAGE_KEY);

    if (encryptedValue) {
      try {
        const decrypted = await decrypt(encryptedValue);
        memoryApiKey = decrypted;
      } catch (error) {
        console.error('Failed to decrypt API key during hydration, clearing storage:', error);
        // If decryption fails, clear the corrupted data
        window.sessionStorage.removeItem(STORAGE_KEY);
        memoryApiKey = null;
      }
    }
  } finally {
    isHydrating = false;
  }
}

// Auto-hydrate on module load (client-side only)
if (isBrowser()) {
  hydrationPromise = hydrateApiKey();
}

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
 * Resets hydration state to allow re-hydration
 */
export function clearStoredApiKey(): void {
  memoryApiKey = null;
  hydrationPromise = null;
  isHydrated = false;

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

    const encryptedValue = window.sessionStorage.getItem(STORAGE_KEY);
    if (!encryptedValue) {
      isHydrated = true;
      return false;
    }

    try {
      const decrypted = await decrypt(encryptedValue);
      memoryApiKey = decrypted;
      isHydrated = true;
      return true;
    } catch (error) {
      console.error('Failed to hydrate API key from sessionStorage:', error);
      // Clear corrupted data
      window.sessionStorage.removeItem(STORAGE_KEY);
      memoryApiKey = null;
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
