// apps/web/src/lib/i18n/__tests__/resolve-locale.test.ts
import { describe, expect, it } from 'vitest';

import { resolveLocale } from '@/lib/i18n/resolve-locale';

describe('resolveLocale', () => {
  it('returns exact match when present', () => {
    expect(resolveLocale('it-IT', ['it-IT', 'it', 'en'])).toBe('it-IT');
  });

  it('falls back from region to language', () => {
    expect(resolveLocale('it-IT', ['it'])).toBe('it');
  });

  it('returns null when only language requested but region available', () => {
    // User did NOT request a region; we should not upgrade.
    expect(resolveLocale('it', ['it-IT'])).toBeNull();
  });

  it('returns exact when both available', () => {
    expect(resolveLocale('it-IT', ['it-IT', 'it'])).toBe('it-IT');
  });

  it('returns null for unmatched locale', () => {
    expect(resolveLocale('de', ['it', 'fr'])).toBeNull();
  });

  it('returns null for empty available list', () => {
    expect(resolveLocale('it', [])).toBeNull();
  });

  it('is case-insensitive on the language segment', () => {
    expect(resolveLocale('IT', ['it'])).toBe('it');
    expect(resolveLocale('it', ['IT'])).toBe('IT');
  });

  it('preserves region casing in the returned value', () => {
    // We return the matching available locale string verbatim (don't re-normalize).
    expect(resolveLocale('it-it', ['it-IT'])).toBe('it-IT');
  });
});
