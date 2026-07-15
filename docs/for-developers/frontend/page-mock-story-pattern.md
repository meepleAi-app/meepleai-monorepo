# Page-mock story pattern (DS-17 Phase 2.5+)

This pattern documents how to migrate a page-mock HTML (`admin-mockups/design_files/sp4-*.html` +
`.jsx` twin) into a Storybook story that USES the real Client component. Adopted Phase 2.5
retroactive rewrite; Phase 3 sweep (67 page-mock + 48 component-mock) copies questo pattern.

**Spec**: [`docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md`](../../superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md)
**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)

## argTypes matrix pattern (DEC-P3-3)

Each mockup migrated to a story uses **1 story file per mockup** con `argTypes` controls per
axis. Storybook genera matrix interattiva via addon-controls toolbar; snapshot tests
programmaticamente iterano i frame canonical del mockup HTML stage.

### Axis discovery

Per ogni mockup, inspect il `.jsx` twin:
1. Grep `stateOverride`, `variant`, `initialTab`, `initialView`, `drawerOpen`, `bulk` → identify axis
2. Cross-ref `MOBILE_STATES` array, `DesktopFrame label="NN · ..."` → identify canonical frames
3. Story `argTypes` mirror prop interface (use `description: 'Documentation only'` per axis non
   propagated al component); `args` default = first frame del mockup

### Stage frame export pattern

Per ogni frame del mockup HTML stage (`PhoneShell key={s.id}` / `DesktopFrame label="..."`):

```tsx
export const FrameNN_ShortName: Story = {
  name: 'NN · Viewport · Description',  // mirror exact label dal mockup JSX
  parameters: { msw: { handlers: mswForState(state) } },
};
```

Story `name` mirror del mockup label numerico (es. `'09 · Desktop · All · Grid 4-col + Activity rail'`)
permette pixel-faithful cross-ref nel review process.

Storybook slugify pattern: title `'Pages/SP4/Library / Mockup Matrix'` + story name
`'09 · Desktop · All · Grid 4-col + Activity rail'` → URL `pages-sp4-library-mockup-matrix--frame-09-all-grid-rail`
(verifica via `grep storybook-static/index.json` post-`build-storybook`).

## design_intent classification workflow (DEC-P3-1+2)

Pre-flight per ogni mockup migration:

1. **Grep mockup JSX twin** per markers `Diverge|REFACTOR-FORWARD|design-forward`
2. **Classify**:
   - `current`: mockup riflette il design del componente OGGI → migrate normally
   - `forward-refactor`: mockup è target di refactor pending, ma allineato a roadmap → migrate, story rende current component, fidelity.json documenta divergence
   - `forward-refactor-obsolete`: mockup è target obsoleto (codebase ha evoluto in direction diversa) → **SKIP migration**, apri tracking issue, fidelity.json con `obsolete_tracking_issue` field
3. **Tracking issue template per obsoleti**:
   - Title: `[DS-17 mockup obsolete] {mockup_filename} — design_intent forward-refactor-obsolete`
   - Body: divergenza vs codebase corrente + decision needed (mockup rewrite OR component rollback)

## Viewport opt-in (DEC-P3-4)

Default solo Desktop 1440x900. Mobile opt-in via fidelity.json:

```json
"viewports": ["desktop", "mobile"]
```

**Phase 2.5 limitation**: `@storybook/addon-viewport` NOT installed. Mobile frames in stories
defer a Phase 4 hardening (would render at Desktop viewport without addon-viewport). Per Phase 2.5,
all pilot stories ship Desktop-only frames.

## Pilot reference (Phase 2.5 shipped)

| Pilot | Story file | Frames | design_intent | Baseline status |
|---|---|---|---|---|
| Library | `src/app/(authenticated)/library/_content.stories.tsx` | 9 Desktop (Frame09-17) | current | CAPTURED Phase 4 prelude (#2120) |
| GameDetail | `src/app/(authenticated)/games/[id]/_components/GameDetailView.stories.tsx` | 3 Desktop (Frame07-09) | current | CAPTURED Phase 4 prelude (#2120) |
| Dashboard | _(DELETED, forward-refactor-obsolete)_ | — | forward-refactor-obsolete (tracking #2114) | n/a |

## MSW handler URL pattern

Stories use **wildcard prefix** `'*/api/v1/...'` in `http.get(...)`. Reason: client uses relative
URLs whereas global handlers use `${API_BASE}/api/v1/...`. Wildcard matches both.

## Anti-patterns

- ❌ **Multiple story files per mockup**: DEC-P3-3 mandates 1 file con argTypes matrix.
- ❌ **Skip design_intent audit**: silently migra mockup obsolete = drift garantito.
- ❌ **Force Mobile viewport per ogni mockup**: requires `@storybook/addon-viewport` install (Phase 4).
- ❌ **Hardcode args/state nelle story `args`**: usa `argTypes` controls, defaults centralizzati in `meta.args`.
- ❌ **Story name divergence dal mockup label**: story name MUST mirror exact mockup frame label.
- ❌ **Centralized `.storybook/stories/`**: pattern repo-consolidato is side-by-side col component.
- ❌ **Reimplementare primitives**: usa `apps/web/src/components/ui/v2/` esistenti.
- ❌ **Skip JSDoc `@mockup`**: serve per coverage tracking (DS-17-1 audit-coverage.mjs).

## Snapshot gate trajectory

| Phase | Stato | Threshold | Viewports | CI behaviour | Baselines |
|---|---|---|---|---|---|
| ✅ Phase 2 v2 scaffolding | shipped non-blocking | 5% area | Desktop 1440x900 | continue-on-error | 0 (error wall) |
| ✅ **Phase 2.5 hardening** | shipped + baselines captured Phase 4 prelude | 5% area | Desktop 1440x900 | continue-on-error | 12 (Library 9 + GameDetail 3) |
| ✅ **Phase 4 prelude** (#2120) | shipped — wiring fix + baselines | 5% area | Desktop 1440x900 | continue-on-error | 12 |
| ⏳ Phase 3 sweep (5 sub-issue) | post Phase 2.5 | 5% area | Desktop primary + Mobile opt-in | continue-on-error | ~470+ |
| 🚫 **DESCOPED FROM CI (2026-07-15, #2063)** | local dev tool only | 5% area | Desktop 1440x900 | **removed from CI** | 12 win32 (local) |

## Visual gate descope (2026-07-15, #2063)

The Storybook visual pixel-gate was **removed from CI** and retained as a **local developer tool**. It never reached the planned `blocking` flip; here's why the trajectory stopped.

**Root blocker — platform mismatch.** The committed baselines are `*-win32.png` (captured on Windows dev boxes via `pnpm test:storybook:snapshots`). CI runs on `ubuntu-latest`, where Playwright looks for `*-linux.png` that were never generated. So on CI the step could only ever find missing baselines — it produced no real signal and was `continue-on-error: true` the entire time (green theatre). Promoting it to blocking was structurally impossible without Linux baselines.

**Reinforcing reasons.**
- The previous full-page visual gate was **removed on 2026-05-20** precisely for font/locale/render-drift false positives. A scoped 5%-area pixel gate carries the same intrinsic fragility.
- Structural anti-drift is already enforced by **5 blocking CI gates**: `lint:mockup-state-naming`, `lint:tokens:mockups`, `mockup-annotations:audit`, `lint:storybook-states`, `lint:bgg-mockups`. These make drift *structurally* detectable; the incremental value of a fragile pixel gate over them is low.

**What is kept.** `playwright.storybook.config.ts` + `e2e/storybook/*.snapshot.spec.ts` + the 12 win32 baselines remain in-repo as a **local, opt-in** regression tool for Windows developers (`pnpm test:storybook:snapshots`).

**To re-promote to a CI gate** (if ever): generate Linux baselines in a `mcr.microsoft.com/playwright` container (or a `workflow_dispatch` job running `--update-snapshots` on ubuntu that commits `*-linux.png`), rigenerate all 5 specs, then re-add the CI step without `continue-on-error`.

## Fix log — Phase 4 prelude IntlProvider hardening (#2120)

Phase 2.5 shipped con baseline DEFERRED per "intl object missing" error wall. Phase 4 prelude
investigation found the **real root cause was NOT dual react-intl module instances**, but a
combination of three Storybook configuration issues that compounded into the same error symptom:

### Diagnostic findings

1. **Duplicate preview files**: `.storybook/preview.ts` (legacy) AND `.storybook/preview.tsx`
   (Phase 2.5 ship) both existed. Storybook loaded `preview.ts` (alphabetical priority),
   silently ignoring the Phase 2.5 `<ReactIntlProvider>` wiring in `preview.tsx`.
2. **Missing `staticDirs` config**: `.storybook/main.ts` had `staticDirs: ['../public']` commented
   out. `pnpm build-storybook` couldn't serve `mockServiceWorker.js` from public dir → MSW
   `initialize()` threw, blocking decorator chain init.
3. **Missing `mockServiceWorker.js`**: never generated (no `pnpm exec msw init public` ever run).
4. **Missing `parameters.nextjs.navigation`**: Storybook 10 + `@storybook/nextjs` framework
   requires explicit Next.js navigation router mocks (`usePathname`, `useRouter`, `useSearchParams`).
   LibraryHub consumes all three → crash on render.

The intl error was the FIRST visible symptom but not the root cause — once the decorator
chain stopped loading entirely, every provider-dependent error chained up to "Could not find
required intl object" as the first thrown context lookup.

### Fix applied

- **Removed** `apps/web/.storybook/preview.ts` (legacy file)
- **Enabled** `staticDirs: ['../public']` in `apps/web/.storybook/main.ts`
- **Generated** `apps/web/public/mockServiceWorker.js` via `pnpm exec msw init public`
- **Added** `parameters.nextjs = { appDirectory: true, navigation: { pathname: '/', query: {} } }`
  globally in `apps/web/.storybook/preview.tsx`
- **Replaced** Storybook-local flattened `it.json` import + `<ReactIntlProvider>` wire with the
  production `<IntlProvider locale="it">` wrapper from `@/components/providers/IntlProvider`
  (single source of truth for i18n setup)

Plus snapshot spec hardening: `waitForLoadState('networkidle')` doesn't work for the loading-state
fixtures (MSW handler intentionally never resolves the promise), so both `library.snapshot.spec.ts`
and `game-detail.snapshot.spec.ts` were switched to `waitForLoadState('domcontentloaded')` +
2s render delay.

### Regression guard

`apps/web/e2e/storybook/diagnostic.snapshot.spec.ts` checks for known provider-missing error
substrings (`"Could not find required"`, `"No QueryClient set"`, `"router mocks"`, `"IntlProvider"`)
on Button + Library Frame09/13 + GameDetail Frame07. If any of the four root causes regress,
this gate fails before snapshot drift is mis-attributed to design changes.

## Local commands

- `pnpm storybook` — dev server (port 6006)
- `pnpm build-storybook` — static build
- `pnpm test:storybook:snapshots` — run snapshot tests (gate verifications post Phase 4 fix)
- `pnpm test:storybook:snapshots:update` — capture new baselines (post Phase 4 fix)
- `pnpm lint:fidelity` — validate all `*.fidelity.{json,yml}` files

## Refs

- Spec: `docs/superpowers/specs/2026-06-10-ds-17-phase-2.5-and-3-redesign.md`
- Plan: `docs/superpowers/plans/2026-06-10-ds-17-phase-2.5-and-3-redesign-plan.md`
- Fixture pattern: `apps/web/src/__tests__/fixtures/mockup-pilots/README.md`
- Sibling DS-17-1: `docs/for-developers/frontend/mockup-annotation-pattern.md`
- Sibling DS-17-4: `docs/for-developers/frontend/mockup-fidelity-acceptance.md`
- Dashboard obsolete tracking: #2114
- Phase 2.5 sub-issue: #2113
