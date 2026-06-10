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
| Library | `src/app/(authenticated)/library/_content.stories.tsx` | 9 Desktop (Frame09-17) | current | DEFERRED (Phase 4 — IntlProvider blocker) |
| GameDetail | `src/app/(authenticated)/games/[id]/_components/GameDetailView.stories.tsx` | 3 Desktop (Frame07-09) | current | DEFERRED (Phase 4 — IntlProvider blocker) |
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
| ✅ **Phase 2.5 hardening** | shipped (baselines DEFERRED) | 5% area | Desktop 1440x900 | continue-on-error | 0 (IntlProvider blocker, see below) |
| ⏳ Phase 3 sweep (5 sub-issue) | post Phase 2.5 | 5% area | Desktop primary + Mobile opt-in | continue-on-error | ~470+ |
| **Phase 4 hardening** | post Phase 3 | 5% area | Desktop + Mobile per opt-in | **blocking** after 14gg stable | Full matrix |

## ⚠️ Known limitation Phase 2.5 — IntlProvider runtime context

Pilot stories rendono "intl object missing" error wall durante snapshot test execution
nonostante `.storybook/preview.tsx` AllProviders decorator wrappa `<ReactIntlProvider>` con
flattened `it.json` messages. Verifica:

1. `pnpm build-storybook` succeeds (no Webpack error)
2. Bundle includes react-intl module + useIntl
3. preview.tsx imports `IntlProvider as ReactIntlProvider` from 'react-intl' (verified)
4. AllProviders decorator wraps Story (decorator order: withThemeByClassName → AllProviders → Story)
5. Story renders → DashboardClient/LibraryContent/GameDetailView calls `useTranslation` →
   `useIntl()` → returns undefined context → throws

**Suspected root cause**: dual react-intl module instances (preview bundle vs iframe.bundle.js)
→ Context API only works when both consumer + provider share the same module instance. Webpack
chunk splitting may create 2 distinct module instances, each with its own React Context, so
`useIntl()` reads from a context that was never populated by `<IntlProvider>`.

**Phase 4 follow-up plan**:
1. Investigate Webpack config in `.storybook/main.ts` + `@storybook/nextjs` framework adapter
2. Try `resolve.alias` to force single react-intl module
3. OR: switch to `IntlProvider` wrapper that uses `useState` for messages (avoid context inheritance)
4. OR: implement custom `useTranslation` mock alias for Storybook env (similar to vitest config)

Baseline PNG capture deferred until IntlProvider runtime fix lands.

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
