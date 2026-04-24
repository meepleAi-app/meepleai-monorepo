import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  CONSENT_KEY,
  CONSENT_VERSION,
  clearStoredConsent,
  getStoredConsent,
  setStoredConsent,
  type CookieConsent,
} from '../cookie-consent';

describe('lib/cookie-consent', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('getStoredConsent', () => {
    it('returns null when no consent is stored', () => {
      expect(getStoredConsent()).toBeNull();
    });

    it('returns the parsed consent when stored with current version', () => {
      const consent: CookieConsent = {
        version: CONSENT_VERSION,
        essential: true,
        analytics: true,
        functional: false,
        timestamp: '2026-04-24T10:00:00.000Z',
      };
      localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));

      expect(getStoredConsent()).toEqual(consent);
    });

    it('returns null when stored consent has a different version', () => {
      localStorage.setItem(
        CONSENT_KEY,
        JSON.stringify({
          version: '0.9',
          essential: true,
          analytics: true,
          functional: true,
          timestamp: '2026-04-24T10:00:00.000Z',
        })
      );

      expect(getStoredConsent()).toBeNull();
    });

    it('returns null when stored value is malformed JSON', () => {
      localStorage.setItem(CONSENT_KEY, '{not-json');
      expect(getStoredConsent()).toBeNull();
    });

    it('returns null when window is undefined (SSR safety)', () => {
      const originalWindow = globalThis.window;
      // @ts-expect-error intentional SSR simulation
      delete globalThis.window;
      try {
        expect(getStoredConsent()).toBeNull();
      } finally {
        globalThis.window = originalWindow;
      }
    });
  });

  describe('setStoredConsent', () => {
    it('writes a consent payload with version, essential=true, and ISO timestamp', () => {
      setStoredConsent({ analytics: true, functional: false });

      const raw = localStorage.getItem(CONSENT_KEY);
      expect(raw).not.toBeNull();
      const parsed = JSON.parse(raw!) as CookieConsent;
      expect(parsed.version).toBe(CONSENT_VERSION);
      expect(parsed.essential).toBe(true);
      expect(parsed.analytics).toBe(true);
      expect(parsed.functional).toBe(false);
      expect(() => new Date(parsed.timestamp)).not.toThrow();
      expect(new Date(parsed.timestamp).toISOString()).toBe(parsed.timestamp);
    });

    it('overwrites previous consent on repeated calls', () => {
      setStoredConsent({ analytics: true, functional: true });
      setStoredConsent({ analytics: false, functional: false });

      const parsed = JSON.parse(localStorage.getItem(CONSENT_KEY)!) as CookieConsent;
      expect(parsed.analytics).toBe(false);
      expect(parsed.functional).toBe(false);
    });

    it('is a no-op when window is undefined (SSR safety)', () => {
      const originalWindow = globalThis.window;
      // @ts-expect-error intentional SSR simulation
      delete globalThis.window;
      try {
        expect(() => setStoredConsent({ analytics: true, functional: true })).not.toThrow();
      } finally {
        globalThis.window = originalWindow;
      }
    });
  });

  describe('clearStoredConsent', () => {
    it('removes the stored consent key', () => {
      setStoredConsent({ analytics: true, functional: true });
      expect(localStorage.getItem(CONSENT_KEY)).not.toBeNull();

      clearStoredConsent();

      expect(localStorage.getItem(CONSENT_KEY)).toBeNull();
    });

    it('is a no-op when no consent is stored', () => {
      expect(() => clearStoredConsent()).not.toThrow();
    });
  });
});
