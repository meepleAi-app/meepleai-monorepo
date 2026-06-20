// apps/web/src/lib/i18n/resolve-locale.ts
/**
 * Picks the best-fit available locale for a user-preferred locale, per BCP-47.
 *
 * Fallback chain:
 *   1. Exact match (case-insensitive on language; region case-preserved by the
 *      caller-supplied available list).
 *   2. Language-only match: a "xx-YY" user can fall back to "xx" if it exists.
 *   3. null: caller falls back to canonical EN.
 *
 * The hook does NOT upgrade a language-only request to a region-specific
 * available locale ("it" user → "it-IT" available is treated as no match)
 * because the user did not request a specific region and we cannot infer one
 * without surprise.
 *
 * @example
 *   resolveLocale('it-IT', ['it'])         → 'it'    (region drop fallback)
 *   resolveLocale('it', ['it-IT'])         → null    (cannot upgrade)
 *   resolveLocale('it-IT', ['it-IT'])      → 'it-IT' (exact wins)
 *   resolveLocale('it-IT', ['it-IT','it']) → 'it-IT' (exact precedes fallback)
 *
 * Issue #2339 sub-PR 2/3 — see spec
 * docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md §6.
 */
export function resolveLocale(
  userLocale: string,
  availableLocales: ReadonlyArray<string>
): string | null {
  if (availableLocales.length === 0) return null;

  const normalizedUser = userLocale.trim();
  if (!normalizedUser) return null;

  const userLanguage = normalizedUser.split('-')[0].toLowerCase();

  // 1. Exact case-insensitive match
  const exact = availableLocales.find(l => l.toLowerCase() === normalizedUser.toLowerCase());
  if (exact) return exact;

  // 2. Language-only fallback (only when user requested a region)
  const userHasRegion = normalizedUser.includes('-');
  if (userHasRegion) {
    const languageMatch = availableLocales.find(l => l.toLowerCase() === userLanguage);
    if (languageMatch) return languageMatch;
  }

  return null;
}
