# DetailPageLayout primitive — design

| Field | Value |
|---|---|
| Status | accepted |
| Date | 2026-05-13 |
| Author | brainstorming session (user + assistant) |
| Parent | issue #1026 (Stage 3 cluster fixes) |
| Umbrella | issue #1023 (De-versioning) |
| Spec reference | `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §4 |
| Branch | `feature/issue-1026-stage3-detail-page-layout` (from `main-dev`) |

## 0. Stakeholders & motivations

Added 2026-05-17 (spec-hardening #1126 / Cockburn finding).

| Tier | Actor | What they want from the composer | What "broken" looks like for them |
|---|---|---|---|
| Primary | **Stage 3 cluster implementer** | Flexibility to compose hero / connections / tabs / footer without inheriting business logic from the layout. Stable slot semantics across clusters. | Adding a 6th slot or a `variant` prop in a downstream cluster PR because the composer fights the cluster's shape. |
| Secondary | **A11y reviewer** | Landmark roles (`banner`, `complementary`, `navigation`, `main`, `contentinfo`) present and named consistently on every detail route, so a screen-reader user gets the same mental model across `/games/[id]`, `/players/[id]`, `/toolkits/[id]`, etc. | A new cluster ships its own `<div>`-soup detail page and bypasses the composer; landmarks divergence is then irreversible. |
| Tertiary | **Visual-regression reviewer** | Deterministic DOM order (hero → aside → nav → main → footer) so pixelmatch diffs against the mockup are stable across React re-renders. | A composer refactor reorders slots; every downstream cluster's mockup baseline becomes a false-positive diff. |

This section answers Cockburn's spec-panel question — *"Who is the primary stakeholder, and what business goal are they trying to achieve?"* — and pins the goal to the cluster implementer rather than the user. The user experiences the composer transitively, via cluster pages.

## 1. Problem

Spec §4 of the de-versioning roadmap calls for a cross-cutting `DetailPageLayout` primitive that orchestrates `Hero` + `ConnectionBar` + `Tabs` + footer slots. Wave A.4 (issue #603) already shipped the individual pieces under `apps/web/src/components/ui/detail-layout/` (hero, tabs, sticky-cta, contributors-strip, empty/error/not-found states, plus 3 list-item variants) and the canonical `ConnectionBar` primitive lives at `apps/web/src/components/ui/data-display/connection-bar/`. What does not yet exist is the composer that arranges these pieces in the canonical detail-page DOM order with the right landmark a11y semantics.

The composer unblocks new Stage 3 clusters that have no current detail implementation: `player-detail`, `toolkit-detail`, `game-nights`, `discover`, plus the NEW clusters `dashboard` and `hub/<entity>` (once their mockups land via #1097). Already-implemented routes (`game-detail`, `agent-detail`, `kb-detail`, `session-summary`, `/shared-games/[id]`) may opt in later via dedicated refactor PRs — they are not part of Stage 3 cluster work per the parent spec §3 priority table.

## 2. Goals & Non-goals

### Goals

- G1 — Add a new component `DetailPageLayout` under `apps/web/src/components/ui/detail-layout/` that arranges hero / connections / tabs / main content / footer in canonical reading order.
- G2 — Keep the composer dumb: zero business logic, zero state, zero coupling to specific data types. Everything is a `ReactNode` slot.
- G3 — Provide a11y landmarks (`<header>`, `<aside>`, `<nav>`, `<main>`, `<footer>`) so screen-reader users get free navigation between detail-page regions.
- G4 — Achieve 100% line and branch coverage with Vitest unit tests (component is small enough that the cost is negligible).
- G5 — Export the new component and its props type from `apps/web/src/components/ui/detail-layout/index.ts`.

### Non-goals

- NG1 — Do not migrate any existing consumer (e.g. `/shared-games/[id]` page-client) in this PR. Migration of consumers is the job of each Stage 3 cluster PR.
- NG2 — Do not introduce a Storybook story. Storybook reconstruction is listed as future work in the parent spec §8.
- NG3 — Do not introduce a `variant` prop on the composer. The de-versioning spec §4 proposed `variant?: 'authenticated' | 'public'` to drive default footer behavior, but the spec-panel review on 2026-05-13 flagged this as a single-responsibility violation. The composer stays pure — the caller decides what goes into the footer slot.
- NG4 — Do not accept typed `ConnectionItem[]` or `TabDescriptor[]` as props. The composer is a slot arranger; callers pass an already-rendered `<ConnectionBar>` / `<Tabs>` as a `ReactNode`. This keeps the layout decoupled from the connection/tab primitives and avoids re-exporting their types.
- NG5 — Do not extend or modify existing pieces (`Hero`, `Tabs`, `StickyCta`, `ContributorsStrip`, etc.). They are consumed unchanged.

## 3. Decisions taken during brainstorming

| Question | Decision | Rationale |
|---|---|---|
| Variant prop? | C — no variant, footer is a pure slot | YAGNI; can wrap in `AuthenticatedDetailPage` / `PublicDetailPage` later if duplication emerges |
| API style (composition vs configuration)? | B — all-composition, `connections`/`tabs` are `ReactNode` | `Tabs` primitive already owns its state; nothing for the composer to centralize. Consistency wins over 3 lines of caller boilerplate. |
| Scope of the PR? | A — composer only, no consumer migration | Atomic, low-risk PR; aligned with ADR-054 multi-branch hygiene; Wave A.4 already validated the pieces compose for `/shared-games/[id]`. |
| Test strategy? | A — Vitest unit only | Composer is pure slot-ordering; visual regression belongs to the cluster PRs that consume it. |

## 4. API

```tsx
interface DetailPageLayoutProps {
  hero: React.ReactNode;          // required slot
  connections?: React.ReactNode;  // optional — caller passes <ConnectionBar /> rendered
  tabs?: React.ReactNode;         // optional — caller passes <Tabs /> rendered
  children: React.ReactNode;      // tab panels or flat sections
  footer?: React.ReactNode;       // optional — caller decides actions row / ContributorsStrip+StickyCta / nothing
  className?: string;             // passthrough on the wrapper element
}
```

No default props. No prop-derived behavior. No internal state.

### 4.1 Usage examples

Two canonical compositions emerge from the parent spec's composability table (§4 of `2026-05-11-design-system-deversioning.md`).

**Example 1 — Full authenticated detail page** (e.g. `/games/[id]`):

```tsx
import { DetailPageLayout, Hero, Tabs } from '@/components/ui/detail-layout';
import { ConnectionBar } from '@/components/ui/data-display/connection-bar';

<DetailPageLayout
  hero={<Hero {...heroProps} labels={heroLabels} />}
  connections={<ConnectionBar items={connectionItems} />}
  tabs={<Tabs descriptors={tabDescriptors} activeTab={tab} onChange={setTab} labels={tabLabels} />}
  footer={<ActionsRow {...actionsProps} />}
>
  {currentTabPanel}
</DetailPageLayout>
```

**Example 2 — Public flat-scroll detail page** (e.g. `/shared-games/[slug]`):

```tsx
import {
  DetailPageLayout,
  Hero,
  ContributorsStrip,
  StickyCta,
} from '@/components/ui/detail-layout';
import { ConnectionBar } from '@/components/ui/data-display/connection-bar';

<DetailPageLayout
  hero={<Hero {...heroProps} />}
  connections={<ConnectionBar items={connectionItems} />}
  footer={
    <>
      <ContributorsStrip avatars={contributors} labels={contributorsLabels} />
      <StickyCta signInHref="/login" labels={ctaLabels} />
    </>
  }
>
  {sectionsStacked}
</DetailPageLayout>
```

Note on `null` vs `undefined`: passing `connections={null}` renders the `<aside>` wrapper with no children (explicit caller intent — e.g. to reserve the region for a loading placeholder). Omitting the prop (or passing `undefined`) suppresses the wrapper. Test #9 in §7 pins this contract.

## 5. DOM structure

```html
<div className={cn('detail-page-layout', className)}>
  <header>{hero}</header>

  {connections && (
    <aside aria-label="related entities">{connections}</aside>
  )}

  {tabs && (
    <nav aria-label="detail sections">{tabs}</nav>
  )}

  <main>{children}</main>

  {footer && (
    <footer>{footer}</footer>
  )}
</div>
```

Reading order equals DOM order — no CSS `order` reshuffling. The wrapper uses `flex flex-col` plus a Tailwind `gap-*` utility for spacing between slots. No hard-coded pixel values, no inline styles, no CSS variables defined inside this component.

## 6. A11y rationale

- `<header>`, `<main>`, `<footer>` give implicit landmark roles `banner`, `main`, `contentinfo`.
- `<aside aria-label="related entities">` for the connections wrapper produces `role="complementary"` with an accessible name — screen readers can jump straight to the related-entities region.
- `<nav aria-label="detail sections">` for the tabs wrapper produces `role="navigation"` at page scope. The inner `Tabs` primitive owns the WAI-ARIA tablist semantics (it already uses `role="tablist"` per its existing implementation).

## 7. Test plan

`DetailPageLayout.test.tsx` — Vitest + React Testing Library. Ten deterministic tests:

| # | Verifies |
|---|---|
| 1 | Minimal render: only `hero` + `children` produces `<header>` and `<main>` landmarks |
| 2 | Full render with all slots: DOM order is hero → aside → nav → main → footer |
| 3 | `connections` omitted → `<aside>` not in the DOM |
| 4 | `tabs` omitted → `<nav>` not in the DOM |
| 5 | `footer` omitted → `<footer>` not in the DOM |
| 6 | Landmark roles + accessible names: `banner`, `complementary` (name "related entities"), `navigation` (name "detail sections"), `main`, `contentinfo` |
| 7 | `className` passthrough on the root wrapper |
| 8 | Reading order equals DOM order — render with a `<button>` placed inside each of hero / connections / tabs / children / footer, then assert that `Array.from(container.querySelectorAll('button'))` matches the expected source order (no `tabIndex` simulation needed, the test inspects static DOM order only) |
| 9 | `connections={null}` renders the `<aside>` wrapper (explicit caller intent — `null !== undefined` per spec §4.1). Asserts `getByRole('complementary', { name: /related entities/i })` is present even though the slot is empty. |
| 10 | **Caller `className` overrides base utilities** via Tailwind cascade. Render with `className="flex-row"` and assert the root has *both* `flex-row` (caller) and `flex-col` (base). This is a **string-composition contract** assertion — jsdom does not run Tailwind, so the test pins the class *ordering in the DOM* (caller-class-last), which is the precondition Tailwind's later-wins cascade depends on at runtime. Guards against accidental regression if someone refactors `clsx('flex flex-col gap-6', className)` to put `className` first. (Added 2026-05-17 via #1126 / Crispin.) |

Coverage target: 100% line and branch (component is roughly 30 lines, all in conditional slot wrappers).

No snapshot tests. No Playwright. No Storybook.

## 8. Files touched

| File | Action |
|---|---|
| `apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx` | NEW |
| `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx` | NEW |
| `apps/web/src/components/ui/detail-layout/index.ts` | UPDATED — append two new export blocks (named export `DetailPageLayout` + type `DetailPageLayoutProps`); replace the leading JSDoc comment "Wave A.4 (Issue #603) — V2 component family for /shared-games/[id]." with "Detail-layout primitives. Wave A.4 (#603) shipped the pieces; Stage 3 (#1026) added the DetailPageLayout composer." Existing exports are not reordered or modified. |

No other files. No migrations. No backend impact.

## 9. Acceptance criteria

- AC1 — `DetailPageLayout` exported from `apps/web/src/components/ui/detail-layout/index.ts` along with its props type.
- AC2 — `pnpm typecheck && pnpm lint && pnpm test` green in `apps/web`.
- AC3 — Vitest coverage report shows 100% line and branch coverage for `DetailPageLayout.tsx`.
- AC4 — All eight tests in §7 present and passing.
- AC5 — No regression in the existing `detail-layout/` tests (hero, tabs, sticky-cta, contributors-strip, empty-state, error-state, not-found-state, agent-list-item, kb-doc-item, toolkit-list-item).
- AC6 — No new files outside the three listed in §8.
- AC7 — No use of hard-coded colors, hex values, or pixel spacing in `DetailPageLayout.tsx`. Lint rule `local/no-hardcoded-color-utility` passes.

## 10. Rollback

Delete `DetailPageLayout.tsx` and `DetailPageLayout.test.tsx`; revert the two added export lines and the header-comment change in `index.ts`. Zero consumers after this PR — revert is risk-free.

### 10.1 Post-merge monitoring policy

Added 2026-05-17 (spec-hardening #1126 / Nygard finding).

§10 covers deletion mechanics. This subsection covers **regression detection** — how a silent change in the composer is caught downstream after consumers have adopted it.

**Policy**:

- Every Stage 3 cluster PR that adopts `DetailPageLayout` **must** include Playwright visual-regression baselines against its cluster's canonical mockup (`admin-mockups/design_files/sp4-<cluster>.jsx`). These baselines are intended to be enforced by `.github/workflows/visual-regression-conformity.yml` (wired into `main-dev` since 2026-05-12).
- The `Conformity Gate (Desktop + Mobile)` check is governed by the per-project `maxDiffPixelRatio: 0.001` + `threshold: 0.2` thresholds in `apps/web/playwright.config.ts` (i.e. ≤0.1% of pixels may exceed the per-pixel threshold). The legacy "2%" framing from internal docs does **not** match the actual config — always cite `playwright.config.ts` as the source of truth.
- **Phase 3 caveat** (per the workflow's own preamble at `.github/workflows/visual-regression-conformity.yml:14-17`): every spec is currently `test.fixme()`'d, so the gate runs but skips. It is opt-in wiring until Phase 3b lands per-route data stubs. Treat this policy as **forward-looking** until then.
- **Path-filter caveat**: the workflow today triggers only on `library/**` route changes plus shared design tokens / Tailwind config / canonical mockup HTML. A PR that touches *only* `apps/web/src/components/ui/detail-layout/**` does **not** trigger the conformity workflow at all. Until `paths:` is extended to include `apps/web/src/components/ui/detail-layout/**` (follow-up out of this PR), composer-only edits rely on cluster-route consumers triggering the gate transitively.
- **Composer regression checklist**: if a downstream cluster shows an unexpected visual diff above the configured threshold on a PR that does *not* touch the cluster's own code, `DetailPageLayout.tsx` is on the investigation checklist (specifically: changes to slot order, landmark roles, or root `flex-col` base utilities). `git log --follow apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx` is the first triage step.

**Why this matters**: post-PR #1112, the composer is consumed by `/players/[id]` (issue #1113 via PR #1138) and `/toolkits/[id]` (issue #1145 via PR #1163). `/discover` (issue #1147 via PR #1160) is a Stage 3 cluster that intentionally uses `HubLayout` instead of `DetailPageLayout` — it is a sibling, not a consumer of this composer. A subtle change in the composer propagates to the 2 actual consumers without showing up in `DetailPageLayout.test.tsx` (which is structural-only). Visual conformity will be the only behavioral guard once Phase 3b lands.

**Out of scope** for this policy: tracking which consumers exist (the cluster list lives in spec `2026-05-11-design-system-deversioning.md` §3) and runtime telemetry (composer renders nothing observable — no metric to emit).

## 11. Follow-ups (out of scope)

- First real consumer: a Stage 3 cluster PR (suggested order: `player-detail` since it is pending from Wave 3 and Wave 3 mockup `sp4-player-detail.jsx` already exists in `admin-mockups/design_files/`).
- If duplication emerges across cluster consumers in the footer slot (e.g. all public clusters end up passing the same `ContributorsStrip + StickyCta` combo), introduce thin wrappers `AuthenticatedDetailPage` / `PublicDetailPage` in a later PR. Not before duplication is observed.
- Storybook story authoring is deferred (parent spec §8 future work).

## 13. Non-functional requirements

Added 2026-05-17 (spec-hardening #1126 / Wiegers finding).

The composer is on the hot path of every detail page — its blast radius equals the number of cluster consumers (2 today: `/players/[id]` and `/toolkits/[id]`; up to ~6 by end of Stage 3 as the remaining detail clusters land). The NFRs below are *contracts on the composer itself* — they bound the cost any new consumer pays when adopting it. Numbering is independent of the AC list in §9 to keep verifiability decoupled.

| ID | Requirement | Rationale |
|---|---|---|
| NFR1 | Bundle size of the composer (and its transitive imports) ≤ **1 KB gzipped** when imported in a sample consumer page. | Composer ships in every detail route bundle. Anything beyond 1 KB gz suggests the slot-arranger has accidentally absorbed business logic (a CSS-in-JS dependency, a heavy `clsx` alternative, etc.). |
| NFR2 | **Zero** `useState` and `useEffect` (or any other React hook) calls in `DetailPageLayout.tsx`. | Pins the §3 brainstorming decision "all-composition, no state". A hook here would tie the composer to a render lifecycle and break SSR purity. |
| NFR3 | **Zero** context consumption inside the composer. | A composer that reads context becomes implicitly coupled to a provider tree. Callers should pass the data through props/children instead. |
| NFR4 | **Zero** side effects on render: no DOM mutation outside JSX, no logging, no analytics, no subscriptions. | Predictable SSR + safe to call inside `Suspense` boundaries. Side effects belong in the cluster pages, not the layout. |

**Verification commands** (run from repo root):

```bash
# NFR1 — bundle budget check (composer chunk shows up under "Stage 3 design system" bucket)
pnpm --filter @meepleai/web build && node apps/web/scripts/check-bundle-budgets.mjs

# NFR2 — must return ZERO matches
grep -nE 'use(State|Effect|Reducer|Ref|LayoutEffect|Memo|Callback)\(' \
  apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx

# NFR3 — must return ZERO matches
grep -nE 'useContext\(' \
  apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx

# NFR4 — manual code review on every PR touching DetailPageLayout.tsx
#   (reinforced by NFR2: no useEffect = no observable side-effect host).
```

**Enforcement**: NFR2 and NFR3 are mechanically checkable; a future CI step (`pnpm lint:nfr-composer` or similar) may codify them. NFR1 requires the `check-bundle-budgets.mjs` script (already wired but not currently gated on the composer specifically); it is not gated in CI today because the composer has no `import` graph beyond `react` + `clsx`, both of which are already shared with every page in the app.

**Audit (2026-05-17)**: at commit time of this hardening PR, `DetailPageLayout.tsx` is 48 LOC, imports only `react` (type only) and `clsx`, declares no hooks, and renders JSX-only. All four NFRs are satisfied. (Note: this LOC count is precise and load-bearing — if the file grows by any amount, refresh this audit line in the same PR.)

## 14. Evolution policy

Added 2026-05-17 (spec-hardening #1126 / Fowler finding) — **stub**; full content deferred until ≥2 consumers exist.

**Current consumer count**: 2 — `player-detail` (issue #1113 via PR #1138, merged 2026-05-13) and `toolkit-detail` FE (issue #1145 via PR #1163, merged 2026-05-14). `/discover` (issue #1147 via PR #1160) is a Stage 3 sibling but composes `HubLayout`, not `DetailPageLayout` — it is **not** counted here. The "wait for ≥2 consumers" precondition Fowler set is therefore satisfied; the full policy can be expanded in a follow-up PR.

**Policy stub (to be expanded)**:

- **Additive changes** (new optional slot, new optional prop) → no consumer break expected. Stamp as minor change in commit message and CHANGELOG (when one exists). Do not require codemod.
- **Breaking changes** (rename a slot, change a slot's semantic, alter DOM order, remove a landmark wrapper) → require:
  1. A codemod under `tools/codemods/detail-page-layout-<change>.ts` exercising every existing consumer.
  2. A migration note in the cluster spec referencing the new contract.
  3. A coordinated PR that lands composer change + consumer updates together (no two-step in `main-dev`).
- **A11y contract changes** (renaming an `aria-label`, removing a landmark) are always breaking even if technically additive — screen-reader users have memorized the existing names.

**Out of scope of this stub**: deprecation policy (none today), versioning scheme (internal monorepo package; no semver), publishing (not published). These are tracked under #1023 umbrella as cross-cutting de-versioning concerns.
