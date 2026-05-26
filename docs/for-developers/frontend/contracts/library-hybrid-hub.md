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
3 implementation phases after this contract merges:
- **Phase 1 (Foundation)** — `HybridHubItem` union + per-entity mappers +
  `LibraryEntityKey` 3→6 expansion + `LibraryTabs`/`LibraryHybridGrid` prop
  adaptation + unit tests. No new network calls (uses existing hooks).
- **Phase 2 (Surface)** — multi-query orchestration in `LibraryHub` + Hero
  enrichment (badge/subtitle/4 stat chips/Importa BGG+Esporta) + `CrossEntityFilters`.
- **Phase 3 (Advanced)** — `AdvancedFiltersDrawer` (standalone reusable) +
  `RecentActivityRail` cross-entity population.

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
| KB (global) | `useUserKbDocs()` | TBD | `KbDoc[]` | ❌ greenfield (blocks on Q1) |
| Union + mappers | `lib/library/hybrid-hub.{types,mappers}.ts` | — | `HybridHubItem` | ❌ greenfield |
| 6-tab key | expand `LibraryEntityKey` | — | type | ❌ greenfield |

### Composition tree (Phase 2)
```
LibraryHub
 ├─ useLibrary()          → games[]   → libraryEntryToHubItem
 ├─ useAgents()           → agents[]  → agentToHubItem
 ├─ useActiveSessions()   → sessions[]→ sessionToHubItem
 ├─ useRecentChatSessions()→ chats[]  → chatToHubItem
 ├─ useUserKbDocs()       → kb[]      → kbDocToHubItem        (greenfield, Q1)
 ├─ useLibraryActivity()  → activity[] (RecentActivityRail)
 └─ useMemo: merge all → filter by tab+query → sort by sortKey → HybridHubItem[]
```
6 parallel queries (TanStack dedupes/caches). No aggregated BE endpoint in v1 (Q2).

## §5. Component specs (brief)

| Component | Change | Notes |
|---|---|---|
| `LibraryTabs` | adapt prop `LibraryEntityKey` 3→6; sliding indicator already a11y-wired | counts per entity from merged list |
| `LibraryHybridGrid` | accept `HybridHubItem[]` (was `UserLibraryEntry[]`); render `MeepleCard entity={item.entity}` | grid/list/compact preserved |
| `LibraryHeroDesktop` | add badge pill + subtitle + 4 stat chips (games/agents/docs/chats) + Importa BGG + Esporta buttons | stats from merged counts |
| `CrossEntityFilters` | **greenfield**: chip-dropdown row (STATO/STATS/ORD + "Più filtri N") | between tabs + toolbar |
| `AdvancedFiltersDrawer` | **greenfield, standalone reusable**: `{open,onClose,sections[],activeFilters,onApply,entityScope}` | reusable for /games, /agents future |
| `RecentActivityRail` | populate cross-entity (Q3) + Shortcuts section | currently library-only activity |

## §6. Open questions (MUST resolve before Phase 1/2 dispatch)

1. **KB global hook** — `useKbHub` is per-game (`useGamePdfs(gameId)`). The hub
   needs *all* the user's KB docs. Is there a `GET /kb-docs?userId` or must we
   add one? **Coordinate with #1482 (kb-globale)** — likely shares the same
   greenfield `useUserKbDocs` + BE cross-game query. ⛔ blocks the `kb` tab.
2. **Aggregated endpoint** — client-side 6-query fan-out (zero BE, 6 round-trips,
   TanStack-cached) vs new `GET /library/hub` (1 payload, BE CQRS work). v1 =
   client-side; measure, add endpoint only if perf demands.
3. **Activity feed scope** — `useLibraryActivity` emits library events only
   (added/state-changed/removed/session-recorded). Cross-entity timeline (agent
   created, chat started) needs BE `domain_event_logs` projection extension, or
   keep library-scoped for v1?
4. **CrossEntityFilters facets** — which dimensions per entity? Games have
   state/rating/players; agents have type/active; sessions have status. A shared
   facet set vs entity-conditional facets (drives `AdvancedFiltersDrawer` sections).
5. **RBAC / visibility** — any entity-type the user shouldn't see in their own
   hub? (Assumed no — all owned by the user — but confirm for shared/loaned games.)

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
