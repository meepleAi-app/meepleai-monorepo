# KB Inline Citation Markers — FE PR-2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the **frontend** of issue [#1703](https://github.com/meepleAi-app/meepleai-monorepo/issues/1703) — add a pure utility `parseCitationMarkers(text, citations) → ReactNode[]` that swaps the existing numbered-list-below rendering in `DrawerCompleted` for inline `<CitationPill>` rendering when the LLM emits `[N]` markers, with graceful fallback to the existing list-below format when markers are absent or malformed.

**Architecture:** A single pure function lives in a new utility module `apps/web/src/lib/citations/parseCitationMarkers.ts`. It accepts a text string + readonly `KbCitation[]` and returns a `ReactNode[]` segment array. `DrawerCompleted` consumes it via a small adapter: if the parser yields any pill nodes, render inline; otherwise render the current numbered list as fallback. Mobile viewer citations tab and the `KbDocViewerDesktop` citations panel remain untouched (those are list-views by design; inline rendering only applies to the answer-prose surface in the drawer).

**Tech Stack:**
- React 19 + TypeScript strict (no `any`)
- Vitest + React Testing Library + jest-axe (Phase 2 PR #1701 pattern)
- DS-15 semantic tokens (enforced by `pnpm lint:tokens`)
- Existing `CitationPill` from PR #1701 (props: `{ n, refText, docId, page, ariaLabel, onClick?, className? }`)
- Existing `KbCitation` schema (`{ docId, source, page, snippet, score }`) from `apps/web/src/lib/api/schemas/kb-ask.schemas.ts`

**Plan-source spec:** spec-panel iteration 2 (2026-05-30) decision **D-1703-D** + 14 test scenarios + Crispin §1.6 (FE tests 6-9) + Nygard §1.5 (warn dedup + malformed input handling).

---

## §0 — Pre-flight (engineer reads before Task 1)

| Item | Source | Why this matters |
|---|---|---|
| BE PR-1 #1707 — merge status | `gh pr view 1707` | PR-2 should ideally land AFTER BE merges so the LLM actually emits `[N]` markers; however the parser + fallback are independently shippable (fallback gracefully when no markers) |
| `KbCitation` schema shape | `apps/web/src/lib/api/schemas/kb-ask.schemas.ts:24-31` | `{ docId, source, page, snippet, score }` — `source` and `docId` are both strings (BE Snippet quirk); the parser uses `docId` |
| `CitationPill` props | `apps/web/src/components/features/kb-globale/CitationPill.tsx:17-31` | `n`, `refText`, `docId`, `page`, `ariaLabel`, optional `onClick({docId, page})` + `className` |
| `DrawerCompleted` current shape | `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx` | Renders `<ol>` numbered list below answer (D-F fallback shipped by PR #1701 fix `dd5a1eb47`) |
| `DrawerCompleted` test location | `apps/web/src/components/features/kb-globale/__tests__/DrawerShell.test.tsx` | DrawerCompleted is tested inside DrawerShell.test.tsx — no standalone file |
| `KbCitation.page` is `number().int().nonnegative()` | `kb-ask.schemas.ts:27` | Page may be 0 (rare but valid); the pill `refText` builder must handle it |
| `Cross-game` citation source field | `kb-ask.schemas.ts:26-27` | `source = PdfDocumentId` string; FE has been using `docId` everywhere — confirm Pill prop is `docId` (it is, verified) |
| Existing FE utility patterns | `apps/web/src/lib/` — has `card-mappers/`, `agents/`, etc. | Convention: feature-folder + index file + colocated tests. The `citations/` folder is greenfield. |

**P74 verifications already done (do not re-verify)**:
- ✅ `apps/web/src/components/features/kb-globale/CitationPill.tsx` exists with the exact props shown above
- ✅ `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx` exists; renders the numbered `<ol>` D-F fallback
- ✅ `apps/web/src/lib/api/schemas/kb-ask.schemas.ts` exports `KbCitation` and `KbCitationSchema`
- ✅ `apps/web/src/lib/citations/` does NOT yet exist (greenfield directory for this PR)

**Git workflow**:
- Parent branch: `main-dev`
- Feature branch: `feature/issue-1703-fe-inline-citation-parser`
- Target PR base: `main-dev`
- Commit messages: `feat(kb-globale): #1703 Task N — <description>` for impl, `test(kb-globale)` for test-only

**Critical UX invariant (D-F fallback semantics)**:
- When the LLM did NOT emit any `[N]` marker (e.g. older messages, model ignored the instruction, BE not yet merged) → keep rendering the numbered list below the answer **exactly as today**.
- Never show BOTH inline pills AND the list-below — pick one based on whether the parser found markers.
- This is a UX-safety invariant: regression here = visual duplication.

---

## §1 — File structure

### Create

```
apps/web/src/lib/citations/
├── parseCitationMarkers.ts                    # Task 2: pure utility (D-1703-D core)
├── index.ts                                   # Task 2: barrel re-export
└── __tests__/
    └── parseCitationMarkers.test.tsx          # Task 1: 14 test scenarios (RED first)
```

(Note: test file is `.tsx` because some tests render React elements via `react-dom/server` for assertion.)

### Modify

```
apps/web/src/components/features/kb-globale/
└── DrawerCompleted.tsx                        # Task 3: integrate parser + fallback gate

apps/web/src/components/features/kb-globale/__tests__/
└── DrawerShell.test.tsx                       # Task 4: extend "renders completed" test for inline + fallback
```

### Out of scope (do NOT touch)

- `CitationPill.tsx` — already correct, props match exactly (D-E from Phase 2)
- `KbDocViewerDesktop.tsx` citations panel — that's a list-view by design (mockup `sp4-kb-globale.jsx:1196-1280`); inline rendering is drawer-only
- `KbDocViewerMobile.tsx` citations tab — same as above; remains list-view
- `kb-ask.schemas.ts` — no schema change (page-level deep-link unchanged; chunkId tracked in #1702 follow-up)
- Mobile-specific testing — desktop drawer is the only consumer in this PR

---

## §2 — Frozen decisions (spec-panel iteration 2 + Crispin/Nygard refinements)

| # | Decision | Where |
|---|---|---|
| **D-1703-D core** | Pure utility `parseCitationMarkers(text, citations): ReactNode[]` at `apps/web/src/lib/citations/parseCitationMarkers.ts` | Task 2 |
| **regex (strict)** | `/\[(\d+(?:,\s*\d+)*)\]/g` — digit-only inside brackets; excludes `[ref:...]` (has colon) and `[markdown](url)` (has paren) | Task 2 |
| **warn dedup** | maintain per-call `Set<number>` of warned indices; `console.warn` fires at most once per invalid index per parse call | Task 2 |
| **CitationPill onClick prop** | parser accepts an optional `onCitationClick?: (link: {docId, page}) => void` and forwards to each rendered pill | Task 2 |
| **i18n-aware aria-label** | parser accepts `formatAriaLabel: (c, n) => string` (caller supplies; allows i18n) | Task 2 |
| **DrawerCompleted integration** | call parser on `text` + `citations`; if **any** pill node returned, render parser output (inline); else render existing numbered `<ol>` (fallback). NEVER both. | Task 3 |
| **No regression on existing D-F fallback** | Test #11 in DrawerShell.test.tsx (the "renders completed" test) keeps its existing assertions when input has no markers | Task 4 |
| **Mobile UX unchanged** | this PR touches only DrawerCompleted; KbDocViewerMobile citations tab remains list-view | n/a |

---

## §3 — Task 0: Pre-flight setup

**Files:**
- Read: `apps/web/src/components/features/kb-globale/CitationPill.tsx`
- Read: `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx`
- Read: `apps/web/src/lib/api/schemas/kb-ask.schemas.ts`

- [ ] **Step 1: Sync main-dev + create feature branch**

```bash
git checkout main-dev
git pull --ff-only
git branch --show-current  # MUST print: main-dev
git status                 # MUST be clean
git checkout -b feature/issue-1703-fe-inline-citation-parser
git config branch.feature/issue-1703-fe-inline-citation-parser.parent main-dev
```

Expected: clean tree, new branch active.

- [ ] **Step 2: Verify the 3 contract sources**

Read and confirm exact shapes:

1. `KbCitation` from `apps/web/src/lib/api/schemas/kb-ask.schemas.ts:24-31`:
   ```ts
   export const KbCitationSchema = z.object({
     docId: z.string(),
     source: z.string(),
     page: z.number().int().nonnegative(),
     snippet: z.string(),
     score: z.number(),
   });
   export type KbCitation = z.infer<typeof KbCitationSchema>;
   ```

2. `CitationPillProps` from `apps/web/src/components/features/kb-globale/CitationPill.tsx:17-31`:
   ```ts
   export interface CitationPillProps {
     n: number;
     refText: string;
     docId: string;
     page: number;
     ariaLabel: string;
     onClick?: (link: { docId: string; page: number }) => void;
     className?: string;
   }
   ```

3. `DrawerCompleted` current rendering: `<ol>` numbered list with `<li data-citation-index={idx+1} data-citation-page={c.page}>` — this stays as the fallback.

No commit; this is read-only verification.

---

## §4 — Task 1: Write 14 failing tests for parseCitationMarkers

**Files:**
- Create: `apps/web/src/lib/citations/__tests__/parseCitationMarkers.test.tsx`

These tests will not compile until Task 2 creates the module.

- [ ] **Step 1: Create the failing test file**

Create `apps/web/src/lib/citations/__tests__/parseCitationMarkers.test.tsx`:

```tsx
/**
 * parseCitationMarkers — 14 test scenarios per spec-panel D-1703-D (2026-05-30).
 *
 * Strict digit-only regex /\[(\d+(?:,\s*\d+)*)\]/g.
 * Coexists with [ref:docId:page] copyright markers (has colon → excluded)
 * and [text](url) markdown links (has paren → excluded).
 */
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render } from '@testing-library/react';
import { isValidElement, type ReactNode } from 'react';

import type { KbCitation } from '@/lib/api/schemas/kb-ask.schemas';

import { parseCitationMarkers } from '../parseCitationMarkers';

const cite = (docId: string, page: number, snippet = 'snippet'): KbCitation => ({
  docId,
  source: docId,
  page,
  snippet,
  score: 0.9,
});

const formatAriaLabel = (c: KbCitation, n: number): string =>
  `Citazione ${n}, documento ${c.docId}, pagina ${c.page}`;

function renderNodes(nodes: ReactNode[]): HTMLElement {
  // Wrap returned nodes in a fragment-like container so RTL can render them.
  const { container } = render(<>{nodes}</>);
  return container;
}

function countPills(container: HTMLElement): number {
  return container.querySelectorAll('[data-slot="kb-globale-citation-pill"]').length;
}

describe('parseCitationMarkers (D-1703-D)', () => {
  let warnSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterEach(() => {
    warnSpy.mockRestore();
  });

  // ── Scenario 1: happy path ─────────────────────────────────────────────
  it('1. renders inline pills for "Vedi [1] e [2]" with 2 citations', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('Vedi [1] e [2]', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2);
    expect(container.textContent).toContain('Vedi ');
    expect(container.textContent).toContain(' e ');
    expect(warnSpy).not.toHaveBeenCalled();
  });

  // ── Scenario 2: [0] is invalid (1-based) ───────────────────────────────
  it('2. [0] is rendered as literal text + warns (1-based invariant)', () => {
    const nodes = parseCitationMarkers('See [0] here', [cite('d', 1)], { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0);
    expect(container.textContent).toBe('See [0] here');
    expect(warnSpy).toHaveBeenCalledTimes(1);
  });

  // ── Scenario 3: N > citations.length is invalid ────────────────────────
  it('3. [N] where N exceeds citations.length renders as literal + warns', () => {
    const nodes = parseCitationMarkers('See [5] here', [cite('d', 1)], { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0);
    expect(container.textContent).toBe('See [5] here');
    expect(warnSpy).toHaveBeenCalledTimes(1);
  });

  // ── Scenario 4: [N,M] both valid renders 2 pills back-to-back ─────────
  it('4. [N,M] valid mixed renders 2 pills back-to-back', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('Sources [1,2] confirm', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2);
    expect(container.textContent).toContain('Sources ');
    expect(container.textContent).toContain(' confirm');
    // Pills should appear adjacent (no separator between them)
    const pills = container.querySelectorAll('[data-slot="kb-globale-citation-pill"]');
    expect(pills[0].nextSibling).toBe(pills[1]);
  });

  // ── Scenario 5: [1,99,2] mixed valid/invalid — render valid only ──────
  it('5. [1,99,2] mixed valid/invalid skips invalid, renders valid', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('See [1,99,2]', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2); // pills for 1 and 2; 99 skipped
    expect(warnSpy).toHaveBeenCalledTimes(1); // warn fires for [99]
  });

  // ── Scenario 6: [ref:docId:page] copyright marker is preserved ────────
  it('6. [ref:abc:14] copyright marker is rendered as literal text (excluded by regex)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers(
      'Per [ref:abc:14], vedi [1].',
      citations,
      { formatAriaLabel },
    );

    const container = renderNodes(nodes);
    expect(container.textContent).toContain('[ref:abc:14]');
    expect(countPills(container)).toBe(1); // only [1] becomes a pill
    expect(warnSpy).not.toHaveBeenCalled();
  });

  // ── Scenario 7: [text](url) markdown link preserved ───────────────────
  it('7. [link](url) markdown link is rendered as literal text', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers(
      'Vedi [pagina ufficiale](https://example.com) e [1].',
      citations,
      { formatAriaLabel },
    );

    const container = renderNodes(nodes);
    expect(container.textContent).toContain('[pagina ufficiale](https://example.com)');
    expect(countPills(container)).toBe(1);
    expect(warnSpy).not.toHaveBeenCalled();
  });

  // ── Scenario 8: empty input ───────────────────────────────────────────
  it('8. empty input "" returns a single empty text node array', () => {
    const nodes = parseCitationMarkers('', [], { formatAriaLabel });

    expect(nodes).toHaveLength(1);
    expect(nodes[0]).toBe('');
  });

  // ── Scenario 9: no markers in text ────────────────────────────────────
  it('9. text without any markers returns the original text as a single node', () => {
    const text = 'La classe Scout inizia con tre abilità signature.';
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers(text, citations, { formatAriaLabel });

    expect(nodes).toHaveLength(1);
    expect(nodes[0]).toBe(text);
  });

  // ── Scenario 10: markers at start and end of text ─────────────────────
  it('10. markers at start and end are rendered correctly', () => {
    const citations = [cite('doc-1', 14)];
    const nodes1 = parseCitationMarkers('[1] inizia il testo', citations, { formatAriaLabel });
    const nodes2 = parseCitationMarkers('il testo finisce [1]', citations, { formatAriaLabel });

    const c1 = renderNodes(nodes1);
    const c2 = renderNodes(nodes2);
    expect(countPills(c1)).toBe(1);
    expect(c1.textContent?.endsWith(' inizia il testo')).toBe(true);
    expect(countPills(c2)).toBe(1);
    expect(c2.textContent?.startsWith('il testo finisce ')).toBe(true);
  });

  // ── Scenario 11: Italian diacritics preserved with markers ────────────
  it('11. Italian diacritics around markers are preserved (à, è, ù, ò)', () => {
    const text = 'La regolarità delle abilità è confermata [1], però la durabilità no [2].';
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers(text, citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(container.textContent).toContain('regolarità');
    expect(container.textContent).toContain('abilità');
    expect(container.textContent).toContain('però');
    expect(countPills(container)).toBe(2);
  });

  // ── Scenario 12: adjacent markers [1][2] render 2 pills no separator ──
  it('12. adjacent [1][2] (no space between) renders 2 pills with no text separator', () => {
    const citations = [cite('doc-1', 14), cite('doc-2', 21)];
    const nodes = parseCitationMarkers('Sources [1][2] confirm', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(2);
    const pills = container.querySelectorAll('[data-slot="kb-globale-citation-pill"]');
    expect(pills[0].nextSibling).toBe(pills[1]); // no text between
  });

  // ── Scenario 13: repeated [1][1][1] renders 3 pills + warns ONCE ──────
  it('13. repeated valid [1][1][1] renders 3 pills (no warn — index is valid)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('See [1][1][1] here', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(3);
    expect(warnSpy).not.toHaveBeenCalled(); // valid index, no warning
  });

  // ── Scenario 13b: repeated INVALID [99][99][99] warns ONCE (dedup) ────
  it('13b. repeated invalid [99][99][99] warns only ONCE (warn dedup per call)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('Bad [99][99][99]', citations, { formatAriaLabel });

    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0);
    expect(container.textContent).toBe('Bad [99][99][99]');
    expect(warnSpy).toHaveBeenCalledTimes(1); // dedup: warn fires once per invalid index per call
  });

  // ── Scenario 14: malformed inputs do not crash ────────────────────────
  it('14. malformed "[1 unclosed" + unicode "[１]" pass through as literals, no crash', () => {
    const citations = [cite('doc-1', 14)];
    expect(() =>
      parseCitationMarkers('Vedi [1 e [１]', citations, { formatAriaLabel }),
    ).not.toThrow();

    const nodes = parseCitationMarkers('Vedi [1 e [１]', citations, { formatAriaLabel });
    const container = renderNodes(nodes);
    expect(countPills(container)).toBe(0); // no closing bracket on "[1"; "[１]" is unicode not ASCII
    expect(container.textContent).toContain('[1');
    expect(container.textContent).toContain('[１]');
    expect(warnSpy).not.toHaveBeenCalled(); // LLM noise — silent skip
  });

  // ── Type contract: returned array nodes are strings or ReactElements ──
  it('returned array consists only of strings (text) and ReactElements (pills)', () => {
    const citations = [cite('doc-1', 14)];
    const nodes = parseCitationMarkers('Hello [1] world', citations, { formatAriaLabel });

    for (const node of nodes) {
      const isText = typeof node === 'string';
      const isElement = isValidElement(node);
      expect(isText || isElement).toBe(true);
    }
  });

  // ── onCitationClick forwarding ─────────────────────────────────────────
  it('forwards onCitationClick to each rendered pill', () => {
    const onClick = vi.fn();
    const citations = [cite('doc-x', 7)];
    const nodes = parseCitationMarkers('Vedi [1]', citations, {
      formatAriaLabel,
      onCitationClick: onClick,
    });
    const container = renderNodes(nodes);
    const pill = container.querySelector('[data-slot="kb-globale-citation-pill"]');
    expect(pill).not.toBeNull();
    (pill as HTMLButtonElement).click();
    expect(onClick).toHaveBeenCalledWith({ docId: 'doc-x', page: 7 });
  });
});
```

- [ ] **Step 2: Run tests to verify they FAIL**

```bash
cd apps/web && pnpm test src/lib/citations/__tests__/parseCitationMarkers.test.tsx --run 2>&1 | tail -15
```

Expected: **build failure** — `Cannot find module '../parseCitationMarkers'`. This is the correct TDD RED state.

- [ ] **Step 3: Commit the failing tests**

```bash
git add apps/web/src/lib/citations/__tests__/parseCitationMarkers.test.tsx
git commit -m "test(kb-globale): #1703 Task 1 — failing tests for parseCitationMarkers (D-1703-D)

- 14+2 test scenarios per spec-panel D-1703-D (2026-05-30):
  - Scenario 1: happy path inline pills
  - Scenarios 2-3: invalid [0] and [N>len] render as literal + warn
  - Scenarios 4-5: [N,M] valid + mixed valid/invalid
  - Scenarios 6-7: [ref:docId:page] copyright marker and [link](url) preserved
  - Scenarios 8-9: empty input + no-markers identity
  - Scenarios 10-11: boundary positions + Italian diacritics
  - Scenarios 12-13: adjacent + repeated markers (valid + invalid dedup)
  - Scenario 14: malformed [1 + unicode [１] silently pass through (LLM noise)
  - Type contract + onCitationClick forwarding
- All RED until Task 2 implements parseCitationMarkers"
```

---

## §5 — Task 2: Implement parseCitationMarkers utility

**Files:**
- Create: `apps/web/src/lib/citations/parseCitationMarkers.ts`
- Create: `apps/web/src/lib/citations/index.ts`

- [ ] **Step 1: Create the parser module**

Create `apps/web/src/lib/citations/parseCitationMarkers.ts`:

```ts
/**
 * parseCitationMarkers — pure utility for inline citation pill rendering.
 *
 * Splits answer text on the strict pattern /\[(\d+(?:,\s*\d+)*)\]/g and
 * substitutes valid 1-based indices with <CitationPill> elements.
 *
 * Invalid indices ([0] or N > citations.length) render as literal text + warn
 * (deduplicated per call). Markdown links [text](url) and copyright markers
 * [ref:docId:page] are excluded by the strict regex (they contain non-digit
 * characters inside the brackets).
 *
 * Issue #1703 D-1703-D (spec-panel 2026-05-30).
 *
 * @see apps/web/src/components/features/kb-globale/CitationPill.tsx
 * @see apps/web/src/lib/api/schemas/kb-ask.schemas.ts (KbCitation)
 */
import { type ReactNode } from 'react';

import { CitationPill } from '@/components/features/kb-globale/CitationPill';
import type { KbCitation } from '@/lib/api/schemas/kb-ask.schemas';

const MARKER_REGEX = /\[(\d+(?:,\s*\d+)*)\]/g;

export interface ParseCitationMarkersOptions {
  /** Builds an i18n-aware aria-label for each pill. */
  formatAriaLabel: (citation: KbCitation, n: number) => string;
  /** Optional click handler forwarded to each pill. */
  onCitationClick?: (link: { docId: string; page: number }) => void;
  /**
   * Optional ref-text builder. Default: `p.{page}`.
   * Override for custom display (e.g. localized "Pagina {n}").
   */
  formatRefText?: (citation: KbCitation, n: number) => string;
}

/**
 * Parses inline `[N]` and `[N,M,...]` markers in answer text and returns an
 * array of React nodes: strings for free text and `<CitationPill>` elements
 * for valid indices. Invalid indices remain as literal text and emit a
 * deduplicated `console.warn`.
 */
export function parseCitationMarkers(
  text: string,
  citations: readonly KbCitation[],
  options: ParseCitationMarkersOptions,
): ReactNode[] {
  if (text.length === 0) {
    return [''];
  }

  const { formatAriaLabel, onCitationClick, formatRefText } = options;
  const refText = formatRefText ?? ((c: KbCitation) => `p.${c.page}`);

  const nodes: ReactNode[] = [];
  const warnedIndices = new Set<number>();
  let lastIndex = 0;
  let pillKey = 0;

  // Iterate matches of the strict regex.
  for (const match of text.matchAll(MARKER_REGEX)) {
    const matchStart = match.index;
    if (matchStart === undefined) continue;

    // Emit any free text accumulated since the last match.
    if (matchStart > lastIndex) {
      nodes.push(text.slice(lastIndex, matchStart));
    }

    // Parse the indices inside the brackets (e.g. "1" or "1,2,3").
    const inner = match[1] ?? '';
    const indices = inner.split(',').map(s => Number.parseInt(s.trim(), 10));

    const validPillsForThisMatch: ReactNode[] = [];
    for (const n of indices) {
      if (!Number.isInteger(n) || n < 1 || n > citations.length) {
        if (!warnedIndices.has(n)) {
          // eslint-disable-next-line no-console
          console.warn(
            `[parseCitationMarkers] Citation marker [${n}] out of bounds (${citations.length} citations)`,
          );
          warnedIndices.add(n);
        }
        continue;
      }
      const citation = citations[n - 1];
      if (citation === undefined) continue;
      validPillsForThisMatch.push(
        <CitationPill
          key={`citation-pill-${pillKey++}-${n}`}
          n={n}
          refText={refText(citation, n)}
          docId={citation.docId}
          page={citation.page}
          ariaLabel={formatAriaLabel(citation, n)}
          onClick={onCitationClick}
        />,
      );
    }

    if (validPillsForThisMatch.length === indices.length && validPillsForThisMatch.length > 0) {
      // All indices in this match were valid → emit pills.
      nodes.push(...validPillsForThisMatch);
    } else if (validPillsForThisMatch.length > 0) {
      // Mixed valid/invalid — emit only the valid pills (D-1703-D Scenario 5).
      nodes.push(...validPillsForThisMatch);
    } else {
      // All indices invalid → render the original match as literal text.
      nodes.push(match[0]);
    }

    lastIndex = matchStart + match[0].length;
  }

  // Trailing free text after the last match (or full text if no matches).
  if (lastIndex < text.length) {
    nodes.push(text.slice(lastIndex));
  }

  // Empty array would break consumers that map over nodes; guarantee ≥ 1 node.
  if (nodes.length === 0) {
    nodes.push(text);
  }

  return nodes;
}
```

Note: this file is `.ts` not `.tsx` despite returning JSX — the `<CitationPill>` element creation works because `.ts` files in this project compile JSX (next.js + tsconfig allow it). If the build fails with a JSX-in-ts error, rename to `.tsx`.

Actually, **rename to `.tsx`** to be safe. The file path becomes `apps/web/src/lib/citations/parseCitationMarkers.tsx`.

- [ ] **Step 2: Create barrel index**

Create `apps/web/src/lib/citations/index.ts`:

```ts
export { parseCitationMarkers } from './parseCitationMarkers';
export type { ParseCitationMarkersOptions } from './parseCitationMarkers';
```

- [ ] **Step 3: Update test file import to use the new path**

Open `apps/web/src/lib/citations/__tests__/parseCitationMarkers.test.tsx` and verify the import:

```tsx
import { parseCitationMarkers } from '../parseCitationMarkers';
```

If you renamed the parser file to `.tsx`, the import string stays `../parseCitationMarkers` (no extension needed). No change required.

- [ ] **Step 4: Run all 16 tests — expect GREEN**

```bash
cd apps/web && pnpm test src/lib/citations/__tests__/parseCitationMarkers.test.tsx --run 2>&1 | tail -15
```

Expected: 16 PASS, 0 FAIL.

If a test fails, check:
- `match.index` (string `.matchAll()` provides this) for offset tracking
- Warn dedup uses the SET of indices (not the matched substring) so `[99][99][99]` warns once
- For invalid matches, the original `match[0]` (e.g. `[5]`) is pushed as literal — preserving the bracket characters

- [ ] **Step 5: Verify lint + typecheck**

```bash
cd apps/web && pnpm typecheck && pnpm lint --fix
```

Expected: 0 errors. If lint complains about the `eslint-disable-next-line no-console`, that's intentional for the warn call; keep the comment.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/citations/parseCitationMarkers.tsx apps/web/src/lib/citations/index.ts
git commit -m "feat(kb-globale): #1703 Task 2 — parseCitationMarkers utility (D-1703-D)

- Pure utility apps/web/src/lib/citations/parseCitationMarkers.tsx
- Strict regex /\[(\\d+(?:,\\s*\\d+)*)\\]/g (digit-only inside brackets)
- Returns ReactNode[] of text strings and <CitationPill> elements
- Warn dedup per-call (Set<number> of warned indices)
- onCitationClick forwarded to each pill (D-E deep-link payload)
- formatAriaLabel + formatRefText callbacks for i18n / customization
- Edge cases per D-1703-D: [0], [N>len], [N,M], [ref:...], markdown links,
  empty input, no-markers, boundaries, diacritics, adjacent, repeated,
  malformed [1, unicode [１] — all 16 tests pass"
```

---

## §6 — Task 3: Integrate parser into DrawerCompleted with fallback gate

**Files:**
- Modify: `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx`

- [ ] **Step 1: Read the current DrawerCompleted**

Current rendering structure (relevant excerpt around lines 30-52):

```tsx
<div data-testid="drawer-state-completed" className="flex-1 flex flex-col">
  <div className="flex-1 p-4 overflow-y-auto">
    <div className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed mb-3">
      {text}
    </div>
    {/* D-F: NUMBERED LIST below answer — index + page ref + snippet all visible */}
    {citations.length > 0 && (
      <ol className="space-y-1 mb-3" data-testid="citation-list">
        {citations.map((c, idx) => (
          <li
            key={`${c.docId}-${c.page}-${idx}`}
            data-citation-index={String(idx + 1)}
            data-citation-page={String(c.page)}
            className="text-xs text-muted-foreground flex gap-2"
          >
            <span className="font-mono font-bold text-entity-kb">{idx + 1}</span>
            <span>
              p.{c.page} · {c.snippet}
            </span>
          </li>
        ))}
      </ol>
    )}
    <div ...>✓ {labels.completedLabel} · {totalTokens} tokens · ...</div>
  </div>
  ...
</div>
```

- [ ] **Step 2: Update props interface to accept parser i18n labels**

Extend `DrawerCompletedLabels` with two new optional fields (named to be consumed by the orchestrator's i18n catalog in PR #1701 LABELS.drawer.completed):

```tsx
export interface DrawerCompletedLabels {
  completedLabel: string;
  copyLabel: string;
  regenerateLabel: string;
  /** Optional i18n template for inline pill aria-label. If omitted, defaults to:
   *  "Citation N, document {docId}, page {page}" (English-y, but works in any locale). */
  inlineCitationAriaLabel?: (citation: { docId: string; page: number; snippet: string }, n: number) => string;
}
```

Use the Edit tool to replace the existing `DrawerCompletedLabels` interface (lines 6-10) with the extended one above.

- [ ] **Step 3: Import the parser**

At the top of the file, add the import (group with other internal imports):

```tsx
import { parseCitationMarkers } from '@/lib/citations';
```

- [ ] **Step 4: Replace the rendering logic**

Replace the JSX inside the `flex-1 p-4 overflow-y-auto` div (the text + numbered list + metadata block, roughly lines 31-61) with the parser-gated version:

```tsx
      <div className="flex-1 p-4 overflow-y-auto">
        {(() => {
          const defaultAriaLabel = (
            c: { docId: string; page: number },
            n: number,
          ): string => `Citation ${n}, document ${c.docId}, page ${c.page}`;
          const ariaLabelBuilder = labels.inlineCitationAriaLabel ?? defaultAriaLabel;
          const parsedNodes = parseCitationMarkers(text, citations, {
            formatAriaLabel: (c, n) => ariaLabelBuilder(c, n),
          });
          // Inline path is active when the parser yielded at least one ReactElement
          // (i.e. at least one CitationPill). Otherwise fall back to the numbered list.
          const hasInlinePills = parsedNodes.some(node => typeof node !== 'string');

          return (
            <>
              <div
                className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed mb-3"
                data-testid="drawer-completed-answer"
                data-render-mode={hasInlinePills ? 'inline' : 'fallback'}
              >
                {hasInlinePills ? parsedNodes : text}
              </div>
              {/* D-F fallback: numbered list below answer (rendered only when
                  no inline pills were produced — never both — UX safety invariant) */}
              {!hasInlinePills && citations.length > 0 && (
                <ol className="space-y-1 mb-3" data-testid="citation-list">
                  {citations.map((c, idx) => (
                    <li
                      key={`${c.docId}-${c.page}-${idx}`}
                      data-citation-index={String(idx + 1)}
                      data-citation-page={String(c.page)}
                      className="text-xs text-muted-foreground flex gap-2"
                    >
                      <span className="font-mono font-bold text-entity-kb">{idx + 1}</span>
                      <span>
                        p.{c.page} · {c.snippet}
                      </span>
                    </li>
                  ))}
                </ol>
              )}
              <div
                data-tokens={String(totalTokens)}
                data-elapsed={String((elapsedMs / 1000).toFixed(1))}
                data-citations={String(citations.length)}
                className="p-2 rounded-sm bg-entity-kb/5 border border-entity-kb/20 text-xs text-entity-kb font-mono"
              >
                ✓ {labels.completedLabel} · {totalTokens} tokens ·{' '}
                {(elapsedMs / 1000).toFixed(1)}s · {citations.length} citations
              </div>
            </>
          );
        })()}
      </div>
```

The `data-render-mode="inline" | "fallback"` attribute is added so tests can assert which path was taken without depending on subtle DOM differences.

- [ ] **Step 5: Run all kb-globale tests**

```bash
cd apps/web && pnpm test src/components/features/kb-globale --run 2>&1 | tail -20
```

Expected: ALL pass. The existing `DrawerShell.test.tsx` test `'renders completed: full text + citation list (D-F: NUMBERED LIST below answer)'` may fail at this point because the rendering changed when `text` has no markers — the test asserts the numbered list is visible, which IS the fallback we preserve. Verify the test expects `data-citation-list` is present (it should be — that's the fallback path). If the test fails, the most likely cause is:
- The "Risposta completa." text in the test contains no `[N]` markers → fallback path triggers → numbered list IS rendered → existing assertions should still pass.

If the test does fail, capture the error message and proceed to Task 4 which extends the test to cover both inline and fallback paths explicitly.

- [ ] **Step 6: Typecheck + lint + tokens**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm lint:tokens
```

Expected: 0 errors, 0 violations.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/features/kb-globale/DrawerCompleted.tsx
git commit -m "feat(kb-globale): #1703 Task 3 — DrawerCompleted parser integration with fallback (D-1703-D)

- Calls parseCitationMarkers(text, citations) at render time
- If parser yields any CitationPill ReactElement → inline rendering
- Otherwise → numbered list below answer (D-F Phase 2 fallback preserved)
- NEVER both (UX safety invariant — visual duplication forbidden)
- New data-render-mode='inline'|'fallback' attribute on answer container for testability
- New optional DrawerCompletedLabels.inlineCitationAriaLabel for i18n
- Default aria-label builder is locale-neutral fallback when label not provided
- Mobile viewer citations tab untouched (list-view by design — out of scope)"
```

---

## §7 — Task 4: Extend DrawerShell.test.tsx for inline + fallback paths

**Files:**
- Modify: `apps/web/src/components/features/kb-globale/__tests__/DrawerShell.test.tsx`

- [ ] **Step 1: Locate the existing "renders completed" test**

Find the test labeled (or similar):

```
it('renders completed: full text + citation list (D-F: NUMBERED LIST below answer)', () => { ... })
```

This test currently passes a `partialText: 'Risposta completa.'` which contains NO `[N]` markers — fallback path. After Task 3 it should STILL pass because fallback preserves the numbered list.

- [ ] **Step 2: Add a new test for the inline path**

Append a sibling test that exercises the parser:

```tsx
  it('renders completed: inline pills when text contains [N] markers (D-1703-D inline path)', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'La classe Scout ha 3 abilità [1][2]. Perks parte da [3].',
      citations: [
        { docId: 'd1', source: 'd1', page: 14, snippet: 'cite uno', score: 0.9 },
        { docId: 'd1', source: 'd1', page: 14, snippet: 'cite due', score: 0.8 },
        { docId: 'd2', source: 'd2', page: 21, snippet: 'cite tre', score: 0.7 },
      ],
      totalTokens: 412,
      elapsedMs: 2100,
    };
    const { container } = render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />,
    );

    expect(screen.getByTestId('drawer-state-completed')).toBeInTheDocument();
    expect(screen.getByTestId('drawer-completed-answer')).toHaveAttribute(
      'data-render-mode',
      'inline',
    );
    const pills = container.querySelectorAll('[data-slot="kb-globale-citation-pill"]');
    expect(pills).toHaveLength(3);
    // Fallback list MUST NOT be present (UX safety invariant)
    expect(screen.queryByTestId('citation-list')).toBeNull();
  });

  it('renders completed: fallback list when text has no [N] markers (D-F fallback preserved)', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'Risposta semplice senza marker.',
      citations: [{ docId: 'd1', source: 'd1', page: 14, snippet: 'cite uno', score: 0.9 }],
      totalTokens: 100,
      elapsedMs: 500,
    };
    render(
      <DrawerShell
        state={state}
        labels={labels}
        suggestions={[]}
        onAsk={vi.fn()}
        onStop={vi.fn()}
        onReset={vi.fn()}
        onClose={vi.fn()}
        onEmptyCta={vi.fn()}
      />,
    );

    expect(screen.getByTestId('drawer-completed-answer')).toHaveAttribute(
      'data-render-mode',
      'fallback',
    );
    // The numbered list IS present in the fallback path
    expect(screen.getByTestId('citation-list')).toBeInTheDocument();
    expect(screen.getByText(/cite uno/i)).toBeInTheDocument();
  });
```

- [ ] **Step 3: Run the DrawerShell test suite**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/DrawerShell.test.tsx --run 2>&1 | tail -15
```

Expected: ALL pass (original 11 + 2 new = 13).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/features/kb-globale/__tests__/DrawerShell.test.tsx
git commit -m "test(kb-globale): #1703 Task 4 — DrawerShell coverage for inline + fallback paths

- New test: inline path with [1][2][3] markers → 3 pills + no citation-list
- New test: fallback path with no markers → citation-list present
- Asserts data-render-mode='inline' | 'fallback' attribute
- UX safety invariant verified: never both inline pills AND fallback list
- Original 'renders completed' test still passes (it uses 'Risposta completa.'
  which has no markers → fallback path, numbered list still visible)"
```

---

## §8 — Task 5: i18n catalog update + orchestrator wiring

**Files:**
- Modify: `apps/web/src/locales/it.json`
- Modify: `apps/web/src/locales/en.json`
- Optionally modify: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`

PR #1701 shipped the i18n keys for kbGlobale.drawer.completed but the `inlineCitationAriaLabel` template is new. This task adds it.

- [ ] **Step 1: Find the existing drawer.completed namespace in it.json**

```bash
grep -n "completedLabel\|completed.*COMPLETED" apps/web/src/locales/it.json | head -5
```

You should see lines like `"completedLabel": "COMPLETED"` inside `kbGlobale.drawer.completed`.

- [ ] **Step 2: Add the new template key to it.json**

Inside `kbGlobale.drawer.completed`, append the new key. Final structure:

```json
"completed": {
  "completedLabel": "COMPLETED",
  "copyLabel": "Copia",
  "regenerateLabel": "Rigenera",
  "inlineCitationAriaLabelTemplate": "Citazione {n}, documento {docId}, pagina {page}"
}
```

(The template uses placeholders consumed by the orchestrator when constructing the `inlineCitationAriaLabel` callback.)

- [ ] **Step 3: Mirror in en.json**

Inside the corresponding `kbGlobale.drawer.completed` in `en.json`:

```json
"completed": {
  "completedLabel": "COMPLETED",
  "copyLabel": "Copy",
  "regenerateLabel": "Regenerate",
  "inlineCitationAriaLabelTemplate": "Citation {n}, document {docId}, page {page}"
}
```

- [ ] **Step 4: Wire the template in KbGlobaleView.tsx (optional)**

PR #1701's `KbGlobaleView.tsx` declares an in-file `LABELS` constant. Locate the `drawer.shell.completed` section and add:

```ts
completed: {
  completedLabel: 'COMPLETED',
  copyLabel: 'Copia',
  regenerateLabel: 'Rigenera',
  inlineCitationAriaLabel: (c, n) => `Citazione ${n}, documento ${c.docId}, pagina ${c.page}`,
},
```

If LABELS still uses string constants (not i18n hooks yet), the callback form above is fine. Future i18n extraction will replace the function body with an interpolated template, but the callback shape is stable.

- [ ] **Step 5: Run all kb-globale tests (regression)**

```bash
cd apps/web && pnpm test src/app/'(authenticated)'/knowledge-base/global --run 2>&1 | tail -15
```

Expected: all 26 existing tests (Phase 1 + Phase 2) still pass.

```bash
cd apps/web && pnpm test src/components/features/kb-globale --run 2>&1 | tail -15
```

Expected: all kb-globale tests pass including the 2 new Task 4 cases.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
# include KbGlobaleView.tsx if you wired the callback in Step 4
git add apps/web/src/app/'(authenticated)'/knowledge-base/global/_components/KbGlobaleView.tsx 2>/dev/null || true
git commit -m "feat(kb-globale): #1703 Task 5 — i18n template for inline citation aria-label

- it.json + en.json: new kbGlobale.drawer.completed.inlineCitationAriaLabelTemplate
- Templates use placeholders {n}, {docId}, {page} consumed by the orchestrator
- KbGlobaleView LABELS.drawer.shell.completed gets the new callback shape
  (if string-LABELS pattern still in place; otherwise i18n extraction comes later)
- Phase 1 + Phase 2 + new Task 4 tests all PASS"
```

---

## §9 — Task 6: Verify + push + open PR

**Files:**
- All from previous tasks

- [ ] **Step 1: Final verification — all kb-globale tests + lint + tokens + bundle**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm lint:tokens
```

Expected: 0 errors, 0 violations.

```bash
cd apps/web && pnpm test \
  src/lib/citations/__tests__/parseCitationMarkers.test.tsx \
  src/components/features/kb-globale/__tests__/DrawerShell.test.tsx \
  src/app/'(authenticated)'/knowledge-base/global/_components/__tests__/ \
  --run 2>&1 | tail -20
```

Expected: 16 (parser) + 13 (DrawerShell incl. 2 new) + 26 (orchestrator) all PASS. ~55 tests total green.

```bash
cd apps/web && pnpm build 2>&1 | grep -i "knowledge-base/global"
```

Expected: bundle for `/knowledge-base/global` ≤ 120 KB (Phase 1+2 + parser is tiny ~2KB). No regression.

- [ ] **Step 2: Push branch**

```bash
git push -u origin feature/issue-1703-fe-inline-citation-parser
```

- [ ] **Step 3: Open PR**

```bash
gh pr create --base main-dev \
  --title "feat(kb-globale): #1703 FE — inline citation pills (parseCitationMarkers + DrawerCompleted)" \
  --body "$(cat <<'EOF'
## Summary

Implements **FE PR-2** of issue #1703 — adds the inline citation pill rendering on top of the BE [N] markers shipped by PR #1707.

A pure utility \`parseCitationMarkers(text, citations) → ReactNode[]\` lives at \`apps/web/src/lib/citations/parseCitationMarkers.tsx\`. \`DrawerCompleted\` consumes it: if the parser yields any pill nodes, render inline; otherwise render the existing numbered list below (D-F Phase 2 fallback). Mobile viewer and document viewer citations panels remain list-views by design.

## Spec-panel decisions implemented (D-1703-D)

Source: \`docs/superpowers/plans/2026-05-30-kb-inline-citation-markers-fe.md\` + spec-panel iteration 2 ([#1703 issuecomment-4582774753](https://github.com/meepleAi-app/meepleai-monorepo/issues/1703#issuecomment-4582774753))

| Decision | Where |
|---|---|
| Pure utility \`parseCitationMarkers\` | Task 2 |
| Strict regex \`/\\\\[(\\\\d+(?:,\\\\s*\\\\d+)*)\\\\]/g\` (digit-only) | Task 2 |
| Warn dedup per-call | Task 2 |
| 16 test scenarios (incl. all 14 from D-1703-D + type contract + onClick forwarding) | Task 1 |
| DrawerCompleted integration with fallback gate | Task 3 |
| UX safety invariant (never both inline AND fallback) | Task 3 |
| i18n templates for aria-label | Task 5 |

## Backwards compatibility

- D-F fallback path (numbered list below) **preserved exactly** when LLM does not emit \`[N]\` markers
- Old messages, models that ignore the prompt instruction, BE PR-1 not yet merged — all gracefully degrade
- Mobile viewer + document viewer citations panels **unchanged** (list-view by design)
- All Phase 1 + Phase 2 tests (≥ 26 in /knowledge-base/global) pass with 0 regression

## Test plan

- [x] \`pnpm test src/lib/citations\` → 16 PASS (parser scenarios)
- [x] \`pnpm test src/components/features/kb-globale/__tests__/DrawerShell.test.tsx\` → 13 PASS (11 original + 2 new for inline/fallback paths)
- [x] \`pnpm test src/app/(authenticated)/knowledge-base/global\` → 26 PASS (0 regression)
- [x] \`pnpm typecheck\` clean
- [x] \`pnpm lint\` clean
- [x] \`pnpm lint:tokens\` 0 violations
- [x] \`pnpm build\` bundle size unchanged (parser ~2KB negligible)

## Depends on

- BE PR-1 [#1707](https://github.com/meepleAi-app/meepleai-monorepo/pull/1707) — provides the actual \`[N]\` markers in LLM responses. **This FE PR is shippable independently** because the parser gracefully falls back to the existing numbered list when no markers are present; merging FE first means production traffic continues to see the numbered list until BE merges, then automatically switches to inline pills.

## Follow-ups

- **Counter increment site** (still TBD per BE PR-1): either BE post-processor scans assembled tokens at \`Complete\` event, OR FE thin reporter increments \`meepleai.rag.citation_markers.emitted_total{compliant,agent_typology}\` based on parser outcome
- **PR-3 (tech-debt)**: unify existing \`[ref:documentId:pageNum]\` copyright marker syntax with \`[N]\` system

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Comment on #1703 with the FE PR link**

```bash
gh issue comment 1703 --body "**FE PR-2 opened**: see linked PR. Implements D-1703-D (parseCitationMarkers utility + DrawerCompleted inline integration + fallback gate). Shippable independently from BE PR-1 #1707 thanks to graceful fallback to D-F numbered list when no markers present."
```

---

## §10 — Self-review (engineer should run this before submitting PR)

### Spec coverage

| Spec section | Task |
|---|---|
| D-1703-D pure utility creation | Task 2 |
| Strict digit-only regex | Task 2 |
| Warn dedup per-call | Task 2 |
| 14 test scenarios (Crispin §1.6 + Nygard §1.5) | Task 1 (16 tests — 14 spec + 2 contract) |
| DrawerCompleted integration | Task 3 |
| Fallback to numbered list when no markers | Task 3 |
| Mobile viewer unchanged | n/a (out of scope per spec D-1703-D) |
| i18n-aware aria-label | Tasks 2 + 5 |
| Phase 2 regression (Phase 1+2 tests still pass) | Task 6 |

### Type consistency

- `KbCitation` ({ docId, source, page, snippet, score }) consumed identically in `parseCitationMarkers` (Task 2) and `DrawerCompleted` (Task 3)
- `CitationPillProps` (n, refText, docId, page, ariaLabel, onClick?) — parser passes exactly these
- `ParseCitationMarkersOptions` ({ formatAriaLabel, onCitationClick?, formatRefText? }) — name + arity consistent across Task 2 impl, Task 1 test calls, Task 3 caller, Task 5 wiring
- `data-render-mode="inline" | "fallback"` attribute introduced in Task 3, asserted in Task 4 (string literals match)

### Placeholder scan

No "TODO" / "implement later" / "add error handling" / "similar to Task N" wording. All step bodies contain complete code blocks or exact commands.

### Open questions retired

- ~~"Where should the parser live"~~ → `apps/web/src/lib/citations/` (new feature folder, sibling to `card-mappers`, `agents`, etc.)
- ~~"How to handle warn deduplication"~~ → per-call `Set<number>` (Task 2 §1)
- ~~"Mobile UX scope"~~ → out of scope (Task 3 fallback gate doesn't touch mobile; mobile viewer citations tab stays list-view)
- ~~"i18n aria-label format"~~ → optional callback `inlineCitationAriaLabel?: (citation, n) => string` on `DrawerCompletedLabels` (Tasks 3 + 5)

### Risk flags

- **Independent shippability**: FE PR-2 is safe to ship BEFORE BE PR-1 because the parser falls back gracefully when no markers are present. Merging BE later flips the production rendering to inline automatically (no FE redeploy needed). Alternatively, holding FE-2 until BE-1 merges is also fine — both orders work.
- **JSX-in-`.ts` build risk**: the plan specifies renaming to `.tsx` to be safe. If you keep `.ts`, the build may fail with "JSX can only be used in .tsx files" depending on the project's tsconfig.
- **`KbCitation.source` vs `docId`**: the schema has both fields (BE Snippet quirk). FE has standardized on `docId` everywhere. Parser uses `docId` exclusively for the pill prop; `source` is unused in this PR.

---

**End of plan. Self-review complete. Ready for execution.**
