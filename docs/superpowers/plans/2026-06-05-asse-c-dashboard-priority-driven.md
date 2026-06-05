# Asse C — Dashboard Priority-Driven Implementation Plan (v2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **🚨 v2 — REWRITE post-discovery 2026-06-05**: Dashboard infrastruttura (DashboardClient + DashboardEngine + sections components) GIÀ shipped pre-asse-C (epic #4575 Gaming Hub). Plan v2 è **scope pivot from hub-blocks → priority-driven** mantenendo riuso heavy componenti esistenti. Effort M ~5gg (vs v1 stima M ~4gg, lieve aumento per BE friends-activity + GameNightDrawer + MVP computation).

**Goal:** Refactor in-place `/dashboard` da 4 hub blocks entity-driven (Giochi/Sessioni/Agenti/Toolkit) a 4 sezioni priority-driven temporal (Prossimi/Recenti/Suggested/Friends), aggiungere FriendsActivity BE endpoint, creare GameNightDrawerContent, MVP computation server-side.

**Architecture:** Riuso componenti esistenti (DashboardSection + EmptyCTA + SectionSkeleton + ErrorBanner + GamesCarousel + Hero) + new ProssimiSection + RecentiSection + FriendsActivitySection + GameNightDrawerContent. Backend: extend DashboardEndpoints con friends-activity + GN list endpoints. MVP computation in handler con ScoringStrategyFactory.

**Tech Stack:** Frontend: Next.js 16 + React 19 + Zustand cascade-store (asse B) + TanStack Query · Backend: .NET 9 + MediatR + EF Core + Redis cache + IScoringStrategy (asse A)

**Issue**: [#1898](https://github.com/meepleAi-app/meepleai-monorepo/issues/1898) (parent umbrella [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895))
**Spec consolidato**: [`docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`](../specs/2026-06-04-claude-design-alignment-spec-panel-review.md)
**Effort target**: M ~5 gg dev + 1 gg test = **6 gg totali**

---

## Decisioni lockate (DEC-1..DEC-4)

| ID | Decisione | Rationale |
|----|-----------|-----------|
| **DEC-1** | Refactor in-place su `/dashboard` (no route alternativo) | Riuso componenti esistenti, NO flag complexity |
| **DEC-2** | Aggiungi FriendsActivity BE endpoint in asse C scope | Friends section completamente funzionale |
| **DEC-3** | Crea `GameNightDrawerContent` come componente asse C | Pattern PlayerDrawer riferimento, riusabile asse D |
| **DEC-4** | MVP computation server-side (backend aggregate) | Single round-trip, no duplicate logic FE/BE |

---

## Gap Analysis: codebase pre-asse-C vs target

| Componente target | Stato reale | Plan v2 azione |
|-------------------|-------------|----------------|
| `/dashboard` page route | ✅ ESISTE | REUSE |
| DashboardClient + DashboardEngine + Hero | ✅ ESISTONO | REUSE + REFACTOR layout |
| DashboardSection wrapper | ✅ ESISTE | REUSE |
| EmptyCTA + SectionSkeleton + ErrorBanner | ✅ ESISTONO | REUSE |
| GamesCarousel (per Suggested) | ✅ ESISTE | REUSE direct |
| EventsList + SessionsTimeline + PlayersAvatarList | ✅ ESISTONO | RIUSARE come building blocks |
| ProssimiSection (Planned + InProgress) | ❌ NUOVO | T2 |
| RecentiSection (Completed + MVP + paginazione) | ❌ NUOVO | T3 |
| SuggestedSection wrap GamesCarousel | ❌ NUOVO wrapper | T4 |
| FriendsActivitySection | ❌ NUOVO | T5 |
| GameNightDrawerContent | ❌ NUOVO (PlayerDrawer riferimento) | T6 |
| FriendsActivity BE endpoint | ❌ NUOVO | T1 BE |
| Dashboard summary endpoint con MVP computation | ⚠️ Esiste base, manca MVP | T1 BE extend |
| Empty state matrix 4×5 | ⚠️ Esiste EmptySection, validare uniformità | T7 audit |
| Designer Review Checklist | ❌ N/A | T7 |

---

## Work Packages v2

| WP | Scope | Effort | Critical path | Sub-task |
|----|-------|--------|---------------|----------|
| **WP1** | Backend: FriendsActivity endpoint + Dashboard summary extend con MVP | M (~8h) | YES (blocca C UI) | T1 |
| **WP2** | ProssimiSection (Planned + InProgress) | M (~6h) | YES (blocca DashboardClient) | T2 |
| **WP3** | RecentiSection (Completed + MVP + paginazione "Vedi tutti") | M (~6h) | YES | T3 |
| **WP4** | SuggestedSection (wrap GamesCarousel) | S (~3h) | NO (parallelo) | T4 |
| **WP5** | FriendsActivitySection | M (~5h) | NO (parallelo) | T5 |
| **WP6** | GameNightDrawerContent + cascade-store integration | M (~6h) | YES (blocca click-card flow) | T6 |
| **WP7** | DashboardClient refactor + E2E + acceptance | M (~6h) | YES (chiude WP) | T7 |

**Mix-model hint (P120)**: 2 haiku (T4 wrap component + T7 mechanical close) + 5 sonnet (T1 BE + T2-T6 judgment UI).

**Total**: 7 task TDD bite-sized. ~6gg effort realistic.

---

## File Structure

### New files (Frontend)
- `apps/web/src/app/(authenticated)/dashboard/_components/sections/ProssimiSection.tsx`
- `apps/web/src/app/(authenticated)/dashboard/_components/sections/RecentiSection.tsx`
- `apps/web/src/app/(authenticated)/dashboard/_components/sections/SuggestedSection.tsx`
- `apps/web/src/app/(authenticated)/dashboard/_components/sections/FriendsActivitySection.tsx`
- `apps/web/src/components/dashboard/GameNightDrawerContent.tsx`
- `apps/web/src/components/dashboard/__tests__/GameNightDrawerContent.test.tsx`
- 4 test files mirror per sections
- `apps/web/src/hooks/use-dashboard-priority.ts` (data fetching aggregator)
- `apps/web/src/hooks/__tests__/use-dashboard-priority.test.tsx`

### Modified files (Frontend)
- `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` (refactor layout 4 hub blocks → 4 priority sections)
- `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx` (update assertions)
- `apps/web/src/components/dashboard/DashboardEngine.ts` (extend states/transitions per priority sections)

### New files (Backend)
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/Dashboard/GetFriendsActivityQuery.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/Queries/Dashboard/GetFriendsActivityQueryHandler.cs`
- `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/FriendActivityDto.cs`
- 3 test files mirror

### Modified files (Backend)
- `apps/api/src/Api/Routing/DashboardEndpoints.cs` (add friends-activity endpoint + extend Recenti DTO con MVP)
- Repository extensions if needed

---

## WP1 — Backend FriendsActivity + MVP computation

### Task 1: FriendsActivity endpoint + Recenti MVP aggregate

**Mix-model**: sonnet · **Effort**: M (~8h)

**Files**:
- New: query + handler + DTO friends-activity
- Modify: DashboardEndpoints.cs (add route + extend Recenti)
- Tests: handler + integration

**FriendActivity DTO**:
```csharp
public record FriendActivityDto(
    Guid FriendUserId,
    string Avatar,
    string Name,
    string Verb,  // "completed" | "created" | "joined"
    Guid GameOrEventId,
    string GameOrEventType,  // "game" | "gameNight"
    string GameOrEventName,
    DateTimeOffset Timestamp
);
```

**Query**:
```csharp
public record GetFriendsActivityQuery(Guid UserId, int Limit = 10) : IRequest<IReadOnlyList<FriendActivityDto>>;
```

**Handler logic (friends qualification MAJ-5)**:
1. Find friends = User-linked players con almeno 1 shared GameNight last 90gg
2. Aggregate recent activities:
   - GameNight completed by friend
   - GameNight created by friend (organizer)
   - GameNight joined by friend (RSVP accepted)
3. Sort by timestamp desc, limit N
4. Cache Redis 5min

**MVP computation** (per Recenti GameNight):
```csharp
// Extend GameNightSummaryDto:
public record GameNightSummaryDto(
    Guid Id,
    string Name,
    DateTimeOffset Date,
    int SessionCount,
    Guid? MvpPlayerId,
    string? MvpDisplayName,
    string[] GamePreviewThumbnails  // 3 mini cover URLs
);

// In handler: per ogni completed GN, calcola MVP via IScoringStrategy
// Player with most wins across sessions (when ScoreType is BinaryWin/Objectives)
// OR Player with highest sum points (when ScoreType is Points)
// OR Player at position 1 (when ScoreType is Ranking)
```

**Endpoint**:
- `GET /api/v1/dashboard/friends-activity?limit=10` → `FriendActivityDto[]`
- Extend `GET /api/v1/dashboard` per include MVP fields in recenti

**Acceptance**:
- 10+ unit test handler
- 1 integration test endpoint
- Cache hit/miss verified
- 401 unauth handling

---

## WP2 — ProssimiSection

### Task 2: ProssimiSection priority #1

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files**:
- New: `_components/sections/ProssimiSection.tsx`
- Test: mirror

**Component structure**:
- Heading "Prossimi"
- 2-3 GameNight card (Planned + InProgress, ordine ascending date)
- Card content: data micro-label + nome + counter RSVP + "IN CORSO" badge
- CTA "+ Nuova Game Night" rosa entity color
- Click card → cascade-store openDrawer('game-night', id)
- States: default / empty (CTA replaces section) / loading (skeleton) / error (banner) / offline (cache+banner)

**Data**: useDashboardSummary hook returns `prossimi: GameNightSummaryDto[]`

---

## WP3 — RecentiSection con MVP + paginazione

### Task 3: RecentiSection priority #2

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files**:
- New: `_components/sections/RecentiSection.tsx`
- Test: mirror

**Component structure**:
- Heading "Recenti"
- 2-3 GameNight card (Completed, ordine descending date)
- Card content: data + nome + counter partite ("3 partite") + MVP serata + 3 mini cover
- **Invariante #7**: 1 card = 1 GameNight wrapper (NON N card per le N session)
- Footer link "Vedi tutti i completati →" → `/game-nights?status=completed` (MAJ-7)
- Click card → cascade-store openDrawer('game-night', id) + tabId 'sessions'
- States empty: hidden (no section render)

**MVP rendering**: leggere `summary.mvpDisplayName` da WP1 endpoint

---

## WP4 — SuggestedSection wrap

### Task 4: SuggestedSection priority #3

**Mix-model**: haiku · **Effort**: S (~3h)

**Files**:
- New: `_components/sections/SuggestedSection.tsx`
- Test: mirror

**Component structure**:
- Heading "Potresti giocare"
- Wrap existing `GamesCarousel` (4-6 cards horizontal scroll)
- Click → /games/[id]
- States empty/error: hidden (silent fallback)

**MVP fixture algorithm**: owned games NOT played in last 30 days, sorted by play count desc. Document MIN-2 roadmap inline.

---

## WP5 — FriendsActivitySection

### Task 5: FriendsActivitySection priority #4

**Mix-model**: sonnet · **Effort**: M (~5h)

**Files**:
- New: `_components/sections/FriendsActivitySection.tsx`
- Test: mirror
- New: `use-friends-activity.ts` hook (TanStack Query)

**Component structure**:
- Heading "Cosa fanno i tuoi"
- 2-3 entry: avatar + name + verb + game/event ref + timestamp relativo
- Verb i18n map: 'completed' → "ha completato", 'created' → "ha creato", 'joined' → "si è unito a"
- Click avatar → cascade-store openDrawer('player', friendUserId)
- Empty fallback: "Nessuna attività recente dai tuoi amici"
- Loading: skeleton 3 entry
- Error: hidden (silent)

---

## WP6 — GameNightDrawerContent

### Task 6: GameNightDrawerContent + cascade-store integration

**Mix-model**: sonnet · **Effort**: M (~6h)

**Files**:
- New: `components/dashboard/GameNightDrawerContent.tsx`
- New: `components/dashboard/use-game-night-detail.ts` (TanStack Query fetcher)
- Test: mirror

**Reference**: PlayerDrawerContent in `apps/web/src/components/ui/data-display/extra-meeple-card/entities/PlayerDrawerContent.tsx`

**Component structure**:
- Header: nome + data + location + status badge (Planned/InProgress/Completed colored)
- Players list: avatar + nome + ✓ User badge OR Guest badge (asse A invariante #16)
- Sessions list (InProgress/Completed): "Session 1: Wingspan · MVP Marco · 87 pt" (formatted da polymorphic ScoreData)
- CTA contestuali per status:
  - Planned: "Modifica RSVP" | "Cancel"
  - InProgress: "Aggiungi session" | "Termina serata"
  - Completed: "Aggiungi note" | "Aggiungi foto"
- Click Player → cascade-store.pushDrawer('player', playerId) [swap drawer]

**Data fetcher**: `GET /api/v1/game-nights/{id}/detail` (verify if exists or needs creation in T1)

---

## WP7 — DashboardClient refactor + E2E + acceptance

### Task 7: DashboardClient layout refactor + E2E

**Mix-model**: haiku · **Effort**: M (~6h)

**Files**:
- Modify: `DashboardClient.tsx` (replace 4 hub blocks → ProssimiSection + RecentiSection + SuggestedSection + FriendsActivitySection in fixed order)
- Modify: `__tests__/DashboardClient.test.tsx`
- New: `apps/web/e2e/dashboard-priority-flow.spec.ts`
- Modify: CLAUDE.md + spec changelog

**Layout refactor**:
```tsx
export function DashboardClient() {
  return (
    <DashboardEngineProvider>
      <DashboardHero />
      <div className="space-y-8">
        <ProssimiSection />
        <RecentiSection />
        <SuggestedSection />
        <FriendsActivitySection />
      </div>
    </DashboardEngineProvider>
  );
}
```

**E2E test**:
- Render dashboard authenticated → 4 sezioni in ordine
- Click card Prossimi → drawer GameNight opens
- Click Player in drawer → drawer swap to Player
- ESC → back to GameNight drawer
- Backdrop → close all
- Empty state global → CTA "Crea prima GN" replaces Prossimi

**Acceptance**:
- 4 sezioni ordine fisso (Prossimi → Recenti → Suggested → Friends)
- "Sessioni in corso" section RIMOSSA
- Card Recenti = 1 per GameNight (NOT N per sessions)
- DrawerStack pattern wired
- Empty state matrix 4×5 (verifica EmptySection riusata)

---

## Self-Review Checklist (post-plan v2)

**Spec coverage post-discovery**:
- [x] DEC-1 refactor in-place → WP7 T7 layout swap
- [x] DEC-2 BE FriendsActivity → WP1 T1
- [x] DEC-3 GameNightDrawerContent → WP6 T6
- [x] DEC-4 MVP server-side → WP1 T1
- [x] MAJ-5 Friends qualification 90gg → WP1 T1
- [x] MAJ-6 Empty-state matrix 4×5 → WP7 T7 audit
- [x] MAJ-7 paginazione Recenti → WP3 T3
- [x] MIN-2 suggested roadmap → WP4 T4 doc
- [x] MIN-5 MVP definition → WP1 T1

**Type consistency**:
- `FriendActivityDto` consistent T1
- `GameNightSummaryDto` (extended con MVP fields) consistent T1 → T3 → T6
- cascade-store entity types (`'game-night'`, `'player'`) consistent T2/T3/T6

**Critical path**:
- T1 (BE) → T2/T3/T5 (UI consume endpoint)
- T6 (GameNightDrawer) → unblock click-card flow T2/T3
- T4 (SuggestedSection) parallel
- T7 final close

**Effort verification**:
- T1: 8h ≈ 1gg
- T2: 6h ≈ 0.8gg
- T3: 6h ≈ 0.8gg
- T4: 3h ≈ 0.4gg
- T5: 5h ≈ 0.6gg
- T6: 6h ≈ 0.8gg
- T7: 6h ≈ 0.8gg
- **Total**: ~40h ≈ 5gg dev + 1gg buffer = **6gg** ✓

---

## Execution Handoff

**Plan v2 complete and saved to `docs/superpowers/plans/2026-06-05-asse-c-dashboard-priority-driven.md`.**

**Critical path**: T1 (BE foundation) → T2+T3 → T6 → T7 close
**Parallel opportunity**: T4 + T5 parallel con T6

**Recommended sequence (subagent-driven)**:
1. T1 BE first (FriendsActivity + MVP)
2. T6 GameNightDrawer (block-removing)
3. T2 ProssimiSection
4. T3 RecentiSection
5. T4 SuggestedSection + T5 FriendsActivitySection parallel
6. T7 final integration

---

## Changelog

- **2026-06-05 v2**: initial plan post-discovery + spec-panel. DEC-1..DEC-4 lockate. 7 task TDD. M ~6gg effort.
