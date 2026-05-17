# DetailPageLayout Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship a tiny composer primitive `DetailPageLayout` that arranges hero / connections / tabs / main / footer ReactNode slots with WAI-ARIA landmarks, unblocking Stage 3 cluster work (#1026).

**Architecture:** Pure slot-arranger React component. Zero state, zero business logic, zero coupling to other primitives' types. Wrapped in landmark HTML elements (`<header>`, `<aside>`, `<nav>`, `<main>`, `<footer>`) with accessible names on the non-trivial ones. Tailwind utility classes only — no hard-coded colors or spacing.

**Tech Stack:** React 19, TypeScript, Tailwind 4, `clsx` for class composition, Vitest + React Testing Library for unit tests.

**Spec:** `docs/for-developers/specs/2026-05-13-detail-page-layout-design.md`

**Branch (already created):** `feature/issue-1026-stage3-detail-page-layout` (parent: `main-dev`)

---

## File Structure

| File | Responsibility |
|---|---|
| `apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx` | The composer component + exported props interface |
| `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx` | Eight Vitest unit tests covering minimal render, full render, conditional slots, landmark names, className passthrough, reading order |
| `apps/web/src/components/ui/detail-layout/index.ts` | Barrel: append two named exports for the new component and its props type; replace the leading JSDoc with text that mentions the Stage 3 addition |

No other files. No test fixtures. No new dependencies (`clsx` is already a workspace dependency, RTL + Vitest are already set up in `apps/web`).

---

## Preamble — Commit the plan itself

Before starting Task 1, commit this plan document so that the diff guard in Step 6.2 sees both the spec and the plan as expected files (they were created during brainstorming + writing-plans phases, not during implementation).

```bash
git add docs/superpowers/plans/2026-05-13-detail-page-layout.md
git commit -m "docs(plans): DetailPageLayout implementation plan (refs #1026)"
```

---

## Task 1 — Failing tests scaffold + minimal passing render

**Files:**
- Create: `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx`
- Create: `apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx`

This task seeds the test file with the first two tests (minimal render + full render ordering), watches them fail, then writes the minimal component that makes both pass. The component grows in later tasks as more tests get added.

- [ ] **Step 1.1: Create the failing test file**

Path: `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx`

```tsx
/**
 * DetailPageLayout — Stage 3 composer primitive (Issue #1026).
 *
 * Tests verify slot ordering, conditional rendering, landmark a11y, and
 * className passthrough. The composer is a pure slot-arranger: tests do not
 * exercise any business logic because the component contains none.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { DetailPageLayout } from './DetailPageLayout';

describe('DetailPageLayout', () => {
  it('renders header and main with only hero + children (other slots omitted)', () => {
    render(
      <DetailPageLayout hero={<span>hero-content</span>}>
        <p>main-content</p>
      </DetailPageLayout>,
    );

    expect(screen.getByRole('banner')).toHaveTextContent('hero-content');
    expect(screen.getByRole('main')).toHaveTextContent('main-content');
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
    expect(screen.queryByRole('contentinfo')).not.toBeInTheDocument();
  });

  it('renders all five landmarks in DOM order: hero → aside → nav → main → footer', () => {
    const { container } = render(
      <DetailPageLayout
        hero={<span data-testid="slot-hero">H</span>}
        connections={<span data-testid="slot-connections">C</span>}
        tabs={<span data-testid="slot-tabs">T</span>}
        footer={<span data-testid="slot-footer">F</span>}
      >
        <span data-testid="slot-main">M</span>
      </DetailPageLayout>,
    );

    const order = Array.from(
      container.querySelectorAll<HTMLElement>('[data-testid^="slot-"]'),
    ).map((node) => node.dataset.testid);

    expect(order).toEqual([
      'slot-hero',
      'slot-connections',
      'slot-tabs',
      'slot-main',
      'slot-footer',
    ]);
  });
});
```

- [ ] **Step 1.2: Run the test file and confirm it fails**

Run from `apps/web/`:

```bash
pnpm test src/components/ui/detail-layout/DetailPageLayout.test.tsx --run
```

Expected: tests fail with a module-resolution error (`Cannot find module './DetailPageLayout'`). This is the failing state for both tests.

- [ ] **Step 1.3: Create the minimal component**

Path: `apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx`

```tsx
/**
 * DetailPageLayout — composer primitive for entity detail pages.
 *
 * Stage 3 cross-cutting (Issue #1026, umbrella #1023). Arranges hero,
 * connections, tabs, main content and footer ReactNode slots with WAI-ARIA
 * landmarks. The composer has no state and no business logic; callers pass
 * already-rendered children for each slot.
 */

import type { JSX, ReactNode } from 'react';

import clsx from 'clsx';

export interface DetailPageLayoutProps {
  /** Required slot rendered inside the page `<header>` (banner landmark). */
  readonly hero: ReactNode;
  /** Optional slot rendered inside an `<aside>` with accessible name "related entities". */
  readonly connections?: ReactNode;
  /** Optional slot rendered inside a `<nav>` with accessible name "detail sections". */
  readonly tabs?: ReactNode;
  /** Main content — tab panels or flat sections — rendered inside `<main>`. */
  readonly children: ReactNode;
  /** Optional slot rendered inside the page `<footer>` (contentinfo landmark). */
  readonly footer?: ReactNode;
  /** Optional passthrough class applied to the root wrapper. */
  readonly className?: string;
}

export function DetailPageLayout({
  hero,
  connections,
  tabs,
  children,
  footer,
  className,
}: DetailPageLayoutProps): JSX.Element {
  return (
    <div data-slot="detail-page-layout" className={clsx('flex flex-col gap-6', className)}>
      <header>{hero}</header>
      {connections !== undefined && (
        <aside aria-label="related entities">{connections}</aside>
      )}
      {tabs !== undefined && <nav aria-label="detail sections">{tabs}</nav>}
      <main>{children}</main>
      {footer !== undefined && <footer>{footer}</footer>}
    </div>
  );
}
```

- [ ] **Step 1.4: Run the tests again and confirm both pass**

```bash
pnpm test src/components/ui/detail-layout/DetailPageLayout.test.tsx --run
```

Expected: 2 passing, 0 failing.

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx \
        apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx
git commit -m "feat(detail-layout): DetailPageLayout composer minimal slot order (refs #1026)"
```

---

## Task 2 — Conditional slot rendering (3 tests)

**Files:**
- Modify: `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx` (append 3 tests)

The component already supports conditional slots from Task 1, so these tests should pass immediately when added. We add them anyway because they document the contract per the spec §7.

- [ ] **Step 2.1: Append three tests to the existing `describe('DetailPageLayout', () => {})` block**

Insert before the closing `});` of the describe block:

```tsx
  it('omits the <aside> when connections is undefined', () => {
    render(
      <DetailPageLayout hero={<span>H</span>}>
        <p>M</p>
      </DetailPageLayout>,
    );
    expect(screen.queryByRole('complementary')).not.toBeInTheDocument();
  });

  it('omits the <nav> when tabs is undefined', () => {
    render(
      <DetailPageLayout hero={<span>H</span>}>
        <p>M</p>
      </DetailPageLayout>,
    );
    expect(screen.queryByRole('navigation')).not.toBeInTheDocument();
  });

  it('omits the <footer> when footer is undefined', () => {
    render(
      <DetailPageLayout hero={<span>H</span>}>
        <p>M</p>
      </DetailPageLayout>,
    );
    expect(screen.queryByRole('contentinfo')).not.toBeInTheDocument();
  });
```

- [ ] **Step 2.2: Run the test file and confirm all 5 pass**

```bash
pnpm test src/components/ui/detail-layout/DetailPageLayout.test.tsx --run
```

Expected: 5 passing, 0 failing.

- [ ] **Step 2.3: Commit**

```bash
git add apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx
git commit -m "test(detail-layout): document conditional slot omission contract (refs #1026)"
```

---

## Task 3 — Landmark accessible names (1 test)

**Files:**
- Modify: `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx` (append 1 test)

Spec §6 mandates accessible names on the `<aside>` ("related entities") and `<nav>` ("detail sections"). The implementation from Task 1 already provides them; this test pins the contract.

- [ ] **Step 3.1: Append the landmark-name test**

Insert before the closing `});`:

```tsx
  it('exposes accessible names on the non-trivial landmarks', () => {
    render(
      <DetailPageLayout
        hero={<span>H</span>}
        connections={<span>C</span>}
        tabs={<span>T</span>}
        footer={<span>F</span>}
      >
        <span>M</span>
      </DetailPageLayout>,
    );

    expect(screen.getByRole('banner')).toBeInTheDocument();
    expect(screen.getByRole('complementary', { name: /related entities/i })).toBeInTheDocument();
    expect(screen.getByRole('navigation', { name: /detail sections/i })).toBeInTheDocument();
    expect(screen.getByRole('main')).toBeInTheDocument();
    expect(screen.getByRole('contentinfo')).toBeInTheDocument();
  });
```

- [ ] **Step 3.2: Run the test file and confirm all 6 pass**

```bash
pnpm test src/components/ui/detail-layout/DetailPageLayout.test.tsx --run
```

Expected: 6 passing, 0 failing.

- [ ] **Step 3.3: Commit**

```bash
git add apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx
git commit -m "test(detail-layout): pin aria-label contract on aside + nav (refs #1026)"
```

---

## Task 4 — className passthrough + reading order (2 tests)

**Files:**
- Modify: `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx` (append 2 tests)

- [ ] **Step 4.1: Append the className passthrough test**

Insert before the closing `});`:

```tsx
  it('passes the className prop through to the root wrapper', () => {
    const { container } = render(
      <DetailPageLayout hero={<span>H</span>} className="custom-layout-class">
        <p>M</p>
      </DetailPageLayout>,
    );

    const root = container.firstElementChild;
    expect(root).not.toBeNull();
    expect(root).toHaveClass('custom-layout-class');
    // base utilities from the component itself are also present
    expect(root).toHaveClass('flex');
    expect(root).toHaveClass('flex-col');
  });
```

- [ ] **Step 4.2: Append the reading-order test using focusable elements**

```tsx
  it('places a focusable element from each slot in source order', () => {
    const { container } = render(
      <DetailPageLayout
        hero={<button type="button">hero-btn</button>}
        connections={<button type="button">connections-btn</button>}
        tabs={<button type="button">tabs-btn</button>}
        footer={<button type="button">footer-btn</button>}
      >
        <button type="button">main-btn</button>
      </DetailPageLayout>,
    );

    const labels = Array.from(container.querySelectorAll('button')).map(
      (b) => b.textContent,
    );
    expect(labels).toEqual([
      'hero-btn',
      'connections-btn',
      'tabs-btn',
      'main-btn',
      'footer-btn',
    ]);
  });
```

- [ ] **Step 4.3: Run the test file and confirm all 8 pass**

```bash
pnpm test src/components/ui/detail-layout/DetailPageLayout.test.tsx --run
```

Expected: 8 passing, 0 failing.

- [ ] **Step 4.4: Run coverage on this file and confirm 100% line + branch**

```bash
pnpm test src/components/ui/detail-layout/DetailPageLayout.test.tsx --coverage --run
```

Expected: in the coverage table, `DetailPageLayout.tsx` shows 100% lines, 100% branches, 100% functions.

If the project uses a coverage-config that does not include this file path by default, check `apps/web/vitest.config.ts` and verify the include glob picks up `src/components/ui/detail-layout/**/*.tsx`. Adjust the run flag to target the coverage report scope if needed (e.g. `--coverage.include='src/components/ui/detail-layout/DetailPageLayout.tsx'`).

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx
git commit -m "test(detail-layout): className passthrough + reading order (refs #1026)"
```

---

## Task 5 — Wire into the barrel export

**Files:**
- Modify: `apps/web/src/components/ui/detail-layout/index.ts`

- [ ] **Step 5.1: Read the existing `index.ts` to confirm its current layout**

```bash
cat apps/web/src/components/ui/detail-layout/index.ts
```

Confirm the first lines contain the JSDoc starting with `/**` then `* Wave A.4 (Issue #603) — V2 component family for /shared-games/[id].` and exports start with `export { AgentListItem } ...`.

- [ ] **Step 5.2: Replace the leading JSDoc and append the two new exports**

Replace the leading JSDoc block (the comment that currently reads `Wave A.4 (Issue #603) — V2 component family for /shared-games/[id].`) with:

```ts
/**
 * Detail-layout primitives.
 *
 * Wave A.4 (Issue #603) shipped the pieces (Hero, Tabs, StickyCta,
 * ContributorsStrip, AgentListItem, KbDocItem, ToolkitListItem, and the
 * EmptyState / ErrorState / NotFoundState family).
 *
 * Stage 3 (Issue #1026) adds the DetailPageLayout composer that arranges
 * these pieces with WAI-ARIA landmarks. The composer is consumer-agnostic:
 * each slot accepts a ReactNode, so callers can use any of the primitives
 * above (or none) without the composer knowing.
 */
```

Then append at the end of the file (after the existing `ToolkitListItem` exports):

```ts

export { DetailPageLayout } from './DetailPageLayout';
export type { DetailPageLayoutProps } from './DetailPageLayout';
```

Do not reorder or modify any other export.

- [ ] **Step 5.3: Sanity-import the new symbol from outside the barrel to prove the export resolves**

Run from `apps/web/`:

```bash
pnpm typecheck
```

Expected: no errors. If TypeScript complains about the new file path, double-check Step 1.3 saved at `apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx` (note casing).

- [ ] **Step 5.4: Run the full app test suite to make sure no Wave A.4 test broke**

```bash
pnpm test --run
```

Expected: full suite green. If any test in `apps/web/src/components/ui/detail-layout/` regresses, revert this task and investigate — Task 5 should be a pure additive change.

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/components/ui/detail-layout/index.ts
git commit -m "feat(detail-layout): export DetailPageLayout from barrel (refs #1026)"
```

---

## Task 6 — Final verification + PR

**Files:** none modified.

- [ ] **Step 6.1: Run the full quality gate**

From `apps/web/`:

```bash
pnpm typecheck && pnpm lint && pnpm test --run
```

Expected: all three commands exit 0. If lint flags `local/no-hardcoded-color-utility` on `DetailPageLayout.tsx`, that means a hex/rgb value slipped in — open the file and remove it; the only utility classes used should be `flex`, `flex-col`, `gap-6`, plus the caller-provided `className`.

- [ ] **Step 6.2: Confirm no extra files were touched beyond the spec §8 list**

```bash
git diff --name-only main-dev
```

Expected output (exact, no extras):

```
apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx
apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx
apps/web/src/components/ui/detail-layout/index.ts
docs/superpowers/plans/2026-05-13-detail-page-layout.md
docs/for-developers/specs/2026-05-13-detail-page-layout-design.md
```

(The two `docs/superpowers/` files were committed in earlier brainstorming + planning steps; everything else must be the three implementation files.)

- [ ] **Step 6.3: Push the branch**

```bash
git push -u origin feature/issue-1026-stage3-detail-page-layout
```

- [ ] **Step 6.4: Open the PR against `main-dev`**

```bash
gh pr create --base main-dev \
  --title "feat(detail-layout): DetailPageLayout composer primitive (Stage 3 — cross-cutting)" \
  --body "$(cat <<'EOF'
## Summary

Ships the cross-cutting `DetailPageLayout` composer primitive per spec §4 of the de-versioning roadmap. Unblocks Stage 3 cluster work (#1026): `player-detail`, `toolkit-detail`, `game-nights`, `discover`, and the NEW clusters once their mockups land via #1097.

- New: `apps/web/src/components/ui/detail-layout/DetailPageLayout.tsx` — pure slot-arranger, 5 `ReactNode` slots (hero / connections / tabs / children / footer), zero state, zero coupling
- New: `apps/web/src/components/ui/detail-layout/DetailPageLayout.test.tsx` — 8 Vitest unit tests covering DOM order, conditional slots, landmark accessible names, className passthrough, reading order
- Updated: `apps/web/src/components/ui/detail-layout/index.ts` — barrel export of the composer + props type; JSDoc header expanded to mention Stage 3

No consumer migrated in this PR (spec NG1). First real consumer will be a separate Stage 3 cluster PR (suggested: `player-detail`).

## Spec & plan

- Design spec: `docs/for-developers/specs/2026-05-13-detail-page-layout-design.md`
- Implementation plan: `docs/superpowers/plans/2026-05-13-detail-page-layout.md`
- Parent spec: `docs/for-developers/specs/2026-05-11-design-system-deversioning.md` §4

## Test plan

- [x] `pnpm typecheck` green
- [x] `pnpm lint` green (no hard-coded colors)
- [x] `pnpm test --run` green — 8 new tests pass, existing detail-layout tests unchanged
- [x] `pnpm test --coverage` shows 100% line + branch on `DetailPageLayout.tsx`

Refs: #1026, #1023, #1066
EOF
)"
```

- [ ] **Step 6.5: Link the PR from the parent issue**

```bash
gh issue comment 1026 --body "Stage 3 cross-cutting primitive shipped: PR opened for the \`DetailPageLayout\` composer. Cluster PRs can now consume it. Suggested next cluster: \`player-detail\` (Wave 3 pending, mockup \`sp4-player-detail.jsx\` exists)."
```

---

## Self-Review Notes (from plan author)

- **Spec coverage** — Each AC in spec §9 maps to: AC1 → Task 5; AC2/AC7 → Step 6.1; AC3 → Step 4.4; AC4 → Tasks 1-4 (the 8 tests); AC5 → Step 5.4 (full suite run); AC6 → Step 6.2 (diff guard).
- **Placeholders** — None. Every code block contains the actual content the engineer types in.
- **Type consistency** — The component name `DetailPageLayout`, props interface `DetailPageLayoutProps`, and barrel export names are identical across Tasks 1, 4, 5, and 6. The `connections`/`tabs`/`footer` slot names match between spec §4, the component signature (Task 1.3), and every test (Tasks 1-4).
- **No spec drift** — `clsx` is used (codebase convention, verified in `sticky-cta.tsx`, `hero.tsx`, etc.) instead of a hypothetical `cn` util. The component imports `JSX` + `ReactNode` from `react` as a `import type`, matching the Wave A.4 file convention.
