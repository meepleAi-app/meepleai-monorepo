# `/library` Phase 0.5 Hybrid Hub Contract — library-hybrid-hub (Issue #1585)

> **Status**: Phase 0.5 contract (doc-only). No implementation in this PR.
> **Parent**: #1585. **Prior art**: #574 / PR #638 (Wave B.3, deliberately reduced scope).
> **Pattern**: mirrors #1482 (kb-globale) Phase 0.5 contract discipline.

## §1. Overview

### Tier classification
**Tier L** — brownfield expansion of an already-migrated surface (Wave B.3). The
current `/library` renders only library game entries in 3 tabs (`all|kb|loaned`).
The SP4 mockup (`admin-mockups/design_files/sp4-library-desktop.jsx`, 1585 LOC) is
a **6-tab hybrid hub** surfacing all the user's entities (games, agents, KB docs,
sessions, chats) in one grid + an activity sidebar.

### Dispatch strategy
3 FE phases + 3 BE work items, intertwined (see §10 for issue tracking):
- **Phase 1 (Foundation) — FE-only, NO BE dependency** — `HybridHubItem` union +
  per-entity mappers + `LibraryEntityKey` 3→6 expansion (single SSOT in the
  domain) + `LibraryTabs`/`LibraryHybridGrid` prop adaptation + unit tests. **Can
  start immediately**, in parallel with BE work. → **#1591**
- **BE-1 / BE-2 / BE-3** — backend prerequisites for Phase 2 (kb tab + agents
  scope) and Phase 3 (activity rail). See §6 resolved questions + §10. →
  **#1588 / #1589 / #1590**
- **Phase 2 (Surface)** — multi-query orchestration in `LibraryHub` + Hero
  enrichment (badge/subtitle/4 stat chips/Importa BGG+Esporta) + `CrossEntityFilters`.
  Depends on BE-1 (kb tab) and BE-2 (agents tab); games/sessions/chat parts are
  unblocked and can land first. → **#1592**
- **Phase 3 (Advanced)** — `AdvancedFiltersDrawer` (standalone reusable, no BE
  dep) + `RecentActivityRail` cross-entity (depends on BE-3). → **#1593**

### Why a contract first
Wave B.3 reduced scope to ship fast (decision C2+C3). Re-expanding touches the
data layer (5 entity sources) + the shared `LibraryEntityKey` type + 2 prop
contracts. Defining the `HybridHubItem` union + the REUSE/GREENFIELD split +
the BE open questions **before** dispatching impl avoids the anti-pattern of
building mappers against the wrong shape or assuming a non-existent aggregated
endpoint.

## §2. Route decision

### Recommendation: keep `/library` (no new route)
The hub IS the library landing. No redirect, no new route. The current
`app/(authenticated)/library/page.tsx` → `LibraryContent` → `LibraryHub`
orchestrator stays; only `LibraryHub` internals expand.

### Wishlist sub-route preserved
`/library/wishlist` remains a standalone route (referenced by the MiniNav
config in `LibraryHub.tsx`). Not in scope for this refactor.

### URL state SSOT
Tab + filters become URL params for shareability/back-button:
- `?tab=all|games|agents|kb|sessions|chat` (default `all`)
- `?q=<search>` · `?sort=recent|title|rating|state` · `?view=grid|list|compact`
- existing `?state=` dev/visual-test override stays gated by `STATE_OVERRIDE_ENABLED`

## §3. `HybridHubItem` discriminated union (the Tier-L core)

The grid currently accepts `UserLibraryEntry[]` (game-only). Phase 1 introduces a
discriminated union so one grid renders 5 entity kinds via `MeepleCard`
(which already supports `entity={'game'|'agent'|'kb'|'session'|'chat'}`).

```ts
// apps/web/src/lib/library/hybrid-hub.types.ts  (GREENFIELD)
type HybridHubEntity = 'game' | 'agent' | 'kb' | 'session' | 'chat';

interface HybridHubItemBase {
  id: string;            // entry/entity id (selection key)
  entity: HybridHubEntity;
  title: string;
  subtitle?: string;     // publisher / game name / etc.
  updatedAt: string;     // ISO — drives 'recent' sort across entities
  href: string;          // canonical navigation target
}

interface GameHubItem    extends HybridHubItemBase { entity: 'game';    gameId: string; rating?: number; state?: LibraryGameState; imageUrl?: string; }
interface AgentHubItem   extends HybridHubItemBase { entity: 'agent';   gameName?: string; agentType: string; isActive: boolean; }
interface KbHubItem      extends HybridHubItemBase { entity: 'kb';      gameName?: string; processingState: string; pageCount?: number; }
interface SessionHubItem extends HybridHubItemBase { entity: 'session'; gameName: string; status: string; playerCount: number; }
interface ChatHubItem    extends HybridHubItemBase { entity: 'chat';    gameName?: string; messageCount?: number; }

type HybridHubItem = GameHubItem | AgentHubItem | KbHubItem | SessionHubItem | ChatHubItem;
```

### Mappers (GREENFIELD, pure functions, unit-testable in isolation)
`lib/library/hybrid-hub.mappers.ts` — one per source, each `(SourceDto) => HybridHubItem`:
- `libraryEntryToHubItem(e: UserLibraryEntry): GameHubItem`
- `agentToHubItem(a: AgentDto): AgentHubItem`
- `kbDocToHubItem(d: GamePdfDto | KbDoc): KbHubItem`
- `sessionToHubItem(s: GameSessionDto): SessionHubItem`
- `chatToHubItem(c: ChatSession): ChatHubItem`

### `LibraryEntityKey`: single SSOT + axis change (NOT an additive 3→6)

**SSOT** — today `LibraryEntityKey` is declared **twice** (two independent
`export type`, same literal): in `lib/library/library-filters.ts` (next to
`filterByEntity`) and in `components/features/library/LibraryTabs.tsx` (re-exported
by the barrel `index.ts`, which is where `LibraryHub` imports it). Two distinct
types that *coincidentally* share a value → expanding one and not the other drifts
silently (TS can't catch it, they are not the same type). **Resolution**: the
domain module `lib/library/library-filters.ts` is the single SSOT; `LibraryTabs.tsx`
**drops** its own `export type` and imports it. The domain defines the entity
categories; the UI consumes them.

**Axis change** — the current 3 keys are *filters over games* (one entity kind,
three filters; all operate on `UserLibraryEntry[]`), not entity types. The 6 keys
are *entity types*. So "3→6" is a change of categorization **axis**, and two keys
change meaning under the same name:

| Key | Before (filter-over-games) | After (entity-type tab) |
|---|---|---|
| `all` | all library games | merged set of all entities |
| `kb` | games *with* a PDF (`isKbEntry`) | KB documents (`KbHubItem`) — **re-semanticized** |
| `loaned` | games on loan (`InPrestito`) | **removed as a tab** → becomes a STATO filter inside `games` |
| `games`/`agents`/`sessions`/`chat` | — | new entity-type tabs |

**`filterByEntity` splits in two**: (a) `deriveHybridItems` filters the union by
`item.entity` (new); (b) the game-state filters (ex-`loaned`, ex-`with-KB`) survive
as chips inside the `games` tab (`CrossEntityFilters` STATO chip), **reusing** the
existing `isKbEntry` + `currentState === 'InPrestito'` logic — nothing is thrown
away, the state filters just move from tabs to chips.

## §4. Hook composition matrix

### Reuse vs greenfield (all verified via discovery 2026-05-26)

| Need | Hook | Endpoint | Returns | Status |
|---|---|---|---|---|
| Games | `useLibrary({page,pageSize,sortBy,sortDescending})` | `GET /library` | `PaginatedLibraryResponse` | ✅ reuse |
| Library stats | `useLibraryStats()` | `GET /library/stats` | `UserLibraryStats` | ✅ reuse |
| Activity | `useLibraryActivity(limit)` | `GET /library/activity` | `LibraryActivityItem[]` | ✅ reuse |
| Remove | `useRemoveGameFromLibrary()` | `DELETE /library/games/{id}` | mutation | ✅ reuse |
| Agents | `useAgents({activeOnly?,type?})` | `GET /agents` | `AgentDto[]` | ✅ reuse |
| Sessions | `useActiveSessions(limit)` | `GET /sessions/active` | `PaginatedSessionsResponse` | ✅ reuse |
| Chats | `useRecentChatSessions(limit)` | `api.chatSessions.getRecent` | `ChatSession[]` | ✅ reuse |
| KB (per-game) | `useUserKbStatus(gameId)` / `useGamePdfs(gameId)` | per-game | `GamePdfDto[]` | ⚠️ per-game only — see Q1 |
| KB (global) | `useUserKbDocs()` | `GET /kb-docs?userId` (BE-1) | `KbDoc[]` | ❌ greenfield → **BE-1 #1588** |
| Union + mappers | `lib/library/hybrid-hub.{types,mappers}.ts` | — | `HybridHubItem` | ❌ greenfield |
| 6-tab key | expand `LibraryEntityKey` | — | type | ❌ greenfield |

### Composition tree (Phase 2)
```
LibraryHub
 ├─ useLibrary()          → games[]   → libraryEntryToHubItem
 ├─ useAgents()           → agents[]  → agentToHubItem
 ├─ useActiveSessions()   → sessions[]→ sessionToHubItem
 ├─ useRecentChatSessions()→ chats[]  → chatToHubItem
 ├─ useUserKbDocs()       → kb[]      → kbDocToHubItem        (greenfield, via BE-1 #1588)
 ├─ useLibraryActivity()  → activity[] (RecentActivityRail)
 └─ useMemo: merge all → filter by tab+query → sort by sortKey → HybridHubItem[]
```
6 parallel queries (TanStack dedupes/caches). No aggregated BE endpoint in v1 (Q2).

**Unwrap**: paginated sources (`useLibrary` → `PaginatedLibraryResponse`,
`useActiveSessions` → `PaginatedSessionsResponse`) are unwrapped to `.items` by the
mappers/orchestration before merge; array sources (`useAgents`,
`useRecentChatSessions`) pass through directly.

**Merge cardinality (v1)**: the 5 sources have heterogeneous cardinality
(paginated / fixed `limit` / all). In the `all` tab each source contributes at
most ~20 items, merged by recency — `all` is a cross-entity **dashboard**. Full
pagination/scroll lives in the single-entity tabs (the **complete** view). No
client-side global pagination (it would require the Q2 aggregated endpoint).

## §5. Component specs (brief)

| Component | Change | Notes |
|---|---|---|
| `LibraryTabs` | adapt prop `LibraryEntityKey` 3→6; sliding indicator already a11y-wired | counts per entity from merged list |
| `LibraryHybridGrid` | accept `HybridHubItem[]` (was `UserLibraryEntry[]`); render `MeepleCard entity={item.entity}` | grid/list/compact preserved; **`selectionMode='select'` is game-scoped** — `LibraryHub` forces `browse` when `tab !== 'games'` |
| `LibraryHeroDesktop` | add badge pill + subtitle + 4 stat chips (games/agents/docs/chats) + Importa BGG + Esporta buttons | stats from merged counts |
| `CrossEntityFilters` | **greenfield**: chip-dropdown row (STATO/STATS/ORD + "Più filtri N") | between tabs + toolbar; in `games` the STATO chip carries the ex-`loaned`/ex-`kb` state filters (`Owned/Wishlist/InPrestito` + `isKbEntry`) |
| `AdvancedFiltersDrawer` | **greenfield, standalone reusable**: `{open,onClose,sections[],activeFilters,onApply,entityScope}` | reusable for /games, /agents future |
| `RecentActivityRail` | populate cross-entity (Q3) + Shortcuts section | currently library-only activity |

## §6. Resolved questions (panel decisions 2026-05-26 — see §10 for issue tracking)

Resolved via a socratic spec panel grounded in BE exploration (Fowler / Newman /
Hohpe / Nygard / Wiegers). Each decision is verified against real `.NET` code
(`file:line` references live in the linked issue bodies).

1. **KB global hook** — ✅ **Resolved**: new endpoint `GET /kb-docs?userId`
   (list cross-game per-user, paginated, filters `PdfDocumentEntity` by
   `UploadedByUserId` — the field exists). FE hook `useUserKbDocs()` greenfield.
   **Distinct from #1482's semantic search** (search ≠ list); they share infra,
   not the endpoint, so #1585 is no longer coupled to #1482's delivery.
   → **BE-1 #1588**.
2. **Aggregated endpoint** — ✅ **Resolved**: client-side fan-out (zero BE work).
   6 parallel queries TanStack-cached, cap ~20/source in `all` (cross-entity
   dashboard), full pagination in the single tabs (see §4 merge cardinality).
   Evidence supporting the decision: `GET /dashboard` V1 was the mega-aggregate
   precedent — and is deprecated; degradazione parziale > tutto-o-niente on a
   6-source surface. Re-evaluate only if a measured perf problem or global
   cross-entity pagination becomes a requirement.
3. **Activity feed scope** — ✅ **Resolved (forward-only)**: extend
   `EventTypeRegistry` to register domain events from Agent / Chat / KB /
   Session BCs (in addition to the 2 library events already registered).
   Generalize the activity endpoint/DTO from `gameId/gameTitle` to
   `entityType/entityId/title`. **No backfill**: the rail starts empty for
   pre-deploy entities and populates forward from deploy — acceptable for a
   "recent activity" rail. The runtime fan-out via `ActivityTimelineService`
   (Administration BC) was the alternative considered but not chosen (less
   durable). → **BE-3 #1590**.
4. **CrossEntityFilters facets** — ✅ **Resolved**: **entity-conditional**
   facets (per-tab). Each tab exposes only its own filters; the `all` tab keeps
   only search + sort globals. `AdvancedFiltersDrawer` renders sections per
   `entityScope`. Reflects the heterogeneous DTO fields (no logical intersection
   to force-share):
   - `games`: `state` (`Owned/Wishlist/InPrestito` + `with-KB`, inherited from
     the ex-`loaned`/`kb` tabs) / rating / players / year / complexity
   - `agents`: `Type` / `IsActive` / `IsRecentlyUsed`
   - `sessions`: `Status` / `SessionType` / `Participants.Count`
   - `kb`: `processingState` / `pageCount`
   - `chat`: `messageCount`
5. **RBAC / visibility** — ✅ **Resolved**: almost all entities are properly
   user-scoped (`GET /library`, KB endpoints, chat-threads, sessions). Loaned
   and shared games are **not** a hub concern (loaned games stay in the owner's
   library; `/library/shared/{token}` is a public token-based surface, out of
   hub scope). **One real finding**: `GET /agents` is *global* (not user-scoped)
   — the hub's `agents` tab must therefore filter to **agents whose
   `GameId ∈ caller's library`** ("assistants for my games"). → **BE-2 #1589**.

## §7. Bundle budget
Phase 2/3 lazy-split the `AdvancedFiltersDrawer` (Radix Dialog) to keep the
landing payload lean. Target: hub landing ≤ +80KB over current `/library`;
drawer chunk ≤ +60KB loaded on demand.

## §8. Test plan
- **Mappers** (Phase 1): unit test each `*ToHubItem` in isolation — input DTO,
  assert `HybridHubItem` shape + `href` + `updatedAt`.
- **Merge/filter/sort** (Phase 1): `deriveHybridItems(sources, tab, query, sort)`
  pure function — tab filtering, query match across entities, cross-entity recent sort.
- **Tabs/grid** (Phase 1): 6-tab render + count badges; grid renders correct
  `MeepleCard entity` per item kind; jest-axe per state.
- **Orchestration** (Phase 2): `LibraryHub` with mocked hooks → 5 entity kinds
  appear in `all` tab; tab switch filters; empty/loading/error FSM preserved.

## §9. Acceptance criteria (for the eventual impl PRs)
- AC1 — 6 entity tabs with live counts; `all` shows merged set.
- AC2 — grid renders games+agents+kb+sessions+chats via `MeepleCard`, navigates per `href`.
- AC3 — Hero shows badge + subtitle + 4 stat chips + 3 CTAs (Aggiungi/Importa BGG/Esporta).
- AC4 — `CrossEntityFilters` chip row + `AdvancedFiltersDrawer` open/apply/clear.
- AC5 — `RecentActivityRail` populated (scope per Q3).
- AC6 — 5-state FSM (default/loading/empty/filtered-empty/error) preserved.
- AC7 — i18n it+en for all new copy; jest-axe clean per state.
- AC8 — no regression to `/library/wishlist` or MiniNav shell config.
- AC9 — bulk-selection (archive) available only in the `games` tab; all other tabs
  (incl. `all`) are always `browse` (no `BulkSelectionBar` mounted).

## §10. Implementation plan (issues)

§6 resolutions translate into the following tracked work.

### BE work (prerequisites for the FE phases that depend on them)
| Item | Goal | Unblocks |
|---|---|---|
| **BE-1 #1588** | `GET /kb-docs?userId` (list cross-game per-user, paginated) | `kb` tab in Phase 2 |
| **BE-2 #1589** | `GET /agents` scoped to caller's library games (`GameId ∈ library`) | `agents` tab in Phase 2 |
| **BE-3 #1590** | Extend `EventTypeRegistry` (Agent/Chat/KB/Session) + generalize activity endpoint/DTO — forward-only | `RecentActivityRail` in Phase 3 |

### FE phases
| Phase | Issue | Depends on |
|---|---|---|
| **Phase 1 — Foundation** | **#1591** | — (FE-only, **start immediately** in parallel with BE work) |
| **Phase 2 — Surface** | **#1592** | BE-1 #1588 (kb tab), BE-2 #1589 (agents tab); games/sessions/chat parts unblocked |
| **Phase 3 — Advanced** | **#1593** | BE-3 #1590 (rail); the `AdvancedFiltersDrawer` is unblocked |

### Critical path
- **Phase 1 is unblocked now** (FE-only) — open it in parallel with the BE work.
- The longest path to AC1-AC9 completion runs through **BE-3 → Phase 3 rail**
  (the `EventTypeRegistry` extension touches 4 bounded contexts and is the
  largest single BE work item).
