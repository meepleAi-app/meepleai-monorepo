# SP5 F3-FU-3 — KB Tool-Pages Re-skin Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-skin the 7 admin Knowledge Base tool-pages to match the SP5 mockups, moving the page title into a shared layout band, using Tailwind utilities only — no logic, no SSE, no data-flow changes.

**Architecture:** A shared `<KbTopBand>` (sticky h1 "Knowledge Base" + dynamic crumbs + actions) is added to `knowledge-base/layout.tsx` above the existing `<KbSubNav>`. Each tool-page loses its local `<h1>` and its body is restyled with Tailwind utilities mapped from the mockups. Behavioral logic (hooks, useQuery, SSE, refetch intervals) is untouched — existing tests are the regression gate.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind v4 (semantic + entity tokens, post DS-16), Vitest, TypeScript.

**Design doc:** `docs/superpowers/specs/2026-05-28-sp5-admin-kb-toolpages-reskin-design.md` (read the ADDENDUM section first — it overrides earlier assumptions).

**Mockup reference (committed `2a0ab1117`):** `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-{vectors,queue,pipeline,embedding,feedback,settings}.html` + existing `sp5-admin-rag-backup.html` (snapshots).

---

## Ground rules (read before any task)

1. **Tailwind utilities only.** No new `.admin-*` CSS file. No new React primitive components (KpiCard/AdminPanel etc.). Mockups are the *visual* reference, translated to utilities.
2. **Contenuti testuali invariati.** Re-skin = className + DOM structure. Do NOT translate EN→IT or change copy. Mockup IT labels (`Ricerca semantica`) do NOT replace runtime EN (`Semantic Search`) — that would break `VectorStorePage` tests.
3. **No logic changes.** `useQuery`, `useMutation`, `useQueueSSE`, `useJobSSE`, `refetchInterval`, state, handlers stay byte-identical. If you spot a real bug, note it for a follow-up issue — do not fix it here.
4. **Token mapping** — use the table in design-doc §A3. Quick reference:
   - `font-quicksand` (display) · `font-mono` (labels/values/IDs) · body = no class
   - `text-foreground` (+ `dark:text-zinc-100` where existing code does) · `text-muted-foreground`
   - `bg-card` (pattern `bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md`) · `bg-muted`
   - `border-border/60` (there is NO `border-border-light` utility)
   - entity accent: `text-entity-kb` / `bg-entity-kb/12` / `border-l-4 border-l-entity-kb`
   - status chip: success → `bg-entity-toolkit/12 text-entity-toolkit`; danger → `bg-entity-event/12 text-entity-event`; warning → `bg-amber-500/14 text-amber-600`
5. **ESLint.** Files that use `zinc`/`amber`/`emerald`/`rose` raw palette need a file-level disable on line 1 (admin pattern):
   ```tsx
   /* eslint-disable local/no-hardcoded-color-utility -- admin KB tool-page chrome: zinc/amber dark palette (admin convention, DS-13c scope deferred to DS-16) */
   ```
   Many target files already have this (F3.1). Add only if missing AND you introduce a flagged utility.
6. **Commands run from `apps/web/`** for tests/lint/typecheck; **git runs from repo root.** All on branch `feature/issue-1652-kb-toolpages-reskin` (already created, parent `main-dev`).
7. **One commit per task**, message `refactor(admin-kb): re-skin <surface> (#1652)` (header task uses `feat(admin-kb): KB layout section band (#1652)`).
8. **Refactoring, not feature** — the classic "write failing test first" applies ONLY to Task 1 (which changes tested DOM). Re-skin tasks follow: capture baseline green → re-skin → confirm still green/typecheck/lint → commit.

---

## File Structure

**Create:**
- `apps/web/src/components/admin/knowledge-base/explorer/KbTopBand.tsx` — sticky section band (h1 + crumbs slot + actions slot)
- `apps/web/src/components/admin/knowledge-base/explorer/KbCrumbs.tsx` — pathname → static breadcrumb label
- `apps/web/src/components/admin/knowledge-base/explorer/KbTopActions.tsx` — search affordance (⌘K visual) + "+ Upload PDF" link
- `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbTopBand.test.tsx` — band renders single h1 "Knowledge Base"
- `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCrumbs.test.tsx` — crumb label per route

**Modify (layout + 7 surfaces):**
- `apps/web/src/app/admin/(dashboard)/knowledge-base/layout.tsx` — wrap band + body
- `apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx` — remove h1, re-skin
- `apps/web/src/app/admin/(dashboard)/knowledge-base/embedding/page.tsx`
- `apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx` + `feedback/page.tsx` (h2)
- `apps/web/src/app/admin/(dashboard)/knowledge-base/snapshots/page.tsx` — remove h1, re-skin
- `apps/web/src/components/admin/knowledge-base/kb-settings.tsx` + `settings/page.tsx` (h1)
- `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-dashboard-client.tsx` — remove h1, re-skin scaffold
- `apps/web/src/components/admin/knowledge-base/processing-metrics.tsx` + `rag-pipeline-flow.tsx` + `pipeline/page.tsx` (h1)

**Modify (tests):**
- `apps/web/__tests__/admin/knowledge-base/vector-collections.test.tsx` — relocate the "page header" assertion
- `apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx` — remove 4 obsolete `it.skip`

---

## Task 1: KB layout section band + header-move

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/explorer/KbTopBand.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/KbCrumbs.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/KbTopActions.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbTopBand.test.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCrumbs.test.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/layout.tsx`
- Modify: 7 surfaces (remove local h1 — see A4 table)
- Modify: `apps/web/__tests__/admin/knowledge-base/vector-collections.test.tsx`

- [ ] **Step 1: Baseline — confirm the suite is green before touching anything**

Run (from `apps/web/`):
```bash
pnpm test __tests__/admin/knowledge-base/vector-collections.test.tsx
```
Expected: PASS (6 tests). Note the test `should render page header` asserts `getByText('Vector Store')`.

- [ ] **Step 2: Write failing test for KbCrumbs (pathname → label)**

Create `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCrumbs.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react';
import { KbCrumbs } from '../KbCrumbs';

vi.mock('next/navigation', () => ({ usePathname: () => mockPath }));
let mockPath = '/admin/knowledge-base';

describe('KbCrumbs', () => {
  it('renders Explorer label at KB root', () => {
    mockPath = '/admin/knowledge-base';
    render(<KbCrumbs />);
    expect(screen.getByText(/Explorer/)).toBeInTheDocument();
  });

  it('renders Vector Collections label on /vectors', () => {
    mockPath = '/admin/knowledge-base/vectors';
    render(<KbCrumbs />);
    expect(screen.getByText(/Vector Collections/)).toBeInTheDocument();
  });

  it('renders Processing Queue label on /queue', () => {
    mockPath = '/admin/knowledge-base/queue';
    render(<KbCrumbs />);
    expect(screen.getByText(/Processing Queue/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run it — verify it fails (module not found)**

Run: `pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbCrumbs.test.tsx`
Expected: FAIL — `Cannot find module '../KbCrumbs'`.

- [ ] **Step 4: Implement KbCrumbs**

Create `apps/web/src/components/admin/knowledge-base/explorer/KbCrumbs.tsx`:
```tsx
'use client';

import { usePathname } from 'next/navigation';

const KB_BASE = '/admin/knowledge-base';

const LABELS: ReadonlyArray<readonly [string, string]> = [
  [`${KB_BASE}/vectors`, 'Vector Collections'],
  [`${KB_BASE}/queue`, 'Processing Queue'],
  [`${KB_BASE}/pipeline`, 'RAG Pipeline'],
  [`${KB_BASE}/embedding`, 'Embedding'],
  [`${KB_BASE}/feedback`, 'Feedback'],
  [`${KB_BASE}/settings`, 'Settings'],
  [`${KB_BASE}/snapshots`, 'Snapshots'],
  [`${KB_BASE}/upload`, 'Upload'],
];

function labelFor(pathname: string): string {
  if (pathname === KB_BASE) return 'Explorer';
  const hit = LABELS.find(([href]) => pathname === href || pathname.startsWith(`${href}/`));
  return hit ? hit[1] : 'Explorer';
}

export function KbCrumbs() {
  const pathname = usePathname();
  return (
    <div className="font-mono text-[11px] text-muted-foreground mt-0.5">
      Admin · KB · {labelFor(pathname)}
    </div>
  );
}
```

- [ ] **Step 5: Run KbCrumbs test — verify pass**

Run: `pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbCrumbs.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 6: Write failing test for KbTopBand**

Create `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbTopBand.test.tsx`:
```tsx
import { render, screen } from '@testing-library/react';
import { KbTopBand } from '../KbTopBand';

vi.mock('next/navigation', () => ({ usePathname: () => '/admin/knowledge-base/vectors' }));

describe('KbTopBand', () => {
  it('renders a single h1 "Knowledge Base"', () => {
    render(<KbTopBand />);
    const h1s = screen.getAllByRole('heading', { level: 1 });
    expect(h1s).toHaveLength(1);
    expect(h1s[0]).toHaveTextContent('Knowledge Base');
  });

  it('renders the Upload PDF action', () => {
    render(<KbTopBand />);
    expect(screen.getByRole('link', { name: /Upload PDF/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 7: Run it — verify it fails**

Run: `pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbTopBand.test.tsx`
Expected: FAIL — `Cannot find module '../KbTopBand'`.

- [ ] **Step 8: Implement KbTopActions + KbTopBand**

Create `apps/web/src/components/admin/knowledge-base/explorer/KbTopActions.tsx`:
```tsx
import Link from 'next/link';

export function KbTopActions() {
  return (
    <div className="flex items-center gap-2">
      <div className="hidden md:flex items-center gap-2 rounded-lg border border-border/60 bg-card px-3 py-1.5 text-sm text-muted-foreground w-[280px]">
        <span aria-hidden>🔍</span>
        <input
          className="flex-1 bg-transparent outline-none text-foreground placeholder:text-muted-foreground"
          placeholder="Search docs, chunks, games…"
          aria-label="Search knowledge base"
        />
        <kbd className="rounded border border-border/60 bg-muted px-1.5 font-mono text-[10px]">⌘K</kbd>
      </div>
      <Link
        href="/admin/knowledge-base/upload"
        className="inline-flex items-center gap-1.5 rounded-md bg-entity-kb px-3 py-1.5 text-sm font-semibold font-quicksand text-white shadow-sm hover:brightness-110"
      >
        + Upload PDF
      </Link>
    </div>
  );
}
```
> The search input is a visual affordance (matches mockup); wiring ⌘K behavior is out of scope (follow-up #1655). `text-white` on colored `bg-entity-kb` is ESLint-allowed (the `.e-bg` exemption).

Create `apps/web/src/components/admin/knowledge-base/explorer/KbTopBand.tsx`:
```tsx
import { KbCrumbs } from './KbCrumbs';
import { KbTopActions } from './KbTopActions';

export function KbTopBand() {
  return (
    <header className="sticky top-0 z-40 -mx-6 flex items-center gap-4 border-b border-border/60 bg-background/80 px-6 py-3 backdrop-blur-md">
      <div>
        <h1 className="font-quicksand text-xl font-extrabold tracking-tight text-foreground">
          Knowledge Base
        </h1>
        <KbCrumbs />
      </div>
      <div className="flex-1" />
      <KbTopActions />
    </header>
  );
}
```

- [ ] **Step 9: Run KbTopBand test — verify pass**

Run: `pnpm test src/components/admin/knowledge-base/explorer/__tests__/KbTopBand.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 10: Wire band into layout**

Modify `apps/web/src/app/admin/(dashboard)/knowledge-base/layout.tsx`:
```tsx
import { type ReactNode } from 'react';

import { KbSubNav } from '@/components/admin/knowledge-base/explorer/KbSubNav';
import { KbTopBand } from '@/components/admin/knowledge-base/explorer/KbTopBand';

export default function KnowledgeBaseLayout({ children }: { children: ReactNode }) {
  return (
    <div className="px-6">
      <KbTopBand />
      <div className="space-y-6 pt-6">
        <KbSubNav />
        {children}
      </div>
    </div>
  );
}
```
> `KbTopBand` uses `-mx-6` for full-bleed border (the `px-6` wrapper compensates), same trick `KbSubNav` already uses.

- [ ] **Step 11: Remove local h1 from the 7 surfaces**

For each file, replace its top-level `<h1>` page header with nothing (the band owns it). Where a subtitle `<p>` existed beside the h1, keep it ONLY if it carries page-specific info not in the crumbs; otherwise remove the whole header `<div>`. Exact edits:
- `vectors/page.tsx` — remove the `<div className="flex items-center justify-between flex-wrap gap-4">` header block containing `<h1>Vector Store</h1>` + subtitle; keep the Refresh `<Button>` by moving it into the first content row (e.g. the KPI strip header or a small right-aligned toolbar). Re-skin proper happens in Task 2 — here only delete the h1 block, preserving the Refresh button somewhere valid.
- `embedding/page.tsx` — remove `<h1>Embedding Service</h1>` header block; preserve Refresh button.
- `snapshots/page.tsx` — remove `<h1>Snapshot RAG</h1>` header block; preserve Refresh + Create buttons.
- `settings/page.tsx` — remove `<h1>Knowledge Base Settings</h1>` (wrapper).
- `pipeline/page.tsx` — remove `<h1>RAG Pipeline Overview</h1>` (wrapper). The `<h2>` headings inside `processing-metrics.tsx` / `rag-pipeline-flow.tsx` STAY (section headings).
- `queue/components/queue-dashboard-client.tsx` — demote `<h1>Processing Queue</h1>` (line ~92): remove it (the back-arrow Link + SSE indicator row stays). The crumb + tab express identity.
- `feedback/page.tsx` — the heading is already `<h2>Feedback KB Utenti</h2>` (not h1). Leave as-is OR demote to a section label during Task 4. No change required here.

> Keep all non-heading JSX, buttons, and logic intact. This step ONLY removes/relocates headings.

- [ ] **Step 12: Update vector-collections.test.tsx — relocate the "page header" assertion**

The page no longer renders `<h1>Vector Store</h1>` (it's in the layout band, not in the page under test). In `apps/web/__tests__/admin/knowledge-base/vector-collections.test.tsx`, change the `should render page header` test to assert on a stable page element instead. Replace the `getByText('Vector Store')` assertion (line ~101) with an assertion on the KPI label that survives the re-skin:
```tsx
it('should render the stats KPIs', () => {
  // ...existing render setup...
  expect(screen.getByText('Total Vectors')).toBeInTheDocument();
});
```
> If `Total Vectors` text differs in the current file, use whatever stat label the page renders today (the recon shows a "Total Vectors" KPI). The point: assert on page-owned content, not the relocated h1.

- [ ] **Step 13: Run impacted suites — verify all green**

Run (from `apps/web/`):
```bash
pnpm test __tests__/admin/knowledge-base/vector-collections.test.tsx src/components/admin/knowledge-base/explorer/__tests__/
```
Expected: PASS (6 vectors + 2 KbTopBand + 3 KbCrumbs = 11).

- [ ] **Step 14: Typecheck + token lint**

Run (from `apps/web/`):
```bash
pnpm typecheck
pnpm lint:tokens
```
Expected: typecheck 0 errors; lint:tokens 0 violations.

- [ ] **Step 15: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/explorer/ apps/web/src/app/admin/\(dashboard\)/knowledge-base/layout.tsx apps/web/src/app/admin/\(dashboard\)/knowledge-base/vectors/page.tsx apps/web/src/app/admin/\(dashboard\)/knowledge-base/embedding/page.tsx apps/web/src/app/admin/\(dashboard\)/knowledge-base/snapshots/page.tsx apps/web/src/app/admin/\(dashboard\)/knowledge-base/settings/page.tsx apps/web/src/app/admin/\(dashboard\)/knowledge-base/pipeline/page.tsx apps/web/src/app/admin/\(dashboard\)/knowledge-base/queue/components/queue-dashboard-client.tsx apps/web/__tests__/admin/knowledge-base/vector-collections.test.tsx
git commit -m "feat(admin-kb): KB layout section band + header-move (#1652)"
```

---

## Task 2: Re-skin vectors page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx`
- Reference mockup: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-vectors.html`
- Test: `apps/web/__tests__/admin/knowledge-base/vector-collections.test.tsx` + `kb-hub-gaps.test.tsx` (VectorStorePage 5)

- [ ] **Step 1: Baseline green**

Run (from `apps/web/`):
```bash
pnpm test __tests__/admin/knowledge-base/vector-collections.test.tsx "src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
```
Expected: PASS.

- [ ] **Step 2: Re-skin the 3 visual blocks (className only)**

Open the mockup `sp5-admin-kb-vectors.html` side-by-side. Translate these blocks; keep all hooks, state, handlers, and text content:

**(a) KPI strip** — 4-col grid. Each KPI:
```tsx
<div className="flex flex-col gap-1 rounded-[10px] border border-border/60 bg-card p-4 border-l-4 border-l-entity-kb">
  <span className="font-mono text-[10px] font-bold uppercase tracking-wide text-muted-foreground">Total Vectors</span>
  <span className="font-quicksand text-[28px] font-extrabold tabular-nums text-foreground">{totalVectors}</span>
  <span className="font-mono text-[11px] text-entity-toolkit">▲ +{delta} last 24h</span>
</div>
```
Entity accent per card: Total Vectors → `border-l-entity-kb`; Games Indexed → `border-l-entity-game`; Dimensions → `border-l-entity-chat`; Avg Health → `border-l-entity-toolkit`.

**(b) Semantic search panel** — wrap in admin-panel pattern:
```tsx
<section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
  <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
    <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">Semantic Search</h2>
    {/* keep existing status/meta inline elements */}
  </div>
  <div className="p-3.5">{/* existing search row + results, restyled */}</div>
</section>
```
> KEEP heading text `Semantic Search` (EN) — `kb-hub-gaps.test.tsx` asserts it. Do NOT use mockup's `Ricerca semantica`.

**(c) Game breakdown grid** — same `section` panel wrapper, grid of game cards inside.

- [ ] **Step 3: Run tests — verify still green**

Run (from `apps/web/`):
```bash
pnpm test __tests__/admin/knowledge-base/vector-collections.test.tsx "src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
```
Expected: PASS (same count as baseline). If a test that asserts text/behavior fails, you changed content — revert that part.

- [ ] **Step 4: Typecheck + token lint**
```bash
pnpm typecheck && pnpm lint:tokens
```
Expected: clean.

- [ ] **Step 5: Commit**
```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx"
git commit -m "refactor(admin-kb): re-skin vectors page (#1652)"
```

---

## Task 3: Re-skin embedding page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/embedding/page.tsx`
- Reference mockup: `sp5-admin-kb-embedding.html`
- Tests: none (gate = typecheck + lint + manual)

- [ ] **Step 1: Confirm no unit test exists, capture typecheck baseline**
```bash
pnpm typecheck
```
Expected: 0 errors (baseline).

- [ ] **Step 2: Re-skin 2 blocks (className only; KEEP both `useQuery` + `refetchInterval: 30_000`)**

**(a) Service status card** — `rounded-[10px] border border-border/60 bg-card p-4`; header `flex items-center gap-2` with icon + `<h2 className="font-quicksand text-[13px] font-extrabold">Service Status</h2>` + a `status-chip` (`inline-flex items-center gap-1 rounded-full px-2 py-0.5 font-mono text-[10px] font-bold uppercase bg-entity-toolkit/12 text-entity-toolkit` for healthy). Inner grid of model/device/dims/languages stays.

**(b) Throughput KPI grid** — 6 KPIs, `grid grid-cols-2 md:grid-cols-3 gap-3`, each using the KPI pattern from Task 2 step 2(a). Entity accents: requests → `entity-kb`, failures → `entity-event`, failure rate → `entity-event`, avg/total duration → `entity-chat`, chars → `entity-toolkit`.

- [ ] **Step 3: Typecheck + token lint**
```bash
pnpm typecheck && pnpm lint:tokens
```
Expected: clean.

- [ ] **Step 4: Commit**
```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/embedding/page.tsx"
git commit -m "refactor(admin-kb): re-skin embedding page (#1652)"
```

---

## Task 4: Re-skin feedback panel

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx`
- Modify (optional, heading): `apps/web/src/app/admin/(dashboard)/knowledge-base/feedback/page.tsx`
- Reference mockup: `sp5-admin-kb-feedback.html`
- Tests: none

- [ ] **Step 1: Typecheck baseline**
```bash
pnpm typecheck
```

- [ ] **Step 2: Re-skin (className only; KEEP `useQuery` + Italian labels Utile/Non utile/Tutti/Prec/Succ)**

Wrap the filter row + list in the admin-panel pattern. Each feedback item:
```tsx
<div className="flex items-start gap-3 rounded-lg border border-border/60 bg-card p-3">
  {/* keep thumbs icon, Badge (Utile/Non utile), message id, date, optional comment */}
</div>
```
Outcome filter `<Select>` stays functionally identical; only restyle the wrapper row to `flex items-center gap-3`. Pagination buttons → `btn-admin sm` equivalent: `rounded-md border border-border/60 bg-card px-2 py-1 text-[11px] font-quicksand font-bold hover:bg-muted disabled:opacity-40`.

- [ ] **Step 3: Typecheck + token lint**
```bash
pnpm typecheck && pnpm lint:tokens
```

- [ ] **Step 4: Commit**
```bash
git add apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx "apps/web/src/app/admin/(dashboard)/knowledge-base/feedback/page.tsx"
git commit -m "refactor(admin-kb): re-skin feedback panel (#1652)"
```

---

## Task 5: Re-skin snapshots page

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/snapshots/page.tsx`
- Reference mockup: `sp5-admin-rag-backup.html` (pre-existing)
- Tests: none

- [ ] **Step 1: Typecheck baseline** — `pnpm typecheck`

- [ ] **Step 2: Re-skin (className only; KEEP `useQuery` + restore/delete `useMutation` + AlertDialog confirms)**

Backup list → admin-panel pattern with `divide-y divide-border/60`. Each `SnapshotCard` row: icon + metadata + restore/delete buttons (`btn-admin sm` pattern). Info/error/success banners → use semantic tokens (info `bg-entity-chat/12 text-entity-chat`, success `bg-entity-toolkit/12 text-entity-toolkit`, error `bg-entity-event/12 text-entity-event`) instead of raw `bg-blue-50`/`bg-emerald-50`/`bg-red-50` where present. Status chips for backup state per mockup.

- [ ] **Step 3: Typecheck + token lint** — `pnpm typecheck && pnpm lint:tokens`

- [ ] **Step 4: Commit**
```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/snapshots/page.tsx"
git commit -m "refactor(admin-kb): re-skin snapshots page (#1652)"
```

---

## Task 6: Re-skin settings panel

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/kb-settings.tsx`
- Reference mockup: `sp5-admin-kb-settings.html`
- Tests: none

- [ ] **Step 1: Typecheck baseline** — `pnpm typecheck`

- [ ] **Step 2: Re-skin (className only; KEEP `useQuery` settings + clearCache/rebuildIndex `useMutation` + typed-confirm logic)**

6 setting cards → `grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-3` (mockup is 3-up). Each `SettingsCard`: admin-panel pattern, header `flex items-center gap-2` (icon + `<h3 className="font-quicksand text-[13px] font-extrabold">`), body `space-y-0` setting rows. Read-only chips → `status-chip readonly` = `bg-muted text-muted-foreground`. Danger Zone → `rounded-[10px] border border-entity-event/40 bg-entity-event/5 p-4` with header `text-entity-event`; Rebuild Index + Clear Cache buttons keep typed-confirm flow, restyle to `btn-admin danger` = `rounded-md border border-entity-event/55 bg-card px-3 py-1.5 text-entity-event hover:bg-entity-event/8`.

- [ ] **Step 3: Typecheck + token lint** — `pnpm typecheck && pnpm lint:tokens`

- [ ] **Step 4: Commit**
```bash
git add apps/web/src/components/admin/knowledge-base/kb-settings.tsx
git commit -m "refactor(admin-kb): re-skin settings panel (#1652)"
```

---

## Task 7: Re-skin queue dashboard scaffold

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-dashboard-client.tsx`
- Reference mockup: `sp5-admin-kb-queue.html`
- Tests: `apps/web/__tests__/admin/queue/{queue-alerts-banner,queue-filters}.test.tsx`

- [ ] **Step 1: Baseline green**
```bash
pnpm test __tests__/admin/queue/
```
Expected: PASS (13 tests).

- [ ] **Step 2: Re-skin SCAFFOLD ONLY (className only — CRITICAL: do NOT touch `useQueueSSE`, `useJobSSE`, `useQueueList`, `useJobDetail`, EventSource, reconnect, or the sub-components' internals)**

In `queue-dashboard-client.tsx`, restyle only the wrapper layout:
- Header row: keep back-arrow Link + `SSEConnectionIndicator` + Refresh + Add PDF buttons; restyle to mockup control-bar look. (h1 already removed in Task 1.)
- 2-column split: keep `grid grid-cols-1 lg:grid-cols-5 gap-4`, restyle the two column containers (`lg:col-span-2` list / `lg:col-span-3` detail) with admin-panel pattern (`rounded-[10px] border border-border/60 bg-card`).
- Spacing between `<QueueAlertsBanner>`, `<QueueControlBar>`, `<QueueCapacityIndicator>`, `<QueueStatsBar>`, `<BulkActionsBar>`, `<QueueFiltersBar>`, `<MetricsDashboard>` stays via `space-y-4`.
> Re-skinning each sub-component (`QueueControlBar`, `QueueList`, `JobDetailPanel`, `MetricsDashboard`) deeper than the wrapper is OPTIONAL and per-component — only if time allows and tests stay green. The scaffold restyle is the required deliverable.

- [ ] **Step 3: Run tests — verify still green**
```bash
pnpm test __tests__/admin/queue/
```
Expected: PASS (13). SSE/behavior unchanged.

- [ ] **Step 4: Typecheck + token lint** — `pnpm typecheck && pnpm lint:tokens`

- [ ] **Step 5: Commit**
```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/"
git commit -m "refactor(admin-kb): re-skin queue dashboard scaffold (#1652)"
```

---

## Task 8: Re-skin pipeline components

**Files:**
- Modify: `apps/web/src/components/admin/knowledge-base/processing-metrics.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/rag-pipeline-flow.tsx`
- Reference mockup: `sp5-admin-kb-pipeline.html`
- Test: `kb-hub-gaps.test.tsx` (RAGPipelineFlow Stage Drill-Down — 4 tests MUST stay green)

- [ ] **Step 1: Baseline green**
```bash
pnpm test "src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
```
Expected: PASS (VectorStorePage 5 + RAGPipelineFlow 4 = 9 active; 4 skipped).

- [ ] **Step 2: Re-skin `rag-pipeline-flow.tsx` (className only — KEEP `useQuery` 30s refetch, stage click handlers, `expandedStage` state)**

5-stage flow → `flex items-center justify-between gap-4 overflow-x-auto`; each stage button keeps its onClick + state, restyle to `rounded-lg border-2 bg-muted p-4` with status dot. **The stage buttons must remain `role=button`/clickable with the same accessible names** — the 4 drill-down tests click them by text. Health summary 3 cards → semantic tokens (healthy `bg-entity-toolkit/10`, warning `bg-amber-500/10`, error `bg-entity-event/10`). Distribution stat cards + Recent Activity list → admin-panel pattern.

- [ ] **Step 3: Run RAGPipelineFlow tests — verify still green**
```bash
pnpm test "src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx" -t "RAGPipelineFlow"
```
Expected: PASS (4). If a drill-down test fails, you changed a stage button's text or click behavior — revert.

- [ ] **Step 4: Re-skin `processing-metrics.tsx` (className only — KEEP `useQuery` 60s refetch)**

Step metric cards → KPI/admin-panel pattern grid. Percentile comparison table → restyle `<table>` with `text-sm`, header row `bg-muted font-mono text-[10px] uppercase text-muted-foreground`, keep `font-mono` on numeric cells + `durationColor()` logic intact.

- [ ] **Step 5: Run full suite — verify green**
```bash
pnpm test "src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
```
Expected: PASS (9 active).

- [ ] **Step 6: Typecheck + token lint** — `pnpm typecheck && pnpm lint:tokens`

- [ ] **Step 7: Commit**
```bash
git add apps/web/src/components/admin/knowledge-base/processing-metrics.tsx apps/web/src/components/admin/knowledge-base/rag-pipeline-flow.tsx
git commit -m "refactor(admin-kb): re-skin pipeline components (#1652)"
```

---

## Task 9: Cleanup obsolete tests + final gate + PR

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx`

- [ ] **Step 1: Remove the 4 obsolete `it.skip` blocks**

In `kb-hub-gaps.test.tsx`, delete the entire `describe('KnowledgeBasePage — Hub Gap Fixes', ...)` block (lines ~199–254) — the 4 `it.skip` with `TODO(F3-FU-3): rewrite for Explorer; pre-existing test for hub-cards landing is obsolete after F3.1 rebuild`. They tested the pre-F3.1 hub-cards landing which no longer exists (replaced by Explorer in PR #1649). The `VectorStorePage` (5) and `RAGPipelineFlow` (4) describe blocks STAY untouched.

- [ ] **Step 2: Run the file — verify only the live suites remain green**
```bash
pnpm test "src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
```
Expected: PASS (9 tests, 0 skipped).

- [ ] **Step 3: Full frontend gate**

Run (from `apps/web/`):
```bash
pnpm test:admin-dashboard
pnpm typecheck
pnpm lint
pnpm lint:tokens
```
Expected: all green, 0 token violations. If the full `pnpm test` is fast enough, run it to confirm zero regressions against `main-dev` baseline.

- [ ] **Step 4: Commit cleanup**
```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx"
git commit -m "test(admin-kb): drop obsolete F3.1 hub-cards tests (#1652)"
```

- [ ] **Step 5: Push + open PR to main-dev**

```bash
git push -u origin feature/issue-1652-kb-toolpages-reskin
gh pr create --base main-dev --title "feat(admin-kb): SP5 F3-FU-3 — re-skin KB tool-pages + section band (#1652)" --body "<see body template below>"
```
PR body must include: link to #1652, the header-decision rationale (band owns h1, sub-pages lose local h1), Tailwind-utilities strategy (per user decision), list of 7 re-skinned surfaces, the `vector-collections.test.tsx` assertion relocation, the 4 obsolete tests removed, and a request for manual designer (Sara) review. Confirm base is `main-dev` NOT `main`.

- [ ] **Step 6: Code review before merge** (per CLAUDE.md + global rule)

Run `/code-review:code-review <PR-URL>` and address any BLOCKER/HIGH findings before merge. Then update issue #1652 DoD locally + on GitHub. Merge only after review + green CI; the branch auto-deletes on merge.

---

## Self-Review (checked against design doc + recon)

**Spec coverage:** Every surface in design-doc §3 table → Tasks 2–8. Header decision §2/§A4 → Task 1. Test cleanup §A7 → Task 9. ✅
**Placeholder scan:** No "TBD"/"handle edge cases"/"similar to" — each task names exact files, mockup, token classes, and commands. Re-skin steps reference the committed mockup as the complete visual source (legitimate — duplicating 2000+ lines of JSX inline is not feasible; the mockup HTML IS the spec). ✅
**Type/name consistency:** `KbTopBand`, `KbCrumbs`, `KbTopActions` defined in Task 1 and referenced consistently. `labelFor`/`LABELS` internal to KbCrumbs. ✅
**Known soft spots flagged:** (1) Task 1 step 11 relocates the Refresh button when deleting the vectors/embedding/snapshots h1 header block — executor must keep it reachable; (2) Task 7 queue sub-component depth is explicitly optional; (3) crumbs are static labels (live counts deferred — not in scope, avoids logic change). ✅
