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

`DetailPageLayout.test.tsx` — Vitest + React Testing Library. Eight deterministic tests:

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

## 11. Follow-ups (out of scope)

- First real consumer: a Stage 3 cluster PR (suggested order: `player-detail` since it is pending from Wave 3 and Wave 3 mockup `sp4-player-detail.jsx` already exists in `admin-mockups/design_files/`).
- If duplication emerges across cluster consumers in the footer slot (e.g. all public clusters end up passing the same `ContributorsStrip + StickyCta` combo), introduce thin wrappers `AuthenticatedDetailPage` / `PublicDetailPage` in a later PR. Not before duplication is observed.
- Storybook story authoring is deferred (parent spec §8 future work).
