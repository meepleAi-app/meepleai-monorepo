# Shared Game Translations — Frontend Hook Design (sub-PR 2/3)

> **Status**: DESIGN APPROVED — 2026-06-20
> **Tracker issue**: [#2339](https://github.com/meepleAi-app/meepleai-monorepo/issues/2339)
> **Wave 1 reference**: [`docs/superpowers/specs/2026-06-15-shared-game-translations-design.md`](./2026-06-15-shared-game-translations-design.md) — BE foundation + admin endpoints SHIPPED via PR [#2370](https://github.com/meepleAi-app/meepleai-monorepo/pull/2370) (`cd041ca35`)
> **Plan**: [`docs/superpowers/plans/2026-06-20-translations-fe-hook.md`](../plans/2026-06-20-translations-fe-hook.md)
> **Companion (sub-PR 3/3)**: [`docs/superpowers/plans/2026-06-20-translations-seed-subpr3.md`](../plans/2026-06-20-translations-seed-subpr3.md) — seed translations IT curate
> **Sub-PR position**: 2 di 3 — FE consumption layer. Sub-PR 1/3 = BE foundation (SHIPPED). Sub-PR 3/3 = seed data.

## 1. Contesto

Wave 1 (PR #2370) ha shipped la foundation BE: `shared_game_translations` table, aggregate `SharedGameTranslation`, `IGameTitleResolver` che enricha 4 query handler (`GetAllSharedGames`, `SearchSharedGames`, `GetFilteredSharedGames`, `GetPendingApprovalGames`) con il campo nullable `Translations: IReadOnlyList<SharedGameTranslationDto>?` su `SharedGameDto`. Il DTO C# è già live in `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs:64` come **field opzionale al fondo del positional record** (nullable, default null).

Sub-PR 2/3 chiude il gap consumer-side: il payload arriva sulla FE già arricchito, ma:

1. Le Zod schemas in `apps/web/src/lib/api/schemas/shared-games.schemas.ts` **non hanno ancora** il campo `translations[]` — i 141 callsite di `game.title` su 82 file leggono il titolo canonical EN.
2. Non esiste un punto centrale che applichi le policy di selezione (locale fallback, source priority, override esplicito utente).
3. La a11y richiede che il titolo localizzato venga annunciato come tale agli screen reader (l'utente IT che legge "Catan" su un titolo che esiste in IT come "I Coloni di Catan" deve poter risolvere l'ambiguità).

Questo doc lock-in il design del hook `useGameTitle()` + DTO TS update + consumer migration. Output: ResolvedTitle pattern (NON plain string) + 5 REQ-FE + locale resolution chain BCP-47 + 6 GWT scenarios + 6-row test matrix + axe AA gate.

### 1.1 Why sub-PR 2/3 separately from sub-PR 1/3

- **Independent merge gate**: il BE foundation è già pulito in staging snapshot, no rollback risk se la FE ship in PR diversa.
- **Review scope chiaro**: PR 1/3 = 53 file BE; PR 2/3 = ~30 file FE diff (hook + types + 82 consumer touch); PR 3/3 = seed bash + research doc. Squash isolato facilita revert chirurgico.
- **Sub-PR 3/3 dipende solo dal BE endpoint** (Wave 5 #2339 admin POST/PUT/DELETE), NON dal FE hook. I 2 stream possono procedere in parallelo se Wave 5 ships prima.

## 2. Decisioni locked

Lockate via spec-panel review (Wiegers/Cockburn/Adzic/Fowler/Crispin/Gregory, 2026-06-20).

> ✅ **DECISION BUNDLE LOCKED 2026-06-20** — 11 DEC accepted-as-default in user review session 2026-06-20:
> - DEC-FE-1..8 + DEC-FE-10 + DEC-FE-DEFER accepted senza modifiche (bundle "Accept all defaults")
> - DEC-FE-9 lockato esplicito su i18n chiave react-intl (NOT hardcoded EN string)
> - Audit trail: spec-panel review session 2026-06-20, sub-agent A output, user AskUserQuestion 2026-06-20

| # | Status | Q | Decisione | Rationale |
|---|---|---|---|---|
| DEC-FE-1 | ✅ **LOCKED 2026-06-20** | Hook return type | `ResolvedTitle` struct (NON `string`) | Consumer a11y + admin badge richiedono `source/locale` discriminator; plain string nasconde il context (Fowler hard reject su API design) |
| DEC-FE-2 | ✅ **LOCKED 2026-06-20** | Source priority | `manual` > `auto-openrouter` > `community` | Gregory: manual è curato umano → quality > automation > unverified community |
| DEC-FE-3 | ✅ **LOCKED 2026-06-20** | Locale fallback chain | `region-specific` → `language-only` → `canonical` (NO upgrade `it` → `it-IT`) | BCP-47 `it-IT` → `it` → `en` (Crispin matrix row 3). User request `it` NON viene upgradato a `it-IT` per evitare surprise behavior. |
| DEC-FE-4 | ✅ **LOCKED 2026-06-20** | Explicit override wins | User profile `preferredLocale` precede browser `Accept-Language` | Wiegers REQ-FE-5: respect user agency, no surprise |
| DEC-FE-5 | ✅ **LOCKED 2026-06-20** | Locale resolution side | Client-side (FE hook) | "Both" DTO shape già lockato in Wave 1 spec §2 → BE non fa resolution, payload include sempre tutte le translation attive |
| DEC-FE-6 | ✅ **LOCKED 2026-06-20** | DTO TS update path | Manual edit a `apps/web/src/lib/api/schemas/shared-games.schemas.ts` (Zod schemas) | Non c'è OpenAPI-generator wire (confermato via grep su `schemas/` directory). FE types sono scritti a mano, validati a runtime via Zod |
| DEC-FE-7 | ✅ **LOCKED 2026-06-20** | Cache strategy | `useMemo` deps `[game.id, game.translations, locale]` | Hook chiamato dentro `.map()` su list pages — memoize per evitare re-resolve su ogni render; non React Query (locale è puro client) |
| DEC-FE-8 | ✅ **LOCKED 2026-06-20** | Consumer migration | Codemod-assisted grep+replace, ESLint **warn** initial → **error** post-migration (14gg trajectory verde) | 82 file × 141 occurrence — graduale, hook adoption non-blocking inizialmente. Promotion `warn` → `error` via follow-up issue dopo trajectory verde verificata |
| DEC-FE-9 | ✅ **LOCKED 2026-06-20** | a11y pattern | `aria-label = intl.formatMessage({ id: 'common.localizedFromEnglish' }, { localizedTitle, originalTitle })` via `react-intl` | WCAG 2.1 SC 3.1.2 Language of Parts. Solo quando `source === 'translation'`. **i18n chiave** (NON hardcoded EN string) → cresce naturale a 5+ locales, zero string drift. Richiede aggiunta chiave in `apps/web/src/locales/{it,en}.json` (§5.4) |
| DEC-FE-10 | ✅ **LOCKED 2026-06-20** | "Untranslated" badge | Solo admin pages (`/admin/shared-games/...`); end-user UI no | Gregory: free-tier user non deve essere bombardato di "italiano mancante" notifications. Admin curator vuole signal |
| DEC-FE-DEFER | ✅ **LOCKED 2026-06-20** | Search filter scope | Canonical-only in sub-PR 2/3 (search input continua a matchare `SharedGame.title` EN) | Apre follow-up issue per decidere se search filter deve anche matchare `Translations[i].title` (es. user IT cerca "Coloni" → trova Catan via translation). NON in scope sub-PR 2/3 per evitare DB index changes + perf risk |

## 3. Use Cases

Tre actor distinti emergono dal panel (Cockburn). Goal/success/failure modes esplicitati.

### 3.1 UC-1: Free-tier user browse catalogo

- **Primary actor**: end-user authenticated, browser `it-IT`, profile language `null` (mai impostato).
- **Goal**: vedere "I Coloni di Catan" sul Discover hub invece di "Catan" se il translation esiste.
- **Frequency**: ogni page render (alta) — `useGameTitle()` chiamato dentro `.map()` su 10-50 card.
- **Success scenario**: hook risolve in <1ms via memoization; UI rendering 0 layout shift; aria-label localizzato attached.
- **Failure modes**:
  - Translation array vuoto → fallback canonical EN, no indicator, no console.warn (silent — è il caso più comune per giochi non-popolari).
  - Translation array `null` (BE non l'ha mandato — admin endpoint legacy) → treat as empty, fallback canonical EN.
  - Locale non-matchable → fallback canonical EN, no error UI.

### 3.2 UC-2: Admin curator translations

- **Primary actor**: admin user via `/admin/shared-games/{id}` editing translations.
- **Goal**: vedere subito quali giochi non hanno IT translation per prioritizzare il backlog.
- **Frequency**: bassa (1-2 sessioni/settimana), accuracy-sensitive.
- **Success scenario**: hook ritorna `source: 'canonical'` e UI mostra badge "⚠ untranslated" sui list item; admin clicca e arriva al form modale.
- **Failure modes**:
  - Translation source `auto-openrouter` confuso con `manual` → admin pensa che sia curato umano. Mitigation: badge differenziato (icona MachineLearning vs Person).

### 3.3 UC-3: Player in live game session

- **Primary actor**: end-user in `/sessions/{id}/live` durante partita attiva.
- **Goal**: vedere lo stesso titolo del gioco su ogni component (header, breadcrumb, score panel) senza flicker su re-render.
- **Frequency**: continua per durata partita (10-90 min), consistency-sensitive.
- **Success scenario**: hook è puro/memoized → re-render del live session non triggera locale re-resolution; titolo stabile.
- **Failure modes**:
  - User cambia locale durante la session (raro ma possibile, theme-toggle pattern) → hook re-resolve su nuovo locale; nessun crash, titolo aggiornato graceful.
  - SSE/WebSocket re-fetch del game DTO con `translations` campo diverso (es. admin appena aggiunto IT translation mid-session) → memoization invalidata, hook ritorna nuovo titolo. Comportamento atteso, non un bug.

## 4. Requirements

Formato SMART (Wiegers): `WHO SHALL WHAT WHEN/WHERE`. Priority `P0` = must-ship sub-PR 2/3.

### REQ-FE-1 — Locale exact-match precedence (P0)

> `useGameTitle(game)` **SHALL** return the localized title when the resolved user locale matches a translation with `source ∈ {'manual', 'auto-openrouter', 'community'}` EXACTLY (case-insensitive on language tag, region preserved per BCP-47).

**Acceptance criterion**: Given `game.translations = [{ locale: 'it', title: 'I Coloni di Catan', source: 'manual' }]` and `useUserLocale() → 'it'`, the hook returns `{ value: 'I Coloni di Catan', source: 'translation', locale: 'it', provider: 'manual' }`.

**Rationale**: zero-config happy path per il 90% degli use case.

### REQ-FE-2 — Canonical EN fallback (P0)

> The hook **SHALL** fallback to `game.title` (canonical EN) when no translation matches the resolved locale chain (REQ-FE-3), without emitting any error or warning.

**Acceptance criterion**: Given `game.translations = [{ locale: 'fr', ... }]` and `useUserLocale() → 'de'`, the hook returns `{ value: game.title, source: 'canonical', locale: 'en' }`. No `console.warn`. No `provider` field.

**Rationale**: free-tier users (UC-1) outnumber localized users; silent fallback is correct UX.

### REQ-FE-3 — BCP-47 locale resolution chain (P0)

> The hook **SHALL** preferire una translation `it` su una translation `it-IT` quando l'utente browser è `it-IT` ma solo `it` translation esiste, e viceversa. La chain è: `region-specific` → `language-only` → `canonical`.

**Acceptance criterion**: matrix 6-row in §8 covers:
- User `it-IT`, translations `[it]` → match on language fallback (`it` wins).
- User `it`, translations `[it-IT]` → NO match (region is more specific than user request), fallback canonical EN.
- User `it-IT`, translations `[it-IT, it]` → match exact (`it-IT` wins).

**Rationale**: BCP-47 standard "Tags for Identifying Languages" (RFC 5646) § Subtag Negotiation. The hook is the single source of truth for matching.

**Note implementativa**: helper puro `resolveLocale(userLocale: string, availableLocales: string[]): string | null` testato in isolamento (Task 3).

### REQ-FE-4 — Source priority `manual > auto-openrouter > community` (P0)

> The hook **SHALL** prefer `manual` source over `auto-openrouter` over `community` when multiple translations exist for the same locale.

**Acceptance criterion**: Given `game.translations = [{ locale: 'it', source: 'auto-openrouter', title: 'Coloni MT' }, { locale: 'it', source: 'manual', title: 'I Coloni di Catan' }]`, the hook returns title `'I Coloni di Catan'` with `provider: 'manual'`.

**Rationale**: Gregory finding — manual è curato umano, quality bar superiore. Auto-OpenRouter è MT (machine translation) non revisionata. Community è user-generated, no moderation in MVP (Wave 1 spec §10).

**Note pratica BE**: il DB unique index `uq_active_translation_per_locale ON (shared_game_id, locale) WHERE NOT is_deleted` impedisce di avere 2 row attive con stesso `(game, locale)` simultaneamente. Quindi REQ-FE-4 si applica solo a translations soft-deleted ri-attivate o a stale snapshot del payload. Il check FE è defensive — costa zero ma blinda casi edge.

### REQ-FE-5 — User explicit override precedence (P0)

> The hook **SHALL** respect the user's explicit locale preference (via `useUserLocale()` reading `UserProfile.Language`) over the browser `Accept-Language` header.

**Acceptance criterion**: Given browser `it-IT` and `useUserLocale() → 'en'` (user has set `Language=en` in profile), the hook returns canonical EN even when `game.translations` includes `[{ locale: 'it', ... }]`. No surprise localization.

**Rationale**: Wiegers — respect user agency. `useUserLocale` hook (existing `apps/web/src/hooks/useUserLocale.ts`) already implements the fallback chain `profile → browser → default`. The translation hook MUST consume this hook (NOT `navigator.language` directly) so the override propagates.

## 5. Contract API

### 5.1 Type definition

```ts
// apps/web/src/lib/i18n/use-game-title.ts

/**
 * The provider that authored a translation, mirroring backend `TranslationSource` enum.
 * Used by the admin badge to differentiate human-curated vs machine-translated content.
 */
export type TranslationProvider = 'manual' | 'auto-openrouter' | 'community';

/**
 * Result of resolving a game's title for the current viewer locale.
 *
 * Discriminated union via `source`:
 * - `source: 'canonical'` → no translation matched; `value` is `game.title` (EN); `provider` is undefined.
 * - `source: 'translation'` → a translation matched; `value` is the localized title; `provider` is the authoring source.
 *
 * `locale` reflects the resolved tag (e.g. `'it-IT'` if exact match, `'it'` if BCP-47 fallback, `'en'` if canonical).
 */
export interface ResolvedTitle {
  value: string;
  source: 'canonical' | 'translation';
  locale: string;
  provider?: TranslationProvider;
}

/**
 * Optional override knobs. The default behavior reads locale from `useUserLocale()`.
 *
 * - `locale`: force a specific locale (e.g. for admin previewing other locales).
 *   Bypasses the user/browser chain entirely.
 */
export interface UseGameTitleOptions {
  locale?: string;
}

/**
 * Resolve the best-fit title for a SharedGame given the current viewer's locale.
 *
 * Pure + memoized: safe to call inside `.map()` callbacks. Re-resolves only when
 * `game.id`, `game.translations`, or the resolved locale changes.
 *
 * @example list of game cards (DEC-FE-9 react-intl pattern)
 * ```tsx
 * function GameGrid({ games }: { games: SharedGame[] }) {
 *   const intl = useIntl();
 *   return games.map(g => {
 *     const { value, source, locale } = useGameTitle(g);
 *     const ariaLabel = source === 'translation'
 *       ? intl.formatMessage(
 *           { id: 'common.localizedFromEnglish' },
 *           { localizedTitle: value, originalTitle: g.title }
 *         )
 *       : value;
 *     return <h3 aria-label={ariaLabel}>{value}</h3>;
 *   });
 * }
 * ```
 *
 * @example admin previewing French
 * ```tsx
 * const { value } = useGameTitle(game, { locale: 'fr' });
 * ```
 */
export function useGameTitle(
  game: Pick<SharedGame, 'title' | 'translations'>,
  options?: UseGameTitleOptions
): ResolvedTitle;
```

### 5.2 Type narrowing aside

The `Pick<SharedGame, 'title' | 'translations'>` input bound (not full `SharedGame`) lets unit tests pass minimal fixtures and avoids coupling the hook to fields it doesn't need (the consumer migration step can rely on `SharedGame | SharedGameDetail` since both will satisfy the bound after Task 1).

### 5.3 Why `useMemo` not React Query

- React Query is for async data fetching with cache invalidation. Title resolution is **pure client-side computation** from already-loaded `game.translations[]`.
- `useMemo([game.id, game.translations, locale])` is sufficient and avoids extra QueryClient cache overhead. Deps stable across re-renders because:
  - `game.id` is a UUID string (referential equality safe).
  - `game.translations` is the same array reference unless the parent fetch returns a fresh payload (React Query response semantics).
  - `locale` is a string from `useUserLocale()` which itself memoizes.

### 5.4 i18n keys da aggiungere (DEC-FE-9 ✅ LOCKED 2026-06-20)

aria-label localization usa `react-intl` con chiave `common.localizedFromEnglish`. Da aggiungere:

**`apps/web/src/locales/it.json`**:
```json
{
  "common.localizedFromEnglish": "{localizedTitle} (tradotto da: {originalTitle})"
}
```

**`apps/web/src/locales/en.json`**:
```json
{
  "common.localizedFromEnglish": "{localizedTitle} (localized from English: {originalTitle})"
}
```

**Per future locales** (es. `es`, `fr`, `de` — out of scope sub-PR 2/3, ma chiave i18n garantisce zero-touch sviluppo futuro):
```jsonc
// es.json: "{localizedTitle} (traducido del inglés: {originalTitle})"
// fr.json: "{localizedTitle} (traduit de l'anglais: {originalTitle})"
// de.json: "{localizedTitle} (übersetzt aus dem Englischen: {originalTitle})"
```

**Reject hardcoded EN string**: `aria-label = \`${value} (localized from English: ${original})\`` rifiutato perché un utente DE che vede un game con solo IT translation avrebbe aria-label in EN — string drift. Con `intl.formatMessage`, l'aria-label segue automatically il locale UI dell'utente.

**Test consideration**: nei Vitest unit test, wrap component con `<IntlProvider locale="en" messages={enMessages}>...` come pattern già usato in altri component test (vedi `apps/web/src/components/.../[component].test.tsx` per reference).

## 6. Locale Resolution Algorithm

Pure helper extracted for unit-testability:

```ts
// apps/web/src/lib/i18n/resolve-locale.ts

/**
 * Picks the best-fit available locale for a user-preferred locale.
 *
 * Fallback chain per BCP-47:
 *   1. Exact match (case-insensitive language, case-preserved region).
 *   2. Language-only match (drop region: 'it-IT' → 'it').
 *   3. null (caller falls back to canonical EN).
 *
 * `null` available locales list returns `null` immediately.
 *
 * Examples:
 *   resolveLocale('it-IT', ['it'])      → 'it'    (region drop fallback)
 *   resolveLocale('it', ['it-IT'])      → null    (cannot upgrade language to region)
 *   resolveLocale('it-IT', ['it-IT'])   → 'it-IT' (exact match wins)
 *   resolveLocale('it-IT', ['it-IT','it']) → 'it-IT' (exact wins over fallback)
 *   resolveLocale('de', ['it','fr'])    → null
 */
export function resolveLocale(
  userLocale: string,
  availableLocales: ReadonlyArray<string>
): string | null;
```

**Decision tree**:

```
user="xx-YY" → exact "xx-YY" in available? → yes → return "xx-YY"
                                            ↓ no
              language "xx" in available? → yes → return "xx"
                                            ↓ no
                                          → return null

user="xx"    → exact "xx" in available?   → yes → return "xx"
                                            ↓ no
              (NO upgrade to "xx-YY" — user did not request a region)
                                          → return null
```

**Note BCP-47**: case normalization is `language: lowercase`, `region: uppercase`. The BE VO `Locale.Create()` already enforces this on storage (`apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/Locale.cs:432`), so payload-side strings are already in canonical form. The FE helper normalizes input defensively (`userLocale.toLowerCase()` on the language segment) but trusts the payload as-is.

## 7. Gherkin Scenarios (Acceptance-driven)

Adzic — executable spec via Gherkin GWT. These map 1:1 to test cases in Task 2 (Vitest).

```gherkin
Feature: Localized game titles

  Background:
    Given a game "Catan" with canonical EN title "Catan"

  Scenario: S1 - User browser locale it-IT, game has only 'it' translation
    Given the game has translation { locale: "it", title: "I Coloni di Catan", source: "manual" }
    And the user has no profile locale set
    And the browser language is "it-IT"
    When I render the game card
    Then the displayed title is "I Coloni di Catan"
    And the source is "translation"
    And the resolved locale is "it"
    And the provider is "manual"
    And the heading aria-label resolves via react-intl key "common.localizedFromEnglish"
    And with UI locale "it" the aria-label is "I Coloni di Catan (tradotto da: Catan)"
    And with UI locale "en" the aria-label is "I Coloni di Catan (localized from English: Catan)"

  Scenario: S2 - User explicit override EN, game has IT translation
    Given the game has translation { locale: "it", title: "I Coloni di Catan", source: "manual" }
    And the user profile locale is "en"
    And the browser language is "it-IT"
    When I render the game card
    Then the displayed title is "Catan"
    And the source is "canonical"
    And the resolved locale is "en"
    And there is no localization indicator
    And no aria-label augmentation occurs

  Scenario: S3 - Manual translation wins over auto-openrouter for same locale
    Given the game has translations:
      | locale | title              | source           |
      | it     | Coloni MT          | auto-openrouter  |
      | it     | I Coloni di Catan  | manual           |
    And the user resolved locale is "it"
    When I render the game card
    Then the displayed title is "I Coloni di Catan"
    And the provider is "manual"

  Scenario: S4 - No matching translation, silent canonical fallback
    Given the game has translation { locale: "fr", title: "Les Colons de Catane", source: "manual" }
    And the user resolved locale is "de"
    When I render the game card
    Then the displayed title is "Catan"
    And the source is "canonical"
    And no console warning is emitted
    And no error UI is shown

  Scenario: S5 - Translations array missing (null), backward compat
    Given the game has translations = null (legacy admin endpoint payload)
    And the user resolved locale is "it"
    When I render the game card
    Then the displayed title is "Catan"
    And the source is "canonical"
    And no error is thrown

  Scenario: S6 - BCP-47 fallback it-IT → it
    Given the game has translation { locale: "it", title: "I Coloni di Catan", source: "manual" }
    And the user resolved locale is "it-IT"
    When I render the game card
    Then the displayed title is "I Coloni di Catan"
    And the resolved locale is "it" (fallback, not "it-IT")
    And the provider is "manual"

  Scenario: S7 - Admin preview overrides user locale
    Given the game has translation { locale: "fr", title: "Les Colons de Catane", source: "manual" }
    And the user resolved locale is "it"
    When I call useGameTitle(game, { locale: "fr" })
    Then the displayed title is "Les Colons de Catane"
    And the source is "translation"
    And the resolved locale is "fr"
```

## 8. Test Matrix (Crispin)

6-row mandatory matrix. Each row = 1 Vitest test in Task 2.

| # | Browser locale | Profile override | Available translations | Expected title | Expected source | Expected locale |
|---|---|---|---|---|---|---|
| T1 | `en` | — | `[]` (none) | canonical EN | `canonical` | `en` |
| T2 | `it` | — | `[{it, manual}]` | IT manual | `translation` | `it` |
| T3 | `it-IT` | — | `[{it, manual}]` | IT manual (region fallback) | `translation` | `it` |
| T4 | `de` | — | `[{it, manual}, {fr, community}]` | canonical EN | `canonical` | `en` |
| T5 | `it` | `en` (explicit) | `[{it, manual}]` | canonical EN (override wins) | `canonical` | `en` |
| T6 | `it` | — | `[{it, manual}, {it, auto-openrouter}]` | IT manual (source priority) | `translation` | `it` |

**Plus 3 a11y rows** (axe + react-intl, DEC-FE-9 LOCKED):

| # | Scenario | Axe rule | Expected |
|---|---|---|---|
| A1 | Localized title rendered with `intl.formatMessage('common.localizedFromEnglish')` aria-label (UI locale=en) | `aria-allowed-attr`, `aria-valid-attr-value` | 0 violations + aria-label contains "localized from English" |
| A2 | Canonical title rendered with no aria-label augmentation | `aria-required-children` (parent heading) | 0 violations |
| A3 | Localized title aria-label with UI locale=it via `IntlProvider locale="it"` wrap | `aria-allowed-attr` | aria-label contains "tradotto da" (verifica chiave i18n switching) |

## 9. Cache & Memoization

```tsx
function useGameTitle(game, options) {
  const fallbackLocale = useUserLocale();
  const locale = options?.locale ?? fallbackLocale;

  return useMemo(
    () => resolveTitle(game, locale),
    [game.id, game.translations, locale]
  );
}
```

**Why `[game.id, game.translations, locale]`**:
- `game.id` is the stable identity key. If the parent fetch returns the same id but a different translations array (admin just added IT translation), the second dep triggers re-resolve.
- `game.translations` reference equality is sufficient — React Query / fetch responses produce a fresh array on each successful refetch.
- `locale` from `useUserLocale()` is itself memoized (the existing hook uses `useState`, so the reference is stable until the profile fetch updates it).

**Anti-pattern banned**: `useMemo(() => ..., [game])` would invalidate on every re-render because parents often spread `{ ...game }` — too coarse.

**Performance budget**: 100 cards × `useGameTitle()` = 100 `useMemo` calls. React `useMemo` overhead is ~1µs per call; 100µs/frame is well under the 16ms frame budget. No perf concern.

## 10. Consumer Migration Plan

### 10.1 Grep pattern

The reference grep produced 141 occurrences across 82 files (verified 2026-06-20). Cluster:

| Cluster | Files | Pattern | Strategy |
|---|---|---|---|
| Render-only display (no logic) | ~60 | `<h3>{game.title}</h3>` | Replace with `const { value } = useGameTitle(game); <h3>{value}</h3>` |
| Inside `.map()` over list | ~15 | `games.map(g => <Card title={g.title} />)` | Hook MUST be called at the leaf component level, NOT inside `.map()` body of a parent function component (Rules of Hooks) — extract `<GameCardContent game={g} />` if not already |
| Storybook/test fixtures | ~5 | `mockGame.title === 'X'` | Leave canonical EN; fixtures should not rely on translation resolution |
| Search input filter / sort | ~10 | `games.filter(g => g.title.includes(...))` | Open question: search by canonical only, or also by translations? **DEC-FE-DEFER**: search remains canonical-only in sub-PR 2/3 (Wiegers parking lot — out of scope, follow-up filed) |
| Type definitions / generic | ~10 | `type X = Pick<SharedGame, 'title'>` | No change — type stays |
| Admin pages | ~20 | `game.title` displayed alongside metadata | Use hook + show "untranslated" badge per DEC-FE-10 |

### 10.2 Migration order

1. **Task 6.1**: Run `grep -rn "game\.title" apps/web/src/ --include="*.tsx" --include="*.ts"` → save baseline count (141).
2. **Task 6.2**: Migrate the 5 highest-traffic surfaces first:
   - `apps/web/src/components/discover/GameDiscoverDetail.tsx` (UC-1 hot path)
   - `apps/web/src/components/features/hub/HubGameCard.tsx` (Discover row card)
   - `apps/web/src/components/games/MeepleGameCard.tsx` (catalog card)
   - `apps/web/src/components/library/MeepleUserLibraryCard.tsx` (Library card)
   - `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx` (shared game detail)
3. **Task 6.3**: Sweep the rest with the codemod (`scripts/codemod/use-game-title-migration.ts`, ts-morph or jscodeshift).
4. **Task 6.4**: ESLint custom rule `local/prefer-use-game-title` in **warn** mode (NOT error in this PR) — flags raw `game.title` access in JSX expressions. Promote to error in a follow-up PR after 14gg main-dev trajectory verde.

### 10.3 Risk mitigation

- **Tests stale**: 82 files include 9 `__tests__/*.test.tsx`. After each migration sub-batch, run `pnpm test --changed`.
- **Storybook snapshots drift**: 5 `.stories.tsx` files touched. Re-record via `pnpm storybook:test --update-snapshots` only if visual conformity gate exists (note: visual gate retired 2026-05-20 per CLAUDE.md — so no auto-gate, just sanity check).
- **Type errors at consumer**: the `translations?: SharedGameTranslationDto[]` Zod field is **optional**; existing consumers that don't pass it through will still typecheck.

### 10.4 Out-of-scope for migration

- Search input filter (UC-DEFER above). Open follow-up issue: "Should search match localized titles?". Stays canonical-only for sub-PR 2/3.
- Storybook fixtures that hardcode `title: 'Catan'`. They remain English; user-facing pages use the hook.
- BE-emitted notification text (e.g. game-night invite email) — that's a server-side concern, separate issue.

## 11. Sub-PR 3/3 Scope (preview)

Detail lives in [`docs/superpowers/plans/2026-06-20-translations-seed-subpr3.md`](../plans/2026-06-20-translations-seed-subpr3.md). Summary here for cross-reference:

- **Research doc** `infra/scripts/seed-sp4/translations-research.md`: per-game IT title verification (publisher URL + retailer + native review) + classification `manual` vs `auto-openrouter`.
- **Seed data** `infra/scripts/seed-sp4/data.json` extension: add `gameTranslations[]` array with `{ gameSlug, locale: "it", title, source }`.
- **Seed script** `infra/scripts/seed-sp4/45-translations.sh`: POST sequential to `/api/v1/admin/games/{id}/translations` for each game-translation pair.
- **ADR-059 update** §5.x: clarify translation seeds are not in BGG ToS scope (titles only, fact-based per Feist doctrine).
- **Q4 doc closure** in `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md` §9: replace "follow-up dedicato" note with "RESOLVED via #2339 sub-PR 3/3".

Note: sub-PR 3/3 depends on Wave 5 admin endpoints (`POST /api/v1/admin/games/{id}/translations`) being shipped first — those are part of issue #2339 task 14 (still TODO at sub-PR 2/3 time). If Wave 5 ships in parallel, sub-PR 3/3 can land same day as sub-PR 2/3.

## 12. Out of Scope (esplicito non-goals)

- ❌ Server-side locale negotiation via `Accept-Language` middleware (Wave 1 already lockata su "Both" DTO, no `ILocaleProvider`).
- ❌ Community translations moderation queue (Wave 1 spec §10).
- ❌ User preference UI panel for setting `preferredLocale` (existing `useUserLocale` hook reads from `UserProfile.Language`, which is managed in `/profile/settings` — already shipped).
- ❌ Search input localized filter (DEC-FE-DEFER, follow-up issue).
- ❌ E2E translation of admin endpoint error messages (separate concern).
- ❌ Description field localization (sub-PR 2/3 wires `translations[].description` into the type but no UI consumer reads it yet; admin form for description ships in sub-PR 3/3 spec or follow-up).
- ❌ Promoting `local/prefer-use-game-title` ESLint rule to error mode (deferred to follow-up post-14gg trajectory).
- ❌ Storybook fixtures localization (canonical EN forever; UI tests use English).

## 13. Acceptance Criteria

- [ ] DTO TS Zod schema `SharedGameSchema` and `SharedGameDetailSchema` extended with optional `translations: SharedGameTranslationDtoSchema[]`.
- [ ] New schema `SharedGameTranslationDtoSchema` exported from `shared-games.schemas.ts`.
- [ ] Hook `useGameTitle(game, options?): ResolvedTitle` shipped in `apps/web/src/lib/i18n/use-game-title.ts`.
- [ ] Pure helper `resolveLocale(userLocale, available)` shipped in `apps/web/src/lib/i18n/resolve-locale.ts`.
- [ ] Pure helper `pickBestTranslation(translations, locale)` shipped in `apps/web/src/lib/i18n/pick-best-translation.ts`.
- [ ] 6 Vitest unit tests covering test matrix T1-T6 (§8) all green.
- [ ] 2 axe a11y tests A1+A2 green.
- [ ] 5 highest-traffic consumers migrated (§10.2 Task 6.2 list).
- [ ] Remaining consumers swept via codemod or PR comment marker, all 141 occurrences accounted for.
- [ ] ESLint rule `local/prefer-use-game-title` in **warn** mode, no false positives on type-only references.
- [ ] E2E Playwright happy-path: `pnpm test:e2e --grep "translations-fe-hook"` green with Accept-Language `it` showing IT title on Library and Discover.
- [ ] `pnpm typecheck` + `pnpm lint` green project-wide.
- [ ] `pnpm test --coverage` ≥85% on new files.
- [ ] Issue #2339 body updated with sub-PR 2/3 closure note.

## 14. References

- Wave 1 design: [`docs/superpowers/specs/2026-06-15-shared-game-translations-design.md`](./2026-06-15-shared-game-translations-design.md)
- Wave 1 plan TDD: [`docs/superpowers/plans/2026-06-15-shared-game-translations.md`](../plans/2026-06-15-shared-game-translations.md)
- Wave 1 shipped PR: [#2370](https://github.com/meepleAi-app/meepleai-monorepo/pull/2370) (`cd041ca35`)
- Sub-PR 2/3 plan TDD: [`docs/superpowers/plans/2026-06-20-translations-fe-hook.md`](../plans/2026-06-20-translations-fe-hook.md)
- Sub-PR 3/3 plan TDD: [`docs/superpowers/plans/2026-06-20-translations-seed-subpr3.md`](../plans/2026-06-20-translations-seed-subpr3.md)
- BE DTO: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameDto.cs:29-64`
- BE Translation DTO: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/SharedGameTranslationDto.cs:13-17`
- FE Zod schemas: `apps/web/src/lib/api/schemas/shared-games.schemas.ts:166-199`
- Existing locale hook: `apps/web/src/hooks/useUserLocale.ts:100-141`
- Existing i18n provider: `apps/web/src/components/providers/IntlProvider.tsx`
- ADR-059 (catalog seed legal posture, relevant for sub-PR 3/3): [`docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md`](../../for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md)
- Q4 closure note: [`docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md`](../../for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md) §9
- BCP-47 / RFC 5646 — Tags for Identifying Languages
- WCAG 2.1 SC 3.1.2 Language of Parts
