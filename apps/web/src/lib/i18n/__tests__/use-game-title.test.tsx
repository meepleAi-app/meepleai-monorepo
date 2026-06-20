// apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { renderHook } from '@testing-library/react';

import { useGameTitle } from '@/lib/i18n/use-game-title';
import * as useUserLocaleModule from '@/hooks/useUserLocale';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

const BASE_GAME: Pick<SharedGame, 'id' | 'title' | 'translations'> = {
  id: '00000000-0000-0000-0000-000000000001' as never,
  title: 'Catan',
  translations: [],
};

function mockUserLocale(locale: 'it' | 'en' | 'es' | 'fr' | 'de') {
  vi.spyOn(useUserLocaleModule, 'useUserLocale').mockReturnValue(locale);
}

describe('useGameTitle (matrix T1-T6)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('T1: en user, no translations → canonical EN', () => {
    mockUserLocale('en');
    const { result } = renderHook(() => useGameTitle(BASE_GAME));
    expect(result.current).toEqual({
      value: 'Catan',
      source: 'canonical',
      locale: 'en',
    });
  });

  it('T2: it user, [it manual] → IT manual translation', () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current).toEqual({
      value: 'I Coloni di Catan',
      source: 'translation',
      locale: 'it',
      provider: 'manual',
    });
  });

  it('T3: it-IT user, [it manual] → IT manual (BCP-47 fallback)', () => {
    // The user-locale hook returns 'it' for it-IT (drops region). To simulate
    // it-IT we override via the options arg.
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game, { locale: 'it-IT' }));
    expect(result.current).toEqual({
      value: 'I Coloni di Catan',
      source: 'translation',
      locale: 'it', // resolved via fallback, not it-IT
      provider: 'manual',
    });
  });

  it('T4: de user, [it manual, fr community] → canonical EN', () => {
    mockUserLocale('de');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'IT', description: null, source: 'manual' as const },
        { locale: 'fr', title: 'FR', description: null, source: 'community' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current.value).toBe('Catan');
    expect(result.current.source).toBe('canonical');
  });

  it('T5: explicit override en, [it manual] → canonical EN (override wins)', () => {
    // Even with browser/profile it, an explicit options.locale='en' overrides.
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [{ locale: 'it', title: 'IT', description: null, source: 'manual' as const }],
    };
    const { result } = renderHook(() => useGameTitle(game, { locale: 'en' }));
    expect(result.current).toEqual({
      value: 'Catan',
      source: 'canonical',
      locale: 'en',
    });
  });

  it('T6: it user, [it manual, it auto-openrouter] → manual wins (source priority)', () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [
        { locale: 'it', title: 'Auto MT', description: null, source: 'auto-openrouter' as const },
        { locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const },
      ],
    };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current).toEqual({
      value: 'I Coloni di Catan',
      source: 'translation',
      locale: 'it',
      provider: 'manual',
    });
  });

  it('handles null translations payload defensively (backward compat)', () => {
    mockUserLocale('it');
    const game = { ...BASE_GAME, translations: null as never };
    const { result } = renderHook(() => useGameTitle(game));
    expect(result.current.value).toBe('Catan');
    expect(result.current.source).toBe('canonical');
  });

  it('memoizes: re-render with same inputs returns same object reference', () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [{ locale: 'it', title: 'IT', description: null, source: 'manual' as const }],
    };
    const { result, rerender } = renderHook(() => useGameTitle(game));
    const first = result.current;
    rerender();
    expect(result.current).toBe(first);
  });
});
