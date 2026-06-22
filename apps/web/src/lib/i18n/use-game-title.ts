// apps/web/src/lib/i18n/use-game-title.ts
import { useMemo } from 'react';

import { useUserLocale, type SupportedLocale } from '@/hooks/useUserLocale';
import type {
  SharedGame,
  SharedGameTranslationDto,
  TranslationProvider,
} from '@/lib/api/schemas/shared-games.schemas';
import { pickBestTranslation } from '@/lib/i18n/pick-best-translation';
import { resolveLocale } from '@/lib/i18n/resolve-locale';

/**
 * Result of resolving a game's title for the current viewer locale.
 *
 * Discriminated by `source`:
 * - `'canonical'` → no translation matched; `value` is `game.title` (EN);
 *                  `provider` is undefined.
 * - `'translation'` → a translation matched; `value` is the localized title;
 *                    `provider` is the authoring source (manual / auto-openrouter / community).
 *
 * `locale` reflects the resolved tag (e.g. `'it-IT'` if exact match, `'it'` if
 * BCP-47 fallback, `'en'` if canonical fallback).
 */
export interface ResolvedTitle {
  value: string;
  source: 'canonical' | 'translation';
  locale: string;
  provider?: TranslationProvider;
}

export interface UseGameTitleOptions {
  /**
   * Force a specific locale, bypassing `useUserLocale()` entirely.
   * Useful for admin previewing other locales without changing profile state.
   */
  locale?: string;
}

interface GameTitleInput {
  title: string;
  translations: ReadonlyArray<SharedGameTranslationDto> | null;
}

/**
 * Resolve the best-fit title for a SharedGame given the current viewer locale.
 *
 * Pure + memoized: safe to call inside `.map()` callbacks. Re-resolves only
 * when `game.id` (when present), `game.translations`, or the resolved locale
 * changes.
 *
 * Per design spec §5: REQ-FE-1..5 enforce locale exact-match → BCP-47 language
 * fallback → canonical EN, with source priority manual > auto-openrouter >
 * community when multiple translations exist for the same locale.
 *
 * Issue #2339 sub-PR 2/3 — see
 * docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md.
 */
export function useGameTitle(
  game: GameTitleInput & Partial<Pick<SharedGame, 'id'>>,
  options?: UseGameTitleOptions
): ResolvedTitle {
  const profileLocale: SupportedLocale = useUserLocale();
  const targetLocale: string = options?.locale ?? profileLocale;

  // Destructure primitive/stable props so memoization is keyed by value, not
  // by `game` object reference (which may churn in parent re-renders).
  // Note: `game.id` is intentionally excluded — `resolveTitle` is a pure
  // function of `{title, translations}` so id has no bearing on the output.
  const { title, translations } = game;

  return useMemo(
    () => resolveTitle({ title, translations }, targetLocale),
    [title, translations, targetLocale]
  );
}

/**
 * Pure resolution function — exported for unit testing without React lifecycle.
 * @internal
 */
export function resolveTitle(game: GameTitleInput, locale: string): ResolvedTitle {
  const translations = game.translations ?? [];

  if (translations.length === 0) {
    return { value: game.title, source: 'canonical', locale: 'en' };
  }

  const availableLocales = Array.from(new Set(translations.map(t => t.locale)));
  const matched = resolveLocale(locale, availableLocales);

  if (matched === null) {
    return { value: game.title, source: 'canonical', locale: 'en' };
  }

  const best = pickBestTranslation(translations, matched);
  if (best === null) {
    // Defensive: resolveLocale matched but pickBest returned null — shouldn't
    // happen in practice (same input set). Fallback canonical.
    return { value: game.title, source: 'canonical', locale: 'en' };
  }

  return {
    value: best.title,
    source: 'translation',
    locale: matched,
    provider: best.source,
  };
}
