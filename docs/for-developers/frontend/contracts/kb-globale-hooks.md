# `/knowledge-base/global` Phase 0.5 Hook Composition Contract — kb-globale (Issue #1482)

> **Mandatory Phase 0.5 deliverable** per the Tier L dispatch rule (anti-pattern from Wave C.1 PR #697 RCA: do NOT dispatch implementation subagents without a sub-hook contract). This doc grounds every hook/schema/endpoint in **verified existing infrastructure** (P74) and isolates what is **greenfield**. Implementation is a separate session.
>
> Date: 2026-05-26 · Branch: `feature/issue-1482-kb-globale-phase05` · Pattern parent: [`gamebook-upload-hooks.md`](./gamebook-upload-hooks.md).

## §1. Overview

### Tier classification

**Tier L** — 10 components + an AI streaming drawer with a 4-state FSM + cross-game search + doc viewer (desktop/mobile) + inline editor. Multiple independent data hooks. Bundle-heavy (PDF viewer + SSE drawer + editor).

### Dispatch strategy

Two-phase, mirroring gamebook-upload (Foundation + Interactions):

- **Phase 1 — Foundation** (~Tier M): route shell + `HeroSearch` + `KbHomeDesktop` + `KbSearchResultsDesktop` + `FilterAccordion` + `KbEmptyState` + `useGlobalKbSearch` hook + Zod schemas. No AI drawer, no editor.
- **Phase 2 — Interactions** (~Tier M): `KbDocViewerDesktop` + `KbDocViewerMobile` + `KbEditorDesktop` + `DrawerShell` (4 AI states) + `CitationPill` + `useKbAskStream` hook + lazy-split bundles.

**Rationale**: Foundation ships a usable read/search surface fast; Interactions layers the heavy AI + editor behind lazy boundaries. Each phase is independently reviewable and CI-gateable.

### Parent issue + planned sub-PRs

- Parent: [#1482](https://github.com/meepleAi-app/meepleai-monorepo/issues/1482)
- Sub-PR A (Foundation): search + home + results + filters + empty + `useGlobalKbSearch`
- Sub-PR B (Interactions): viewer + editor + AI drawer + `useKbAskStream` + `CitationPill`
- (This PR delivers **only the contract** — no component code.)

## §2. Route decision

### Recommendation: `/knowledge-base/global`

| Candidate | Verdict | Rationale |
|---|---|---|
| `/knowledge-base/global` | ✅ **CHOSEN** | Semantically clear (`knowledge-base` domain + `/global` cross-game scope); mirrors the game-scoped `/library/[gameId]/kb` (#1481); aligns with issue #1482 naming; deep-linkable `?q=&docId=&chunkId=` |
| `/knowledge-base/search` | ❌ | "search" undersells the home/viewer/editor surfaces; the route is more than search |
| split `/kb/search` + `/kb/[id]/viewer` + `/kb/[id]/edit` | ⚠️ defer | Premature fragmentation; viewer/editor can be query-param sub-states of `/knowledge-base/global` in v1, split later if deep-link SEO demands it |

### Current route state (verified)

- `apps/web/src/app/(authenticated)/knowledge-base/page.tsx` → `redirect('/library')` (legacy)
- `apps/web/src/app/(authenticated)/knowledge-base/[id]/page.tsx` → per-doc viewer (exists)
- `/library/[gameId]/kb` → game-scoped KB hub (#1481)
- **NO** `/knowledge-base/global` route exists yet → greenfield page shell.

**Action**: create `apps/web/src/app/(authenticated)/knowledge-base/global/page.tsx`. Leave the `/knowledge-base` → `/library` redirect intact (it is the bare-route default; `/global` is the new explicit destination). Nav entry for global KB search is a follow-up (out of scope here).

### URL state SSOT

```
/knowledge-base/global                         → KbHomeDesktop (landing)
/knowledge-base/global?q=azul+scoring          → KbSearchResultsDesktop
/knowledge-base/global?q=...&docId=xxx          → KbDocViewer (query-param sub-state)
/knowledge-base/global?q=...&docId=xxx&chunkId=yyy → viewer scrolled to chunk
/knowledge-base/global?docId=xxx&edit=1         → KbEditorDesktop (curator)
```

Filters live in URL too: `?docType=rulebook,faq&game=<id>&lang=it`. No `useState` mirror for shareable state (mirror gamebook-upload §6 SSOT discipline).

## §3. AI drawer FSM (the Tier-L core)

The AI "Ask" drawer (`DrawerShell` + 4 variants) is the highest-risk surface. It **mirrors the proven `useAgentChatStream`** SSE precedent (verified at `apps/web/src/hooks/useAgentChatStream.ts`).

### 4-state FSM

```
        sendQuery()                Token events            Complete event
  idle ─────────────▶ streaming ──(accumulate)──▶ streaming ─────────────▶ completed
   ▲                     │                                                    │
   │                     │ Error event / connection drop / timeout            │ reset()/newQuery()
   │                     ▼                                                    │
   │                  error ◀───────────────────────────────────────────────┘
   └──── reset() ────────┘
```

| State | Trigger in | Renders | Exit |
|---|---|---|---|
| **idle** | initial / `reset()` | welcome msg + 3 suggested-question buttons + input | `sendQuery()` → streaming |
| **streaming** | `sendQuery()` | user bubble + partial answer (token-by-token, `chunkAppend` anim) + 3-dot pulse + inline `CitationPill`s appearing as `Citations` events arrive + token/latency meta + **Stop** button | `Complete` → completed · `Error`/drop/timeout → error |
| **completed** | `Complete` event | full answer + all numbered citations + completion meta (✓ tokens · time · N citations) + actions (Copy/Regenerate/Share/Save) | `sendQuery()` (new) → streaming · `reset()` → idle |
| **error** | `Error` event / connection drop / timeout | error banner (3 sub-kinds: connection 📡 auto-retry 3s · timeout ⏱️ continue/cancel · partial ⚠️ shows truncated text + `[stream interrotto]` + retry) | retry → streaming · `reset()` → idle |

### Hook: `useKbAskStream` (greenfield, mirrors `useAgentChatStream`)

```ts
// apps/web/src/hooks/useKbAskStream.ts (NEW — mirror useAgentChatStream.ts)
export interface KbAskStreamState {
  readonly status: 'idle' | 'streaming' | 'completed' | 'error';
  readonly partialText: string;          // accumulated tokens
  readonly citations: readonly KbCitation[]; // accumulated from Citations events
  readonly totalTokens: number;
  readonly elapsedMs: number;
  readonly error: { kind: 'connection' | 'timeout' | 'partial'; message: string } | null;
  readonly connectionStatus: ConnectionStatus; // reuse from useAgentChatStream
  readonly retryCount: number;
}
export function useKbAskStream(): {
  state: KbAskStreamState;
  ask: (query: string, scope?: { gameId?: string }) => void;
  stop: () => void;
  reset: () => void;
};
```

- **SSE wire format**: reuse `StreamingEventType` enum + `CitationSchema` from `streaming.schemas.ts` (verified — `StateUpdate/Citations/Token/Complete/Error`). The kb-ask endpoint emits the same `RagStreamingEvent` shape as agent chat.
- **Endpoint** (BACKEND — see §7 open questions): `POST /api/v1/kb/ask` (cross-game) or `/api/v1/games/{gameId}/kb/ask` (scoped). Body `{ query, gameId? }`. **Greenfield FE client method** required.
- **Citations**: each citation carries `documentId, chunkId, chunkPosition, page?, snippet, score` so `CitationPill` click can deep-link the viewer to the chunk (`?docId=&chunkId=`).

## §4. Hook composition matrix

### Reuse vs greenfield (all verified)

| Hook / schema | Path | Verdict |
|---|---|---|
| `useAgentChatStream` (SSE state machine) | `src/hooks/useAgentChatStream.ts` | ✅ **MIRROR** for `useKbAskStream` |
| `StreamingEventType` + `CitationSchema` + `StreamingCitationsSchema` | `src/lib/api/schemas/streaming.schemas.ts` | ✅ **REUSE as-is** |
| `KbDocDetail` schema | `src/lib/api/schemas/kb-chunks.schemas.ts` | ✅ **REUSE** (viewer hero) |
| `KbChunkSummary` / `KbChunkDetail` | `src/lib/api/schemas/kb-chunks.schemas.ts` | ✅ **REUSE** (results + viewer body) |
| `useKbDocDetail(docId)` | `src/hooks/queries/useKbDocDetail.ts` | ✅ **REUSE** (viewer open) |
| `useKbChunksList(docId)` (infinite) | `src/hooks/queries/useKbChunksList.ts` | ✅ **REUSE** (viewer lazy scroll) |
| `useKbHub` (per-game status) | `src/hooks/queries/useKbHub.ts` | ✅ **MIRROR** → `useGlobalKbStatus()` |
| `useGlobalKbSearch(query, filters)` | — | ❌ **GREENFIELD** (BE `VectorSemanticSearchQueryHandler` supports cross-game per its source comment, but NO FE hook/client) |
| `useKbAskStream()` | — | ❌ **GREENFIELD** (mirror useAgentChatStream) |
| KB editor mutation (`useUpdateKbDocMeta`) | — | ❌ **GREENFIELD** (verify BE PATCH endpoint exists before Phase 2) |

### Composition tree

```
/knowledge-base/global/page.tsx (server shell + Suspense)
└─ KbGlobaleView (client orchestrator — reads URL SSOT)
   ├─ HeroSearch ────────────── useGlobalKbSearch (debounced)   [Foundation]
   ├─ (no q) KbHomeDesktop ──── useGlobalKbStatus + recent docs [Foundation]
   ├─ (q)    KbSearchResultsDesktop                              [Foundation]
   │         ├─ FilterAccordion (URL-param facets)
   │         ├─ result cards (KbChunkSummary)
   │         └─ KbEmptyState (zero results)
   ├─ (docId) KbDocViewer{Desktop|Mobile} ── useKbDocDetail + useKbChunksList  [Interactions, lazy]
   │          └─ CitationPill (in-chunk refs)
   ├─ (edit=1) KbEditorDesktop ── useUpdateKbDocMeta            [Interactions, lazy, curator]
   └─ DrawerShell (AI ask) ───── useKbAskStream                 [Interactions, lazy]
      ├─ idle / streaming / completed / error variants
      └─ CitationPill (streaming + completed)
```

No-waterfall: search, status, and ask hooks are independent. Viewer hooks fire only on `docId` presence. Drawer hook fires only on drawer-open.

## §5. Schema contract (greenfield additions)

New file `apps/web/src/lib/api/schemas/kb-globale.schemas.ts`:

```ts
// Cross-game search request/response
export const GlobalKbSearchFiltersSchema = z.object({
  docType: z.array(z.enum(['rulebook', 'faq', 'errata', 'guide'])).optional(),
  gameId: z.string().uuid().nullable().optional(),
  language: z.string().optional(),
});
export const GlobalKbSearchResultSchema = z.object({
  // mirror KbChunkSummary + doc context for cross-game display
  chunkId: z.string(),
  docId: z.string(),
  docTitle: z.string(),
  gameId: z.string().uuid().nullable(),
  gameName: z.string().nullable(),
  docType: z.enum(['rulebook', 'faq', 'errata', 'guide']),
  headingPath: z.array(z.string()),
  snippet: z.string(),
  pageNumber: z.number().int().nullable(),
  score: z.number(),
});
export const GlobalKbSearchResponseSchema = z.object({
  items: z.array(GlobalKbSearchResultSchema),
  nextCursor: z.string().nullable(),
  totalCount: z.number().int().nonnegative(),
});

// kb-ask citation (extends streaming CitationSchema with chunk nav)
export const KbCitationSchema = CitationSchema.extend({
  chunkId: z.string(),
  chunkPosition: z.number().int().nonnegative(),
});
```

**Reuse** `CitationSchema` (import from `streaming.schemas.ts`), `KbDocDetail`/`KbChunkDetail` (from `kb-chunks.schemas.ts`), `KbDocType` enum.

## §6. 10 component specs (brief)

| # | Component | Phase | Props (sketch) | Reuse |
|---|---|---|---|---|
| 1 | `HeroSearch` | F | `{ value, onChange, onSubmit, placeholder, labels }` | — |
| 2 | `KbHomeDesktop` | F | `{ status, recentDocs, shortcuts, labels }` | mirror kb-hub `HubDefault` |
| 3 | `KbSearchResultsDesktop` | F | `{ results, filters, onFilterChange, isLoading, labels }` | mirror toolkits-index body |
| 4 | `FilterAccordion` | F | `{ facets, selected, onToggle, labels }` | — |
| 5 | `KbEmptyState` | F | `{ kind: 'no-docs'\|'no-results', labels, onAction }` | mirror GamesEmptyState |
| 6 | `KbDocViewerDesktop` | I (lazy) | `{ doc, chunks, activeChunkId, citations, labels }` | useKbDocDetail + useKbChunksList |
| 7 | `KbDocViewerMobile` | I (lazy) | same + bottom-sheet variant | shared Drawer primitive |
| 8 | `KbEditorDesktop` | I (lazy) | `{ doc, onSave, labels }` | needs `useUpdateKbDocMeta` (verify BE) |
| 9 | `DrawerShell` (+4 AI states) | I (lazy) | `{ state, onAsk, onStop, onReset, labels }` | useKbAskStream (mirror useAgentChatStream) |
| 10 | `CitationPill` | I | `{ n, ref, onClick }` | custom v2 (NOT chat-unified CitationBadge) |

All pure presentational, labels-injected, DS-15 tokens, `data-slot`, jest-axe per file (standard this-session pattern).

## §7. Open questions (MUST resolve before Phase 1/2 dispatch)

| # | Question | Owner | Blocks |
|---|---|---|---|
| Q1 | **Does a cross-game KB search FE endpoint exist?** BE `VectorSemanticSearchQueryHandler` supports it (source comment) but no FE client method found. Need `POST /api/v1/kb/search` route + response contract confirmed. | BE | `useGlobalKbSearch` (Foundation blocker) |
| Q2 | **Does a kb-ask SSE endpoint exist** (`POST /api/v1/kb/ask`) or only agent-chat? If only per-agent, the AI drawer needs a kb-scoped ask endpoint. | BE | `useKbAskStream` (Interactions blocker) |
| Q3 | **CitationPill click behavior**: nav viewer to chunk, open modal, or highlight? Recommend: deep-link `?docId=&chunkId=` + scroll. | Design | Drawer + viewer integration |
| Q4 | **RBAC for global search**: private games included? Only shared/community? Only user's library? | Product/BE | search scope + result filtering |
| Q5 | **Editor scope**: edit doc content (chunks) or only metadata (title/type/tags/lang)? Recommend metadata-only v1. | Product | `KbEditorDesktop` + `useUpdateKbDocMeta` |
| Q6 | **Filter facets** beyond docType/game/language? (tags? date?) | Design | `FilterAccordion` |

**Foundation (Phase 1) can start once Q1 + Q4 are resolved. Interactions (Phase 2) needs Q2 + Q3 + Q5.**

## §8. Bundle budget

Umbrella target **≤ +150 KB** (vs gamebook-upload's +120 KB — kb-globale adds PDF/markdown viewer + SSE drawer + editor).

- **Foundation**: ~50 KB (search + home + results + filters + empty + 1 hook)
- **Interactions**: ~100 KB, ALL lazy-split via `dynamic(() => import(...), { ssr: false })` (mirror `/discover` `_DiscoverBelowFoldRows` pattern):
  - viewer chunk (PDF/markdown render) — load on `docId`
  - editor chunk — load on `edit=1` (curator-only)
  - AI drawer chunk (SSE + citation render) — load on drawer-open
- Add a `.bundle-budgets.json` entry for `/knowledge-base/global`.

## §9. Test plan

### Sub-hook isolation (mirror gamebook-upload §16)

```
useKbAskStream:
  - idle → streaming on ask()
  - parses CitationSchema from Citations event
  - accumulates tokens → partialText
  - Complete event → completed + final citations
  - Error event → error (3 sub-kinds)
  - stop() aborts mid-stream
  - retry on connection drop (max 2)
useGlobalKbSearch:
  - debounced query → results
  - filters change → refetch with facets
  - empty results → totalCount 0
  - error → error state
```

### Component + integration

- Per-component unit tests + jest-axe (standard).
- Drawer FSM integration (MSW-mocked SSE) — verify the 4-state transitions.
- URL-SSOT integration: `?q=`/`?docId=`/`?edit=1` route to correct surface.

## §10. Acceptance criteria (for the eventual impl PRs)

- [ ] Q1–Q6 resolved (this contract surfaces them; resolution may need BE issues)
- [ ] Foundation PR: 5 components + `useGlobalKbSearch` + schemas + route shell + tests
- [ ] Interactions PR: 5 components + `useKbAskStream` + lazy-split + tests
- [ ] AI drawer 4-state FSM matches mockup; SSE mirrors `useAgentChatStream`
- [ ] Citations reuse `streaming.schemas.ts`; viewer deep-link via `chunkId`
- [ ] Bundle ≤ +150 KB (Interactions all lazy)
- [ ] DS-15 + jest-axe per component
- [ ] Matrix 10 rows pending → done; route summary updated

---

**Generated by Claude Code (Opus 4.7).** Issue #1482 Phase 0.5. **This contract delivers design only — no component implementation.** All reuse claims verified against existing code (P74); greenfield items explicitly flagged. Implementation dispatches in 2 follow-up PRs (Foundation + Interactions) once §7 open questions resolve.
