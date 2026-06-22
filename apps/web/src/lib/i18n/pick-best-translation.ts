// apps/web/src/lib/i18n/pick-best-translation.ts
import type {
  SharedGameTranslationDto,
  TranslationProvider,
} from '@/lib/api/schemas/shared-games.schemas';

/**
 * Source priority chain. Lower index = higher priority.
 *
 * - `manual`           → admin-curated, highest quality (REQ-FE-4).
 * - `auto-openrouter`  → machine-translated via DeepSeek V3.
 * - `community`        → community-sourced, no moderation in MVP.
 */
const SOURCE_PRIORITY: ReadonlyArray<TranslationProvider> = [
  'manual',
  'auto-openrouter',
  'community',
];

/**
 * Picks the highest-priority translation matching an exact locale string.
 *
 * Does NOT apply BCP-47 fallback — that's `resolveLocale`'s job. Call this
 * AFTER resolving the user's requested locale to an available one.
 *
 * @returns The best translation or `null` if no exact match exists.
 *
 * Issue #2339 sub-PR 2/3 — see spec
 * docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md §5.4 REQ-FE-4.
 */
export function pickBestTranslation(
  translations: ReadonlyArray<SharedGameTranslationDto>,
  locale: string
): SharedGameTranslationDto | null {
  const matching = translations.filter(t => t.locale === locale);
  if (matching.length === 0) return null;

  for (const source of SOURCE_PRIORITY) {
    const found = matching.find(t => t.source === source);
    if (found) return found;
  }

  // Should be unreachable (all enum members covered), but defensive.
  return matching[0];
}
