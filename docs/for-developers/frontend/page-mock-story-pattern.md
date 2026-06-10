# Page-mock story pattern (DS-17-6-v2)

This pattern documents how to migrate a page-mock HTML (`admin-mockups/design_files/sp4-*.html`)
into a Storybook story that USES the real Client component. Adopted Phase 2 pilot; Phase 3
sweep replicates it across the remaining 67 page-mock + 48 component-mock.

**Sub-issue**: [#2092](https://github.com/meepleAi-app/meepleai-monorepo/issues/2092) (DS-17-6-v2) · **Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) · **Spec**: [`docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md`](../../superpowers/specs/2026-06-09-ds-17-phase-2-design.md)

## Quick start

1. Identify the Client component for the route (`page.tsx` typically delegates to a `Client.tsx` or `_content.tsx` sibling).
2. Add a fixture file under `apps/web/src/__tests__/fixtures/mockup-pilots/<name>.ts` exporting `MOCK_<NAME>` + `MOCK_<NAME>_EMPTY`.
3. Create `<ClientComponent>.stories.tsx` side-by-side col Client component.
4. Add stories `Default`, `Empty` (min); optional `Loading`, `Error` per visible state matrix.
5. JSDoc top with `@mockup admin-mockups/design_files/sp4-X.html` (mirrors DS-17-1 page.tsx annotations).
6. Verify `pnpm build-storybook` succeeds, then commit.

## Pilot examples (Phase 2)

| Pilot | Story file | Fixture file | States shipped |
|---|---|---|---|
| sp4-dashboard | `apps/web/src/app/(authenticated)/dashboard/DashboardClient.stories.tsx` | `__tests__/fixtures/mockup-pilots/dashboard.ts` | Default + Empty + Loading + Error |
| sp4-library-desktop | `apps/web/src/app/(authenticated)/library/_content.stories.tsx` | `__tests__/fixtures/mockup-pilots/library.ts` | Default + Empty + Loading |
| sp4-game-detail | `apps/web/src/app/(authenticated)/games/[id]/_components/GameDetailView.stories.tsx` | `__tests__/fixtures/mockup-pilots/game-detail.ts` | Default + Empty + Error |

Different state coverage per pilot is intentional — pilots ship the states that
exercise component-specific behaviour. Phase 3 sweep can copy either pattern.

## Decorators inherited from `.storybook/preview.tsx`

These are global — no per-story setup required:

- **MockAuthProvider** — fake user `storybook-user` with `role='user'`. Override per-story
  via `parameters.auth = { user: { ...customUser } }` (TBD; not yet implemented).
- **QueryClientProvider** — React Query with `retry: false, staleTime: Infinity`. Each story
  gets a fresh client.
- **ThemeProvider + withThemeByClassName** — light/dark toggle via Storybook toolbar.
  Default `light` (cream `#f7f3ee`).
- **TooltipProvider** — Radix tooltips work out-of-box.
- **msw-storybook-addon** — global handlers from `src/__tests__/mocks/handlers/`; per-story
  `parameters.msw.handlers` overrides.

### Known limitation: Zustand stores without per-story reset

Some Client components (e.g. `LibraryContent`) write to Zustand stores on mount. The
preview.tsx does NOT provide a global Zustand reset decorator; stores accumulate state
across Storybook navigations. **Benign for screenshot capture and the typical "open the
story" review flow.** If state pollution causes visible issues, add a per-story decorator:

```ts
import { useRecentsStore } from '@/stores/use-recents';

export const Empty: Story = {
  decorators: [
    (Story) => {
      useRecentsStore.setState({ recents: [] });
      return <Story />;
    },
  ],
  // ...
};
```

## State conventions

Every page-mock story should at minimum cover:

| State | How |
|---|---|
| **Default** | MSW returns populated `MOCK_<NAME>` fixture |
| **Empty** | MSW returns `MOCK_<NAME>_EMPTY` (zero-length arrays / null) |
| **Loading** | MSW handler returns `new Promise<Response>(() => {})` (never resolves) |
| **Error** | MSW returns 500 with `{ error: 'server error' }` |

**Minimum**: `Default + Empty`. Optional: Loading, Error, SSE, Offline, A11y-focused —
chosen per-pilot based on the visible state matrix of the page.

## Fixture conventions

- Path: `apps/web/src/__tests__/fixtures/mockup-pilots/<name>.ts`
- Export shape: `MOCK_<NAME>: <Type>` + `MOCK_<NAME>_EMPTY: <Type>`
- Re-export from `__tests__/fixtures/mockup-pilots/index.ts` barrel
- TypeScript types derived from `apps/web/src/lib/api/schemas/*.schemas.ts` (Zod
  schema `z.infer<>`) — never invent type shape
- Per-fixture imports (no composite bundles) — keeps MSW handler granularity flexible
- **Anti-pattern**: hardcoded `Date.now()` or current-date values (use ISO literal
  `'2026-06-09T...'` and document in fixture comment)
- **Anti-pattern**: dataset bloat (>1MB per fixture). Cap at ~10 entries per array.

## MSW handler URL pattern

Stories use **wildcard prefix** `'*/api/v1/...'` in `http.get(...)`. Reason: the production
client uses relative URLs `/api/v1/...` whereas the global handlers in
`src/__tests__/mocks/handlers/` use `${API_BASE}/api/v1/...`. The wildcard matches both,
keeping stories independent of how `NEXT_PUBLIC_API_BASE` is configured in Storybook env.

## Anti-patterns

- ❌ **Centralized `.storybook/stories/`**: pattern repo-consolidato is **side-by-side**
  col component. 133 stories follow this convention.
- ❌ **Hardcode dei mock dentro la story**: usa `parameters.msw.handlers` override delle
  fixture importate, niente object literal in-line.
- ❌ **Reimplementare primitives**: usa `apps/web/src/components/ui/v2/` esistenti
  (umbrella DEC-6).
- ❌ **Skip JSDoc `@mockup`**: serve per coverage tracking (DS-17-1 audit-coverage.mjs).
- ❌ **Inventare type shape**: derive sempre da `z.infer<typeof XSchema>` o dichiara
  `type` inline e documenta che è ad-hoc.

## Snapshot gate trajectory

| Phase | Threshold | CI behaviour | Status |
|---|---|---|---|
| ✅ **Phase 2 v2 scaffolding (now)** | 5% area diff (light theme, 1440x900) | **non-blocking**, artifact uploaded, **baseline absent** | Config + specs + CI shipped DS-17-8-v2 (#2095) |
| **Phase 4 — baseline capture + decorator fix** | 5% area diff | non-blocking → **blocking** after 14gg stable | TBD follow-up |
| **DS-17 close** | 5% area diff + multi-viewport (375/768/1440) | blocking on 3 viewports | TBD |

### Known limitation: IntlProvider not yet wired

Pilot Client components (DashboardClient, LibraryContent, GameDetailView) call
`useTranslation()` from `@/hooks/useTranslation` which underlies `react-intl`'s
`useIntl`. The current global `.storybook/preview.tsx` decorator stack does NOT
wrap children in an `IntlProvider`, so when the pilot stories render at snapshot
time the components throw `[React Intl] Could not find required intl object`.

The fix path involves:
1. Importing `it.json` messages + flatten them with the existing `flattenMessages`
   helper from `@/locales`.
2. Adding `IntlProvider` (or `ReactIntlProvider` directly) at the top of the
   `AllProviders` chain in `.storybook/preview.tsx`.
3. Investigating why a naive `import { IntlProvider } from '@/components/providers/IntlProvider'`
   addition does not propagate context to the rendered Story tree (decorator
   tree order, Storybook adapter `'use client'` boundary, or webpack runtime
   chunk init order).

Phase 2 v2 ships the scaffolding without committed baseline PNGs. Phase 4
hardening fixes the decorator integration and captures the real baselines.

Local commands (work today, but currently render the error wall):
- `pnpm test:storybook:snapshots` — run snapshot tests
- `pnpm test:storybook:snapshots:update` — capture new baseline (post-fix)

When a snapshot fails on a PR (post Phase 4):
1. Open Storybook locally: `pnpm storybook` → navigate to failing story
2. Compare side-by-side with mockup HTML reference
3. If UI is correct → update baseline: `pnpm test:storybook:snapshots:update <story>`
4. If UI is wrong → fix the component / fixture

## Verification

```bash
cd apps/web && pnpm build-storybook && pnpm storybook
# open http://localhost:6006/?path=/story/pages-sp4-dashboard-mockup-pilot--default
```

Side-by-side col mockup HTML reference per pixel-faithfulness check (Phase 4 visual gate
formalizes this via Playwright snapshot — Sub-issue 2 DS-17-8-v2).

## Refs

- Spec: `docs/superpowers/specs/2026-06-09-ds-17-phase-2-design.md`
- Plan: `docs/superpowers/plans/2026-06-09-ds-17-phase-2-implementation-plan.md`
- Fixture pattern: `apps/web/src/__tests__/fixtures/mockup-pilots/README.md`
- Sibling DS-17-1 annotation: `docs/for-developers/frontend/mockup-annotation-pattern.md`
- Sibling DS-17-4 acceptance: `docs/for-developers/frontend/mockup-fidelity-acceptance.md`
