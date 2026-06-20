# Shared Game Translations — Frontend Hook Implementation Plan (sub-PR 2/3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship FE hook `useGameTitle()` + Zod DTO `translations[]` extension + 5 highest-traffic consumer migration + ESLint rule warn-mode + axe AA gate, closing sub-PR 2/3 of issue [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339) per design spec [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](../specs/2026-06-20-translations-fe-hook-design.md).

**Architecture:** Pure helpers `resolveLocale` + `pickBestTranslation` composed inside `useGameTitle()` hook → `ResolvedTitle` discriminated union. Hook consumes existing `useUserLocale` for the override chain. Zod schema extension is additive (optional field). No new dependencies; no React Query; pure `useMemo` cache.

**Tech Stack:** Next.js 16 + React 19 + TypeScript 5 + Zod 3 + Vitest + React Testing Library + `@axe-core/react` + MSW (for Accept-Language mocking) + Playwright (E2E happy-path).

**Spec source**: [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](../specs/2026-06-20-translations-fe-hook-design.md)

---

## Branch & PR conventions

- **Parent branch**: `main-dev` (per CLAUDE.md branch hygiene)
- **Feature branch**: `feature/issue-2339-translations-fe-hook`
- **Branch parent config**:
  ```bash
  git config branch.feature/issue-2339-translations-fe-hook.parent main-dev
  ```
- **PR target**: `main-dev`
- **PR title**: `feat(catalog): #2339 sub-PR 2/3 — useGameTitle hook + DTO translations[] + consumer migration`
- **Commit prefix**: `feat(catalog)`, `test(catalog)`, `refactor(catalog)`, `chore(lint)` as appropriate
- **Co-author footer** on commits:
  ```
  Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
  ```

---

## File Structure

### Files to CREATE

```
apps/web/src/lib/i18n/
├── resolve-locale.ts             (pure BCP-47 fallback helper)
├── pick-best-translation.ts      (pure source priority helper)
├── use-game-title.ts             (hook wrapping the 2 helpers + useMemo)
└── __tests__/
    ├── resolve-locale.test.ts
    ├── pick-best-translation.test.ts
    └── use-game-title.test.tsx
```

### Files to MODIFY

```
apps/web/src/lib/api/schemas/shared-games.schemas.ts
  → add SharedGameTranslationDtoSchema export
  → add `translations: z.array(SharedGameTranslationDtoSchema).nullable().default([])`
    to SharedGameSchema and SharedGameDetailSchema
```

### Files to MIGRATE (5 highest-traffic, Task 6.2 list)

```
apps/web/src/components/discover/GameDiscoverDetail.tsx
apps/web/src/components/features/hub/HubGameCard.tsx
apps/web/src/components/games/MeepleGameCard.tsx
apps/web/src/components/library/MeepleUserLibraryCard.tsx
apps/web/src/app/(public)/shared-games/[id]/page-client.tsx
```

### Files to CREATE (codemod + lint)

```
apps/web/scripts/codemod/use-game-title-migration.ts     (jscodeshift script, idempotent)
apps/web/eslint-rules/prefer-use-game-title.js           (custom rule, warn mode)
apps/web/.eslintrc.js                                    (modify: register rule)
```

### Test files

```
apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx    (6 matrix rows + 2 a11y)
apps/web/src/lib/i18n/__tests__/resolve-locale.test.ts     (8+ BCP-47 cases)
apps/web/src/lib/i18n/__tests__/pick-best-translation.test.ts (4+ source priority cases)
apps/web/e2e/translations-fe-hook.spec.ts                  (Playwright Accept-Language flow)
```

---

## Pre-flight checks

- [ ] **Pre-flight 0.1: Verify HEAD is on main-dev clean**

  ```bash
  git branch --show-current  # MUST print main-dev
  git status                 # MUST show clean tree
  git pull --ff-only         # MUST succeed
  ```

  If `git branch --show-current` prints `feature/...`, STOP. Run `git checkout main-dev && git pull` first (per CLAUDE.md § Branch Hygiene Rule).

- [ ] **Pre-flight 0.2: Verify BE DTO + repository still on main-dev**

  ```bash
  grep -n "Translations = null" apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs
  ```

  Expected: line 64 confirms `IReadOnlyList<SharedGameTranslationDto>? Translations = null`. If missing, sub-PR 1/3 (PR #2370) was reverted — STOP and investigate.

- [ ] **Pre-flight 0.3: Confirm existing `useUserLocale` hook still exposes the expected API**

  ```bash
  grep -n "^export function useUserLocale" apps/web/src/hooks/useUserLocale.ts
  ```

  Expected: line 100 confirms `useUserLocale(): SupportedLocale`. If signature changed, adapt Task 2 accordingly.

- [ ] **Pre-flight 0.4: Create feature branch**

  ```bash
  git checkout -b feature/issue-2339-translations-fe-hook
  git config branch.feature/issue-2339-translations-fe-hook.parent main-dev
  ```

---

## Task 1: Zod schema extension — DTO TypeScript update

**Files:**
- Modify: `apps/web/src/lib/api/schemas/shared-games.schemas.ts`

### Step 1.1: Add `SharedGameTranslationDtoSchema` export

After line 160 (just before `// ========== Shared Game DTOs ==========`), insert:

```ts
// ========== Translations (Issue #2339 sub-PR 2/3) ==========

/**
 * Translation source provider, mirroring backend `TranslationSource` enum.
 *
 * - `manual` — admin-curated translation (highest quality).
 * - `auto-openrouter` — machine-translated via OpenRouter (DeepSeek V3).
 * - `community` — community-sourced (future feature, no moderation in MVP).
 *
 * See `useGameTitle()` hook (apps/web/src/lib/i18n/use-game-title.ts) for the
 * source priority chain (manual > auto-openrouter > community).
 */
export const TranslationProviderSchema = z.enum(['manual', 'auto-openrouter', 'community']);
export type TranslationProvider = z.infer<typeof TranslationProviderSchema>;

/**
 * Single non-EN game title localization (Issue #2339).
 *
 * Returned by 4 SharedGameCatalog query handlers (GetAllSharedGames,
 * SearchSharedGames, GetFilteredSharedGames, GetPendingApprovalGames) enriched
 * via `IGameTitleResolver`. Canonical EN remains on `SharedGameDto.title`.
 */
export const SharedGameTranslationDtoSchema = z.object({
  locale: z.string().min(2).max(10), // 'it', 'en-GB', etc. (ISO 639-1 + optional region)
  title: z.string().min(1).max(500),
  description: z.string().nullable(),
  source: TranslationProviderSchema,
});

export type SharedGameTranslationDto = z.infer<typeof SharedGameTranslationDtoSchema>;
```

### Step 1.2: Extend `SharedGameSchema` with `translations` field

Modify `SharedGameSchema` (currently at line 166-199). Add the new field just before the closing `})`:

```ts
  // Issue #2339 sub-PR 2/3 — localized titles. Null = legacy admin endpoint
  // that doesn't surface translations; empty array = explicit "no translations".
  // FE consumers use `useGameTitle(game)` to resolve the best-fit title; never
  // read `translations` directly to avoid bypassing the source/locale priority
  // chain documented in docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md.
  translations: z.array(SharedGameTranslationDtoSchema).nullable().default([]),
```

### Step 1.3: Extend `SharedGameDetailSchema` identically

In `SharedGameDetailSchema` (line 263-313), add the same `translations` field just before the closing `})`.

### Step 1.4: Run typecheck to verify additive

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errors. The Zod schema's `.default([])` means existing fetches that don't surface `translations` deserialize as `[]`, so no consumer is forced to handle nullability.

### Step 1.5: Commit

```bash
git add apps/web/src/lib/api/schemas/shared-games.schemas.ts
git commit -m "feat(catalog): extend SharedGameSchema with optional translations[] (#2339 sub-PR 2/3)"
```

---

## Task 2: Pure helper `resolveLocale` + tests

**Files:**
- Create: `apps/web/src/lib/i18n/resolve-locale.ts`
- Create: `apps/web/src/lib/i18n/__tests__/resolve-locale.test.ts`

### Step 2.1: Write failing tests (TDD red)

```ts
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
```

Run: `cd apps/web && pnpm vitest run src/lib/i18n/__tests__/resolve-locale.test.ts`. Expected: FAIL (module not found).

### Step 2.2: Implement helper (TDD green)

```ts
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
```

Run: `pnpm vitest run src/lib/i18n/__tests__/resolve-locale.test.ts`. Expected: 8/8 pass.

### Step 2.3: Commit

```bash
git add apps/web/src/lib/i18n/resolve-locale.ts apps/web/src/lib/i18n/__tests__/resolve-locale.test.ts
git commit -m "feat(catalog): add resolveLocale BCP-47 fallback helper (#2339 sub-PR 2/3)"
```

---

## Task 3: Pure helper `pickBestTranslation` + tests

**Files:**
- Create: `apps/web/src/lib/i18n/pick-best-translation.ts`
- Create: `apps/web/src/lib/i18n/__tests__/pick-best-translation.test.ts`

### Step 3.1: Write failing tests

```ts
// apps/web/src/lib/i18n/__tests__/pick-best-translation.test.ts
import { describe, expect, it } from 'vitest';

import { pickBestTranslation } from '@/lib/i18n/pick-best-translation';
import type { SharedGameTranslationDto } from '@/lib/api/schemas/shared-games.schemas';

const TR = (overrides: Partial<SharedGameTranslationDto>): SharedGameTranslationDto => ({
  locale: 'it',
  title: 'Titolo',
  description: null,
  source: 'manual',
  ...overrides,
});

describe('pickBestTranslation', () => {
  it('returns null when no translation matches locale', () => {
    expect(pickBestTranslation([TR({ locale: 'it' })], 'fr')).toBeNull();
  });

  it('prefers manual over auto-openrouter for same locale', () => {
    const result = pickBestTranslation(
      [
        TR({ locale: 'it', title: 'Auto', source: 'auto-openrouter' }),
        TR({ locale: 'it', title: 'Manual', source: 'manual' }),
      ],
      'it'
    );
    expect(result?.title).toBe('Manual');
    expect(result?.source).toBe('manual');
  });

  it('prefers auto-openrouter over community for same locale', () => {
    const result = pickBestTranslation(
      [
        TR({ locale: 'it', title: 'Community', source: 'community' }),
        TR({ locale: 'it', title: 'Auto', source: 'auto-openrouter' }),
      ],
      'it'
    );
    expect(result?.title).toBe('Auto');
    expect(result?.source).toBe('auto-openrouter');
  });

  it('returns single match when only one source available', () => {
    const result = pickBestTranslation(
      [TR({ locale: 'it', title: 'Only', source: 'community' })],
      'it'
    );
    expect(result?.title).toBe('Only');
    expect(result?.source).toBe('community');
  });

  it('returns null for empty translations list', () => {
    expect(pickBestTranslation([], 'it')).toBeNull();
  });

  it('matches exact locale only (no BCP-47 fallback here — that is resolveLocale)', () => {
    expect(pickBestTranslation([TR({ locale: 'it' })], 'it-IT')).toBeNull();
  });
});
```

Run: expect FAIL.

### Step 3.2: Implement helper

```ts
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
```

### Step 3.3: Run + commit

```bash
pnpm vitest run src/lib/i18n/__tests__/pick-best-translation.test.ts
# expect 6/6 pass

git add apps/web/src/lib/i18n/pick-best-translation.ts apps/web/src/lib/i18n/__tests__/pick-best-translation.test.ts
git commit -m "feat(catalog): add pickBestTranslation source-priority helper (#2339 sub-PR 2/3)"
```

---

## Task 4: `useGameTitle` hook composition

**Files:**
- Create: `apps/web/src/lib/i18n/use-game-title.ts`
- Create: `apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx` (matrix T1-T6)

### Step 4.1: Write failing tests (matrix T1-T6 from design spec §8)

```tsx
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
      translations: [{ locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const }],
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
      translations: [{ locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const }],
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
```

Run: expect FAIL (module not found).

### Step 4.2: Implement hook

```ts
// apps/web/src/lib/i18n/use-game-title.ts
import { useMemo } from 'react';

import { useUserLocale, type SupportedLocale } from '@/hooks/useUserLocale';
import { pickBestTranslation } from '@/lib/i18n/pick-best-translation';
import { resolveLocale } from '@/lib/i18n/resolve-locale';
import type {
  SharedGame,
  SharedGameTranslationDto,
  TranslationProvider,
} from '@/lib/api/schemas/shared-games.schemas';

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

  return useMemo(
    () => resolveTitle(game, targetLocale),
    [game.id, game.translations, targetLocale, game.title]
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
```

### Step 4.3: Run tests

```bash
pnpm vitest run src/lib/i18n/__tests__/use-game-title.test.tsx
```

Expected: 8/8 pass (6 matrix + null defense + memoization).

### Step 4.4: Commit

```bash
git add apps/web/src/lib/i18n/use-game-title.ts apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx
git commit -m "feat(catalog): add useGameTitle hook with locale + source priority (#2339 sub-PR 2/3)"
```

---

## Task 5: Axe a11y tests for localization aria-label

**Files:**
- Modify: `apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx` (add A1+A2 from design spec §8)

### Step 5.1: Add a11y test rows

Append to the file from Task 4:

```tsx
import { axe } from 'jest-axe';
import { render } from '@testing-library/react';

describe('useGameTitle a11y (axe AA)', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('A1: localized title rendered with aria-label includes canonical EN', async () => {
    mockUserLocale('it');
    const game = {
      ...BASE_GAME,
      translations: [{ locale: 'it', title: 'I Coloni di Catan', description: null, source: 'manual' as const }],
    };

    function Card() {
      const { value, source } = useGameTitle(game);
      const ariaLabel = source === 'translation'
        ? `${value} (localized from English: ${game.title})`
        : undefined;
      return <h3 aria-label={ariaLabel}>{value}</h3>;
    }

    const { container } = render(<Card />);
    const heading = container.querySelector('h3')!;
    expect(heading.getAttribute('aria-label')).toBe('I Coloni di Catan (localized from English: Catan)');

    const axeResults = await axe(container);
    expect(axeResults.violations).toHaveLength(0);
  });

  it('A2: canonical title rendered with no aria-label augmentation', async () => {
    mockUserLocale('en');
    const { container } = render((() => {
      const { value, source } = useGameTitle(BASE_GAME);
      const ariaLabel = source === 'translation' ? `${value} (localized)` : undefined;
      return <h3 aria-label={ariaLabel}>{value}</h3>;
    })());
    const heading = container.querySelector('h3')!;
    expect(heading.getAttribute('aria-label')).toBeNull();

    const axeResults = await axe(container);
    expect(axeResults.violations).toHaveLength(0);
  });
});
```

### Step 5.2: Run + commit

```bash
pnpm vitest run src/lib/i18n/__tests__/use-game-title.test.tsx
# expect 10/10 pass (8 from Task 4 + 2 a11y)

git add apps/web/src/lib/i18n/__tests__/use-game-title.test.tsx
git commit -m "test(catalog): add axe AA a11y tests for useGameTitle (#2339 sub-PR 2/3)"
```

---

## Task 6: Migrate 5 highest-traffic consumers + grep sweep

### Step 6.1: Baseline grep

```bash
grep -rn "game\.title\|game\?.title" apps/web/src/ --include="*.tsx" --include="*.ts" | wc -l
```

Expected: ~141 (per design spec §10.1). Save to `audits/2026-06-20-game-title-baseline.txt` for diff verification at end.

### Step 6.2: Migrate `HubGameCard.tsx` (template for the other 4)

Read the file, identify the `<h3>{game.title}</h3>` site, replace with:

```tsx
'use client';

import { useGameTitle } from '@/lib/i18n/use-game-title';

// ... existing imports ...

export function HubGameCard({ game, ... }: Props) {
  const { value: title, source } = useGameTitle(game);
  const titleAriaLabel = source === 'translation'
    ? `${title} (localized from English: ${game.title})`
    : undefined;

  return (
    // ... existing JSX ...
    <h3
      aria-label={titleAriaLabel}
      className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground"
    >
      {title}
    </h3>
    // ...
  );
}
```

### Step 6.3: Migrate the remaining 4 highest-traffic surfaces

Apply the same pattern to:
- `apps/web/src/components/discover/GameDiscoverDetail.tsx`
- `apps/web/src/components/games/MeepleGameCard.tsx`
- `apps/web/src/components/library/MeepleUserLibraryCard.tsx`
- `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx`

For each:
1. Add the `useGameTitle` import.
2. Replace direct `game.title` access with `const { value: title, source } = useGameTitle(game);`.
3. Wire `aria-label` per source.
4. Use `title` variable inside JSX.
5. Update unit tests in `__tests__/<component>.test.tsx` to mock `useGameTitle` or pass `translations: []` fixtures.

Run `pnpm test --changed` after each file to keep regressions surfaced early.

### Step 6.4: Codemod sweep for the remaining ~77 files

Create `apps/web/scripts/codemod/use-game-title-migration.ts` (ts-morph):

```ts
// apps/web/scripts/codemod/use-game-title-migration.ts
// Idempotent codemod: replaces `game.title` JSX expressions with `useGameTitle(game).value`.
//
// DOES NOT migrate:
//  - non-component files (.ts files, .stories.tsx, __tests__)
//  - search/filter logic (filter(g => g.title.includes(...)) — deliberate, search stays canonical)
//  - storybook fixtures
//
// Run: pnpm tsx apps/web/scripts/codemod/use-game-title-migration.ts --dry-run
//      pnpm tsx apps/web/scripts/codemod/use-game-title-migration.ts --apply

import { Project, SyntaxKind } from 'ts-morph';
import path from 'node:path';

const ROOT = path.resolve(__dirname, '../../src');
const APPLY = process.argv.includes('--apply');

// Skip these directories — they are not user-facing consumers.
const SKIP_PATTERNS = [/__tests__/, /\.stories\.tsx$/, /\.test\./, /\/lib\/i18n\//];

const project = new Project({ tsConfigFilePath: path.resolve(__dirname, '../../tsconfig.json') });

let touched = 0;
let skipped = 0;

for (const file of project.getSourceFiles(`${ROOT}/**/*.{ts,tsx}`)) {
  const filePath = file.getFilePath();
  if (SKIP_PATTERNS.some(p => p.test(filePath))) {
    skipped++;
    continue;
  }
  // ... heuristic: find `game.title` PropertyAccessExpression inside JsxExpression,
  // determine if `game` is typed as SharedGame, and rewrite.
  // (Full implementation in actual codemod file.)
  // ...
}

console.log(`Codemod: ${touched} files touched, ${skipped} files skipped`);
if (APPLY) project.saveSync();
```

Run dry-run, review the diff, then apply:

```bash
pnpm tsx apps/web/scripts/codemod/use-game-title-migration.ts --dry-run
# review output

pnpm tsx apps/web/scripts/codemod/use-game-title-migration.ts --apply
```

### Step 6.5: Verify total migration

```bash
grep -rn "game\.title" apps/web/src/ --include="*.tsx" --include="*.ts" \
  | grep -v "__tests__" \
  | grep -v ".stories.tsx" \
  | grep -v "useGameTitle" \
  | grep -v "filter\|sort\|includes" \
  | wc -l
```

Expected: 0. If non-zero, inspect each remaining occurrence — they're either:
- type-only references (`Pick<SharedGame, 'title'>`) — OK, leave them.
- defensive aria-label callsites referencing canonical EN — OK, that's the design.
- legitimate other (storybook, mock) — OK.

### Step 6.6: Commit

```bash
git add -p  # carefully stage migrated component files
git add apps/web/scripts/codemod/
git commit -m "refactor(catalog): migrate 5 highest-traffic + codemod sweep to useGameTitle (#2339 sub-PR 2/3)"
```

---

## Task 7: ESLint rule `local/prefer-use-game-title` (warn mode)

**Files:**
- Create: `apps/web/eslint-rules/prefer-use-game-title.js`
- Modify: `apps/web/.eslintrc.js` (or whichever config registers `local/*` rules)

### Step 7.1: Implement rule

```js
// apps/web/eslint-rules/prefer-use-game-title.js
/**
 * @fileoverview Warn when `game.title` is accessed inside JSX expression
 * containers without going through the `useGameTitle()` hook.
 *
 * Issue #2339 sub-PR 2/3 — encourages adoption of localization-aware title
 * rendering. WARN-only in this PR; promote to ERROR in a follow-up PR after
 * 14gg of trajectory verde on main-dev.
 */

'use strict';

module.exports = {
  meta: {
    type: 'suggestion',
    docs: {
      description: 'Prefer useGameTitle() hook over raw game.title in JSX',
      category: 'Best Practices',
      recommended: false,
    },
    schema: [],
    messages: {
      preferHook:
        'Use `useGameTitle(game)` instead of `game.title` directly in JSX. The hook resolves locale + source priority. See docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md.',
    },
  },

  create(context) {
    return {
      // Match `game.title` inside JsxExpressionContainer
      'JSXExpressionContainer MemberExpression[object.name="game"][property.name="title"]'(node) {
        // Allow if the surrounding line includes "useGameTitle" — heuristic, may need refinement
        const sourceCode = context.getSourceCode();
        const lineText = sourceCode.lines[node.loc.start.line - 1];
        if (lineText && lineText.includes('useGameTitle')) return;

        context.report({ node, messageId: 'preferHook' });
      },
    };
  },
};
```

### Step 7.2: Register rule in ESLint config

Modify `apps/web/.eslintrc.js` (or `eslint.config.js` for ESLint 9 flat config) to register `local/prefer-use-game-title: 'warn'`. Reference existing `local/no-bgg-host` and `local/no-hardcoded-color-utility` rules as templates.

### Step 7.3: Run lint to verify rule fires (or not) correctly

```bash
cd apps/web && pnpm lint
```

Expected: no NEW errors (rule is warn). Any preexisting violations from non-migrated files surface as warnings only — counted but non-blocking.

### Step 7.4: Commit

```bash
git add apps/web/eslint-rules/prefer-use-game-title.js apps/web/.eslintrc.js
git commit -m "chore(lint): add local/prefer-use-game-title rule in warn mode (#2339 sub-PR 2/3)"
```

---

## Task 8: E2E Playwright happy-path

**Files:**
- Create: `apps/web/e2e/translations-fe-hook.spec.ts`

### Step 8.1: Write E2E spec

```ts
// apps/web/e2e/translations-fe-hook.spec.ts
import { test, expect } from '@playwright/test';

/**
 * Issue #2339 sub-PR 2/3 — E2E happy path verifying that an IT user sees
 * IT-localized titles when seed translations exist. Seed translation data
 * lands in sub-PR 3/3; this test will be guarded by `test.skip()` until then.
 *
 * The test asserts behavior, not the absence of UI flicker (which is covered
 * by unit tests around useMemo identity preservation).
 */

test.describe('useGameTitle E2E', () => {
  test.beforeEach(async ({ page }) => {
    // Force browser locale via context override
    await page.context().addInitScript(() => {
      Object.defineProperty(navigator, 'language', { get: () => 'it-IT' });
      Object.defineProperty(navigator, 'languages', { get: () => ['it-IT', 'it'] });
    });
  });

  test.skip(({ }, testInfo) => !testInfo.project.metadata.seedTranslations,
    'Requires sub-PR 3/3 seed translations to land first');

  test('IT user sees IT-localized title on Library page', async ({ page }) => {
    await page.goto('/library');

    // After seed sub-PR 3/3 lands, "Catan" → "I Coloni di Catan" in card heading
    const catanCard = page.getByRole('article').filter({ hasText: /Coloni di Catan|Catan/ });
    await expect(catanCard).toBeVisible();

    const heading = catanCard.getByRole('heading', { level: 3 });
    await expect(heading).toHaveText('I Coloni di Catan');
    await expect(heading).toHaveAttribute(
      'aria-label',
      /Localized from English: Catan/
    );
  });

  test('IT user sees IT-localized title on Discover page', async ({ page }) => {
    await page.goto('/games?tab=discover');
    const heading = page.getByRole('heading', { name: /Coloni di Catan/i });
    await expect(heading).toBeVisible();
  });

  test('EN-override user sees canonical EN even with browser it-IT', async ({ page }) => {
    // Set profile override via cookie or login as user with Language='en'
    await page.context().addCookies([{ name: 'preferredLocale', value: 'en', url: 'http://localhost:3000' }]);
    await page.goto('/library');

    const heading = page.getByRole('heading', { name: /^Catan$/ });
    await expect(heading).toBeVisible();
    await expect(heading).not.toHaveAttribute('aria-label', /Localized/);
  });
});
```

### Step 8.2: Run E2E

```bash
cd apps/web && pnpm test:e2e --grep "translations-fe-hook"
```

Expected: 3 tests skipped (requires sub-PR 3/3 seed data). After sub-PR 3/3 ships, project metadata flips `seedTranslations: true` and tests run.

### Step 8.3: Commit

```bash
git add apps/web/e2e/translations-fe-hook.spec.ts
git commit -m "test(catalog): add E2E translations-fe-hook spec (gated on sub-PR 3/3) (#2339 sub-PR 2/3)"
```

---

## Task 9: Push, PR, close sub-PR 2/3

### Step 9.1: Final pre-push checks

```bash
cd apps/web
pnpm typecheck   # MUST be green
pnpm lint        # MUST be green (warns allowed for prefer-use-game-title on non-migrated files)
pnpm test --coverage src/lib/i18n   # ≥85% on new files
```

If any gate red, FIX before push.

### Step 9.2: Push

```bash
git push -u origin feature/issue-2339-translations-fe-hook
```

### Step 9.3: Open PR

```bash
gh pr create --base main-dev \
  --title "feat(catalog): #2339 sub-PR 2/3 — useGameTitle hook + DTO translations[] + consumer migration" \
  --body "$(cat <<'EOF'
## Summary

Sub-PR 2/3 of #2339. Implements FE hook + DTO TS schema + consumer migration
per spec [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md).

- `SharedGameSchema` + `SharedGameDetailSchema` extended with optional `translations[]`
- New schema `SharedGameTranslationDtoSchema` + `TranslationProviderSchema`
- Hook `useGameTitle(game, options?): ResolvedTitle` with REQ-FE-1..5 enforced
- Pure helpers `resolveLocale` (BCP-47 fallback) + `pickBestTranslation` (source priority)
- 5 highest-traffic consumers migrated (Library card, Hub card, Discover detail, MeepleGameCard, Shared game detail page)
- Codemod sweep for remaining consumers (idempotent, dry-runnable)
- ESLint rule `local/prefer-use-game-title` in **warn** mode (promote to error follow-up)
- 6 matrix test rows + 2 axe a11y rows green
- E2E Playwright happy-path skeleton (gated on sub-PR 3/3 seed)

## Decisions locked (designer review pending)

See `## 2. Decisioni locked` in the spec. Key picks:
- DEC-FE-1: `ResolvedTitle` discriminated union return (NOT plain string)
- DEC-FE-2: source priority `manual > auto-openrouter > community`
- DEC-FE-3: BCP-47 fallback chain region → language → canonical
- DEC-FE-5: client-side locale resolution (BE sends "Both" shape)
- DEC-FE-8: consumer migration codemod-assisted, ESLint rule in warn mode

## Out of scope

- Seed translations IT (sub-PR 3/3 — separate PR)
- Search input localized filter (DEC-FE-DEFER, follow-up issue)
- Description field UI (deferred)
- Promoting ESLint rule to error (follow-up post-14gg trajectory)

## Test plan

- [x] Vitest matrix T1-T6 green
- [x] Axe AA a11y tests A1+A2 green
- [x] Coverage ≥85% on new files
- [x] `pnpm typecheck` + `pnpm lint` green
- [x] 5 highest-traffic consumers visually verified locally
- [x] Codemod dry-run + apply produces clean diff
- [ ] E2E Playwright gated; flips to green when sub-PR 3/3 ships seed data

Closes part of #2339 (sub-PR 2/3).

🤖 Generated with [Claude Code](https://claude.com/claude-code)

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>
EOF
)"
```

### Step 9.4: Update #2339 body

```bash
gh issue comment 2339 --body "Sub-PR 2/3 opened: #<PR_NUMBER>. FE hook + DTO + consumer migration shipped. Next: sub-PR 3/3 seed translations IT."
```

Update the progress table at top of #2339:

```bash
gh issue edit 2339 --body-file <(gh issue view 2339 --json body --jq '.body' | sed 's|⏳ TODO | ⏳ Sub-PR 2/3 IN REVIEW |')
```

(Or hand-edit the Sub-PR 2 row in the table from `⏳ TODO` → `⏳ IN REVIEW` → `✅ MERGED` post-merge.)

### Step 9.5: Code review subagent (per CLAUDE.md `/implementa` Phase 6 rule)

```bash
# After pushing:
# /code-review:code-review <PR_URL>
```

If reviewer finds blockers, fix in NEW commits (per CLAUDE.md "always create new commits rather than amending"). After review approved, merge — auto-delete branch on merge.

### Step 9.6: Post-merge cleanup

```bash
git checkout main-dev
git pull
git branch -D feature/issue-2339-translations-fe-hook
git remote prune origin
```

---

## Effort estimate

| Task | Effort |
|---|---|
| Task 1: Zod schema extension | 0.5h |
| Task 2: `resolveLocale` helper + tests | 1.5h |
| Task 3: `pickBestTranslation` helper + tests | 1h |
| Task 4: `useGameTitle` hook + matrix tests | 2h |
| Task 5: Axe a11y tests | 1h |
| Task 6: 5 consumers + codemod sweep | 3h |
| Task 7: ESLint rule | 1h |
| Task 8: E2E spec (skeleton) | 1h |
| Task 9: PR + review + cleanup | 1h |
| **Total** | **~12h** (~1.5gg single FTE) |

---

## Self-review checklist

- [ ] **Spec coverage**: each spec §3-13 maps to a task or is explicitly out-of-scope
  - §3 Use cases → Task 4 (hook covers all 3 UC paths)
  - §4 REQ-FE-1..5 → Tasks 2, 3, 4 (helpers + composition)
  - §5 Contract API → Task 4
  - §6 Locale algorithm → Task 2
  - §7 Gherkin → Task 4 (matrix tests)
  - §8 Test matrix → Tasks 4, 5
  - §9 Cache strategy → Task 4 (useMemo)
  - §10 Consumer migration → Task 6
  - §11 Sub-PR 3/3 scope → separate plan
  - §12 Out of scope → respected (no search, no preference UI, no description editor)
- [ ] **No placeholders**: every code block contains real implementation
- [ ] **Type consistency**: `ResolvedTitle` shape consistent across Tasks 4, 5, 6
- [ ] **Tests assert behavior, not implementation**: matrix rows test inputs/outputs, NOT useMemo deps
- [ ] **Test file paths match Files declarations**: `__tests__/use-game-title.test.tsx` location verified
- [ ] **Branch hygiene preflight 0.1-0.4 executed**: HEAD on main-dev before `git checkout -b`
- [ ] **PR target main-dev confirmed**: NOT main, NOT main-staging
- [ ] **Code review subagent invoked**: Step 9.5 not skipped

---

## Cross-references

- **Spec source**: [`docs/superpowers/specs/2026-06-20-translations-fe-hook-design.md`](../specs/2026-06-20-translations-fe-hook-design.md)
- **Companion sub-PR 3/3 plan**: [`docs/superpowers/plans/2026-06-20-translations-seed-subpr3.md`](./2026-06-20-translations-seed-subpr3.md)
- **Wave 1 spec (BE foundation)**: [`docs/superpowers/specs/2026-06-15-shared-game-translations-design.md`](../specs/2026-06-15-shared-game-translations-design.md)
- **Wave 1 plan TDD**: [`docs/superpowers/plans/2026-06-15-shared-game-translations.md`](./2026-06-15-shared-game-translations.md)
- **Tracker**: [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339)
- **Wave 1 shipped PR**: [#2370](https://github.com/meepleAi-app/meepleai-monorepo/pull/2370) (`cd041ca35`)
