# KB Globale Phase 2 — Interactions Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Phase 2 "Interactions" surface of `/knowledge-base/global` (issue #1482) — 4 lazy-loaded components (`KbDocViewerDesktop`, `KbDocViewerMobile`, `DrawerShell` with 5-state FSM, `CitationPill`) + 1 greenfield SSE hook (`useKbAskStream`) + new Zod discriminated-union schemas + orchestrator extension, wiring the merged BE endpoints `POST /api/v1/knowledge-base/ask/global` (#1661 PR-2) and reusing `useKbDocDetail` / `useKbChunksList`.

**Architecture:** Extends the Phase 1 `KbGlobaleView` orchestrator (`apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`) with two new lazy branches: viewer (`?docId=`) and drawer (`?ask=1`). The orchestrator stays a thin URL-SSOT router; viewer is a `react-pdf` lazy chunk; drawer is a portal-mounted FSM with 5 sub-states (idle / streaming / completed / completed-empty / error×5-sub-kinds). Citations are rendered as a numbered list below the answer (NOT inline — system prompt does not instruct `[N]` markers, verified D-F).

**Tech Stack:**
- Next.js 16 App Router (`'use client'` orchestrator), React 19, TypeScript strict
- TanStack Query (`useKbDocDetail`, `useKbChunksList` reuse — no new query needed)
- Zod 4 discriminated union for SSE wire-format validation
- `react-pdf@^10.4.1` + `pdfjs-dist@5.7.284` (already installed) for PDF viewer
- `fetch` + `ReadableStream` for SSE parsing (no `EventSource` — POST body required)
- MSW for SSE mocking (Phase 1 pattern already established)
- DS-15 semantic tokens (`bg-card`, `text-foreground`, `text-entity-kb`, etc.) — enforced by `lint:tokens`
- shadcn primitives: `BottomSheet` (mobile viewer base), existing `Drawer` primitive
- jest-axe for component a11y assertions

**Plan-source spec:** spec-panel review 2026-05-30 (Wiegers/Adzic/Fowler/Nygard/Crispin), in conversation. Decisions D-E through D-M frozen.

---

## §0 — Pre-flight checklist (Engineer reads before Task 1)

| Item | Source | Why this matters |
|---|---|---|
| Phase 1 IS merged in `main-dev` (PR #1688 squash `6b9020ca22`) | `git log main-dev` | Plan assumes Phase 1 files exist; verify before starting |
| BE `/ask/global` endpoint live | `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs:149-164` | If missing, plan blocks — BE was #1661 PR-2 |
| Snippet has NO `chunkId`/`chunkPosition` | `Models/Contracts.cs:30` — `Snippet(text, source, page, line, score)` | Drives D-E (page-level deep-link only) |
| `BuildSystemPrompt` does NOT instruct `[N]` markers | `RagPromptAssemblyService.cs:711-799` | Drives D-F (render citations as list, NOT inline) |
| Numeric enum wire format | `SseJsonOptions.cs` | Drives D-I (z.literal(0/1/4/5/7) discriminator) |
| `react-pdf@^10.4.1` already in `package.json:168` | `apps/web/package.json` | No new dependency to add |
| `BottomSheet.tsx` primitive exists | `apps/web/src/components/ui/overlays/BottomSheet.tsx` | Mobile viewer extends, NOT greenfield |
| `useKbDocDetail` / `useKbChunksList` reusable 1:1 | `hooks/queries/useKbDocDetail.ts`, `useKbChunksList.ts` | Hooks consume `{docId, enabled?}` — no adapter needed |
| `useAgentChatStream` is the parent pattern | `apps/web/src/hooks/useAgentChatStream.ts` | Mirror for `useKbAskStream` (with different retry policy per D-L) |
| `KbSearchResultsDesktop.onResultClick` predisposto | `components/features/kb-globale/KbSearchResultsDesktop.tsx:75` | Already an optional callback — Task 9 wires it |

**Git workflow (CLAUDE.md compliance):**
- Parent branch: `main-dev`
- Feature branch: `feature/issue-1482-phase2-interactions` (create from `main-dev` after pre-flight `git pull --ff-only`)
- Target PR base: `main-dev`
- Commit messages: `feat(kb-globale): #1482 Task N — <description>` (Phase 1 pattern)

---

## §1 — File structure (Create + Modify)

### Create (new files)

```
apps/web/src/lib/api/schemas/
├── kb-ask.schemas.ts                                    # Task 1: Zod KbCitation + KbAskEvent discriminated union
└── __tests__/kb-ask.schemas.test.ts                     # Task 1: Zod validation tests

apps/web/src/lib/api/clients/
└── kbAskClient.ts                                       # Task 2: fetch + ReadableStream SSE parser

apps/web/src/hooks/
├── useKbAskStream.ts                                    # Task 3: FSM 5-state hook (greenfield)
└── __tests__/useKbAskStream.test.ts                     # Task 3: 12 FSM transition tests

apps/web/src/components/features/kb-globale/
├── CitationPill.tsx                                     # Task 4: pill component (page-level deep-link D-E)
├── KbDocViewerDesktop.tsx                               # Task 6: react-pdf 3-col layout
├── KbDocViewerMobile.tsx                                # Task 7: BottomSheet variant
├── DrawerShell.tsx                                      # Task 8: FSM orchestrator (header + state slot)
├── DrawerIdle.tsx                                       # Task 8: idle sub-state
├── DrawerStreaming.tsx                                  # Task 8: streaming sub-state
├── DrawerCompleted.tsx                                  # Task 8: completed sub-state
├── DrawerEmpty.tsx                                      # Task 8: completed-empty sub-state (D-L Adzic)
├── DrawerError.tsx                                      # Task 8: error sub-state (5 sub-kinds D-L)
└── __tests__/
    ├── CitationPill.test.tsx                            # Task 4
    ├── KbDocViewerDesktop.test.tsx                      # Task 6
    ├── KbDocViewerMobile.test.tsx                       # Task 7
    └── DrawerShell.test.tsx                             # Task 8 (5 state variants + FSM transitions)
```

### Modify (existing files)

```
apps/web/src/app/(authenticated)/knowledge-base/global/_components/
├── KbGlobaleView.tsx                                    # Task 9: add viewer + drawer branches (lazy)
└── __tests__/KbGlobaleView.integration.test.tsx        # Task 10: S8..S11 MSW tests

apps/web/src/components/features/kb-globale/
└── KbSearchResultsDesktop.tsx                           # Task 9: wire onResultClick → orchestrator (already typed)

apps/web/messages/
├── it.json                                              # Task 10: pages.kbGlobale.viewer.* + drawer.* + citation.*
└── en.json                                              # Task 10: parity

apps/web/
└── .bundle-budgets.json                                 # Task 10: /knowledge-base/global ≤ 120 KB (40 Foundation + 80 Interactions)

docs/for-developers/frontend/
└── v2-migration-matrix.md                               # Task 10: 4 rows pending → done
```

### Out of scope (deferred — DO NOT TOUCH)

- `KbEditorDesktop` + `useUpdateKbDocMeta` — BE-blocked by #1687.
- `FilterAccordion` — BE-blocked by #1686.
- `useGlobalKbSearch` — already shipped Phase 1, no changes.

---

## §2 — Frozen decisions (from spec-panel)

| # | Decision | Code impact |
|---|---|---|
| **D-E** | Page-level deep-link `?docId=&page=` (Snippet has no chunkId) | `CitationPill` onClick router push; viewer scrolls to page (NOT chunk) |
| **D-F** | Render citations as numbered list BELOW answer (LLM doesn't emit `[N]` markers) | NO inline parser; mockup's inline pills are aspirational |
| **D-G** | `react-pdf@^10.4.1` lazy chunk + CDN worker | Use `dynamic(() => import('./KbDocViewerDesktop'), { ssr: false })` |
| **D-H** | Reuse `useKbDocDetail` + `useKbChunksList` 1:1 | No new query hooks; no adapter |
| **D-I** | New file `kb-ask.schemas.ts` with `z.discriminatedUnion('type', ...)` on numeric literals | NO modifications to `kb-globale.schemas.ts` Phase 1 |
| **D-J** | Mobile viewer extends `BottomSheet.tsx` primitive | Wrapper, not greenfield |
| **D-K** | MSW SSE mock + jest-axe per component + ~12 FSM tests | Phase 1 MSW pattern already established |
| **D-L** | 5 FSM states (idle / streaming / completed / **completed-empty** / **error×5-sub-kinds**); backoff exp `[1s, 3s, 9s]`, max 3 retry, 30s timeout | Diverges from `useAgentChatStream` (2 retry × 2s linear) — documented follow-up |
| **D-M** | Bundle budget Phase 2 ≤ 80 KB lazy; `.bundle-budgets.json` total ≤ 120 KB | Verify via `pnpm build` size analyzer |

**Follow-up issues to file post-merge** (Task 10 deliverable):
- P3 `feat(kb): expose chunkId in Snippet for /ask/global` (upgrade D-E to chunk-level)
- P3 `feat(kb): instruct LLM to emit [N] citation markers in BuildSystemPrompt` (unlocks D-F inline rendering)
- P3 `refactor(hooks): align useAgentChatStream retry policy with useKbAskStream` (remove D-L divergence)

---

## Task 0: Pre-flight setup + verify D-F

**Files:**
- Read: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/RagPromptAssemblyService.cs:711-799`
- Read: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs:80-150`

- [ ] **Step 1: Sync main-dev and create feature branch**

```bash
git checkout main-dev
git pull --ff-only
git branch --show-current  # MUST print: main-dev
git status                 # MUST be clean (no untracked kb-globale/* files)
git checkout -b feature/issue-1482-phase2-interactions
git config branch.feature/issue-1482-phase2-interactions.parent main-dev
```

Expected: clean tree, new branch active.

- [ ] **Step 2: Verify D-F finding by reading the prompt template**

Read `RagPromptAssemblyService.cs:711-799` and confirm: the system prompt does NOT contain the strings `[1]`, `[N]`, `cite as`, `numbered reference`, `bracket`. The closest thing is "Quote the relevant rule text directly, including page/section if available."

**Conclusion (already documented in D-F):** the LLM will produce free-form text with page references like "p.14 §4.1", NOT `[1]` markers. Phase 2 renders citations as a **numbered list below the answer**.

- [ ] **Step 3: Verify BE error codes**

Read `CrossGameStreamQaQueryHandler.cs` and confirm the 4 error codes used:
- `RBAC_RESOLUTION_FAILED` (line ~173)
- `RETRIEVAL_FAILED` (line ~197)
- `PROMPT_ASSEMBLY_FAILED` (line ~224)
- `LLM_STREAMING_FAILED` (line ~272)

These will map to `error.kind = 'server'` in `useKbAskStream` (D-L sub-kind).

- [ ] **Step 4: Verify edge case — empty accessible games**

Read `CrossGameStreamQaQueryHandler.cs:80-90`. Confirm: when `accessibleGameIds.Count == 0`, the handler emits `StateUpdate("Ricerca nella tua libreria...")` followed by `Complete(0, 0, 0, 0, null)` — NO `Citations` event, NO `Token` events.

This is the `completed-empty` state added by Adzic in spec-panel D-L.

- [ ] **Step 5: Commit branch + read-only check**

```bash
git status  # should be clean
git log --oneline -1  # should match origin/main-dev tip
```

No commit yet — Task 0 is pure verification. Branch is ready for Task 1.

---

## Task 1: Zod schemas — `kb-ask.schemas.ts`

**Files:**
- Create: `apps/web/src/lib/api/schemas/kb-ask.schemas.ts`
- Create: `apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts`

- [ ] **Step 1: Write the failing schema test**

Create `apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import {
  KbCitationSchema,
  KbAskEventSchema,
  type KbAskEvent,
} from '../kb-ask.schemas';

describe('KbCitationSchema', () => {
  it('parses a valid page-level citation (D-E: no chunkId)', () => {
    const valid = {
      docId: '550e8400-e29b-41d4-a716-446655440000',
      source: '550e8400-e29b-41d4-a716-446655440000',
      page: 14,
      snippet: 'The Scout class begins with three signature abilities.',
      score: 0.87,
    };
    expect(KbCitationSchema.parse(valid)).toEqual(valid);
  });

  it('rejects negative page numbers', () => {
    expect(() =>
      KbCitationSchema.parse({
        docId: 'd', source: 's', page: -1, snippet: 't', score: 0.5,
      }),
    ).toThrow();
  });

  it('rejects missing required fields', () => {
    expect(() => KbCitationSchema.parse({ docId: 'd' })).toThrow();
  });
});

describe('KbAskEventSchema (discriminated union on numeric type)', () => {
  it('parses StateUpdate (type=0)', () => {
    const evt = { type: 0 as const, data: { message: 'Ricerca…' } };
    const parsed = KbAskEventSchema.parse(evt);
    expect(parsed.type).toBe(0);
  });

  it('parses Citations (type=1)', () => {
    const evt = {
      type: 1 as const,
      data: {
        citations: [{
          docId: 'd', source: 's', page: 14,
          snippet: 't', score: 0.9,
        }],
      },
    };
    expect(KbAskEventSchema.parse(evt).type).toBe(1);
  });

  it('parses Token (type=7)', () => {
    expect(KbAskEventSchema.parse({ type: 7 as const, data: { token: 'hello' } }).type).toBe(7);
  });

  it('parses Complete (type=4) with null confidence', () => {
    const evt = {
      type: 4 as const,
      data: {
        totalTokens: 412,
        promptTokens: 150,
        completionTokens: 262,
        estimatedReadingTimeMinutes: 0,
        confidence: null,
      },
    };
    expect(KbAskEventSchema.parse(evt).type).toBe(4);
  });

  it('parses Error (type=5) with message + code', () => {
    const evt = {
      type: 5 as const,
      data: { message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' },
    };
    expect(KbAskEventSchema.parse(evt).type).toBe(5);
  });

  it('rejects unknown type value (e.g. 99)', () => {
    expect(() => KbAskEventSchema.parse({ type: 99, data: {} })).toThrow();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts --run
```

Expected: FAIL with "Cannot find module '../kb-ask.schemas'".

- [ ] **Step 3: Write minimal schema implementation**

Create `apps/web/src/lib/api/schemas/kb-ask.schemas.ts`:

```ts
/**
 * KB Ask SSE schemas (Issue #1482 Phase 2 Interactions)
 *
 * Wire format matches `apps/api/src/Api/Infrastructure/Serialization/SseJsonOptions.cs`:
 *   - camelCase property naming
 *   - NUMERIC enum values (NOT strings) — frontend MUST use z.literal(0/1/4/5/7)
 *   - Envelope shape: `{ type: number, data: {...}, timestamp?: string }`
 *
 * D-E (spec-panel 2026-05-30): NO `chunkId` / `chunkPosition` — BE Snippet shape is
 *   `(text, source, page, line, score)`. Deep-link uses `?docId=&page=` only.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx (Drawer 6a-6d)
 * @see apps/api/src/Api/Models/Contracts.cs:30 (Snippet) + 98 (RagStreamingEvent)
 * @see apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/CrossGameStreamQa/CrossGameStreamQaQueryHandler.cs
 */

import { z } from 'zod';

/**
 * Page-level citation (D-E: no chunkId; deep-link is `?docId=&page=`).
 * Built from BE `Snippet(text, source, page, line, score)`:
 *   - `source` field carries the `PdfDocumentId` as string (used as `docId`).
 */
export const KbCitationSchema = z.object({
  docId: z.string(),
  source: z.string(),
  page: z.number().int().nonnegative(),
  snippet: z.string(),
  score: z.number(),
});
export type KbCitation = z.infer<typeof KbCitationSchema>;

/** Event types (mirror BE StreamingEventType enum — numeric values).
 *  Only the subset emitted by `/ask/global` is parsed here. */
export const KbAskEventType = {
  StateUpdate: 0,
  Citations: 1,
  Complete: 4,
  Error: 5,
  Token: 7,
} as const;

/** Discriminated union — Zod validates per-type `data` payload. */
export const KbAskEventSchema = z.discriminatedUnion('type', [
  z.object({
    type: z.literal(0),
    data: z.object({
      message: z.string(),
      chatThreadId: z.string().nullable().optional(),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(1),
    data: z.object({
      citations: z.array(KbCitationSchema),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(4),
    data: z.object({
      totalTokens: z.number(),
      promptTokens: z.number().nullable().optional(),
      completionTokens: z.number().nullable().optional(),
      estimatedReadingTimeMinutes: z.number().nullable().optional(),
      confidence: z.number().nullable().optional(),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(5),
    data: z.object({
      message: z.string(),
      code: z.string(),
    }),
    timestamp: z.string().optional(),
  }),
  z.object({
    type: z.literal(7),
    data: z.object({
      token: z.string(),
    }),
    timestamp: z.string().optional(),
  }),
]);
export type KbAskEvent = z.infer<typeof KbAskEventSchema>;

/** Request body for POST /api/v1/knowledge-base/ask/global. */
export const KbAskRequestSchema = z.object({
  query: z.string().min(1),
  language: z.string().optional(),
  topK: z.number().int().positive().optional(),
});
export type KbAskRequest = z.infer<typeof KbAskRequestSchema>;
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts --run
```

Expected: PASS — 7 tests green.

- [ ] **Step 5: Lint + typecheck**

```bash
cd apps/web && pnpm typecheck && pnpm lint --fix
```

Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/schemas/kb-ask.schemas.ts apps/web/src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts
git commit -m "feat(kb-globale): #1482 Task 1 — Zod schemas for /ask/global SSE wire (D-E + D-I)

- KbCitationSchema: page-level (no chunkId, per D-E spec-panel decision)
- KbAskEventSchema: discriminatedUnion('type', ...) on numeric literals (0/1/4/5/7)
- KbAskRequestSchema: { query, language?, topK? }
- 7 test (parse + reject) — mirror BE shapes 1:1
- Matches SseJsonOptions.Default (camelCase + numeric enum)"
```

---

## Task 2: SSE fetch client — `kbAskClient.ts`

**Files:**
- Create: `apps/web/src/lib/api/clients/kbAskClient.ts`
- Create: `apps/web/src/lib/api/clients/__tests__/kbAskClient.test.ts`

- [ ] **Step 1: Write the failing client test**

Create `apps/web/src/lib/api/clients/__tests__/kbAskClient.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { kbAskClient } from '../kbAskClient';
import type { KbAskEvent } from '../../schemas/kb-ask.schemas';

function buildSseStream(events: KbAskEvent[]): ReadableStream<Uint8Array> {
  const encoder = new TextEncoder();
  return new ReadableStream({
    start(controller) {
      for (const evt of events) {
        controller.enqueue(encoder.encode(`data: ${JSON.stringify(evt)}\n\n`));
      }
      controller.close();
    },
  });
}

describe('kbAskClient.askGlobal', () => {
  beforeEach(() => { vi.restoreAllMocks(); });

  it('yields parsed events from SSE stream in order', async () => {
    const events: KbAskEvent[] = [
      { type: 0, data: { message: 'Ricerca…' } },
      { type: 1, data: { citations: [{ docId: 'd', source: 'd', page: 14, snippet: 't', score: 0.9 }] } },
      { type: 7, data: { token: 'La ' } },
      { type: 7, data: { token: 'classe.' } },
      { type: 4, data: { totalTokens: 2, promptTokens: 10, completionTokens: 2, estimatedReadingTimeMinutes: 0, confidence: null } },
    ];
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(buildSseStream(events), { status: 200, headers: { 'content-type': 'text/event-stream' } }),
    );

    const received: KbAskEvent[] = [];
    for await (const evt of kbAskClient.askGlobal({ query: 'scout abilities' }, new AbortController().signal)) {
      received.push(evt);
    }

    expect(received).toEqual(events);
    expect(fetch).toHaveBeenCalledWith(
      expect.stringContaining('/api/v1/knowledge-base/ask/global'),
      expect.objectContaining({ method: 'POST' }),
    );
  });

  it('throws on non-200 HTTP status', async () => {
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response('Unauthorized', { status: 401 }),
    );

    await expect(async () => {
      for await (const _ of kbAskClient.askGlobal({ query: 'q' }, new AbortController().signal)) {
        /* drain */
      }
    }).rejects.toThrow(/HTTP 401/);
  });

  it('handles split events across chunk boundaries (partial line buffering)', async () => {
    const encoder = new TextEncoder();
    const stream = new ReadableStream<Uint8Array>({
      start(controller) {
        controller.enqueue(encoder.encode('data: {"type":0,'));
        controller.enqueue(encoder.encode('"data":{"message":"ok"}}\n\n'));
        controller.close();
      },
    });
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } }),
    );

    const received: KbAskEvent[] = [];
    for await (const evt of kbAskClient.askGlobal({ query: 'q' }, new AbortController().signal)) {
      received.push(evt);
    }
    expect(received).toEqual([{ type: 0, data: { message: 'ok' } }]);
  });

  it('aborts cleanly when signal triggers', async () => {
    const controller = new AbortController();
    const encoder = new TextEncoder();
    const stream = new ReadableStream<Uint8Array>({
      start(c) {
        c.enqueue(encoder.encode(`data: ${JSON.stringify({ type: 7, data: { token: 'a' } })}\n\n`));
        // Never closes — would hang without abort
      },
    });
    vi.spyOn(global, 'fetch').mockResolvedValueOnce(
      new Response(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } }),
    );

    const received: KbAskEvent[] = [];
    const consume = (async () => {
      for await (const evt of kbAskClient.askGlobal({ query: 'q' }, controller.signal)) {
        received.push(evt);
        controller.abort();
      }
    })();
    await consume;
    expect(received).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/lib/api/clients/__tests__/kbAskClient.test.ts --run
```

Expected: FAIL with "Cannot find module '../kbAskClient'".

- [ ] **Step 3: Write minimal client implementation**

Create `apps/web/src/lib/api/clients/kbAskClient.ts`:

```ts
/**
 * kbAskClient — POST /api/v1/knowledge-base/ask/global (SSE).
 *
 * Returns an AsyncIterable<KbAskEvent>. Caller MUST pass an `AbortSignal`
 * and call `controller.abort()` to stop the LLM stream (BE EC-3 — handler
 * stops cleanly on CancellationToken).
 *
 * Wire format: `data: {json}\n\n` (SseJsonOptions.Default, numeric enum).
 *
 * @see apps/web/src/lib/api/schemas/kb-ask.schemas.ts (KbAskEventSchema)
 */

import { KbAskEventSchema, type KbAskEvent, type KbAskRequest } from '../schemas/kb-ask.schemas';

const ENDPOINT = '/api/v1/knowledge-base/ask/global';

async function* parseSseStream(
  response: Response,
  signal: AbortSignal,
): AsyncGenerator<KbAskEvent> {
  if (!response.body) throw new Error('SSE response has no body');
  const reader = response.body.getReader();
  const decoder = new TextDecoder();
  let buffer = '';

  try {
    while (!signal.aborted) {
      const { value, done } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      // Split on `\n\n` (SSE event boundary). Keep the trailing partial in buffer.
      let boundary: number;
      while ((boundary = buffer.indexOf('\n\n')) !== -1) {
        const rawEvent = buffer.slice(0, boundary);
        buffer = buffer.slice(boundary + 2);
        // Each event is a sequence of `data: ...` lines (BE emits one line).
        const dataLine = rawEvent.split('\n').find(l => l.startsWith('data: '));
        if (!dataLine) continue;
        const json = dataLine.slice('data: '.length);
        try {
          const parsed = KbAskEventSchema.parse(JSON.parse(json));
          yield parsed;
        } catch {
          // Malformed event — skip, continue stream (resilience).
        }
      }
    }
  } finally {
    // Always release the reader lock, even if consumer breaks early.
    try { reader.releaseLock(); } catch { /* lock may already be released on abort */ }
  }
}

export const kbAskClient = {
  /**
   * Stream a cross-game KB question.
   * @param body request body (query + optional language + topK)
   * @param signal MUST be from an AbortController owned by the caller
   * @returns AsyncIterable of parsed KbAskEvent (idle event types are filtered upstream by hook)
   */
  async *askGlobal(body: KbAskRequest, signal: AbortSignal): AsyncGenerator<KbAskEvent> {
    const response = await fetch(ENDPOINT, {
      method: 'POST',
      credentials: 'include',
      headers: { 'content-type': 'application/json', accept: 'text/event-stream' },
      body: JSON.stringify(body),
      signal,
    });
    if (!response.ok) throw new Error(`HTTP ${response.status} ${response.statusText}`);
    yield* parseSseStream(response, signal);
  },
} as const;
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/lib/api/clients/__tests__/kbAskClient.test.ts --run
```

Expected: PASS — 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/clients/kbAskClient.ts apps/web/src/lib/api/clients/__tests__/kbAskClient.test.ts
git commit -m "feat(kb-globale): #1482 Task 2 — kbAskClient SSE fetch wrapper

- POST /api/v1/knowledge-base/ask/global with AbortSignal (BE EC-3 honors)
- ReadableStream parser handles split events across chunk boundaries
- Returns AsyncGenerator<KbAskEvent>; Zod-validated per event
- Malformed events are skipped (resilience), HTTP non-2xx throws
- 4 test (full stream, 401, split-chunk, abort)"
```

---

## Task 3: `useKbAskStream` hook (FSM 5-state)

**Files:**
- Create: `apps/web/src/hooks/useKbAskStream.ts`
- Create: `apps/web/src/hooks/__tests__/useKbAskStream.test.ts`

- [ ] **Step 1: Write the failing FSM test suite**

Create `apps/web/src/hooks/__tests__/useKbAskStream.test.ts`:

```ts
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, act, waitFor } from '@testing-library/react';
import { useKbAskStream } from '../useKbAskStream';
import { kbAskClient } from '../../lib/api/clients/kbAskClient';
import type { KbAskEvent } from '../../lib/api/schemas/kb-ask.schemas';

async function* eventsAsAsyncGen(events: KbAskEvent[]): AsyncGenerator<KbAskEvent> {
  for (const e of events) yield e;
}

describe('useKbAskStream — initial state', () => {
  it('starts in idle with empty citations + no error', () => {
    const { result } = renderHook(() => useKbAskStream());
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.partialText).toBe('');
    expect(result.current.state.citations).toEqual([]);
    expect(result.current.state.error).toBeNull();
    expect(result.current.state.retryCount).toBe(0);
  });
});

describe('useKbAskStream — transitions', () => {
  beforeEach(() => { vi.restoreAllMocks(); });

  it('idle → streaming on ask()', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 0, data: { message: 'Ricerca…' } }]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('scout abilities'); });

    await waitFor(() => expect(result.current.state.status).toBe('streaming'));
  });

  it('accumulates tokens into partialText', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'La ' } },
        { type: 7, data: { token: 'classe.' } },
      ]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.partialText).toBe('La classe.'));
  });

  it('stores citations from Citations event (D-E: page-level)', async () => {
    const cite = { docId: 'd', source: 'd', page: 14, snippet: 't', score: 0.9 };
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 1, data: { citations: [cite] } }]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.citations).toEqual([cite]));
  });

  it('streaming → completed on Complete event with tokens', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'a' } },
        { type: 4, data: { totalTokens: 1, promptTokens: 5, completionTokens: 1, estimatedReadingTimeMinutes: 0, confidence: null } },
      ]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.status).toBe('completed'));
    expect(result.current.state.totalTokens).toBe(1);
  });

  it('streaming → completed-empty when Complete arrives with totalTokens=0 and no prior Citations (D-L Adzic)', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 0, data: { message: 'Ricerca nella tua libreria...' } },
        { type: 4, data: { totalTokens: 0, promptTokens: 0, completionTokens: 0, estimatedReadingTimeMinutes: 0, confidence: null } },
      ]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.status).toBe('completed-empty'));
  });

  it('Error event with server-side code maps to kind=server (D-L Nygard)', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 5, data: { message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' } },
      ]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.status).toBe('error'));
    expect(result.current.state.error?.kind).toBe('server');
    expect(result.current.state.error?.code).toBe('RBAC_RESOLUTION_FAILED');
  });

  it('Error event with LLM_STREAMING_FAILED + accumulated partial text maps to kind=partial', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 7, data: { token: 'La classe Scout inizia…' } },
        { type: 5, data: { message: 'LLM crashed', code: 'LLM_STREAMING_FAILED' } },
      ]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.error?.kind).toBe('partial'));
    expect(result.current.state.partialText).toBe('La classe Scout inizia…');
  });

  it('Network throw maps to kind=connection', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      throw new TypeError('Failed to fetch');
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });

    await waitFor(() => expect(result.current.state.error?.kind).toBe('connection'));
  });

  it('stop() aborts mid-stream and transitions to idle', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 7, data: { token: 'a' } }]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });
    await waitFor(() => expect(result.current.state.status).toBe('streaming'));

    act(() => { result.current.stop(); });
    await waitFor(() => expect(result.current.state.status).toBe('idle'));
  });

  it('reset() returns to idle from completed', async () => {
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([
        { type: 4, data: { totalTokens: 1, promptTokens: 1, completionTokens: 1, estimatedReadingTimeMinutes: 0, confidence: null } },
      ]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });
    await waitFor(() => expect(result.current.state.status).toBe('completed'));

    act(() => { result.current.reset(); });
    expect(result.current.state.status).toBe('idle');
    expect(result.current.state.partialText).toBe('');
    expect(result.current.state.citations).toEqual([]);
  });

  it('ask() with topK and language forwards options to client', async () => {
    const spy = vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() =>
      eventsAsAsyncGen([{ type: 0, data: { message: 'ok' } }]),
    );
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q', { language: 'en', topK: 12 }); });

    await waitFor(() =>
      expect(spy).toHaveBeenCalledWith(
        { query: 'q', language: 'en', topK: 12 },
        expect.any(AbortSignal),
      ),
    );
  });

  it('retries on connection error with exponential backoff (1s, 3s, 9s)', async () => {
    vi.useFakeTimers();
    let attempt = 0;
    vi.spyOn(kbAskClient, 'askGlobal').mockImplementation(() => {
      attempt++;
      if (attempt < 3) throw new TypeError('Failed to fetch');
      return eventsAsAsyncGen([{ type: 4, data: { totalTokens: 1, promptTokens: 1, completionTokens: 1, estimatedReadingTimeMinutes: 0, confidence: null } }]);
    });
    const { result } = renderHook(() => useKbAskStream());

    act(() => { result.current.ask('q'); });
    await vi.runAllTimersAsync();

    expect(result.current.state.retryCount).toBeGreaterThanOrEqual(2);
    vi.useRealTimers();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/hooks/__tests__/useKbAskStream.test.ts --run
```

Expected: FAIL with "Cannot find module '../useKbAskStream'".

- [ ] **Step 3: Write minimal hook implementation**

Create `apps/web/src/hooks/useKbAskStream.ts`:

```ts
/**
 * useKbAskStream — FSM 5-state SSE hook for /api/v1/knowledge-base/ask/global.
 *
 * States (D-L spec-panel 2026-05-30):
 *   idle → streaming → completed | completed-empty | error
 *
 * Error sub-kinds:
 *   - connection: fetch throw (TypeError) / network drop
 *   - timeout: 30s without any event (TIMEOUT_MS)
 *   - partial: LLM_STREAMING_FAILED mid-stream (partialText preserved)
 *   - server: RBAC_RESOLUTION_FAILED / RETRIEVAL_FAILED / PROMPT_ASSEMBLY_FAILED
 *   - completed-empty: Complete arrives with totalTokens=0 and NO prior Citations
 *     (Adzic edge case — user has no accessible games)
 *
 * Retry policy: exp backoff [1s, 3s, 9s], max 3 retries, only on `connection` kind.
 *
 * D-F: citations rendered as numbered list BELOW the answer (LLM does NOT emit
 * `[N]` inline markers; verified in `RagPromptAssemblyService.BuildSystemPrompt`).
 *
 * @see apps/web/src/hooks/useAgentChatStream.ts (parent pattern, different retry policy)
 */

import { useCallback, useEffect, useRef, useState } from 'react';
import { kbAskClient } from '../lib/api/clients/kbAskClient';
import type { KbAskEvent, KbCitation, KbAskRequest } from '../lib/api/schemas/kb-ask.schemas';

export type KbAskStatus = 'idle' | 'streaming' | 'completed' | 'completed-empty' | 'error';
export type KbAskErrorKind = 'connection' | 'timeout' | 'partial' | 'server';

export interface KbAskError {
  readonly kind: KbAskErrorKind;
  readonly message: string;
  readonly code?: string;
}

export interface KbAskStreamState {
  readonly status: KbAskStatus;
  readonly partialText: string;
  readonly citations: readonly KbCitation[];
  readonly totalTokens: number;
  readonly elapsedMs: number;
  readonly error: KbAskError | null;
  readonly retryCount: number;
}

export interface AskOptions { language?: string; topK?: number }

export interface UseKbAskStream {
  readonly state: KbAskStreamState;
  ask: (query: string, opts?: AskOptions) => void;
  stop: () => void;
  reset: () => void;
}

const INITIAL_STATE: KbAskStreamState = {
  status: 'idle',
  partialText: '',
  citations: [],
  totalTokens: 0,
  elapsedMs: 0,
  error: null,
  retryCount: 0,
};

const MAX_RETRIES = 3;
const RETRY_DELAYS_MS = [1000, 3000, 9000] as const;
const TIMEOUT_MS = 30_000;
const SERVER_ERROR_CODES = new Set([
  'RBAC_RESOLUTION_FAILED',
  'RETRIEVAL_FAILED',
  'PROMPT_ASSEMBLY_FAILED',
]);

export function useKbAskStream(): UseKbAskStream {
  const [state, setState] = useState<KbAskStreamState>(INITIAL_STATE);
  const abortRef = useRef<AbortController | null>(null);
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const startTimeRef = useRef<number>(0);

  // Cleanup on unmount
  useEffect(() => () => {
    abortRef.current?.abort();
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
  }, []);

  const clearTimer = () => {
    if (timeoutRef.current) { clearTimeout(timeoutRef.current); timeoutRef.current = null; }
  };

  const runStream = useCallback(async (body: KbAskRequest, retryIdx: number) => {
    const ac = new AbortController();
    abortRef.current = ac;
    startTimeRef.current = Date.now();

    setState(s => ({ ...s, status: 'streaming', error: null, retryCount: retryIdx, elapsedMs: 0 }));

    // Timeout watchdog — resets on every event in the loop.
    let receivedAnyEvent = false;
    const resetTimeout = () => {
      clearTimer();
      timeoutRef.current = setTimeout(() => {
        if (!receivedAnyEvent) {
          ac.abort();
          setState(s => ({ ...s, status: 'error', error: { kind: 'timeout', message: 'No response in 30s' } }));
        }
      }, TIMEOUT_MS);
    };
    resetTimeout();

    let hasCitations = false;
    let accumulated = '';

    try {
      for await (const evt of kbAskClient.askGlobal(body, ac.signal)) {
        receivedAnyEvent = true;
        resetTimeout();

        if (evt.type === 0) {
          // StateUpdate — no state change beyond noting we got an event
          continue;
        }
        if (evt.type === 1) {
          hasCitations = true;
          setState(s => ({ ...s, citations: evt.data.citations }));
          continue;
        }
        if (evt.type === 7) {
          accumulated += evt.data.token;
          setState(s => ({ ...s, partialText: accumulated, elapsedMs: Date.now() - startTimeRef.current }));
          continue;
        }
        if (evt.type === 4) {
          clearTimer();
          const completedEmpty = evt.data.totalTokens === 0 && !hasCitations;
          setState(s => ({
            ...s,
            status: completedEmpty ? 'completed-empty' : 'completed',
            totalTokens: evt.data.totalTokens,
            elapsedMs: Date.now() - startTimeRef.current,
          }));
          return;
        }
        if (evt.type === 5) {
          clearTimer();
          const code = evt.data.code;
          const kind: KbAskErrorKind = SERVER_ERROR_CODES.has(code)
            ? 'server'
            : code === 'LLM_STREAMING_FAILED' && accumulated.length > 0
              ? 'partial'
              : 'server';
          setState(s => ({ ...s, status: 'error', error: { kind, message: evt.data.message, code } }));
          return;
        }
      }
    } catch (err) {
      clearTimer();
      if (ac.signal.aborted) return; // stop() called — caller handles state
      const isNetwork = err instanceof TypeError;
      if (isNetwork && retryIdx < MAX_RETRIES) {
        // Retry with exp backoff
        setState(s => ({ ...s, retryCount: retryIdx + 1 }));
        const delay = RETRY_DELAYS_MS[retryIdx] ?? RETRY_DELAYS_MS[RETRY_DELAYS_MS.length - 1];
        timeoutRef.current = setTimeout(() => { void runStream(body, retryIdx + 1); }, delay);
        return;
      }
      setState(s => ({
        ...s,
        status: 'error',
        error: { kind: isNetwork ? 'connection' : 'server', message: err instanceof Error ? err.message : 'Unknown error' },
      }));
    }
  }, []);

  const ask = useCallback((query: string, opts?: AskOptions) => {
    abortRef.current?.abort();
    clearTimer();
    setState({ ...INITIAL_STATE, status: 'streaming' });
    const body: KbAskRequest = { query, ...(opts?.language && { language: opts.language }), ...(opts?.topK && { topK: opts.topK }) };
    void runStream(body, 0);
  }, [runStream]);

  const stop = useCallback(() => {
    abortRef.current?.abort();
    clearTimer();
    setState(INITIAL_STATE);
  }, []);

  const reset = useCallback(() => {
    abortRef.current?.abort();
    clearTimer();
    setState(INITIAL_STATE);
  }, []);

  return { state, ask, stop, reset };
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/hooks/__tests__/useKbAskStream.test.ts --run
```

Expected: PASS — 12 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/useKbAskStream.ts apps/web/src/hooks/__tests__/useKbAskStream.test.ts
git commit -m "feat(kb-globale): #1482 Task 3 — useKbAskStream FSM 5-state hook (D-L)

- 5 states: idle | streaming | completed | completed-empty | error
- 4 error sub-kinds: connection | timeout | partial | server
- completed-empty (Adzic): Complete(totalTokens=0) AND no prior Citations event
- server (Nygard): maps BE codes RBAC/RETRIEVAL/PROMPT_ASSEMBLY_FAILED
- partial: LLM_STREAMING_FAILED mid-stream with accumulated text
- Backoff exp [1s, 3s, 9s], max 3 retry, 30s timeout
- 12 FSM transition tests (incl. partialText accumulation, citations storage,
  stop()/reset(), retry, options forwarding)"
```

---

## Task 4: `CitationPill` component

**Files:**
- Create: `apps/web/src/components/features/kb-globale/CitationPill.tsx`
- Create: `apps/web/src/components/features/kb-globale/__tests__/CitationPill.test.tsx`

- [ ] **Step 1: Write the failing component test**

Create `apps/web/src/components/features/kb-globale/__tests__/CitationPill.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe } from 'jest-axe';
import { CitationPill } from '../CitationPill';

const refMock = { docId: '550e8400-e29b-41d4-a716-446655440000', page: 14 };

describe('CitationPill', () => {
  it('renders the number + ref text', () => {
    render(<CitationPill n={1} refText="p.14" docId={refMock.docId} page={refMock.page} ariaLabel="Citazione 1, pagina 14" />);
    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('p.14')).toBeInTheDocument();
  });

  it('has a button role with aria-label', () => {
    render(<CitationPill n={2} refText="p.14 §4.1" docId={refMock.docId} page={14} ariaLabel="Citazione 2, pagina 14 sezione 4.1" />);
    const btn = screen.getByRole('button', { name: /citazione 2/i });
    expect(btn).toBeInTheDocument();
  });

  it('calls onClick with {docId, page} on click (D-E deep-link)', async () => {
    const onClick = vi.fn();
    render(<CitationPill n={3} refText="p.21" docId={refMock.docId} page={21} ariaLabel="c3" onClick={onClick} />);
    await userEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledWith({ docId: refMock.docId, page: 21 });
  });

  it('is keyboard activatable (Enter + Space)', async () => {
    const onClick = vi.fn();
    render(<CitationPill n={1} refText="p.14" docId={refMock.docId} page={14} ariaLabel="c1" onClick={onClick} />);
    const btn = screen.getByRole('button');
    btn.focus();
    await userEvent.keyboard('{Enter}');
    expect(onClick).toHaveBeenCalledTimes(1);
    await userEvent.keyboard(' ');
    expect(onClick).toHaveBeenCalledTimes(2);
  });

  it('has no a11y violations (jest-axe)', async () => {
    const { container } = render(
      <CitationPill n={1} refText="p.14" docId={refMock.docId} page={14} ariaLabel="Citazione 1, pagina 14" />,
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/CitationPill.test.tsx --run
```

Expected: FAIL — "Cannot find module '../CitationPill'".

- [ ] **Step 3: Write minimal component**

Create `apps/web/src/components/features/kb-globale/CitationPill.tsx`:

```tsx
/**
 * CitationPill — page-level citation chip (D-E spec-panel 2026-05-30).
 *
 * onClick callback receives { docId, page } so the caller can push
 * `?docId=&page=` to the URL (orchestrator wires viewer scroll).
 *
 * Visual: numbered circle + ref text (e.g. "p.14 §4.1"), button-role,
 * DS-15 entity-kb tokens, jest-axe clean.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx:2195 (CitationPill)
 */

import { type JSX } from 'react';
import { cn } from '@/lib/utils';

export interface CitationPillProps {
  /** 1-based index in the citations array */
  n: number;
  /** Display text (e.g. "p.14" or "p.14 §4.1") — caller formats from KbCitation */
  refText: string;
  /** PdfDocumentId (D-E deep-link target) */
  docId: string;
  /** Page number (D-E deep-link target) */
  page: number;
  /** Required accessible label (full sentence, i18n-injected by caller) */
  ariaLabel: string;
  /** Click handler receiving deep-link payload */
  onClick?: (link: { docId: string; page: number }) => void;
  className?: string;
}

export function CitationPill({
  n, refText, docId, page, ariaLabel, onClick, className,
}: CitationPillProps): JSX.Element {
  return (
    <button
      type="button"
      data-slot="kb-globale-citation-pill"
      aria-label={ariaLabel}
      onClick={() => onClick?.({ docId, page })}
      className={cn(
        'inline-flex items-center gap-1 px-1.5 py-0 rounded-full align-baseline',
        'border border-entity-kb/25 bg-entity-kb/10 text-entity-kb',
        'font-mono text-[10px] font-bold cursor-pointer',
        'focus:outline-none focus:ring-2 focus:ring-entity-kb/40 focus:ring-offset-1',
        'transition-colors duration-150 hover:bg-entity-kb/20',
        className,
      )}
    >
      <span
        className={cn(
          'inline-flex items-center justify-center',
          'w-3.5 h-3.5 rounded-full bg-entity-kb text-white text-[9px]',
        )}
      >
        {n}
      </span>
      <span>{refText}</span>
    </button>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/CitationPill.test.tsx --run
```

Expected: PASS — 5 tests green.

- [ ] **Step 5: Lint tokens**

```bash
cd apps/web && pnpm lint:tokens
```

Expected: 0 violations (only DS-15 semantic + entity tokens used).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/kb-globale/CitationPill.tsx apps/web/src/components/features/kb-globale/__tests__/CitationPill.test.tsx
git commit -m "feat(kb-globale): #1482 Task 4 — CitationPill page-level deep-link (D-E)

- Props: { n, refText, docId, page, ariaLabel, onClick? }
- onClick receives { docId, page } (D-E: page-level, no chunkId)
- Button role + aria-label + keyboard (Enter/Space)
- DS-15: border-entity-kb/25, bg-entity-kb/10, text-entity-kb
- 5 test (render, role, click, keyboard, jest-axe)"
```

---

## Task 5: KbDocViewer hook integration verification (no new code)

**Files:**
- Read-only: `apps/web/src/hooks/queries/useKbDocDetail.ts`
- Read-only: `apps/web/src/hooks/queries/useKbChunksList.ts`

This task is a verification step — confirms `useKbDocDetail` and `useKbChunksList` are reusable 1:1 without an adapter (D-H). If the contract has drifted, Task 6/7 need an adapter; this task surfaces that early.

- [ ] **Step 1: Read both hook signatures + confirm props**

```bash
grep -n "export function useKbDocDetail" apps/web/src/hooks/queries/useKbDocDetail.ts
grep -n "export function useKbChunksList" apps/web/src/hooks/queries/useKbChunksList.ts
```

Expected: both accept `{ docId, enabled? }` with `docId` accepting `string | null | undefined`.

- [ ] **Step 2: Verify response shape includes title + page count + chunks with page numbers**

Read `useKbDocDetail.ts` and confirm the returned `KbDocDetail` shape has at least:
- `id: string`
- `title: string` (or `fileName`)
- `pageCount: number` (or equivalent — total pages for sidebar thumbnails)
- **`fileUrl: string`** (URL to the PDF resource — needed by `react-pdf <Document file={...}>` in Task 6 and Task 7)

Read `useKbChunksList.ts` and confirm the returned chunk shape has `pageNumber: number`.

If any of these fields are missing → STOP, file a BE follow-up issue, and pause Phase 2 implementation (this is a P74 verification gate). In particular, `fileUrl` is mandatory for the PDF viewer; if absent (e.g. BE returns only a `documentId` and expects a separate `/kb-docs/{id}/file` endpoint), Task 6 and Task 7 need a separate fetch step — adjust the plan before proceeding.

- [ ] **Step 3: Document findings in commit message (no code change)**

```bash
git commit --allow-empty -m "docs(kb-globale): #1482 Task 5 — verify useKbDocDetail + useKbChunksList reusable 1:1 (D-H)

- useKbDocDetail({docId, enabled?}) returns KbDocDetail with title + pageCount
- useKbChunksList({docId}) returns chunks with pageNumber
- No adapter required for Phase 2 viewer (Task 6, Task 7)
- Verification gate per spec-panel D-H"
```

---

## Task 6: `KbDocViewerDesktop` (react-pdf, 3-col layout)

**Files:**
- Create: `apps/web/src/components/features/kb-globale/KbDocViewerDesktop.tsx`
- Create: `apps/web/src/components/features/kb-globale/__tests__/KbDocViewerDesktop.test.tsx`

- [ ] **Step 1: Write the failing component test**

Create `apps/web/src/components/features/kb-globale/__tests__/KbDocViewerDesktop.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KbDocViewerDesktop } from '../KbDocViewerDesktop';

// Mock react-pdf so jsdom can render without a real worker
vi.mock('react-pdf', () => ({
  Document: ({ children, onLoadSuccess }: { children: React.ReactNode; onLoadSuccess?: (p: { numPages: number }) => void }) => {
    onLoadSuccess?.({ numPages: 24 });
    return <div data-testid="pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => <div data-testid={`pdf-page-${pageNumber}`}>page {pageNumber}</div>,
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

const docMock = { id: 'doc-1', title: 'Gloomhaven Rulebook v2', fileUrl: 'https://example.com/doc.pdf', pageCount: 24 };
const labels = {
  pageLabel: (n: number) => `Pagina ${n}`,
  zoomIn: 'Zoom in',
  zoomOut: 'Zoom out',
  zoomReset: 'Zoom reset',
  thumbnailsLabel: 'Pagine',
  closeLabel: 'Chiudi',
  pageOfTotal: (cur: number, total: number) => `${cur} / ${total}`,
};

describe('KbDocViewerDesktop', () => {
  it('renders the active page', () => {
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByTestId('pdf-page-14')).toBeInTheDocument();
  });

  it('shows page X / Y indicator', () => {
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByText('14 / 24')).toBeInTheDocument();
  });

  it('calls onPageChange when thumbnail clicked', async () => {
    const onPageChange = vi.fn();
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={onPageChange}
        onClose={vi.fn()}
      />,
    );
    // Page thumbnails are rendered as buttons (one per page)
    const thumb = await screen.findByRole('button', { name: /pagina 15/i });
    await userEvent.click(thumb);
    expect(onPageChange).toHaveBeenCalledWith(15);
  });

  it('calls onClose when close button clicked', async () => {
    const onClose = vi.fn();
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={onClose}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('highlights the active page in citations panel when citation matches activePage (D-E page-level)', () => {
    const citations = [
      { n: 1, docId: docMock.id, page: 14, refText: 'p.14', snippet: 'scout abilities' },
      { n: 2, docId: docMock.id, page: 21, refText: 'p.21', snippet: 'perks' },
    ];
    render(
      <KbDocViewerDesktop
        doc={docMock}
        activePage={14}
        citations={citations}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    const activeCite = screen.getByText(/scout abilities/);
    expect(activeCite.closest('[data-active="true"]')).not.toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/KbDocViewerDesktop.test.tsx --run
```

Expected: FAIL — "Cannot find module '../KbDocViewerDesktop'".

- [ ] **Step 3: Write minimal component**

Create `apps/web/src/components/features/kb-globale/KbDocViewerDesktop.tsx`:

```tsx
/**
 * KbDocViewerDesktop — 3-col PDF viewer with thumbnails + center page + citations panel.
 *
 * 12% / 55% / 33% grid (matches admin-mockups/design_files/sp4-kb-globale.jsx:1035).
 *
 * D-E: citation deep-link is page-level — `activePage` highlights citations matching
 * that page in the right panel. No bbox/inline highlight.
 *
 * D-G: react-pdf@^10.4.1 + pdfjs-dist CDN worker. Lazy-imported from orchestrator.
 *
 * Pure presentational — all data props-injected (Phase 1 pattern).
 */

'use client';

import { type JSX, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import { cn } from '@/lib/utils';

// Worker config (one-time on module load; safe with `'use client'` directive)
if (typeof window !== 'undefined' && !pdfjs.GlobalWorkerOptions.workerSrc) {
  pdfjs.GlobalWorkerOptions.workerSrc =
    `https://unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;
}

const ZOOM_LEVELS = [0.75, 1, 1.25, 1.5, 2] as const;

export interface KbDocViewerCitation {
  n: number;
  docId: string;
  page: number;
  refText: string;
  snippet: string;
}

export interface KbDocViewerLabels {
  pageLabel: (n: number) => string;
  zoomIn: string;
  zoomOut: string;
  zoomReset: string;
  thumbnailsLabel: string;
  closeLabel: string;
  pageOfTotal: (cur: number, total: number) => string;
}

export interface KbDocViewerDesktopProps {
  doc: { id: string; title: string; fileUrl: string; pageCount: number };
  activePage: number;
  citations: readonly KbDocViewerCitation[];
  labels: KbDocViewerLabels;
  onPageChange: (page: number) => void;
  onClose: () => void;
}

export function KbDocViewerDesktop({
  doc, activePage, citations, labels, onPageChange, onClose,
}: KbDocViewerDesktopProps): JSX.Element {
  const [zoomIdx, setZoomIdx] = useState(1); // 100%
  const [numPages, setNumPages] = useState(doc.pageCount);
  const scale = ZOOM_LEVELS[zoomIdx];

  return (
    <div
      data-slot="kb-doc-viewer-desktop"
      className="rounded-xl border border-entity-kb/20 bg-card overflow-hidden shadow-md"
    >
      {/* Top toolbar */}
      <div className="flex items-center gap-3 px-5 py-3 border-b border-entity-kb/15 bg-gradient-to-br from-entity-kb/5 to-transparent">
        <div className="min-w-0 flex-1">
          <div className="font-display font-bold text-sm truncate">📄 {doc.title}</div>
          <div className="font-mono text-[10px] text-muted-foreground">
            {labels.pageOfTotal(activePage, numPages)}
          </div>
        </div>
        <div className="flex items-center border border-border rounded-md bg-muted overflow-hidden">
          <button
            type="button"
            aria-label={labels.zoomOut}
            onClick={() => setZoomIdx(i => Math.max(0, i - 1))}
            className="px-3 py-1.5 text-xs font-mono font-bold text-foreground hover:bg-card border-r border-border"
          >−</button>
          <span className="px-3 py-1.5 text-xs font-mono font-bold text-foreground bg-card border-r border-border">
            {Math.round(scale * 100)}%
          </span>
          <button
            type="button"
            aria-label={labels.zoomIn}
            onClick={() => setZoomIdx(i => Math.min(ZOOM_LEVELS.length - 1, i + 1))}
            className="px-3 py-1.5 text-xs font-mono font-bold text-foreground hover:bg-card"
          >+</button>
        </div>
        <button
          type="button"
          aria-label={labels.closeLabel}
          onClick={onClose}
          className="w-8 h-8 rounded-full bg-muted hover:bg-border text-foreground"
        >✕</button>
      </div>

      {/* 3-col body */}
      <div className="grid grid-cols-[12%_55%_33%] min-h-[680px]">
        {/* Page thumbnails sidebar */}
        <aside
          aria-label={labels.thumbnailsLabel}
          className="bg-muted border-r border-border overflow-y-auto p-3"
        >
          <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground mb-3 text-center">
            {labels.thumbnailsLabel}
          </div>
          {Array.from({ length: numPages }, (_, i) => i + 1).map(pageN => {
            const isActive = pageN === activePage;
            return (
              <button
                key={pageN}
                type="button"
                aria-label={labels.pageLabel(pageN)}
                aria-current={isActive ? 'page' : undefined}
                onClick={() => onPageChange(pageN)}
                className={cn(
                  'block w-full mb-2.5 cursor-pointer',
                  isActive ? 'ring-2 ring-entity-kb' : 'ring-1 ring-border',
                  'rounded-sm bg-card aspect-[0.71] hover:ring-entity-kb/50',
                )}
              >
                <span className="font-mono text-[9px] text-center block mt-1 text-foreground">{pageN}</span>
              </button>
            );
          })}
        </aside>

        {/* Center — PDF render */}
        <div className="bg-muted p-6 overflow-y-auto">
          <Document
            file={doc.fileUrl}
            onLoadSuccess={({ numPages: n }: { numPages: number }) => setNumPages(n)}
          >
            <Page pageNumber={activePage} scale={scale} />
          </Document>
        </div>

        {/* Right — citations panel */}
        <aside className="bg-card border-l border-entity-kb/15 p-4">
          <div className="font-display font-bold text-sm mb-3">Citations</div>
          {citations.map(c => {
            const isActive = c.page === activePage;
            return (
              <div
                key={c.n}
                data-active={isActive || undefined}
                className={cn(
                  'rounded-md p-3 mb-2 border',
                  isActive ? 'bg-entity-kb/10 border-entity-kb/40' : 'bg-muted border-border',
                )}
              >
                <div className="font-mono text-[10px] text-muted-foreground">
                  {c.n} · {c.refText}
                </div>
                <div className="text-xs text-foreground mt-1">{c.snippet}</div>
              </div>
            );
          })}
        </aside>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/KbDocViewerDesktop.test.tsx --run
```

Expected: PASS — 5 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/kb-globale/KbDocViewerDesktop.tsx apps/web/src/components/features/kb-globale/__tests__/KbDocViewerDesktop.test.tsx
git commit -m "feat(kb-globale): #1482 Task 6 — KbDocViewerDesktop react-pdf 3-col (D-G + D-E)

- 12%/55%/33% grid: thumbnails | PDF render | citations panel
- react-pdf + CDN worker (pdfjs.GlobalWorkerOptions)
- Zoom 5 levels [0.75, 1, 1.25, 1.5, 2]
- Thumbnails as button-role (a11y), aria-current='page' for active
- Citations highlight (D-E page-level): data-active when c.page === activePage
- Pure presentational — props-driven, no hooks
- 5 test (render page, page indicator, thumbnail click, close, active citation highlight)"
```

---

## Task 7: `KbDocViewerMobile` (BottomSheet variant)

**Files:**
- Create: `apps/web/src/components/features/kb-globale/KbDocViewerMobile.tsx`
- Create: `apps/web/src/components/features/kb-globale/__tests__/KbDocViewerMobile.test.tsx`

- [ ] **Step 1: Read BottomSheet primitive to confirm API**

```bash
head -50 apps/web/src/components/ui/overlays/BottomSheet.tsx
```

Confirm props: `{ open, onOpenChange, children, title?, expandable? }` or similar. Adapt the wrapper API below if the actual signature differs.

- [ ] **Step 2: Write the failing component test**

Create `apps/web/src/components/features/kb-globale/__tests__/KbDocViewerMobile.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KbDocViewerMobile } from '../KbDocViewerMobile';

vi.mock('react-pdf', () => ({
  Document: ({ children }: { children: React.ReactNode }) => <div data-testid="pdf-document">{children}</div>,
  Page: ({ pageNumber }: { pageNumber: number }) => <div data-testid={`pdf-page-${pageNumber}`}>p {pageNumber}</div>,
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

const docMock = { id: 'doc-1', title: 'Gloomhaven Rulebook v2', fileUrl: 'https://example.com/doc.pdf', pageCount: 24 };
const labels = {
  pageLabel: (n: number) => `Pagina ${n}`,
  closeLabel: 'Chiudi',
  pageOfTotal: (c: number, t: number) => `${c} / ${t}`,
  tabPdf: 'PDF',
  tabCitations: 'Citazioni',
};

describe('KbDocViewerMobile', () => {
  it('renders sticky header with title + page indicator', () => {
    render(
      <KbDocViewerMobile
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByText(/gloomhaven rulebook v2/i)).toBeInTheDocument();
    expect(screen.getByText('14 / 24')).toBeInTheDocument();
  });

  it('shows PDF tab by default', () => {
    render(
      <KbDocViewerMobile
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    expect(screen.getByTestId('pdf-page-14')).toBeInTheDocument();
  });

  it('switches to citations tab', async () => {
    const citations = [{ n: 1, docId: docMock.id, page: 14, refText: 'p.14', snippet: 'scout' }];
    render(
      <KbDocViewerMobile
        doc={docMock}
        activePage={14}
        citations={citations}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={vi.fn()}
      />,
    );
    await userEvent.click(screen.getByRole('tab', { name: /citazioni/i }));
    expect(screen.getByText(/scout/)).toBeInTheDocument();
  });

  it('calls onClose when close button pressed', async () => {
    const onClose = vi.fn();
    render(
      <KbDocViewerMobile
        doc={docMock}
        activePage={14}
        citations={[]}
        labels={labels}
        onPageChange={vi.fn()}
        onClose={onClose}
      />,
    );
    await userEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/KbDocViewerMobile.test.tsx --run
```

Expected: FAIL — "Cannot find module '../KbDocViewerMobile'".

- [ ] **Step 4: Write the component**

Create `apps/web/src/components/features/kb-globale/KbDocViewerMobile.tsx`:

```tsx
/**
 * KbDocViewerMobile — bottom-sheet PDF viewer with tab switcher.
 *
 * D-J: extends BottomSheet primitive. Tab switch: PDF | Citations.
 * Same data contract as desktop variant (D-E page-level citations).
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx:1306 (mobile mockup)
 */

'use client';

import { type JSX, useState } from 'react';
import { Document, Page, pdfjs } from 'react-pdf';
import { cn } from '@/lib/utils';
import type { KbDocViewerCitation } from './KbDocViewerDesktop';

if (typeof window !== 'undefined' && !pdfjs.GlobalWorkerOptions.workerSrc) {
  pdfjs.GlobalWorkerOptions.workerSrc =
    `https://unpkg.com/pdfjs-dist@${pdfjs.version}/build/pdf.worker.min.mjs`;
}

export interface KbDocViewerMobileLabels {
  pageLabel: (n: number) => string;
  closeLabel: string;
  pageOfTotal: (cur: number, total: number) => string;
  tabPdf: string;
  tabCitations: string;
}

export interface KbDocViewerMobileProps {
  doc: { id: string; title: string; fileUrl: string; pageCount: number };
  activePage: number;
  citations: readonly KbDocViewerCitation[];
  labels: KbDocViewerMobileLabels;
  onPageChange: (page: number) => void;
  onClose: () => void;
}

export function KbDocViewerMobile({
  doc, activePage, citations, labels, onClose,
}: KbDocViewerMobileProps): JSX.Element {
  const [tab, setTab] = useState<'pdf' | 'citations'>('pdf');

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={doc.title}
      data-slot="kb-doc-viewer-mobile"
      className="fixed inset-x-0 bottom-0 top-12 bg-card rounded-t-2xl border-t border-entity-kb/20 shadow-2xl overflow-hidden flex flex-col"
    >
      {/* Sticky header */}
      <div className="flex items-center gap-3 px-4 py-3 bg-card border-b border-entity-kb/15">
        <button
          type="button"
          aria-label={labels.closeLabel}
          onClick={onClose}
          className="w-8 h-8 rounded-full bg-muted text-foreground"
        >✕</button>
        <div className="flex-1 min-w-0">
          <div className="font-display font-bold text-sm truncate">📄 {doc.title}</div>
        </div>
        <div className="px-2.5 py-1 rounded-full bg-entity-kb/10 text-entity-kb font-mono text-xs font-bold">
          {labels.pageOfTotal(activePage, doc.pageCount)}
        </div>
      </div>

      {/* Tabs */}
      <div role="tablist" className="flex border-b border-border bg-card">
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'pdf'}
          onClick={() => setTab('pdf')}
          className={cn(
            'flex-1 py-2 text-sm font-medium',
            tab === 'pdf' ? 'text-entity-kb border-b-2 border-entity-kb' : 'text-muted-foreground',
          )}
        >{labels.tabPdf}</button>
        <button
          type="button"
          role="tab"
          aria-selected={tab === 'citations'}
          onClick={() => setTab('citations')}
          className={cn(
            'flex-1 py-2 text-sm font-medium',
            tab === 'citations' ? 'text-entity-kb border-b-2 border-entity-kb' : 'text-muted-foreground',
          )}
        >{labels.tabCitations}</button>
      </div>

      {/* Body */}
      <div className="flex-1 overflow-y-auto p-4 bg-muted">
        {tab === 'pdf' ? (
          <Document file={doc.fileUrl}>
            <Page pageNumber={activePage} />
          </Document>
        ) : (
          <ul className="space-y-2">
            {citations.map(c => (
              <li
                key={c.n}
                className="rounded-md p-3 bg-card border border-border"
              >
                <div className="font-mono text-[10px] text-muted-foreground">
                  {c.n} · {c.refText}
                </div>
                <div className="text-xs text-foreground mt-1">{c.snippet}</div>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/KbDocViewerMobile.test.tsx --run
```

Expected: PASS — 4 tests green.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/features/kb-globale/KbDocViewerMobile.tsx apps/web/src/components/features/kb-globale/__tests__/KbDocViewerMobile.test.tsx
git commit -m "feat(kb-globale): #1482 Task 7 — KbDocViewerMobile bottom-sheet (D-J)

- Sticky header (close + title + page indicator)
- Tab switcher: pdf | citations (mockup pattern)
- Reuses KbDocViewerCitation type from desktop variant
- role='dialog' + aria-modal for screen-reader semantics
- 4 test (header, default tab, tab switch, close)"
```

---

## Task 8: `DrawerShell` + 5 FSM sub-states

**Files:**
- Create: `apps/web/src/components/features/kb-globale/DrawerShell.tsx`
- Create: `apps/web/src/components/features/kb-globale/DrawerIdle.tsx`
- Create: `apps/web/src/components/features/kb-globale/DrawerStreaming.tsx`
- Create: `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx`
- Create: `apps/web/src/components/features/kb-globale/DrawerEmpty.tsx`
- Create: `apps/web/src/components/features/kb-globale/DrawerError.tsx`
- Create: `apps/web/src/components/features/kb-globale/__tests__/DrawerShell.test.tsx`

- [ ] **Step 1: Write the failing FSM test**

Create `apps/web/src/components/features/kb-globale/__tests__/DrawerShell.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DrawerShell } from '../DrawerShell';
import type { KbAskStreamState } from '../../../../hooks/useKbAskStream';

const baseState: KbAskStreamState = {
  status: 'idle', partialText: '', citations: [], totalTokens: 0, elapsedMs: 0, error: null, retryCount: 0,
};

const labels = {
  title: 'Ask the Meeple',
  subtitle: 'Knowledge Base',
  closeLabel: 'Chiudi',
  idle: { welcomeTitle: 'Chiedimi qualsiasi cosa', welcomeBody: 'Cerco nei tuoi PDF.', suggestionsLabel: 'Suggerimenti', placeholder: 'Chiedi al Meeple…', sendLabel: 'Invia' },
  streaming: { statusLabel: 'STREAMING', stopLabel: 'Stop streaming' },
  completed: { completedLabel: 'COMPLETED', copyLabel: 'Copy', regenerateLabel: 'Regenerate' },
  empty: { title: 'Nessun documento', body: 'Carica un PDF per iniziare.', cta: 'Vai alla libreria' },
  error: {
    connection: { title: 'Connessione persa', body: 'Retry automatico…', action: 'Riprova ora' },
    timeout: { title: 'Risposta lenta', body: '>30s', action: 'Continua attesa', alt: 'Cancella' },
    partial: { title: 'Risposta incompleta', body: 'Stream interrotto', action: 'Ripeti query' },
    server: { title: 'Errore del server', body: 'Riprova', action: 'Riprova' },
  },
};

describe('DrawerShell — FSM state rendering', () => {
  it('renders idle: welcome + suggestions + input', () => {
    render(<DrawerShell state={baseState} labels={labels} suggestions={['Q1', 'Q2', 'Q3']} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-idle')).toBeInTheDocument();
    expect(screen.getByText(/chiedimi qualsiasi cosa/i)).toBeInTheDocument();
    expect(screen.getAllByText(/Q[123]/)).toHaveLength(3);
  });

  it('renders streaming: partial text + stop button', () => {
    const state: KbAskStreamState = { ...baseState, status: 'streaming', partialText: 'La classe Scout…', citations: [] };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-streaming')).toBeInTheDocument();
    expect(screen.getByText(/la classe scout/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /stop streaming/i })).toBeInTheDocument();
  });

  it('renders completed: full text + citation list (D-F: NUMBERED LIST below answer)', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'completed',
      partialText: 'Risposta completa.',
      citations: [
        { docId: 'd1', source: 'd1', page: 14, snippet: 'cite 1', score: 0.9 },
        { docId: 'd1', source: 'd1', page: 21, snippet: 'cite 2', score: 0.8 },
      ],
      totalTokens: 412,
    };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-completed')).toBeInTheDocument();
    expect(screen.getByText(/risposta completa/i)).toBeInTheDocument();
    // D-F: citations are a NUMBERED LIST below the answer
    expect(screen.getByText(/1/)).toBeInTheDocument();
    expect(screen.getByText(/cite 1/i)).toBeInTheDocument();
    expect(screen.getByText(/cite 2/i)).toBeInTheDocument();
  });

  it('renders completed-empty: dedicated empty state with CTA', async () => {
    const state: KbAskStreamState = { ...baseState, status: 'completed-empty' };
    const onEmptyCta = vi.fn();
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={onEmptyCta} />);
    expect(screen.getByTestId('drawer-state-empty')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: /vai alla libreria/i }));
    expect(onEmptyCta).toHaveBeenCalledOnce();
  });

  it('renders error[connection]: countdown + auto-retry hint', () => {
    const state: KbAskStreamState = { ...baseState, status: 'error', error: { kind: 'connection', message: 'lost' } };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-error-connection')).toBeInTheDocument();
    expect(screen.getByText(/connessione persa/i)).toBeInTheDocument();
  });

  it('renders error[timeout]: continue + cancel actions', () => {
    const state: KbAskStreamState = { ...baseState, status: 'error', error: { kind: 'timeout', message: 'slow' } };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-error-timeout')).toBeInTheDocument();
    expect(screen.getByText(/cancella/i)).toBeInTheDocument();
  });

  it('renders error[partial]: shows accumulated partial text + [stream interrotto]', () => {
    const state: KbAskStreamState = {
      ...baseState,
      status: 'error',
      partialText: 'La classe Scout inizia con',
      error: { kind: 'partial', message: 'LLM crashed' },
    };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-error-partial')).toBeInTheDocument();
    expect(screen.getByText(/la classe scout inizia con/i)).toBeInTheDocument();
  });

  it('renders error[server]: shows code', () => {
    const state: KbAskStreamState = { ...baseState, status: 'error', error: { kind: 'server', message: 'RBAC failed', code: 'RBAC_RESOLUTION_FAILED' } };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    expect(screen.getByTestId('drawer-state-error-server')).toBeInTheDocument();
    expect(screen.getByText(/RBAC_RESOLUTION_FAILED/)).toBeInTheDocument();
  });

  it('calls onClose when header close pressed (all states)', async () => {
    const onClose = vi.fn();
    render(<DrawerShell state={baseState} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={vi.fn()} onReset={vi.fn()} onClose={onClose} onEmptyCta={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onAsk(query) from idle when suggestion clicked', async () => {
    const onAsk = vi.fn();
    render(<DrawerShell state={baseState} labels={labels} suggestions={['How to play?']} onAsk={onAsk} onStop={vi.fn()} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /how to play/i }));
    expect(onAsk).toHaveBeenCalledWith('How to play?');
  });

  it('calls onStop from streaming state', async () => {
    const onStop = vi.fn();
    const state: KbAskStreamState = { ...baseState, status: 'streaming', partialText: 'a' };
    render(<DrawerShell state={state} labels={labels} suggestions={[]} onAsk={vi.fn()} onStop={onStop} onReset={vi.fn()} onClose={vi.fn()} onEmptyCta={vi.fn()} />);
    await userEvent.click(screen.getByRole('button', { name: /stop streaming/i }));
    expect(onStop).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/DrawerShell.test.tsx --run
```

Expected: FAIL — "Cannot find module '../DrawerShell'".

- [ ] **Step 3: Write the 5 sub-state components**

Create `apps/web/src/components/features/kb-globale/DrawerIdle.tsx`:

```tsx
'use client';
import { type JSX, useState } from 'react';

export interface DrawerIdleLabels {
  welcomeTitle: string;
  welcomeBody: string;
  suggestionsLabel: string;
  placeholder: string;
  sendLabel: string;
}

export function DrawerIdle({
  suggestions, onAsk, labels,
}: {
  suggestions: readonly string[];
  onAsk: (query: string) => void;
  labels: DrawerIdleLabels;
}): JSX.Element {
  const [draft, setDraft] = useState('');
  return (
    <div data-testid="drawer-state-idle" className="flex-1 p-4 flex flex-col justify-between">
      <div>
        <div className="p-5 rounded-md bg-entity-kb/5 border border-entity-kb/20 text-center mb-4">
          <div className="text-3xl mb-2">👋</div>
          <div className="font-display font-bold text-sm mb-1">{labels.welcomeTitle}</div>
          <div className="text-xs text-muted-foreground leading-relaxed">{labels.welcomeBody}</div>
        </div>
        <div className="font-mono text-[10px] font-bold uppercase tracking-wider text-muted-foreground mb-2">
          {labels.suggestionsLabel}
        </div>
        {suggestions.map(s => (
          <button
            key={s}
            type="button"
            onClick={() => onAsk(s)}
            className="flex items-center gap-2 w-full p-2.5 mb-2 rounded-md bg-muted border border-border hover:bg-entity-kb/5 text-left text-xs text-foreground"
          >
            <span className="text-entity-kb shrink-0">↪</span>
            <span className="flex-1">{s}</span>
          </button>
        ))}
      </div>
      <form
        onSubmit={e => { e.preventDefault(); if (draft.trim()) { onAsk(draft); setDraft(''); } }}
        className="bg-muted rounded-md border border-entity-kb/20 p-2 flex items-center gap-2"
      >
        <input
          value={draft}
          onChange={e => setDraft(e.target.value)}
          placeholder={labels.placeholder}
          className="flex-1 bg-transparent border-none outline-none font-body text-sm text-foreground"
        />
        <button
          type="submit"
          aria-label={labels.sendLabel}
          className="w-8 h-8 rounded-sm bg-entity-kb text-white text-sm"
        >↑</button>
      </form>
    </div>
  );
}
```

Create `apps/web/src/components/features/kb-globale/DrawerStreaming.tsx`:

```tsx
'use client';
import { type JSX } from 'react';

export interface DrawerStreamingLabels {
  statusLabel: string;
  stopLabel: string;
}

export function DrawerStreaming({
  partialText, totalTokens, elapsedMs, onStop, labels,
}: {
  partialText: string;
  totalTokens: number;
  elapsedMs: number;
  onStop: () => void;
  labels: DrawerStreamingLabels;
}): JSX.Element {
  return (
    <div data-testid="drawer-state-streaming" className="flex-1 flex flex-col">
      <div className="flex-1 p-4 overflow-y-auto">
        <div className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed">
          {partialText}
          <span className="inline-flex gap-1 ml-2 align-middle">
            <span className="w-1.5 h-1.5 rounded-full bg-entity-kb animate-pulse" />
            <span className="w-1.5 h-1.5 rounded-full bg-entity-kb/50 animate-pulse" />
            <span className="w-1.5 h-1.5 rounded-full bg-entity-kb/30 animate-pulse" />
          </span>
        </div>
        <div className="mt-2 font-mono text-[10px] text-muted-foreground flex gap-2">
          <span className="text-entity-kb font-bold">● {labels.statusLabel}</span>
          <span>·</span>
          <span>{totalTokens} tokens</span>
          <span>·</span>
          <span>{(elapsedMs / 1000).toFixed(1)}s</span>
        </div>
      </div>
      <div className="p-3 border-t border-border bg-card">
        <button
          type="button"
          onClick={onStop}
          className="w-full py-2 rounded-md bg-muted border border-border text-sm text-foreground hover:bg-border"
        >◼ {labels.stopLabel}</button>
      </div>
    </div>
  );
}
```

Create `apps/web/src/components/features/kb-globale/DrawerCompleted.tsx`:

```tsx
'use client';
import { type JSX } from 'react';
import type { KbCitation } from '../../../lib/api/schemas/kb-ask.schemas';

export interface DrawerCompletedLabels {
  completedLabel: string;
  copyLabel: string;
  regenerateLabel: string;
}

export function DrawerCompleted({
  text, citations, totalTokens, elapsedMs, onCopy, onRegenerate, labels,
}: {
  text: string;
  citations: readonly KbCitation[];
  totalTokens: number;
  elapsedMs: number;
  onCopy?: () => void;
  onRegenerate?: () => void;
  labels: DrawerCompletedLabels;
}): JSX.Element {
  return (
    <div data-testid="drawer-state-completed" className="flex-1 flex flex-col">
      <div className="flex-1 p-4 overflow-y-auto">
        <div className="bg-muted border border-border rounded-lg p-3 text-sm text-foreground leading-relaxed mb-3">
          {text}
        </div>
        {/* D-F: NUMBERED LIST below answer (no inline pills) */}
        {citations.length > 0 && (
          <ol className="space-y-1 mb-3">
            {citations.map((c, idx) => (
              <li key={`${c.docId}-${c.page}-${idx}`} className="text-xs text-muted-foreground flex gap-2">
                <span className="font-mono font-bold text-entity-kb">{idx + 1}</span>
                <span>p.{c.page} · {c.snippet}</span>
              </li>
            ))}
          </ol>
        )}
        <div className="p-2 rounded-sm bg-emerald-50 border border-emerald-200 text-xs text-emerald-700 font-mono">
          ✓ {labels.completedLabel} · {totalTokens} tokens · {(elapsedMs / 1000).toFixed(1)}s · {citations.length} citations
        </div>
      </div>
      <div className="p-3 border-t border-border bg-card flex gap-2">
        {onCopy && <button type="button" onClick={onCopy} className="text-xs px-3 py-1 rounded-md bg-muted">{labels.copyLabel}</button>}
        {onRegenerate && <button type="button" onClick={onRegenerate} className="text-xs px-3 py-1 rounded-md bg-muted">{labels.regenerateLabel}</button>}
      </div>
    </div>
  );
}
```

Create `apps/web/src/components/features/kb-globale/DrawerEmpty.tsx`:

```tsx
'use client';
import { type JSX } from 'react';

export interface DrawerEmptyLabels {
  title: string;
  body: string;
  cta: string;
}

export function DrawerEmpty({ onCta, labels }: { onCta: () => void; labels: DrawerEmptyLabels }): JSX.Element {
  return (
    <div data-testid="drawer-state-empty" className="flex-1 p-6 flex flex-col items-center justify-center text-center">
      <div className="text-4xl mb-3">📚</div>
      <div className="font-display font-bold text-base mb-2">{labels.title}</div>
      <div className="text-sm text-muted-foreground mb-4 leading-relaxed">{labels.body}</div>
      <button
        type="button"
        onClick={onCta}
        className="px-4 py-2 rounded-md bg-entity-kb text-white text-sm font-medium hover:bg-entity-kb/90"
      >{labels.cta}</button>
    </div>
  );
}
```

Create `apps/web/src/components/features/kb-globale/DrawerError.tsx`:

```tsx
'use client';
import { type JSX } from 'react';
import type { KbAskError } from '../../../hooks/useKbAskStream';

export interface DrawerErrorLabelsForKind {
  title: string;
  body: string;
  action: string;
  alt?: string;
}
export interface DrawerErrorLabels {
  connection: DrawerErrorLabelsForKind;
  timeout: DrawerErrorLabelsForKind;
  partial: DrawerErrorLabelsForKind;
  server: DrawerErrorLabelsForKind;
}

export function DrawerError({
  error, partialText, onRetry, onCancel, labels,
}: {
  error: KbAskError;
  partialText: string;
  onRetry: () => void;
  onCancel?: () => void;
  labels: DrawerErrorLabels;
}): JSX.Element {
  const cfg = labels[error.kind];
  const icons = { connection: '📡', timeout: '⏱️', partial: '⚠', server: '⚙' } as const;
  return (
    <div data-testid={`drawer-state-error-${error.kind}`} className="flex-1 p-4 flex flex-col gap-3">
      {error.kind === 'partial' && partialText && (
        <div className="bg-muted border border-rose-200 rounded-lg p-3 text-sm text-muted-foreground italic">
          {partialText}
          <span className="not-italic font-bold text-rose-700"> [stream interrotto]</span>
        </div>
      )}
      <div className="rounded-md bg-rose-50 border border-rose-200 border-l-4 border-l-rose-500 p-3 flex flex-col gap-2">
        <div className="flex items-center gap-2">
          <span className="text-2xl">{icons[error.kind]}</span>
          <div>
            <div className="font-display font-bold text-sm text-rose-700">{cfg.title}</div>
            <div className="text-xs text-muted-foreground">{cfg.body}</div>
          </div>
        </div>
        {error.code && (
          <div className="font-mono text-[10px] text-muted-foreground">{error.code}</div>
        )}
        <div className="flex gap-2">
          <button
            type="button"
            onClick={onRetry}
            className="flex-1 px-3 py-1.5 rounded-md bg-rose-500 text-white text-xs font-medium"
          >↻ {cfg.action}</button>
          {cfg.alt && onCancel && (
            <button
              type="button"
              onClick={onCancel}
              className="px-3 py-1.5 rounded-md bg-muted text-xs text-foreground"
            >{cfg.alt}</button>
          )}
        </div>
      </div>
    </div>
  );
}
```

Create `apps/web/src/components/features/kb-globale/DrawerShell.tsx`:

```tsx
/**
 * DrawerShell — FSM orchestrator routing on `state.status`.
 *
 * 5 sub-states (D-L spec-panel 2026-05-30):
 *   idle | streaming | completed | completed-empty | error
 *
 * D-F: citations as numbered list below answer (no inline parsing).
 *
 * Pure presentational — consumes `KbAskStreamState` directly from hook.
 *
 * @see admin-mockups/design_files/sp4-kb-globale.jsx:1968 (DrawerShell)
 */

'use client';

import { type JSX } from 'react';
import type { KbAskStreamState } from '../../../hooks/useKbAskStream';
import { DrawerIdle, type DrawerIdleLabels } from './DrawerIdle';
import { DrawerStreaming, type DrawerStreamingLabels } from './DrawerStreaming';
import { DrawerCompleted, type DrawerCompletedLabels } from './DrawerCompleted';
import { DrawerEmpty, type DrawerEmptyLabels } from './DrawerEmpty';
import { DrawerError, type DrawerErrorLabels } from './DrawerError';

export interface DrawerShellLabels {
  title: string;
  subtitle: string;
  closeLabel: string;
  idle: DrawerIdleLabels;
  streaming: DrawerStreamingLabels;
  completed: DrawerCompletedLabels;
  empty: DrawerEmptyLabels;
  error: DrawerErrorLabels;
}

export interface DrawerShellProps {
  state: KbAskStreamState;
  suggestions: readonly string[];
  labels: DrawerShellLabels;
  onAsk: (query: string) => void;
  onStop: () => void;
  onReset: () => void;
  onClose: () => void;
  onEmptyCta: () => void;
  onCopy?: () => void;
  onRegenerate?: () => void;
}

export function DrawerShell({
  state, suggestions, labels, onAsk, onStop, onReset, onClose, onEmptyCta, onCopy, onRegenerate,
}: DrawerShellProps): JSX.Element {
  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label={labels.title}
      data-slot="kb-globale-drawer"
      className="w-[420px] h-[620px] bg-card rounded-xl border border-entity-kb/20 shadow-2xl flex flex-col overflow-hidden"
    >
      {/* Header */}
      <div className="px-4 py-3 border-b border-entity-kb/15 bg-gradient-to-br from-entity-kb/5 to-transparent flex items-center gap-3">
        <span className="w-8 h-8 rounded-sm bg-entity-kb/15 text-entity-kb flex items-center justify-center text-base">🤖</span>
        <div className="flex-1 min-w-0">
          <div className="font-display font-bold text-sm truncate">{labels.title}</div>
          <div className="font-mono text-[10px] text-muted-foreground">{labels.subtitle}</div>
        </div>
        <button
          type="button"
          aria-label={labels.closeLabel}
          onClick={onClose}
          className="w-7 h-7 rounded-full bg-muted text-muted-foreground hover:text-foreground"
        >✕</button>
      </div>

      {/* FSM-routed body */}
      {state.status === 'idle' && (
        <DrawerIdle suggestions={suggestions} onAsk={onAsk} labels={labels.idle} />
      )}
      {state.status === 'streaming' && (
        <DrawerStreaming
          partialText={state.partialText}
          totalTokens={state.totalTokens}
          elapsedMs={state.elapsedMs}
          onStop={onStop}
          labels={labels.streaming}
        />
      )}
      {state.status === 'completed' && (
        <DrawerCompleted
          text={state.partialText}
          citations={state.citations}
          totalTokens={state.totalTokens}
          elapsedMs={state.elapsedMs}
          onCopy={onCopy}
          onRegenerate={onRegenerate}
          labels={labels.completed}
        />
      )}
      {state.status === 'completed-empty' && (
        <DrawerEmpty onCta={onEmptyCta} labels={labels.empty} />
      )}
      {state.status === 'error' && state.error && (
        <DrawerError
          error={state.error}
          partialText={state.partialText}
          onRetry={() => onAsk(state.partialText || '')}
          onCancel={onReset}
          labels={labels.error}
        />
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test src/components/features/kb-globale/__tests__/DrawerShell.test.tsx --run
```

Expected: PASS — 11 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/features/kb-globale/Drawer*.tsx apps/web/src/components/features/kb-globale/__tests__/DrawerShell.test.tsx
git commit -m "feat(kb-globale): #1482 Task 8 — DrawerShell FSM 5-state (D-L + D-F)

- 5 sub-states: idle | streaming | completed | completed-empty | error
- Idle: welcome + 3 suggestions + input form
- Streaming: partial text + 3-dot pulse + tokens/elapsed meta + Stop
- Completed: full answer + numbered citation list below (D-F: no inline)
- Empty (Adzic): zero-result dedicated state + CTA to /library
- Error (4 sub-kinds rendered): connection / timeout / partial / server
- Pure presentational, props-driven (consumes KbAskStreamState directly)
- 11 test (each state + transitions + interactions)"
```

---

## Task 9: Orchestrator extension — `KbGlobaleView` + URL SSOT

**Files:**
- Modify: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`
- Modify: `apps/web/src/components/features/kb-globale/KbSearchResultsDesktop.tsx` (only wire onResultClick — already typed)

- [ ] **Step 1: Write a failing orchestrator unit test**

Append to `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx`:

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { KbGlobaleView } from '../KbGlobaleView';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams('docId=doc-1&page=14'),
}));

// Stub Phase 2 dynamic imports so SSR/client behavior is deterministic in test
vi.mock('../KbDocViewerDesktopLazy', () => ({
  default: () => <div data-testid="kb-doc-viewer-desktop-mounted" />,
}));
vi.mock('../KbDrawerShellLazy', () => ({
  default: () => <div data-testid="kb-drawer-mounted" />,
}));

describe('KbGlobaleView Phase 2 — viewer branch', () => {
  it('mounts viewer when ?docId is present', async () => {
    render(<KbGlobaleView />);
    expect(await screen.findByTestId('kb-doc-viewer-desktop-mounted')).toBeInTheDocument();
  });
});
```

(Adjust `useSearchParams` mock per existing Phase 1 test file structure.)

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test src/app/'(authenticated)'/knowledge-base/global/_components/__tests__/KbGlobaleView.test.tsx --run
```

Expected: FAIL — viewer branch not present in orchestrator.

- [ ] **Step 3: Extend `KbGlobaleView` with viewer + drawer branches (lazy)**

Open `apps/web/src/app/(authenticated)/knowledge-base/global/_components/KbGlobaleView.tsx`. Add at the top (after existing imports):

```tsx
import dynamic from 'next/dynamic';
import { useKbDocDetail } from '@/hooks/queries/useKbDocDetail';
import { useKbAskStream } from '@/hooks/useKbAskStream';

const KbDocViewerDesktopLazy = dynamic(
  () => import('@/components/features/kb-globale/KbDocViewerDesktop').then(m => ({ default: m.KbDocViewerDesktop })),
  { ssr: false },
);
const KbDocViewerMobileLazy = dynamic(
  () => import('@/components/features/kb-globale/KbDocViewerMobile').then(m => ({ default: m.KbDocViewerMobile })),
  { ssr: false },
);
const DrawerShellLazy = dynamic(
  () => import('@/components/features/kb-globale/DrawerShell').then(m => ({ default: m.DrawerShell })),
  { ssr: false },
);
```

In the `KbGlobaleView` component body, after the existing `recent` / `search` hooks, add:

```tsx
// ── Phase 2: viewer branch ─────────────────────────────────────────────
const docIdParam = searchParams.get('docId');
const pageParam = Number(searchParams.get('page')) || 1;
const askParam = searchParams.get('ask') === '1';

const docDetail = useKbDocDetail({ docId: docIdParam, enabled: Boolean(docIdParam) });
const askStream = useKbAskStream();

const openViewer = useCallback((result: { docId: string; page: number }) => {
  const params = new URLSearchParams(searchParams.toString());
  params.set('docId', result.docId);
  params.set('page', String(result.page));
  router.push(`/knowledge-base/global?${params.toString()}`);
}, [router, searchParams]);

const closeViewer = useCallback(() => {
  const params = new URLSearchParams(searchParams.toString());
  params.delete('docId');
  params.delete('page');
  const qs = params.toString();
  router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
}, [router, searchParams]);

const openDrawer = useCallback(() => {
  const params = new URLSearchParams(searchParams.toString());
  params.set('ask', '1');
  router.push(`/knowledge-base/global?${params.toString()}`);
}, [router, searchParams]);

const closeDrawer = useCallback(() => {
  const params = new URLSearchParams(searchParams.toString());
  params.delete('ask');
  const qs = params.toString();
  router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
}, [router, searchParams]);

const onCitationClick = useCallback(({ docId, page }: { docId: string; page: number }) => {
  openViewer({ docId, page });
}, [openViewer]);
```

Modify the existing `<KbSearchResultsDesktop ... />` JSX to add `onResultClick`:

```tsx
<KbSearchResultsDesktop
  /* ... existing props ... */
  onResultClick={(r) => openViewer({ docId: r.docId, page: r.pageNumber ?? 1 })}
/>
```

Append at the end of the orchestrator return (before closing `</div>`):

```tsx
{docIdParam && docDetail.data && (
  <KbDocViewerDesktopLazy
    doc={{ id: docDetail.data.id, title: docDetail.data.title, fileUrl: docDetail.data.fileUrl, pageCount: docDetail.data.pageCount }}
    activePage={pageParam}
    citations={askStream.state.citations.map((c, i) => ({ n: i + 1, docId: c.docId, page: c.page, refText: `p.${c.page}`, snippet: c.snippet }))}
    labels={LABELS.viewer}
    onPageChange={(p) => openViewer({ docId: docIdParam, page: p })}
    onClose={closeViewer}
  />
)}

{askParam && (
  <DrawerShellLazy
    state={askStream.state}
    suggestions={LABELS.drawer.suggestions}
    labels={LABELS.drawer.shell}
    onAsk={(q) => askStream.ask(q)}
    onStop={askStream.stop}
    onReset={askStream.reset}
    onClose={closeDrawer}
    onEmptyCta={() => router.push('/library')}
  />
)}
```

Add `LABELS.viewer` and `LABELS.drawer` sections to the in-file `LABELS` constant (same Phase 1 style; Task 10 will extract to i18n):

```tsx
viewer: {
  pageLabel: (n: number) => `Pagina ${n}`,
  zoomIn: 'Zoom in',
  zoomOut: 'Zoom out',
  zoomReset: 'Reset',
  thumbnailsLabel: 'Pagine',
  closeLabel: 'Chiudi',
  pageOfTotal: (cur: number, total: number) => `${cur} / ${total}`,
},
drawer: {
  suggestions: [
    'Come funziona il setup iniziale?',
    'Quali sono le abilità della classe Scout?',
    'Differenza tra base e enhanced effect?',
  ],
  shell: {
    title: 'Ask the Meeple',
    subtitle: 'Knowledge Base',
    closeLabel: 'Chiudi',
    idle: {
      welcomeTitle: 'Chiedimi qualsiasi cosa sui tuoi giochi',
      welcomeBody: 'Cerco nei tuoi PDF e cito le pagine esatte.',
      suggestionsLabel: 'Suggerimenti',
      placeholder: 'Chiedi al Meeple…',
      sendLabel: 'Invia',
    },
    streaming: { statusLabel: 'STREAMING', stopLabel: 'Stop streaming' },
    completed: { completedLabel: 'COMPLETED', copyLabel: 'Copia', regenerateLabel: 'Rigenera' },
    empty: {
      title: 'Nessun documento nella tua libreria',
      body: 'Carica un PDF dalla libreria per iniziare a chiedere.',
      cta: 'Vai alla libreria',
    },
    error: {
      connection: { title: 'Connessione persa', body: 'Retry automatico in corso…', action: 'Riprova ora' },
      timeout: { title: 'Risposta lenta', body: 'Vuoi continuare ad aspettare?', action: 'Continua attesa', alt: 'Cancella' },
      partial: { title: 'Risposta incompleta', body: 'Lo stream si è interrotto.', action: 'Ripeti query' },
      server: { title: 'Errore del server', body: 'Riprova tra qualche istante.', action: 'Riprova' },
    },
  },
},
```

- [ ] **Step 4: Run the existing Phase 1 integration test suite to ensure no regression**

```bash
cd apps/web && pnpm test src/app/'(authenticated)'/knowledge-base/global/_components/__tests__/ --run
```

Expected: all Phase 1 S1..S7 + new S8 PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/'(authenticated)'/knowledge-base/global/_components/KbGlobaleView.tsx
git commit -m "feat(kb-globale): #1482 Task 9 — orchestrator viewer + drawer branches (D-G + D-J + lazy)

- URL SSOT extension: ?docId=&page= (viewer), ?ask=1 (drawer)
- KbDocViewerDesktopLazy / KbDocViewerMobileLazy / DrawerShellLazy via dynamic({ ssr:false })
- useKbDocDetail({docId}) reused 1:1 (D-H)
- onResultClick wires KbSearchResultsDesktop → openViewer
- LABELS extended with viewer + drawer sections (i18n in Task 10)
- Phase 1 integration tests still PASS (S1..S7 no regression)"
```

---

## Task 10: Integration tests + bundle budget + matrix + i18n

**Files:**
- Modify: `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.integration.test.tsx` (S8..S11)
- Modify: `apps/web/.bundle-budgets.json`
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md` (4 rows pending → done)
- Modify: `apps/web/messages/it.json` (extend pages.kbGlobale.*)
- Modify: `apps/web/messages/en.json` (parity)

- [ ] **Step 1: Add 4 MSW integration tests (S8–S11)**

Open `apps/web/src/app/(authenticated)/knowledge-base/global/_components/__tests__/KbGlobaleView.integration.test.tsx` and append:

```tsx
import { http, HttpResponse } from 'msw';
import { server } from '@/test/msw-server';
import { Document } from 'react-pdf';

vi.mock('react-pdf', () => ({
  Document: ({ children }: { children: React.ReactNode }) => <div data-testid="pdf-document">{children}</div>,
  Page: ({ pageNumber }: { pageNumber: number }) => <div data-testid={`pdf-page-${pageNumber}`}>p {pageNumber}</div>,
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

describe('KbGlobaleView Phase 2 — MSW integration S8..S11', () => {
  // S8: viewer mounts via ?docId param
  it('S8 — ?docId mounts KbDocViewerDesktop with doc detail', async () => {
    server.use(
      http.get('/api/v1/kb-docs/:id', () => HttpResponse.json({
        id: 'doc-1', title: 'Test Rulebook', fileUrl: 'https://x.com/d.pdf', pageCount: 12,
      })),
    );
    // ... render with `?docId=doc-1&page=3` URL
    // assert pdf-page-3 rendered
  });

  // S9 — drawer ask flow → completed
  it('S9 — ?ask=1 + ask flow streams to completed state', async () => {
    server.use(
      http.post('/api/v1/knowledge-base/ask/global', () => {
        const stream = new ReadableStream({
          start(c) {
            const enc = new TextEncoder();
            c.enqueue(enc.encode(`data: ${JSON.stringify({ type: 0, data: { message: 'go' } })}\n\n`));
            c.enqueue(enc.encode(`data: ${JSON.stringify({ type: 7, data: { token: 'hi' } })}\n\n`));
            c.enqueue(enc.encode(`data: ${JSON.stringify({ type: 4, data: { totalTokens: 1, promptTokens: 1, completionTokens: 1, estimatedReadingTimeMinutes: 0, confidence: null } })}\n\n`));
            c.close();
          },
        });
        return new HttpResponse(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } });
      }),
    );
    // ... render with `?ask=1`, click suggestion, await completed state
  });

  // S10 — citation click deep-links to ?docId=&page=
  it('S10 — citation click pushes ?docId=&page= URL', async () => {
    // ... seed drawer state with completed + citations
    // ... click on citation, assert router.push called with expected params
  });

  // S11 — completed-empty state when BE returns Complete(0,...) without Citations
  it('S11 — empty accessible games shows completed-empty CTA', async () => {
    server.use(
      http.post('/api/v1/knowledge-base/ask/global', () => {
        const stream = new ReadableStream({
          start(c) {
            const enc = new TextEncoder();
            c.enqueue(enc.encode(`data: ${JSON.stringify({ type: 0, data: { message: 'Ricerca nella tua libreria...' } })}\n\n`));
            c.enqueue(enc.encode(`data: ${JSON.stringify({ type: 4, data: { totalTokens: 0, promptTokens: 0, completionTokens: 0, estimatedReadingTimeMinutes: 0, confidence: null } })}\n\n`));
            c.close();
          },
        });
        return new HttpResponse(stream, { status: 200, headers: { 'content-type': 'text/event-stream' } });
      }),
    );
    // ... render with `?ask=1`, trigger ask, assert empty CTA appears
  });
});
```

(Adjust render helpers per existing Phase 1 test scaffolding — the test framework + MSW setup are already established by PR #1688.)

- [ ] **Step 2: Run all integration tests**

```bash
cd apps/web && pnpm test src/app/'(authenticated)'/knowledge-base/global --run
```

Expected: S1..S7 (Phase 1) + S8..S11 (Phase 2) all PASS.

- [ ] **Step 3: Update bundle budget**

Open `apps/web/.bundle-budgets.json`. Find the entry for `/knowledge-base/global` (added by Phase 1 Task 8). Update:

```json
{
  "route": "/knowledge-base/global",
  "kbTarget": 120,
  "kbBudgetSource": "Phase 1 (40 KB Foundation) + Phase 2 (80 KB Interactions, all lazy chunks): see docs/superpowers/plans/2026-05-30-kb-globale-interactions.md §2 D-M"
}
```

Run the bundle analyzer:

```bash
cd apps/web && pnpm build 2>&1 | grep -i "knowledge-base/global"
```

Expected: bundle size for `/knowledge-base/global` ≤ 120 KB. Phase 2 chunks (`KbDocViewerDesktop`, `DrawerShell`) appear as separate lazy chunks.

- [ ] **Step 4: Update v2 migration matrix (4 rows pending → done)**

Open `docs/for-developers/frontend/v2-migration-matrix.md`. Find the 4 rows for kb-globale Phase 2 components (sources `sp4-kb-globale.jsx`):
- `KbDocViewerDesktop`
- `KbDocViewerMobile`
- `DrawerShell` (and 5 sub-state variants)
- `CitationPill`

Update their `Status` column from `pending` to `done` and add the PR number (will be filled at PR creation time — leave a TODO marker `PR #TBD`).

- [ ] **Step 5: Extend i18n catalogs**

Open `apps/web/messages/it.json` and add under `pages.kbGlobale`:

```json
"viewer": {
  "pageLabel": "Pagina {n}",
  "zoomIn": "Zoom in",
  "zoomOut": "Zoom out",
  "zoomReset": "Reset",
  "thumbnailsLabel": "Pagine",
  "closeLabel": "Chiudi",
  "pageOfTotal": "{cur} / {total}"
},
"drawer": {
  "title": "Ask the Meeple",
  "subtitle": "Knowledge Base",
  "closeLabel": "Chiudi",
  "idle": {
    "welcomeTitle": "Chiedimi qualsiasi cosa sui tuoi giochi",
    "welcomeBody": "Cerco nei tuoi PDF e cito le pagine esatte.",
    "suggestionsLabel": "Suggerimenti",
    "placeholder": "Chiedi al Meeple…",
    "sendLabel": "Invia"
  },
  "streaming": { "statusLabel": "STREAMING", "stopLabel": "Stop streaming" },
  "completed": { "completedLabel": "COMPLETED", "copyLabel": "Copia", "regenerateLabel": "Rigenera" },
  "empty": {
    "title": "Nessun documento nella tua libreria",
    "body": "Carica un PDF dalla libreria per iniziare a chiedere.",
    "cta": "Vai alla libreria"
  },
  "error": {
    "connection": { "title": "Connessione persa", "body": "Retry automatico in corso…", "action": "Riprova ora" },
    "timeout": { "title": "Risposta lenta", "body": "Vuoi continuare ad aspettare?", "action": "Continua attesa", "alt": "Cancella" },
    "partial": { "title": "Risposta incompleta", "body": "Lo stream si è interrotto.", "action": "Ripeti query" },
    "server": { "title": "Errore del server", "body": "Riprova tra qualche istante.", "action": "Riprova" }
  }
}
```

Mirror parity in `apps/web/messages/en.json` (English translations).

Update `KbGlobaleView.tsx` to consume these i18n keys via `useTranslations()` (replace the in-file `LABELS.viewer` + `LABELS.drawer` block with `t('viewer.pageLabel', ...)` calls — mirror the Phase 1 i18n extraction pattern from Task 8 of the Foundation plan).

- [ ] **Step 6: Final verify (typecheck + lint + tests)**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm lint:tokens && pnpm test src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts src/lib/api/clients/__tests__/kbAskClient.test.ts src/hooks/__tests__/useKbAskStream.test.ts src/components/features/kb-globale/__tests__/ src/app/'(authenticated)'/knowledge-base/global/_components/__tests__/ --run
```

Expected: all green; 0 lint errors; 0 token violations.

- [ ] **Step 7: Commit**

```bash
git add apps/web/.bundle-budgets.json apps/web/messages/it.json apps/web/messages/en.json apps/web/src/app/'(authenticated)'/knowledge-base/global/_components/KbGlobaleView.tsx apps/web/src/app/'(authenticated)'/knowledge-base/global/_components/__tests__/KbGlobaleView.integration.test.tsx docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "test(kb-globale): #1482 Task 10 — S8..S11 integration + budget + matrix + i18n

- S8 viewer mount via ?docId
- S9 ask flow: stream → completed
- S10 citation click deep-link → ?docId=&page=
- S11 completed-empty state (zero accessible games)
- .bundle-budgets.json: /knowledge-base/global ≤ 120 KB (40 Foundation + 80 Interactions lazy)
- v2-migration-matrix.md: 4 rows pending → done
- messages/{it,en}.json: pages.kbGlobale.viewer.* + drawer.* extracted
- KbGlobaleView consumes i18n keys (LABELS constants removed)"
```

- [ ] **Step 8: File P3 follow-up issues**

```bash
gh issue create --title "feat(kb): expose chunkId in Snippet for /ask/global" --body "Upgrade D-E to chunk-level scroll. Currently Snippet is (text, source, page, line, score). Add ChunkId field to enable CitationPill click → ?docId=&chunkId= deep-link (NOT just page). Spec-panel decision: docs/superpowers/plans/2026-05-30-kb-globale-interactions.md §2 D-E." --label "enhancement,area/backend,P3"

gh issue create --title "feat(kb): instruct LLM to emit [N] citation markers in BuildSystemPrompt" --body "Unlocks D-F inline rendering. Current BuildSystemPrompt does not instruct numbered references; FE renders citations as a numbered list below the answer. Adding '[N]' markers would enable inline CitationPill rendering as designed in sp4-kb-globale.jsx. Spec: docs/superpowers/plans/2026-05-30-kb-globale-interactions.md §2 D-F." --label "enhancement,area/backend,P3"

gh issue create --title "refactor(hooks): align useAgentChatStream retry policy with useKbAskStream" --body "useKbAskStream uses backoff exp [1s,3s,9s] max 3 retry per D-L; useAgentChatStream uses linear 2s × 2 retry. Decide which is correct UX (consult Nygard policy) and unify. Spec: docs/superpowers/plans/2026-05-30-kb-globale-interactions.md §2 D-L." --label "enhancement,tech-debt,P3"
```

- [ ] **Step 9: Push branch + open PR**

```bash
git push -u origin feature/issue-1482-phase2-interactions

gh pr create --base main-dev --title "feat(kb-globale): #1482 Phase 2 Interactions — viewer + drawer + citations" --body "$(cat <<'EOF'
## Summary

Implements Phase 2 of [#1482](https://github.com/meepleAi-app/meepleai-monorepo/issues/1482) `/knowledge-base/global` route — the Interactions layer:

- ✅ **`KbDocViewerDesktop`** (3-col react-pdf layout, lazy chunk)
- ✅ **`KbDocViewerMobile`** (bottom-sheet variant, tab switcher PDF/Citations)
- ✅ **`DrawerShell`** (FSM 5-state: idle / streaming / completed / **completed-empty** / **error×4-sub-kinds**)
- ✅ **`CitationPill`** (page-level deep-link `?docId=&page=`)
- ✅ **`useKbAskStream`** (greenfield FSM hook; backoff exp [1s,3s,9s], max 3 retry, 30s timeout)
- ✅ **`kb-ask.schemas.ts`** (Zod discriminated union on numeric event types)
- ✅ Orchestrator extension: viewer + drawer branches lazy-mounted from URL params

## Spec-panel decisions implemented

| # | Decision | Where |
|---|---|---|
| D-E | Page-level deep-link (Snippet has no chunkId) | CitationPill onClick + KbDocViewer* highlighting |
| D-F | Citations as numbered list below answer (LLM no `[N]` markers) | DrawerCompleted |
| D-G | react-pdf@^10.4.1 lazy + CDN worker | KbDocViewerDesktop / Mobile |
| D-H | Reuse `useKbDocDetail` / `useKbChunksList` 1:1 | KbGlobaleView orchestrator |
| D-I | Zod discriminated union numeric type | kb-ask.schemas.ts |
| D-J | Mobile = BottomSheet primitive variant | KbDocViewerMobile |
| D-K | MSW SSE mock + jest-axe + 12 FSM tests | tests/* |
| D-L | 5 states (incl. completed-empty + 4 error sub-kinds) | useKbAskStream + DrawerShell |
| D-M | Bundle ≤ 80 KB lazy (total ≤ 120 KB) | .bundle-budgets.json |

Spec: `docs/superpowers/plans/2026-05-30-kb-globale-interactions.md`

## Test plan

- [ ] `pnpm test src/lib/api/schemas/__tests__/kb-ask.schemas.test.ts` → 7 PASS
- [ ] `pnpm test src/lib/api/clients/__tests__/kbAskClient.test.ts` → 4 PASS
- [ ] `pnpm test src/hooks/__tests__/useKbAskStream.test.ts` → 12 PASS
- [ ] `pnpm test src/components/features/kb-globale/__tests__/` → 25 PASS (CitationPill 5 + Desktop 5 + Mobile 4 + Drawer 11)
- [ ] `pnpm test src/app/'(authenticated)'/knowledge-base/global/_components/__tests__/` → S1..S11 PASS
- [ ] `pnpm typecheck` → 0 errors
- [ ] `pnpm lint` → 0 errors
- [ ] `pnpm lint:tokens` → 0 violations
- [ ] `pnpm build` → /knowledge-base/global bundle ≤ 120 KB

## Follow-up issues filed (P3)

- BE: expose chunkId in Snippet (#TBD) — upgrade D-E to chunk-level
- BE: instruct LLM `[N]` markers (#TBD) — unlock D-F inline
- Hooks: align retry policy (#TBD) — remove D-L divergence

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## §3 — Self-review (engineer should run this before submitting PR)

### Spec coverage

| Spec section | Task |
|---|---|
| D-E (page-level deep-link) | Task 1 (schemas) + Task 4 (CitationPill) + Task 6/7 (viewer wire) + Task 9 (orchestrator) |
| D-F (no inline `[N]` parser) | Task 0 (verify) + Task 8 (DrawerCompleted numbered list) |
| D-G (react-pdf lazy + CDN worker) | Task 6 (Desktop) + Task 7 (Mobile) + Task 9 (dynamic import) |
| D-H (reuse `useKbDocDetail` / `useKbChunksList`) | Task 5 (verify) + Task 9 (orchestrator wires) |
| D-I (discriminated union numeric) | Task 1 (schemas) |
| D-J (BottomSheet base) | Task 7 (Mobile) |
| D-K (MSW + jest-axe + 12 FSM tests) | Task 3 (12 FSM) + Tasks 4/6/7/8 (jest-axe) + Task 10 (MSW S8..S11) |
| D-L (5 states + 4 error sub-kinds + backoff exp) | Task 3 (hook) + Task 8 (UI) |
| D-M (≤ 80 KB Phase 2 lazy, ≤ 120 KB total) | Task 9 (lazy split) + Task 10 (budget update + verify) |
| 4 components scope | Tasks 4 (CitationPill), 6 (Desktop), 7 (Mobile), 8 (Drawer) |
| 1 hook | Task 3 (useKbAskStream) |
| 1 new schema file | Task 1 (kb-ask.schemas.ts) |
| Orchestrator URL SSOT extension | Task 9 |
| i18n catalogs | Task 10 |
| Bundle budget | Task 10 |
| Migration matrix | Task 10 |
| Follow-up issues (P3) | Task 10 step 8 |

### Type consistency check

- `KbCitation` (schemas) used by `useKbAskStream` (hook), `KbDocViewerCitation` (viewer) — viewer transforms `KbCitation` → `KbDocViewerCitation` in orchestrator (Task 9 has the explicit `.map(c => ({...}))`).
- `KbAskStreamState` (hook) consumed by `DrawerShell` props (Task 8) — same name throughout.
- `KbAskError.kind` literal type — `'connection' | 'timeout' | 'partial' | 'server'` consistent across hook (Task 3) + DrawerError (Task 8) + DrawerShell (Task 8).
- `onClick` payload `{ docId, page }` consistent across CitationPill (Task 4) + orchestrator `onCitationClick` (Task 9).

### Placeholder scan

- ✅ No "TBD" or "TODO" in step content (PR body has `#TBD` for follow-up issue numbers — acceptable, filled at file-issue time).
- ✅ No "implement later" / "similar to Task N".
- ✅ Every step has complete code blocks (verified).

---

**End of plan. Self-review complete. Ready for execution.**
