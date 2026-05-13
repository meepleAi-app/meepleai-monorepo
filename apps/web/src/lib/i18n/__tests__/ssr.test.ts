/**
 * Unit tests for the SSR-safe i18n helper (Issue #1101).
 */
import { describe, expect, it } from 'vitest';

import itMessages from '@/locales/it.json';
import { getSsrMessages } from '@/lib/i18n/ssr';

describe('getSsrMessages', () => {
  it('returns the IT subtree for a known path', () => {
    const subtree = getSsrMessages('pages.errors.notFound');

    expect(subtree).toEqual(itMessages.pages.errors.notFound);
    expect(subtree.title).toBe('Pagina non trovata');
    expect(subtree.eyebrow).toBe('404');
    expect(subtree.homeCta).toBe('Torna alla home');
  });

  it('resolves a deeply-nested object path', () => {
    const subtree = getSsrMessages('pages.sharedGameDetail.metadata');

    expect(subtree).toEqual(itMessages.pages.sharedGameDetail.metadata);
    expect(typeof subtree).toBe('object');
  });

  it('returns the same reference as the static import (no defensive copying)', () => {
    const subtree = getSsrMessages('pages.errors.notFound');

    expect(subtree).toBe(itMessages.pages.errors.notFound);
  });

  it('throws when the runtime catalogue is missing the requested path', () => {
    // We bypass the compile-time check to simulate a stale type definition.
    // In production this branch only fires if `it.json` is edited without
    // updating the types that consumers rely on — a build-time mismatch.
    const stalePath = 'pages.errors.thisKeyDoesNotExist' as never;

    expect(() => getSsrMessages(stalePath)).toThrow(
      /path "pages\.errors\.thisKeyDoesNotExist" does not exist/
    );
    expect(() => getSsrMessages(stalePath)).toThrow(/failed at segment "thisKeyDoesNotExist"/);
  });

  it('throws with the failing segment name when the path bottoms out early', () => {
    const stalePath = 'pages.nope.something' as never;

    expect(() => getSsrMessages(stalePath)).toThrow(/failed at segment "nope"/);
  });
});
