/**
 * Secure Storage Utility
 *
 * Provides encryption/decryption for sensitive data stored in browser storage.
 * Uses Web Crypto API (AES-GCM) to encrypt data before storing in sessionStorage.
 *
 * Security Notes:
 * - This mitigates casual inspection and forensic recovery
 * - XSS attacks can still access decrypted data by calling these functions
 * - The encryption key is session-bound and lost on browser close
 * - Complements other security measures (CSP, httpOnly cookies, etc.)
 */

import { logger } from './logger';
import { sanitizeError } from '@/lib/errors';

const ENCRYPTION_KEY_STORAGE = 'meepleai:encryption:key';
const ALGORITHM = 'AES-GCM';
const KEY_LENGTH = 256;
const IV_LENGTH = 12; // 96 bits recommended for AES-GCM

/**
 * Check if we're in a browser environment with crypto support
 */
function isCryptoAvailable(): boolean {
  return (
    typeof window !== 'undefined' &&
    typeof window.crypto !== 'undefined' &&
    typeof window.crypto.subtle !== 'undefined' &&
    typeof window.sessionStorage !== 'undefined'
  );
}

/**
 * Get or generate encryption key for the current session
 * Key is stored in sessionStorage and regenerated on each session
 */
async function getOrGenerateKey(): Promise<CryptoKey> {
  if (!isCryptoAvailable()) {
    throw new Error('Web Crypto API not available');
  }

  // Try to load existing key from sessionStorage
  const storedKeyData = window.sessionStorage.getItem(ENCRYPTION_KEY_STORAGE);

  if (storedKeyData) {
    try {
      const keyData = JSON.parse(storedKeyData);
      const rawKey = Uint8Array.from(atob(keyData.key), c => c.charCodeAt(0));

      return await window.crypto.subtle.importKey(
        'raw',
        rawKey,
        { name: ALGORITHM, length: KEY_LENGTH },
        true,
        ['encrypt', 'decrypt']
      );
    } catch (error) {
      // If key loading fails, generate a new one
      const normalizedError = error instanceof Error ? error : new Error(String(error));
      logger.warn('Failed to load encryption key, generating new one:', {
        component: 'SecureStorage',
        metadata: {
          error: sanitizeError(normalizedError),
        },
      });
    }
  }

  // Generate new key
  const key = await window.crypto.subtle.generateKey(
    { name: ALGORITHM, length: KEY_LENGTH },
    true,
    ['encrypt', 'decrypt']
  );

  // Export and store the key
  const rawKey = await window.crypto.subtle.exportKey('raw', key);
  const keyData = {
    key: btoa(String.fromCharCode(...new Uint8Array(rawKey))),
    created: Date.now(),
  };

  window.sessionStorage.setItem(ENCRYPTION_KEY_STORAGE, JSON.stringify(keyData));

  return key;
}

/**
 * Encrypt a string value using AES-GCM
 *
 * @param plaintext - The string to encrypt
 * @returns Base64-encoded encrypted data with IV prepended
 */
export async function encrypt(plaintext: string): Promise<string> {
  if (!isCryptoAvailable()) {
    // Fallback: return plaintext if crypto not available (SSR, old browsers)
    logger.warn('Web Crypto API not available, storing data unencrypted');
    return plaintext;
  }

  try {
    const key = await getOrGenerateKey();

    // Generate random IV
    const iv = window.crypto.getRandomValues(new Uint8Array(IV_LENGTH));

    // Encode plaintext to bytes
    const encoder = new TextEncoder();
    const data = encoder.encode(plaintext);

    // Encrypt
    const encryptedBuffer = await window.crypto.subtle.encrypt({ name: ALGORITHM, iv }, key, data);

    // Combine IV + encrypted data
    const encryptedArray = new Uint8Array(encryptedBuffer);
    const combined = new Uint8Array(iv.length + encryptedArray.length);
    combined.set(iv, 0);
    combined.set(encryptedArray, iv.length);

    // Convert to base64 for storage
    return btoa(String.fromCharCode(...combined));
  } catch (error) {
    const normalizedError = error instanceof Error ? error : new Error(String(error));
    logger.error('Encryption failed:', normalizedError);
    throw new Error('Failed to encrypt data');
  }
}

/**
 * Decrypt a string value using AES-GCM
 *
 * @param encryptedData - Base64-encoded encrypted data with IV prepended
 * @returns Decrypted plaintext string
 */
export async function decrypt(encryptedData: string): Promise<string> {
  if (!isCryptoAvailable()) {
    // Fallback: return data as-is if crypto not available
    return encryptedData;
  }

  try {
    const key = await getOrGenerateKey();

    // Decode from base64
    const combined = Uint8Array.from(atob(encryptedData), c => c.charCodeAt(0));

    // Extract IV and encrypted data
    const iv = combined.slice(0, IV_LENGTH);
    const encryptedArray = combined.slice(IV_LENGTH);

    // Decrypt
    const decryptedBuffer = await window.crypto.subtle.decrypt(
      { name: ALGORITHM, iv },
      key,
      encryptedArray
    );

    // Decode to string
    const decoder = new TextDecoder();
    return decoder.decode(decryptedBuffer);
  } catch (error) {
    const normalizedError = error instanceof Error ? error : new Error(String(error));
    logger.error('Decryption failed:', normalizedError);
    throw new Error('Failed to decrypt data');
  }
}

/**
 * Clear the encryption key from storage
 * Should be called on logout to ensure old data cannot be decrypted
 */
export function clearEncryptionKey(): void {
  if (isCryptoAvailable()) {
    window.sessionStorage.removeItem(ENCRYPTION_KEY_STORAGE);
  }
}
