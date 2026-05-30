# FE consumer follow-up: chunk-level citation deep-link (#1702) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to execute task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consume the new BE `Snippet.chunkId` (string, composite `"{docId}_{chunkIndex}"`) and `chunkPosition` (int) fields shipped by PR #1713 (merged squash `55741f716`). The FE must:

1. Parse the new fields into `KbCitationSchema` (optional, backward-compatible).
2. Surface them through `CitationPill.onClick({ docId, page, chunkId? })`.
3. Wire `DrawerCompleted` to forward `onCitationClick` to `parseCitationMarkers` (currently UNwired — gap).
4. `KbGlobaleView.openViewer` pushes `?docId=&page=&chunkId=` when chunkId is present.
5. On viewer mount with `?chunkId=` in the URL, resolve via `useKbChunkDetail` to obtain the chunk's actual `pageNumber` and navigate there.
6. **Graceful degrade** (panel D-2): when the chunk lookup fails (404, soft-delete, race, doc replaced), `console.warn` + fall back to page-level scroll using the `?page=` param. No error UI surfaced.
7. New tests cover both the chunk-level happy path AND the page-level fallback.

**Architecture:** Schema-first, then component-by-component bottom-up (pill → wiring → orchestrator → integration test). The viewer (`KbDocViewerDesktop`) remains **purely presentational** — chunk resolution happens in the orchestrator (`KbGlobaleView`) via `useKbChunkDetail`. "Scroll to chunk position" resolves to **page-level scroll using the chunk's resolved `pageNumber`** — sub-page highlight is out of scope for this PR (react-pdf overlay highlighting is a separate UX investment).

**Tech Stack:** React 19 + Next.js 16 App Router + Zod + react-pdf · Vitest + @testing-library/react · TanStack Query (for `useKbChunkDetail`) · existing `logger.warn` wrapper for graceful degrade

---

## Design Decisions

### DD-1: Page-level resolution via chunk lookup (not sub-page highlight)

The viewer (`KbDocViewerDesktop.tsx:160`) renders `<Page pageNumber={activePage} />` from `react-pdf`. It is purely presentational and has no concept of sub-page positioning. Implementing true sub-page scroll/highlight requires custom overlay logic on react-pdf's text layer — a significant UX investment outside this PR's scope.

**Decision: `chunkId` → resolve via `useKbChunkDetail` → use `chunk.pageNumber` as the navigation target.** When `chunk.pageNumber` matches the URL's `?page=` (i.e., BE-emitted page was already correct), this is a no-op. When they differ (e.g., the BE emitted a wrong fallback page), the chunk lookup correctly redirects to the chunk's real page. This delivers "chunk-aware navigation" without sub-page UI complexity. Visual aid for the user (e.g., highlight the citation in the right panel) is bonus, not required for this PR.

### DD-2: Schema additive, optional, passthrough-tolerant

Add `chunkId: z.string().optional()` and `chunkPosition: z.number().int().nonnegative().optional()` to `KbCitationSchema`. Both optional → old BE responses (or per-game endpoints that don't set the fields) continue to parse cleanly. **Do NOT** make them required — would break Phase 2 page-level paths.

### DD-3: Graceful degrade via `console.warn` (no logger wrapper)

Following the existing pattern from `parseCitationMarkers.tsx:78-82`, use a direct `console.warn` for stale-chunkId warnings. This is non-critical noise (page-level fallback works), not structured observability. Reserve `logger.warn(...)` for things ops needs to track. Match the existing convention.

### DD-4: orchestrator-level resolution (not viewer-level)

`KbGlobaleView` already owns the `docIdParam` / `pageParam` URL state and the `openViewer` action. Add `chunkIdParam` to the same state. Conditionally call `useKbChunkDetail({ docId: docIdParam, chunkId: chunkIdParam, enabled: chunkIdParam != null })` and, on success, override `activePage` with `chunk.pageNumber` (if non-null) before passing to `KbDocViewerDesktop`. On error/timeout, fall back to `pageParam` + `console.warn`.

This keeps the viewer dumb and the URL-as-source-of-truth invariant intact.

---

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `apps/web/src/lib/api/schemas/kb-ask.schemas.ts` | Modify (lines 9, 24-31) | Add `chunkId?: string` + `chunkPosition?: int?` to `KbCitationSchema`; remove the stale "NO chunkId" comment |
| `apps/web/src/components/features/kb-globale/CitationPill.tsx` | Modify (props + onClick) | Add optional `chunkId?: string` prop; pass it in `onClick({ docId, page, chunkId? })` payload |
| `apps/web/src/components/features/kb-globale/parseCitationMarkers.tsx` | Modify | When invoking `<CitationPill>` for each citation marker, forward `chunkId` from `KbCitation` to the pill prop |
| `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx` | Modify | Accept new prop `onCitationClick?: (link: { docId: string; page: number; chunkId?: string }) => void` and forward it to `parseCitationMarkers` |
| `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx` | Modify | (1) URL state: add `chunkIdParam`; (2) `openViewer` accepts `chunkId?` and pushes it; (3) call `useKbChunkDetail` to resolve chunk's page; (4) graceful degrade on error; (5) pass `onCitationClick` to `DrawerCompleted`/`DrawerShell` |
| `apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts` | **Create** (if absent) or modify | Test schema parses with/without the new fields |
| `apps/web/src/components/features/kb-globale/__tests__/CitationPill.test.tsx` | Modify | Add tests for `chunkId` prop + payload extension |
| `apps/web/src/components/features/kb-globale/__tests__/parseCitationMarkers.test.tsx` | Modify | Verify `chunkId` is forwarded from `KbCitation` to the rendered `CitationPill` |
| `apps/web/src/components/features/kb-globale/__tests__/DrawerCompleted.test.tsx` (or DrawerShell.test.tsx) | Modify | Test that `onCitationClick` is forwarded through DrawerCompleted to the inline pills |
| `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx` (or `.integration.test.tsx`) | Modify | (1) URL push includes `chunkId` when present; (2) chunk-level happy path — viewer opens at `chunk.pageNumber`; (3) graceful degrade — chunk lookup fails → page-level fallback + warn |

---

## Pre-Flight

- [ ] **Step 0a: Confirm clean working tree on `main-dev`**

```bash
git branch --show-current  # MUST print main-dev
git status                 # MUST be clean
git pull --ff-only         # MUST succeed
```

- [ ] **Step 0b: Create feature branch**

```bash
git checkout -b feature/issue-1702-fe-citation-chunk-deep-link
git config branch.feature/issue-1702-fe-citation-chunk-deep-link.parent main-dev
```

- [ ] **Step 0c: Commit the plan**

```bash
git add docs/superpowers/plans/2026-05-31-1702-fe-citation-chunk-deep-link.md
git commit -m "docs(plans): #1702 FE citation chunk-level deep-link plan"
```

- [ ] **Step 0d: Baseline tests** to confirm starting state:

```bash
cd apps/web && pnpm test --run src/lib/api/schemas src/components/features/kb-globale src/app/\(authenticated\)/knowledge-base 2>&1 | tail -10
```

Record total test count. Phase B must keep these all green at the end.

---

## Task 1: Extend `KbCitationSchema` with optional chunk fields

**Files:**
- Modify: `apps/web/src/lib/api/schemas/kb-ask.schemas.ts`
- Create or modify: `apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts`

### Step 1: Write the failing schema test

Open or create `apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts`. Add (or replace) with these tests:

```typescript
import { describe, it, expect } from 'vitest';
import { KbCitationSchema } from '../kb-ask.schemas';

describe('KbCitationSchema — chunk-level fields (#1702)', () => {
  const basePayload = {
    docId: 'doc-123',
    source: 'PDF:doc-123',
    page: 14,
    snippet: 'rules text',
    score: 0.85,
  };

  it('parses cleanly when chunk fields are absent (page-level legacy payload)', () => {
    const result = KbCitationSchema.safeParse(basePayload);
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.chunkId).toBeUndefined();
      expect(result.data.chunkPosition).toBeUndefined();
    }
  });

  it('parses cleanly with both chunk fields set (cross-game chunk-level payload)', () => {
    const result = KbCitationSchema.safeParse({
      ...basePayload,
      chunkId: 'doc-123_3',
      chunkPosition: 3,
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.chunkId).toBe('doc-123_3');
      expect(result.data.chunkPosition).toBe(3);
    }
  });

  it('parses cleanly with only chunkId (partial set)', () => {
    const result = KbCitationSchema.safeParse({
      ...basePayload,
      chunkId: 'doc-123_0',
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.chunkId).toBe('doc-123_0');
      expect(result.data.chunkPosition).toBeUndefined();
    }
  });

  it('rejects chunkPosition that is negative', () => {
    const result = KbCitationSchema.safeParse({ ...basePayload, chunkPosition: -1 });
    expect(result.success).toBe(false);
  });

  it('rejects chunkPosition that is non-integer', () => {
    const result = KbCitationSchema.safeParse({ ...basePayload, chunkPosition: 1.5 });
    expect(result.success).toBe(false);
  });
});
```

### Step 2: Run to confirm failure

```bash
cd apps/web && pnpm test --run src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts 2>&1 | tail -10
```

Expected: **2 of 5 tests fail** (the "absent fields" + "rejects negative" tests pass with current schema; the others fail because the new fields are stripped or rejected by strict mode). The exact failure depends on whether the existing schema is strict — verify by reading the current schema definition.

### Step 3: Update the schema

Open `apps/web/src/lib/api/schemas/kb-ask.schemas.ts`. The current state (line 24-31):

```typescript
export const KbCitationSchema = z.object({
  docId: z.string(),
  source: z.string(),
  page: z.number().int().nonnegative(),
  snippet: z.string(),
  score: z.number(),
});
export type KbCitation = z.infer<typeof KbCitationSchema>;
```

Replace with:

```typescript
export const KbCitationSchema = z.object({
  docId: z.string(),
  source: z.string(),
  page: z.number().int().nonnegative(),
  snippet: z.string(),
  score: z.number(),
  /**
   * Chunk-level deep-link identifier (#1702). Composite "{docId}_{chunkIndex}" from
   * MultiGameSearchResultItem.ChunkId on the BE. Only set by /ask/global cross-game
   * retrieval; absent from per-game endpoints. When present, FE can resolve via
   * useKbChunkDetail and navigate to the chunk's exact page. Graceful degrade to
   * page-level when null/unresolvable.
   */
  chunkId: z.string().optional(),
  /**
   * Zero-based chunk index within the document (#1702). Mirrors TextChunkEntity.ChunkIndex.
   * Only present alongside chunkId.
   */
  chunkPosition: z.number().int().nonnegative().optional(),
});
export type KbCitation = z.infer<typeof KbCitationSchema>;
```

ALSO update the file-header comment on line 9 (if it still says "NO chunkId / chunkPosition" — replace with a note that the fields were added in #1702 FE follow-up). If the comment is just a stale spec-panel note, delete it.

### Step 4: Run tests — confirm 5/5 pass

```bash
cd apps/web && pnpm test --run src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts 2>&1 | tail -10
```

Expected: 5 passed.

### Step 5: Commit

```bash
git add apps/web/src/lib/api/schemas/kb-ask.schemas.ts apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts
git commit -m "feat(kb-globale): extend KbCitationSchema with optional chunk fields (#1702)"
```

---

## Task 2: Extend `CitationPill` props + onClick payload

**Files:**
- Modify: `apps/web/src/components/features/kb-globale/CitationPill.tsx`
- Modify: `apps/web/src/components/features/kb-globale/__tests__/CitationPill.test.tsx`

### Step 1: Write failing test

Open the test file. Find the existing `onClick` test (around line 38-51). Add the following tests after it:

```typescript
it('passes chunkId in onClick payload when prop provided', async () => {
  const user = userEvent.setup();
  const onClick = vi.fn();
  render(
    <CitationPill
      n={2}
      refText="Rules"
      docId="doc-1"
      page={5}
      chunkId="doc-1_3"
      ariaLabel="Apri citazione 2"
      onClick={onClick}
    />
  );
  await user.click(screen.getByRole('button'));
  expect(onClick).toHaveBeenCalledWith({ docId: 'doc-1', page: 5, chunkId: 'doc-1_3' });
});

it('omits chunkId from onClick payload when prop absent (back-compat)', async () => {
  const user = userEvent.setup();
  const onClick = vi.fn();
  render(
    <CitationPill
      n={1}
      refText="Setup"
      docId="doc-2"
      page={3}
      ariaLabel="Apri citazione 1"
      onClick={onClick}
    />
  );
  await user.click(screen.getByRole('button'));
  // payload MUST be exactly { docId, page } — chunkId key should be absent or undefined
  expect(onClick).toHaveBeenCalledWith({ docId: 'doc-2', page: 3 });
});
```

### Step 2: Run to confirm failure

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale/__tests__/CitationPill.test.tsx 2>&1 | tail -10
```

Expected: 2 new tests fail (TS error: `chunkId` not in `CitationPillProps`).

### Step 3: Update `CitationPill.tsx`

Find the props interface (lines 17-31 per exploration). Replace with:

```typescript
export interface CitationPillProps {
  readonly n: number;
  readonly refText: string;
  readonly docId: string;
  readonly page: number;
  /**
   * Optional chunk identifier (#1702). Composite "{docId}_{chunkIndex}".
   * When provided, included in the onClick payload so the orchestrator
   * can push a chunk-level URL `?chunkId=` for deep-link.
   */
  readonly chunkId?: string;
  readonly ariaLabel: string;
  readonly onClick?: (link: { docId: string; page: number; chunkId?: string }) => void;
  readonly className?: string;
}
```

Update the `onClick` handler (line 47 per exploration):

```typescript
onClick={() => onClick?.(chunkId ? { docId, page, chunkId } : { docId, page })}
```

The conditional spread ensures **back-compat**: when `chunkId` is undefined, the emitted payload is exactly `{ docId, page }` (no `chunkId: undefined` key in the object — matches the legacy assertion).

> **Note (review fix NIT):** the falsy check `chunkId ?` ALSO treats empty-string `""` as absent — falls through to the page-only payload. This is intentional (an empty string is not a valid chunkId), and the type interface comment for `chunkId?: string` should mention this:
> ```typescript
> /**
>  * ...When provided as an empty string, treated as absent (page-only payload).
>  */
> readonly chunkId?: string;
> ```

Don't forget to destructure `chunkId` from `props` at the top of the component function.

### Step 4: Run tests

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale/__tests__/CitationPill.test.tsx 2>&1 | tail -10
```

Expected: all existing tests + 2 new tests pass.

### Step 5: Commit

```bash
git add apps/web/src/components/features/kb-globale/CitationPill.tsx apps/web/src/components/features/kb-globale/__tests__/CitationPill.test.tsx
git commit -m "feat(kb-globale): extend CitationPill with optional chunkId (#1702)"
```

---

## Task 3: Forward `chunkId` from `parseCitationMarkers` to rendered pills

**Files:**
- Modify: `apps/web/src/components/features/kb-globale/parseCitationMarkers.tsx`
- Modify: `apps/web/src/components/features/kb-globale/__tests__/parseCitationMarkers.test.tsx`

### Step 1: Read the current parseCitationMarkers to understand its rendering loop

```bash
wc -l apps/web/src/components/features/kb-globale/parseCitationMarkers.tsx
```

Then Read the file. Identify where it instantiates `<CitationPill ...>` for each citation marker. The current code likely passes `docId={citation.docId} page={citation.page}` without `chunkId`. The change: also pass `chunkId={citation.chunkId}` (will be `undefined` for legacy citations, which the back-compat path of Task 2 handles).

### Step 2: Write the failing test

Append to `parseCitationMarkers.test.tsx`:

```typescript
it('forwards chunkId from KbCitation to rendered CitationPill (#1702)', async () => {
  const user = userEvent.setup();
  const onClick = vi.fn();
  const citations = [
    { docId: 'doc-A', source: 'PDF', page: 7, snippet: 'rules', score: 0.9, chunkId: 'doc-A_2', chunkPosition: 2 },
  ];
  const nodes = parseCitationMarkers('Vedi [1] per dettagli', citations, {
    formatAriaLabel: (n, ref) => `Apri citazione ${n}: ${ref}`,
    onCitationClick: onClick,
  });
  render(<>{nodes}</>);
  await user.click(screen.getByRole('button'));
  expect(onClick).toHaveBeenCalledWith({ docId: 'doc-A', page: 7, chunkId: 'doc-A_2' });
});

it('omits chunkId in payload when citation has no chunkId (page-level back-compat)', async () => {
  const user = userEvent.setup();
  const onClick = vi.fn();
  const citations = [
    { docId: 'doc-B', source: 'PDF', page: 3, snippet: 'setup', score: 0.7 },
  ];
  const nodes = parseCitationMarkers('Vedi [1] qui', citations, {
    formatAriaLabel: (n, ref) => `Apri citazione ${n}: ${ref}`,
    onCitationClick: onClick,
  });
  render(<>{nodes}</>);
  await user.click(screen.getByRole('button'));
  expect(onClick).toHaveBeenCalledWith({ docId: 'doc-B', page: 3 });
});
```

(`render` from `@testing-library/react` may need to be imported in this test file if not already.)

### Step 3: Run to confirm failure

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale/__tests__/parseCitationMarkers.test.tsx 2>&1 | tail -10
```

Expected: the first new test fails (payload misses `chunkId: 'doc-A_2'` because `parseCitationMarkers` doesn't forward it yet); the second passes accidentally (back-compat path works because the citation has no chunkId to forward).

### Step 4: Update `parseCitationMarkers.tsx`

Find the `<CitationPill ...>` instantiation. Add the `chunkId` prop:

```typescript
<CitationPill
  key={...}
  n={n}
  refText={...}
  docId={citation.docId}
  page={citation.page}
  chunkId={citation.chunkId}
  ariaLabel={...}
  onClick={onCitationClick}
/>
```

### Step 5: Run tests

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale/__tests__/parseCitationMarkers.test.tsx 2>&1 | tail -10
```

Expected: all tests pass (existing + 2 new).

### Step 6: Commit

```bash
git add apps/web/src/components/features/kb-globale/parseCitationMarkers.tsx apps/web/src/components/features/kb-globale/__tests__/parseCitationMarkers.test.tsx
git commit -m "feat(kb-globale): forward chunkId through parseCitationMarkers (#1702)"
```

---

## Task 4: Wire `DrawerCompleted` to forward `onCitationClick`

**Files:**
- Modify: `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx`
- Modify: `apps/web/src/components/features/kb-globale/__tests__/DrawerCompleted.test.tsx` (or `DrawerShell.test.tsx` — whichever currently tests inline pills)

### Step 1: Read current `DrawerCompleted.tsx`

```bash
wc -l apps/web/src/components/features/kb-globale/DrawerCompleted.tsx
```

Read the file. Identify:
- The current props interface (lines 20-107 per exploration)
- The `parseCitationMarkers` call — **the gap noted in exploration §11** is that `onCitationClick` is NOT being passed today
- Any parent component that passes props to `DrawerCompleted` (likely `DrawerShell`)

### Step 2: Write the failing test

Open `DrawerCompleted.test.tsx` (or `DrawerShell.test.tsx` — pick the one with existing inline-pill rendering tests around lines 294-329 per exploration §6). Add:

```typescript
it('forwards onCitationClick from DrawerCompleted props to inline pills (#1702 wiring fix)', async () => {
  const user = userEvent.setup();
  const onCitationClick = vi.fn();
  const citations = [
    { docId: 'doc-X', source: 'PDF', page: 12, snippet: 'rule', score: 0.9, chunkId: 'doc-X_5', chunkPosition: 5 },
  ];
  render(
    <DrawerCompleted
      text="Risposta con [1] inline"
      citations={citations}
      // ... other required props matching the actual component signature
      onCitationClick={onCitationClick}
    />
  );
  await user.click(screen.getByRole('button', { name: /citazione 1/i }));
  expect(onCitationClick).toHaveBeenCalledWith({ docId: 'doc-X', page: 12, chunkId: 'doc-X_5' });
});
```

(Adapt the required-props list to match the actual `DrawerCompletedProps` interface — read the file first.)

### Step 3: Run to confirm failure

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale/__tests__/DrawerCompleted.test.tsx 2>&1 | tail -10
```

Expected: new test fails (TS error: `onCitationClick` not in props OR runtime error: `onCitationClick` was never called because `parseCitationMarkers` doesn't receive it).

### Step 4: Update `DrawerCompleted.tsx`

Add `onCitationClick?: (link: { docId: string; page: number; chunkId?: string }) => void` to the `DrawerCompletedProps` interface. Inside the component, pass it to `parseCitationMarkers`:

```typescript
const nodes = parseCitationMarkers(text, citations, {
  formatAriaLabel,
  onCitationClick,  // <-- now wired
});
```

### Step 5: Update parent (likely `DrawerShell.tsx`)

`DrawerShell` is the parent that renders `DrawerCompleted`. It needs to ACCEPT `onCitationClick` from its own props and pass it down. Verify the chain: `KbGlobaleView` → `DrawerShell` → `DrawerCompleted` → `parseCitationMarkers` → `CitationPill`.

Open `DrawerShell.tsx`. Add `onCitationClick?: (...) => void` to `DrawerShellProps`. In the JSX where `<DrawerCompleted ...>` is rendered, pass `onCitationClick={onCitationClick}`.

### Step 6: Run tests

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale/__tests__/DrawerCompleted.test.tsx src/components/features/kb-globale/__tests__/DrawerShell.test.tsx 2>&1 | tail -15
```

Expected: all tests pass (existing + new).

### Step 7: Commit

```bash
git add apps/web/src/components/features/kb-globale/DrawerCompleted.tsx apps/web/src/components/features/kb-globale/DrawerShell.tsx apps/web/src/components/features/kb-globale/__tests__/DrawerCompleted.test.tsx
git commit -m "fix(kb-globale): wire onCitationClick through DrawerCompleted to inline pills (#1702)"
```

---

## Task 5: Orchestrator — `KbGlobaleView` URL state + chunk resolution + graceful degrade

**Files:**
- Modify: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`
- Modify: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx`

This is the largest task. Three sub-steps: URL state, openViewer extension, chunk resolution + degrade.

### Step 1: Read current `KbGlobaleView.tsx`

```bash
wc -l apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/KbGlobaleView.tsx
```

Locate (per exploration §3-5):
- URL state reading around lines 195-197: `docIdParam`, `pageParam`
- `openViewer` handler around lines 235-243
- The point where `KbDocViewerDesktop` is rendered with `activePage={pageParam}` (or similar)

### Step 2: Write failing tests

> **🔴 CRITICAL pre-step (review fix MAJOR-2):** the existing `KbGlobaleView.test.tsx` mocks `DrawerShell` as a no-op stub:
> ```typescript
> vi.mock('@/components/features/kb-globale/DrawerShell', () => ({
>   DrawerShell: () => <div data-slot="kb-globale-drawer" data-testid="kb-globale-drawer" />,
> }));
> ```
> This stub ignores ALL props — `onCitationClick` cannot be exercised through a normal click path. **Before writing the new tests, UPGRADE the mock stub to expose `onCitationClick` via a trigger button**:
> ```typescript
> vi.mock('@/components/features/kb-globale/DrawerShell', () => ({
>   DrawerShell: (props: Record<string, unknown>) => (
>     <div data-slot="kb-globale-drawer" data-testid="kb-globale-drawer">
>       <button
>         data-testid="drawer-citation-trigger"
>         onClick={() => {
>           const fn = props['onCitationClick'] as
>             | ((link: { docId: string; page: number; chunkId?: string }) => void)
>             | undefined;
>           // Tests will configure this trigger via a module-level variable;
>           // alternatively, use a more specific contract here.
>           fn?.({ docId: 'doc-1', page: 5, chunkId: 'doc-1_3' });
>         }}
>       >
>         trigger citation
>       </button>
>     </div>
>   ),
> }));
> ```
> A cleaner approach uses a per-test mock — let each test override the payload before clicking. Choose the variant that fits the file's conventions.
>
> The plan does NOT modify the existing `DrawerShell` MOCK in any other test file (e.g., the integration test) unless they hit the same gap. Verify by re-running the affected test files after the update.

> **Mock for `useKbChunkDetail` (review confirmation):** the existing `KbGlobaleView.test.tsx` uses `vi.mock` for all hooks (lines 45-77 of the test file). Match that convention — `vi.mock('@/hooks/queries/useKbChunkDetail', () => ({ useKbChunkDetail: vi.fn() }))` at the top of the file, then `(useKbChunkDetail as Mock).mockReturnValue({ isSuccess: true, data: { pageNumber: 7 }, isError: false })` per-test.

Add three tests inside a new `describe('chunk-level deep-link (#1702)', ...)` block:

```typescript
describe('chunk-level deep-link (#1702)', () => {
  beforeEach(() => {
    (useKbChunkDetail as Mock).mockReset();
  });

  it('pushes ?chunkId= when openViewer is invoked with chunkId', async () => {
    const user = userEvent.setup();
    const pushMock = vi.fn();
    (useRouter as Mock).mockReturnValue({ push: pushMock, replace: vi.fn() });
    (useKbChunkDetail as Mock).mockReturnValue({ isSuccess: false, isError: false, data: undefined });

    render(<KbGlobaleView /* required props */ />);
    await user.click(screen.getByTestId('drawer-citation-trigger'));

    expect(pushMock).toHaveBeenCalledWith(expect.stringContaining('chunkId=doc-1_3'));
    expect(pushMock).toHaveBeenCalledWith(expect.stringContaining('docId=doc-1'));
    expect(pushMock).toHaveBeenCalledWith(expect.stringContaining('page=5'));
  });

  it('navigates to chunk.pageNumber when chunk lookup succeeds (chunk-level happy path)', () => {
    (useSearchParams as Mock).mockReturnValue(
      new URLSearchParams('docId=doc-1&page=5&chunkId=doc-1_3')
    );
    (useKbChunkDetail as Mock).mockReturnValue({
      isSuccess: true,
      isError: false,
      data: { id: 'doc-1_3', docId: 'doc-1', position: 3, pageNumber: 7, content: '...', headingPath: [], prevChunkId: null, nextChunkId: null, metadata: {} },
    });

    render(<KbGlobaleView /* required props */ />);

    // Assert KbDocViewerDesktop received activePage=7 — either via a viewer mock that captures props,
    // or by inspecting the DOM if the viewer renders the page number visibly.
    expect(screen.getByTestId('kb-doc-viewer-desktop')).toHaveAttribute('data-active-page', '7');
  });

  it('falls back to pageParam + console.warn when chunk lookup fails (graceful degrade)', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    (useSearchParams as Mock).mockReturnValue(
      new URLSearchParams('docId=doc-1&page=5&chunkId=stale-id')
    );
    (useKbChunkDetail as Mock).mockReturnValue({
      isSuccess: false,
      isError: true,
      data: undefined,
    });

    render(<KbGlobaleView /* required props */ />);

    expect(screen.getByTestId('kb-doc-viewer-desktop')).toHaveAttribute('data-active-page', '5');
    expect(warnSpy).toHaveBeenCalledWith(
      expect.stringContaining('chunkId "stale-id" not resolvable')
    );
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();  // no error UI

    warnSpy.mockRestore();
  });
});
```

**Notes:**
- If the existing `KbDocViewerDesktop` mock doesn't expose `activePage` via a DOM attribute, you may need to also upgrade THAT mock to capture/expose its `activePage` prop — same pattern as the `DrawerShell` upgrade above.
- `useRouter`, `useSearchParams` casts to `Mock` rely on the existing `vi.mock('next/navigation', ...)` setup in the file. Read the file first.

### Step 3: Run to confirm failure

```bash
cd apps/web && pnpm test --run src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx 2>&1 | tail -15
```

Expected: 3 new tests fail.

### Step 4: Update `KbGlobaleView.tsx`

Three changes:

**(a) URL state reading** — extend lines 195-197:

```typescript
const docIdParam = searchParams.get('docId');
const pageParam = Number(searchParams.get('page')) || 1;
const chunkIdParam = searchParams.get('chunkId');  // NEW
```

**(b) `openViewer` extension** — replace lines 235-243:

```typescript
const openViewer = useCallback(
  (result: { docId: string; page: number; chunkId?: string }) => {
    const params = new URLSearchParams(searchParams.toString());
    params.set('docId', result.docId);
    params.set('page', String(result.page));
    if (result.chunkId) {
      params.set('chunkId', result.chunkId);
    } else {
      params.delete('chunkId');  // ensure stale chunkId is cleared on plain page navigation
    }
    router.push(`/knowledge-base/global?${params.toString()}`);
  },
  [router, searchParams]
);
```

**(c) Chunk resolution + graceful degrade (review fix MAJOR-1):** add a `useKbChunkDetail` call + page resolution. **`useMemo` must be pure — `console.warn` MUST live in a `useEffect`, not inside the memo body** (React 19 Strict Mode double-invokes memos during development and will fire the warn twice; more importantly, render-time side effects are a React anti-pattern).

```typescript
// Import at top:
import { useKbChunkDetail } from '@/hooks/queries/useKbChunkDetail';
// (useEffect should already be imported from 'react')

// Inside the component, AFTER reading URL params:
const chunkQuery = useKbChunkDetail({
  docId: docIdParam,
  chunkId: chunkIdParam,
  enabled: docIdParam != null && chunkIdParam != null,
});

// Pure derived value — uses chunk's pageNumber when available, falls back to URL pageParam otherwise.
const resolvedPage = useMemo(() => {
  if (chunkQuery.isSuccess && chunkQuery.data?.pageNumber != null) {
    return chunkQuery.data.pageNumber;
  }
  return pageParam;
}, [chunkQuery.isSuccess, chunkQuery.data?.pageNumber, pageParam]);

// Side effect: emit the graceful-degrade warning when (and only when) chunk lookup fails.
useEffect(() => {
  if (chunkQuery.isError && chunkIdParam) {
    console.warn(
      `[KbGlobaleView] chunkId "${chunkIdParam}" not resolvable; falling back to page-level scroll (page=${pageParam}).`
    );
  }
}, [chunkQuery.isError, chunkIdParam, pageParam]);

// When rendering KbDocViewerDesktop, pass activePage={resolvedPage} (instead of pageParam)
```

**(d) Wire `onCitationClick`** — find where `DrawerShell` is rendered and pass the prop:

```typescript
<DrawerShell
  // ... existing props
  onCitationClick={openViewer}
/>
```

**(e) Clear stale `chunkId` on viewer close (review fix MINOR):** the existing `closeViewer` (around lines 245-251) currently only deletes `docId` and `page`. Add `params.delete('chunkId')` so a stale chunkId doesn't carry over when the user closes the viewer and later opens a different citation that has no chunkId:

```typescript
const closeViewer = useCallback(() => {
  const params = new URLSearchParams(searchParams.toString());
  params.delete('docId');
  params.delete('page');
  params.delete('chunkId');  // NEW — prevent stale chunkId persistence
  router.push(`/knowledge-base/global?${params.toString()}`);
}, [router, searchParams]);
```

### Step 5: Run tests

```bash
cd apps/web && pnpm test --run src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx 2>&1 | tail -15
```

Expected: all tests pass.

**Common pitfalls:**

- `console.warn` spy: ensure to spy BEFORE rendering, restore in `afterEach`. Pattern: `const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});` ... `expect(warnSpy).toHaveBeenCalledWith(...)`.
- `useKbChunkDetail` mock: if the existing test uses TanStack Query directly, you may need to wrap in a `QueryClientProvider` and pre-populate via `queryClient.setQueryData(['kb-doc-chunk', docId, chunkId], { pageNumber: 7 })`. Otherwise, use `vi.mock('@/hooks/queries/useKbChunkDetail', () => ({ useKbChunkDetail: vi.fn() }))` and configure per-test return values.

### Step 6: Run full kb-globale suite to confirm no regression

```bash
cd apps/web && pnpm test --run src/components/features/kb-globale src/app/\(authenticated\)/knowledge-base 2>&1 | tail -10
```

Expected: all green.

### Step 7: Commit

```bash
git add apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/KbGlobaleView.tsx apps/web/src/app/\(authenticated\)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx
git commit -m "feat(kb-globale): chunk-level URL deep-link with graceful degrade (#1702)"
```

---

## Task 6: Final verification — full FE suite + typecheck + lint

- [ ] **Step 1: Full FE test suite**

```bash
cd apps/web && pnpm test --run 2>&1 | tail -10
```

Expected: count = baseline + new tests. Allowed pre-existing failures:
- `__tests__/bundle-size.test.ts` (requires `pnpm build` first; ignore unless touching bundles)

If any other tests fail, investigate before proceeding.

- [ ] **Step 2: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | tail -5
```

Expected: 0 errors.

- [ ] **Step 3: Lint**

```bash
cd apps/web && pnpm lint 2>&1 | tail -5
```

Expected: 0 errors (warnings OK if pre-existing).

- [ ] **Step 4: Verify no BE file was accidentally touched**

```bash
git diff main-dev --stat -- 'apps/api/' 2>&1
```

Expected: empty.

---

## Task 7: Push + PR + Close #1702

- [ ] **Step 1: Push**

```bash
git push -u origin feature/issue-1702-fe-citation-chunk-deep-link
```

- [ ] **Step 2: Open PR targeting `main-dev`** — body should explicitly close #1702 (since this completes the 7 remaining FE AC and there is no further follow-up tracked):

```bash
gh pr create --base main-dev --title "feat(kb-globale): #1702 FE — chunk-level citation deep-link (consumer follow-up)" --body "$(cat <<'EOF'
## Summary

**Closes #1702.** Consumes the BE chunkId/chunkPosition fields shipped in PR #1713 (squash 55741f716). Completes the 7 remaining FE AC of the original spec-panel decision (D-2 graceful degrade preserved).

## Changes

1. **`KbCitationSchema`** extended with optional `chunkId?: string` + `chunkPosition?: number` (backward-compat — old payloads still parse cleanly).
2. **`CitationPill`** accepts `chunkId?: string` prop; emits `onClick({ docId, page, chunkId? })`. Back-compat: payload is exactly `{ docId, page }` when chunkId absent.
3. **`parseCitationMarkers`** forwards `citation.chunkId` to each rendered `<CitationPill>`.
4. **`DrawerCompleted` + `DrawerShell`** wire `onCitationClick` through to inline pills (fixes the **pre-existing wiring gap** noted in exploration — Phase 2 shipped pill rendering but never connected the click handler).
5. **`KbGlobaleView` orchestrator**:
   - URL state extended with `chunkIdParam`
   - `openViewer` accepts `chunkId?` and pushes `?chunkId=` when present
   - Calls `useKbChunkDetail` to resolve `chunkId` → actual `pageNumber`
   - Graceful degrade: chunk lookup fails → falls back to `pageParam` + `console.warn` (DD-3); no error UI

## Design decisions

- **DD-1** Page-level resolution via chunk lookup (not sub-page highlight). `useKbChunkDetail` resolves `chunkId` → `pageNumber`; viewer remains presentational. Sub-page highlight is a separate UX investment.
- **DD-2** Schema fields optional + passthrough-tolerant — back-compat with per-game endpoints that never set them.
- **DD-3** `console.warn` (not structured `logger.warn`) for stale-chunkId fallback — matches the `parseCitationMarkers` convention for non-critical LLM noise.
- **DD-4** Orchestrator-level resolution (`KbGlobaleView`), not viewer-level — preserves URL-as-source-of-truth invariant.

## Test coverage

- Schema: 5 unit tests (absent fields / both / partial / negative / non-integer)
- CitationPill: 2 new tests (with chunkId / without — back-compat)
- parseCitationMarkers: 2 new tests (chunkId forwarded / omitted)
- DrawerCompleted wiring: 1 new test (onCitationClick reaches inline pills)
- KbGlobaleView orchestrator: 3 new tests (URL push includes chunkId / chunk-level happy path / graceful degrade with console.warn spy)

## G/W/T scenarios (panel D-3 mapping)

**Scenario 1 — BE wire format (covered by BE PR #1713):** already merged.

**Scenario 2 — FE happy path:**
> Given citation #2 with chunkId="abc-123" and page=14
> When user clicks CitationPill n=2
> Then FE pushes URL ?docId=...&page=14&chunkId=abc-123
> And KbDocViewerDesktop scrolls to chunk's actual page (may differ from URL's page if chunk on different page)
✅ Covered.

**Scenario 3 — Graceful degradation:**
> Given user lands on /knowledge-base/global?docId=X&page=14&chunkId=stale-id
> When KbDocViewerDesktop mounts and chunk lookup fails
> Then viewer scrolls to page 14 (page-level fallback)
> And console.warn logs "chunkId stale-id not resolvable, degraded to page-level scroll"
> And no error UI shown to user
✅ Covered.

## Verification

- ✅ Full FE test suite: pre-merge baseline + N new (record exact counts in commit)
- ✅ `pnpm typecheck` — 0 errors
- ✅ `pnpm lint` — 0 errors
- ✅ `git diff main-dev --stat -- apps/api/` — empty (FE-only PR)

## Plan

`docs/superpowers/plans/2026-05-31-1702-fe-citation-chunk-deep-link.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Monitor CI**. Required check: `GitGuardian Security Checks`. Advisory: Frontend Fast, Frontend Tests shards, CodeQL, Lychee, etc. If all required pass, merge normally (`gh pr merge <PR> --squash --delete-branch`).

- [ ] **Step 4: Post-merge — verify #1702 auto-closed** (PR body says "Closes #1702"):

```bash
gh issue view 1702 --json state
```

Expected: `{"state":"CLOSED"}`. If not auto-closed, manually close with a closing comment summarizing the BE+FE completion (PR #1713 + this PR).

---

## Self-Review Checklist

- [x] **Spec coverage** — each panel decision D-1..D-5 (from issue #1702 spec-panel comment) has a task:
  - D-1 extend `Snippet` (BE-only) → already merged in #1713
  - D-2 FE graceful degrade → Task 5 (KbGlobaleView console.warn + page-level fallback) + test in Task 5 Step 2
  - D-3 G/W/T scenarios → Task 5 covers scenarios 2 (happy path) + 3 (degrade); scenario 1 already covered by BE PR
  - D-4 BC snapshot test → BE-only concern, already covered by #1713
  - D-5 BE PR-1 first, FE PR-2 separate → this PR is the separate FE PR-2 ✅
- [x] **Placeholder scan** — no TBD / TODO; one informed decision left to executor (`DrawerCompleted.test.tsx` props match — read file first)
- [x] **Type consistency** — `chunkId: string`, `chunkPosition: number` (Zod `int().nonnegative().optional()`); payload shape `{ docId, page, chunkId? }` consistent across Tasks 2, 3, 4, 5
- [x] **No BE touched** — Task 6 Step 4 explicit check
- [x] **Closes #1702 explicit in PR body** — Task 7 Step 2 + Step 4

### Review fixes applied (after first-pass review 2026-05-31, APPROVED_WITH_NOTES, confidence 0.88)

| ID | Severity | Status | Fix location |
|---|---|---|---|
| MAJOR-1 | MAJOR | ✅ Fixed | Task 5 Step 4(c) — `console.warn` extracted from `useMemo` into a dedicated `useEffect` (React 19 Strict Mode safety) |
| MAJOR-2 | MAJOR | ✅ Fixed | Task 5 Step 2 — explicit pre-step to upgrade the `DrawerShell` no-op mock to capture and expose `onCitationClick` via a trigger button + `vi.mock('@/hooks/queries/useKbChunkDetail', ...)` mock pattern documented |
| MINOR | MINOR | ✅ Fixed | Task 5 Step 4(e) — `closeViewer` clears stale `chunkId` from URL |
| NIT | NIT | ✅ Fixed | Task 2 Step 3 — JSDoc on `chunkId?: string` notes empty-string treated as absent |
