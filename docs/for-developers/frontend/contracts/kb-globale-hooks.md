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
  docId: z.string().uuid(),
  docTitle: z.string(),
  // BE guarantees gameId/gameName are populated (results without a resolvable
  // game are dropped during enrichment), so both are non-nullable.
  gameId: z.string().uuid(),
  gameName: z.string(),
  // BE emits the raw DocumentType enum string (e.g. "Rulebook"); FE narrows
  // case-insensitively if it needs the enum union.
  docType: z.string(),
  // Best-effort. The chunk metadata heading is not materialized in the
  // pgvector result set yet (D2 known limitation, BE PR-1 #1672), so this
  // ships as a nullable single string instead of a hierarchical array.
  // Will become z.array(z.string()) once the chunk metadata is materialized.
  headingPath: z.string().nullable(),
  snippet: z.string(),
  pageNumber: z.number().int().nullable(),
  score: z.number(),
});
export const GlobalKbSearchResponseSchema = z.object({
  // Field name `results` matches the BE DTO (GlobalKbSearchResponseDto).
  results: z.array(GlobalKbSearchResultSchema),
  nextCursor: z.string().nullable(),
  // The BE intentionally does NOT emit a `totalCount` for cross-game search
  // because computing an exact count across N games is too expensive
  // (Nygard, spec-panel D6). Use `hasMore` to drive "load more" UI.
  hasMore: z.boolean(),
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

| # | Question | Owner | Status | Blocks |
|---|---|---|---|---|
| Q1 | **Does a cross-game KB search FE endpoint exist?** | BE | ✅ **RESOLVED** by #1661 PR-1 (`POST /api/v1/knowledge-base/search/global`, response = `GlobalKbSearchResponseDto` mirroring §5 above; cursor pagination + hasMore). | (unblocked) |
| Q2 | **Does a kb-ask SSE endpoint exist** (`POST /api/v1/kb/ask`) or only agent-chat? | BE | ✅ **RESOLVED** by #1661 PR-2 — `POST /api/v1/knowledge-base/ask/global` emitting `RagStreamingEvent` (mirrors `useAgentChatStream` wire format). Body: `{ query: string, language?: string, topK?: number }`. RBAC-filtered (public ∪ owned ∪ all-for-admin). | `useKbAskStream` (Interactions — now UNBLOCKED) |
| Q3 | **CitationPill click behavior**: nav viewer to chunk, open modal, or highlight? Recommend: deep-link `?docId=&chunkId=` + scroll. | Design | 🟡 **DEFERRED to Phase 2** (decision at Interactions dispatch — recommendation stands: deep-link + scroll) | Drawer + viewer integration |
| Q4 | **RBAC for global search**: private games included? Only shared/community? Only user's library? | Product/BE | ✅ **RESOLVED** by #1661 PR-1: accessible = `SharedGame.IsRagPublic` ∪ `UserLibraryEntry.OwnershipDeclaredAt != null` (user-owned library), excludes other users' private games. Admin/SuperAdmin → all non-deleted. | (unblocked) |
| Q5 | **Editor scope**: edit doc content (chunks) or only metadata (title/type/tags/lang)? Recommend metadata-only v1. | Product/BE | 🚧 **BE-BLOCKED** — `PATCH /kb-docs/{id}` endpoint does NOT exist (verified 2026-05-29). Opened **#1687** (metadata-only v1, mirrors recommendation). `KbEditorDesktop` (#8) **DROPPED from Phase 2 v1** until #1687 lands. | `KbEditorDesktop` deferred |
| Q6 | **Filter facets** beyond docType/game/language? (tags? date?) | Design/BE | 🚧 **BE-BLOCKED** — see D-B below. Opened **#1686** for server-side facets. `FilterAccordion` (#4) **DEFERRED from Foundation** until #1686 lands. | `FilterAccordion` deferred |

### Divergences vs shipped BE (verified 2026-05-29 against #1661 merge)

| # | Divergence | Decision | Impact |
|---|---|---|---|
| **D-A** | `ask/global` request body is `{ Query, Language?, TopK? }` — **no `gameId`** (purely cross-game). Contract §3 ipotised `scope?: { gameId? }`. | `useKbAskStream.ask(query, opts?: { language?: string; topK?: number })`. Default `language='it'`. Phase 2. | hook signature change |
| **D-B** | `search/global` body is `{ Query, Limit, Cursor, Mode, MinScore }` — **no facet params** (docType / gameId / language). `FilterAccordion` (#4) has no BE backing. | **DEFER FilterAccordion** from Foundation v1 → BE follow-up **#1686** (server-side facets). Foundation drops from 6 → 5 components. | Foundation scope ↓ |
| **D-C** | `search/global` accepts `Mode: SearchMode?` + `MinScore: double?` not in contract §5. | **Expose `Mode` as `SearchMode` segmented control in `HeroSearch`** (Semantic default — backed UI replaces the deferred facet panel). `MinScore` stays internal default (not exposed). | new Foundation UI |

**Foundation (Phase 1) UNBLOCKED** (Q1 ✅ + Q4 ✅ + D-A/D-B/D-C resolved + D-B routed to #1686). **5-component scope congelato** — see §11 below.
**Interactions (Phase 2) PARTIALLY UNBLOCKED**: viewer + AI drawer + CitationPill UNBLOCKED (Q2 ✅, Q3 deferred-with-default). `KbEditorDesktop` BLOCKED on #1687.

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
  - empty results → results=[], hasMore=false, nextCursor=null
  - error → error state
```

### Component + integration

- Per-component unit tests + jest-axe (standard).
- Drawer FSM integration (MSW-mocked SSE) — verify the 4-state transitions.
- URL-SSOT integration: `?q=`/`?docId=`/`?edit=1` route to correct surface.

## §10. Acceptance criteria (for the eventual impl PRs)

- [ ] Q1–Q6 resolved (this contract surfaces them; resolution may need BE issues — Q5/Q6 routed to #1687/#1686)
- [ ] Foundation PR: **5** components (was 6, FilterAccordion deferred per D-B) + `useGlobalKbSearch` + schemas + route shell + tests
- [ ] Interactions PR: **4** components (was 5, KbEditorDesktop deferred per Q5/#1687) + `useKbAskStream` + lazy-split + tests
- [ ] AI drawer 4-state FSM matches mockup; SSE mirrors `useAgentChatStream`
- [ ] Citations reuse `streaming.schemas.ts`; viewer deep-link via `chunkId`
- [ ] Bundle ≤ +150 KB (Interactions all lazy)
- [ ] DS-15 + jest-axe per component
- [ ] Matrix 10 rows pending → done; route summary updated

## §11. Spec-panel decisions — 2026-05-29 (Wiegers/Fowler/Nygard/Adzic)

Discussion-mode review of this contract against the BE actually shipped by #1661 (PRs #1672 + #1677). Goal: freeze Foundation scope, route deferred work to BE follow-up issues, eliminate hand-wavy components.

### Frozen Foundation scope (Phase 1 — 5 components, all backed)

| # | Component | Hook / data | BE backing | Notes |
|---|---|---|---|---|
| 1 | `HeroSearch` | local state + URL push | — | + `SearchMode` segmented control (D-C — Semantic default) |
| 2 | `KbHomeDesktop` | `useUserKbDocs` (#1588, already in tree post-#1645) | `GET /api/v1/kb-docs?sortBy=recent&state=ready` ✅ | landing — recent docs cross-game, no new "status" hook (mirror simplification) |
| 3 | `KbSearchResultsDesktop` | `useGlobalKbSearch` (greenfield) | `POST /api/v1/knowledge-base/search/global` ✅ | cursor pagination + `hasMore` "Load more"; no `totalCount` (Nygard D6 — too expensive cross-game) |
| 4 | ~~`FilterAccordion`~~ | — | ❌ (deferred to #1686) | **DROPPED** from Foundation v1 — facets need server-side backing |
| 5 | `KbEmptyState` | — | — | `no-query` (landing CTA) vs `no-results` (search returned 0) |
| — | `useGlobalKbSearch` | greenfield | `POST /api/v1/knowledge-base/search/global` | debounced 250ms, cursor pagination, accepts `query + mode` (no facets in v1) |
| — | `kb-globale.schemas.ts` | greenfield | mirrors `GlobalKbSearchResponseDto` + `GlobalKbSearchResultDto` (10 fields) | `Results / HasMore / NextCursor`; result fields match BE 1:1 |

**Route**: create `apps/web/src/app/(authenticated)/knowledge-base/global/page.tsx` (greenfield). URL SSOT: `?q=&mode=` only (no `docType / game / lang` until #1686).

### Phase 2 Interactions (deferred to a separate dispatch)

- **IN**: `KbDocViewerDesktop`, `KbDocViewerMobile`, `DrawerShell` (4 AI states), `CitationPill`, `useKbAskStream`.
- **OUT** until #1687: `KbEditorDesktop`, `useUpdateKbDocMeta`.
- **Q3 default at dispatch**: CitationPill click → URL push `?docId=&chunkId=` + viewer scroll.

### Open questions retired by this session

- **Q3** → deferred (decision at Phase 2 dispatch, default = deep-link).
- **Q5** → BE-blocked, **#1687**.
- **Q6** → BE-blocked, **#1686**.
- **D-A / D-B / D-C** → resolved above.

### Bundle re-budget

- Foundation: ~40 KB (was ~50; one less component, no facet logic).
- Interactions v1: ~80 KB (was ~100; editor deferred).
- Umbrella stays ≤ +150 KB.

### Impl-readiness checklist (for the writing-plans handoff)

- [x] All Foundation components have verified BE backing
- [x] Zod schemas match the shipped DTO field-by-field
- [x] Greenfield items isolated (`useGlobalKbSearch`, `kb-globale.schemas.ts`, route shell, 5 components)
- [x] Reuse claims verified (`useUserKbDocs`, `streaming.schemas.ts`, `KbChunkSummary` not needed in Foundation)
- [x] BE follow-up issues filed and linked (#1686, #1687)

---

**Generated by Claude Code (Opus 4.7).** Issue #1482 Phase 0.5. **This contract delivers design only — no component implementation.** All reuse claims verified against existing code (P74); greenfield items explicitly flagged. Implementation dispatches in 2 follow-up PRs (Foundation + Interactions) once §7 open questions resolve.
