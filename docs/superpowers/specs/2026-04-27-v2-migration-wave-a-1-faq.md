# V2 Migration · Wave A.1 — FAQ Pilot Spec

**Issue**: #583 (parent: #579 Wave A, umbrella: #578)
**Branch**: `feature/issue-583-migration-wave-a-1-faq` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp3-faq-enhanced.jsx` (992 LOC)
**Visual baseline**: `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-faq-enhanced-*.png` (Linux, PR #575)
**Companion exec plan**: `2026-04-27-v2-migration-phase1-execution.md` §3.1
**Program spec**: `2026-04-26-v2-design-migration.md` (APPROVED 2026-04-27)
**Status**: DRAFT 2026-04-27

---

## 1. Goals

1. **Sostituire** `/faq` v1 → v2 in-place (Big-Bang, no feature flag).
2. **Validare workflow PILOTA** introdotto qui:
   - Playwright project `e2e/visual-migrated/` (route prod vs mockup baseline).
   - Playwright project `e2e/v2-states/` (default/empty/loading/error per route).
   - Hybrid masking via `data-dynamic` HTML attribute.
   - TDD red→green: spec visual-migrated **scritto prima** della riscrittura route.
3. **Estrarre 4 componenti v2 riusabili** in `src/components/ui/v2/faq/` (sblocca A.2-A.5 + Wave B).

## 2. Non-goals

- Backend FAQ CMS (alpha = static dataset in route file o `lib/faq/data.ts`).
- Analytics-driven `popularRank` (Alpha = curated marketing-side; analytics post-alpha).
- Migrazione parallela A.2-A.5 (sequenziale dopo gate A.1).
- Refactor `Accordion` legacy in `data-display/accordion.tsx` (v1 callers restano; v2 usa nuovo `AccordionItem v2`).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| Route page | `apps/web/src/app/(public)/faq/page.tsx` | **Replace** (v1 → v2) |
| Route test | `apps/web/src/app/(public)/faq/__tests__/page.test.tsx` | **Rewrite** (rebuilt to v2 contract) |
| Static data | `apps/web/src/lib/faq/data.ts` | **Create** (`FAQ_CATEGORIES`, `FAQS[]`, `POPULAR_FAQS`) |
| Search helper | `apps/web/src/lib/faq/search.ts` | **Create** (`highlight()`, `renderInline()`, `filterFAQs()`) |
| Search hook | `apps/web/src/hooks/useFaqHashQuery.ts` | **Create** (URL `#q=` ↔ state) |
| Component | `apps/web/src/components/ui/v2/faq/faq-search-bar.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/faq/quick-answer-card.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/faq/category-tabs.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/faq/accordion-item.tsx` | **Create** (v2 — distinct from legacy `data-display/accordion.tsx`) |
| Component index | `apps/web/src/components/ui/v2/faq/index.ts` | **Create** |
| i18n IT | `apps/web/src/locales/it.json` § `pages.faq` | **Replace contents** |
| i18n EN | `apps/web/src/locales/en.json` § `pages.faq` | **Replace contents** |
| Visual test | `apps/web/e2e/visual-migrated/sp3-faq-enhanced.spec.ts` | **Create** |
| State test | `apps/web/e2e/v2-states/faq.spec.ts` | **Create** |
| Playwright config | `apps/web/playwright.config.ts` | **Edit** (add 2 projects) |
| Doc | `docs/frontend/visual-regression.md` | **Edit** (document new projects) |

### 3.2 Component API

#### `FAQSearchBar`

```ts
interface FAQSearchBarProps {
  value: string;
  onChange: (next: string) => void;
  placeholder?: string;          // i18n key passed by caller
  totalCount: number;            // visible to a11y label "X risultati"
  matchCount: number;            // current filtered count
  className?: string;
}
```

Visual: large pill, left search icon, right clear-X (visible iff `value.length > 0`), bg `hsl(var(--c-game) / .04)`, border `hsl(var(--border))`, focus ring `entityHsl('game', 0.4)`.

#### `QuickAnswerCard`

```ts
interface QuickAnswerCardProps {
  faq: FAQ;          // dataset entry
  onClick: () => void;  // scroll to AccordionItem + open
  className?: string;
}
```

Visual: 2x2 grid container; each card border-left 3px `hsl(var(--c-game))`, hover translate-y -2px + shadow. Number badge `#{popularRank}` top-right `text-[10px] tabular-nums`. Question bold, short truncated 2 lines.

#### `CategoryTabs`

```ts
interface CategoryTabsProps {
  categories: FAQCategory[];
  active: CategoryId;            // 'all' | 'account' | 'games' | 'ai' | 'privacy' | 'billing'
  onChange: (id: CategoryId) => void;
  countByCategory: Record<CategoryId, number>;
  sticky?: boolean;              // default true (top: var(--header-h))
}
```

Visual: horizontal scrollable on mobile (no scrollbar via `scrollbar-width: none`), pill buttons `rounded-full`, active = `bg hsl(var(--c-game) / .12)` + border `hsl(var(--c-game))` + text `hsl(var(--c-game))`. Count badge `tabular-nums text-[11px]` next to label.

#### `AccordionItem` (v2)

```ts
interface AccordionItemProps {
  id: string;                    // anchor id ("faq-q1")
  question: ReactNode;           // already highlighted via `highlight()`
  answer: ReactNode;             // rendered via `renderInline()`
  category: FAQCategory;
  defaultOpen?: boolean;
  onOpenChange?: (open: boolean) => void;
  data-dynamic?: never;          // type-safe: parent passes via wrapper, not here
}
```

Visual: chevron 18px rotates 180° on open, `transition-transform 200ms`. Body `max-h` transition `300ms ease-out`. Category badge inline near question (bg `entityHsl(cat.token, 0.1)`). Border-bottom `hsl(var(--border))`, no border on last child.

### 3.3 Data shape

```ts
// lib/faq/data.ts

export type CategoryId = 'all' | 'account' | 'games' | 'ai' | 'privacy' | 'billing';

export interface FAQCategory {
  id: CategoryId;
  labelKey: string;        // i18n key e.g. 'pages.faq.categories.account'
  icon: string;            // emoji, mockup-fidelity
}

export interface FAQ {
  id: string;              // 'q1'..'q14'
  cat: Exclude<CategoryId, 'all'>;
  questionKey: string;     // 'pages.faq.items.q1.question'
  shortKey: string;        // 'pages.faq.items.q1.short'
  longKey: string;         // 'pages.faq.items.q1.long'
  popular: boolean;
  popularRank?: number;    // 1..5, only if popular
}

export const FAQ_CATEGORIES: ReadonlyArray<FAQCategory>;
export const FAQS: ReadonlyArray<FAQ>;
export const POPULAR_FAQS: ReadonlyArray<FAQ>;  // sorted, sliced 4
```

### 3.4 i18n keys (NEW SHAPE)

Replace existing `pages.faq` block in `it.json` + `en.json`:

```json
{
  "pages": {
    "faq": {
      "title": "Domande Frequenti",
      "subtitle": "Tutto quello che devi sapere su MeepleAI durante l'alpha.",
      "search": {
        "placeholder": "Cerca tra le FAQ…",
        "clearLabel": "Cancella ricerca",
        "resultsLabel": "{count, plural, =0 {Nessun risultato} one {1 risultato} other {# risultati}}",
        "totalLabel": "{count} domande totali"
      },
      "categories": {
        "all":     { "label": "Tutte" },
        "account": { "label": "Account & Login" },
        "games":   { "label": "Giochi & Library" },
        "ai":      { "label": "AI & Chat" },
        "privacy": { "label": "Privacy & Dati" },
        "billing": { "label": "Billing & Alpha" }
      },
      "popularSection": {
        "title": "Domande più frequenti",
        "subtitle": "Le risposte rapide che cerchi più spesso."
      },
      "emptyState": {
        "title": "Nessuna FAQ corrisponde alla tua ricerca",
        "subtitle": "Prova con termini diversi o contattaci direttamente.",
        "contactCta": "Contatta supporto"
      },
      "loadingState": {
        "label": "Caricamento FAQ…"
      },
      "items": {
        "q1": { "question": "...", "short": "...", "long": "..." },
        "q2": { "question": "...", "short": "...", "long": "..." },
        ... (q1..q14)
      },
      "contactCta": "Contattaci",
      "howItWorksCta": "Come Funziona"
    }
  }
}
```

EN translations to be authored mirroring IT.

### 3.5 URL hash search behavior

```ts
// hooks/useFaqHashQuery.ts

export function useFaqHashQuery(): {
  query: string;
  setQuery: (next: string) => void;
} {
  // Read initial: window.location.hash.match(/#q=(.+)/) → decodeURIComponent
  // Subscribe: 'hashchange' event → re-read
  // setQuery: history.replaceState(null, '', `#q=${encodeURIComponent(next)}`) (or remove hash if empty)
  // Debounced 250ms write to avoid history spam during typing
}
```

Stateless, deep-linkable, `back` navigates between searches.

### 3.6 Loading / Error / Empty states

| State | Trigger | Visual | data-dynamic |
|-------|---------|--------|---------------|
| `default` | initial render, `query === ''`, `cat === 'all'` | Hero + SearchBar + Popular grid + Tabs + All accordions | none |
| `category-filter` | `cat !== 'all'`, `query === ''` | Hero + SearchBar + Tabs (active highlight) + filtered accordions | none |
| `search-results` | `query !== ''` | Hero + SearchBar (clear visible) + Tabs (count updated) + filtered accordions w/ `<mark>` | search input value (mask) |
| `search-no` | `query !== ''`, no matches | Hero + SearchBar + Tabs (all 0) + EmptyState card w/ contact CTA | search input value |
| `loading` | i18n loading or hydration delay > 100ms | Hero + SearchBar disabled + Tabs disabled + 3 accordion skeletons | skeleton shimmer |

**Note on `loading`**: this route is server-rendered with static dataset; `loading` state is reachable mainly via React Suspense if i18n bundles split-load. Tested via storybook-style harness in `e2e/v2-states/` rather than live route.

### 3.7 `data-dynamic` zones (hybrid masking)

| Selector | Reason |
|----------|--------|
| `[data-dynamic="search-input"]` | User-typed text (varies per test run) |
| `[data-dynamic="results-count"]` | Result count varies if data changes |

Mockup baseline has zero dynamic zones (static dataset); production introduces input only. Mask in `e2e/visual-migrated/sp3-faq-enhanced.spec.ts` with `mask: [page.locator('[data-dynamic]')]`.

## 4. Test plan (TDD red → green)

### 4.1 Visual regression migrated (PRIMARY GATE)

`apps/web/e2e/visual-migrated/sp3-faq-enhanced.spec.ts`:

```ts
test('FAQ route v2 matches mockup baseline (default state)', async ({ page }) => {
  await page.goto('/faq');
  await page.waitForLoadState('networkidle');
  await expect(page).toHaveScreenshot('sp3-faq-enhanced-default.png', {
    maxDiffPixelRatio: 0.001,
    fullPage: true,
    mask: [page.locator('[data-dynamic]')],
  });
});

test('FAQ route v2 matches mockup baseline (search-results)', async ({ page }) => {
  await page.goto('/faq#q=password');
  await page.waitForLoadState('networkidle');
  await expect(page).toHaveScreenshot('sp3-faq-enhanced-search.png', {
    maxDiffPixelRatio: 0.001,
    fullPage: true,
    mask: [page.locator('[data-dynamic]')],
  });
});
```

Baseline source: existing `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-faq-enhanced-*.png` from PR #575. Workflow: copy/symlink mockup baseline → first run captures prod → diff → tune until matches.

**TDD red contract**: spec written and committed BEFORE route rewrite. First CI run on feature branch fails (route still v1) — confirms test wired correctly. Then route rewrite → green.

### 4.2 State coverage

`apps/web/e2e/v2-states/faq.spec.ts`:

```ts
test.describe('FAQ states', () => {
  test('default', async ({ page }) => {
    await page.goto('/faq');
    await expect(page).toHaveScreenshot('faq-default.png', { mask: [page.locator('[data-dynamic]')] });
  });

  test('empty (search-no)', async ({ page }) => {
    await page.goto('/faq#q=zzznonexistentzzz');
    await expect(page.getByRole('heading', { name: /nessuna faq/i })).toBeVisible();
    await expect(page).toHaveScreenshot('faq-empty.png', { mask: [page.locator('[data-dynamic]')] });
  });

  test('loading skeleton', async ({ page }) => {
    // Force suspense via test-only param ?loading=1 (added behind NODE_ENV !== 'production')
    await page.goto('/faq?loading=1');
    await expect(page.getByLabel(/caricamento/i)).toBeVisible();
    await expect(page).toHaveScreenshot('faq-loading.png');
  });

  test('error', async ({ page }) => {
    // Test-only param ?error=1
    await page.goto('/faq?error=1');
    await expect(page.getByRole('alert')).toBeVisible();
    await expect(page).toHaveScreenshot('faq-error.png');
  });
});
```

Test-only `?loading=1` and `?error=1` query params guarded by `process.env.NODE_ENV !== 'production'` in route component. Stripped from prod bundle.

### 4.3 Component unit tests (Vitest, target 85%+)

| Component | Min cases |
|-----------|-----------|
| `FAQSearchBar` | render w/ value, onChange callback, clear button visibility, clear button click, a11y label |
| `QuickAnswerCard` | render question + short, popularRank badge, onClick fires, hover class |
| `CategoryTabs` | render 6 cats, active state, onChange callback, count badges, sticky positioning class |
| `AccordionItem` | open/close on click, keyboard (Enter/Space), aria-expanded, chevron rotation, content animated |
| `useFaqHashQuery` | initial parse, hashchange listener, debounced write, encode/decode |
| `lib/faq/search.ts` | `highlight()` with mark, escape regex, no-match passthrough; `filterFAQs()` by cat + query; `renderInline()` bold/code/list |

### 4.4 E2E happy path

`apps/web/e2e/critical/faq.spec.ts` (existing or new):

```
1. goto /faq
2. expect popular section visible (4 cards)
3. type "password" in search → results filter, mark visible
4. clear search → all FAQs back
5. click "AI & Chat" tab → only ai cat FAQs visible
6. click first accordion → expands, aria-expanded="true"
7. assert no console errors
```

### 4.5 Bundle delta gate

`pnpm build` + `pnpm size`. Budget: `< +50 KB` per acceptance. Update `apps/web/bundle-size-baseline.json` if delta confirmed acceptable.

### 4.6 a11y gate

axe-core run via Playwright in `e2e/a11y/faq.spec.ts`. Must report **zero violations** WCAG AA. Specific checks:

- `<main>` landmark present
- Search input has `<label>` (visually hidden ok, but present)
- Tabs use `role="tablist"` / `role="tab"` / `aria-selected`
- AccordionItem button has `aria-expanded`, `aria-controls`, target panel has `role="region"` + `aria-labelledby`
- `<mark>` highlight has sufficient contrast (`hsl(var(--c-warning) / .28)` on light + dark verified)
- Empty state CTA reachable by keyboard, focus visible

## 5. Migration steps (build sequence)

1. **Spec persisted** ✅ (this file)
2. **Test infra** (Task #14):
   - `playwright.config.ts`: add 2 projects `visual-migrated` + `v2-states` (mirror `visual-mockups` config; `MOCKUP_ONLY_WEBSERVER` flag NOT applicable — we want Next.js prod)
   - `package.json` scripts: `test:visual:migrated`, `test:v2-states`
   - `docs/frontend/visual-regression.md`: document new projects + maskaging strategy
3. **Red visual spec** (Task #15): write `e2e/visual-migrated/sp3-faq-enhanced.spec.ts` referencing baseline → CI fail
4. **Implementation** (Task #16):
   - 4.1 — i18n: replace `pages.faq` in `it.json` + author `en.json`
   - 4.2 — `lib/faq/data.ts` + `lib/faq/search.ts`
   - 4.3 — `hooks/useFaqHashQuery.ts`
   - 4.4 — 4 components in `src/components/ui/v2/faq/` (with stories + unit tests)
   - 4.5 — `app/(public)/faq/page.tsx` rewrite (delete v1, write v2 importing components)
   - 4.6 — Add `apps/web/src/app/(public)/faq/__tests__/page.test.tsx` rewrite
   - 4.7 — Add `e2e/v2-states/faq.spec.ts`
5. **Green** (Task #17): all tests pass, bundle ✓, a11y ✓
6. **PR** (Task #18): target `main-dev`, body template per companion exec plan §3.2

## 6. Risks & mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| Baseline mockup PNG mismatches prod due to font hinting / antialiasing on Linux CI vs local | Medium | Baseline already committed in PR #575 from CI Linux. Local dev verifies via `pnpm test:visual:migrated --update-snapshots` only as draft; final commit uses CI-generated PNG via `workflow_dispatch mode=bootstrap` pattern (PR #575 precedent) |
| Search debounce timing flakiness in visual tests | Low | Disable debounce in test mode via `NEXT_PUBLIC_DISABLE_FAQ_DEBOUNCE=1` in test runner env; or use `await page.waitForTimeout(300)` pre-screenshot |
| Loading state unreachable without artificial delay | Medium | Test-only `?loading=1` param + `<Suspense fallback>` boundary toggled in dev. Acceptable scope: covered in v2-states project, NOT live route |
| Bundle delta exceeds 50 KB due to mocked-up icon set | Low | Use `lucide-react` tree-shake (already in deps); inline SVG for category emoji is just text, zero JS cost |
| Hydration mismatch on URL hash (server renders empty, client reads hash) | Medium | Mark search-dependent UI as `'use client'`, render initial empty state SSR, populate from hash on mount via `useEffect` |
| `popularRank` curated drift between mockup data + i18n keys | Low | `lib/faq/data.ts` is single source for `id/cat/popular/popularRank`; i18n only stores text. Test asserts `POPULAR_FAQS.length === 4` and ranks 1-5 monotonic |

## 7. Acceptance (DoD — gate to A.2)

Migration is complete when **all** apply:

- [ ] `apps/web/src/app/(public)/faq/page.tsx` v1 deleted (`git rm`); v2 implemented
- [ ] 4 v2 components in `src/components/ui/v2/faq/` with stories + ≥85% unit coverage each
- [ ] i18n IT + EN replaced with new shape (14 FAQs × 3 fields = 42 + 6 cats × 1 = 48 + UI strings ~12 = ~60 keys per lang)
- [ ] `e2e/visual-migrated/sp3-faq-enhanced.spec.ts` PASS (default + search-results states)
- [ ] `e2e/v2-states/faq.spec.ts` PASS (default + empty + loading + error)
- [ ] `e2e/critical/faq.spec.ts` E2E happy path PASS
- [ ] axe-core zero violations WCAG AA
- [ ] `pnpm typecheck && pnpm lint` clean (incl. `local/no-hardcoded-hex`)
- [ ] Bundle delta `< +50 KB` (verified `pnpm build && pnpm size`)
- [ ] CI Frontend Build & Test ✅, E2E Critical Paths ✅, GitGuardian ✅
- [ ] PR #XXX target = `main-dev` (NOT `main`)
- [ ] PR body includes mockup link + side-by-side artifact + bundle delta + rollback `git revert <sha>` command
- [ ] Code review APPROVE (≤2 nits acceptable as follow-ups)
- [ ] Merged squash to `main-dev`

**On gate fail**: STOP Wave A, write incident note in companion exec plan §8, do NOT open A.2 until root cause + remediation merged.

## 8. Out of scope (deferred to follow-ups)

- FAQ CMS backend (post-alpha)
- Analytics-driven `popularRank` (post-alpha)
- "Was this helpful?" feedback per FAQ (post-alpha)
- Cross-linked related FAQs (post-alpha)
- Codemod for migrating other v1 routes' i18n shape — A.1 hand-rolls; codemod from A.2+ if pattern stabilizes
