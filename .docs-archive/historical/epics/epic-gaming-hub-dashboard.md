# Epic: Gaming Hub Dashboard

**Epic ID**: TBD (da assegnare su GitHub)
**Status**: 🔄 Planning
**Created**: 2026-02-17
**Target**: User Homepage Dashboard Replacement

---

## 🎯 Overview

Sostituire la dashboard attuale (`/dashboard`) con una nuova **Gaming Hub Dashboard** come homepage per utenti loggati (`/`), con focus su:
- Visualizzazione collezione personale
- Tracking sessioni di gioco
- Statistiche gaming activity

### Current State
- **Existing**: `/dashboard` route con `DashboardHub` component (Epic #3901, Issue #3975)
- **Target**: `/` homepage per utenti loggati con nuovo Gaming Hub design

---

## 🎨 Design Concept

**Layout**: Concept A "Gaming Hub" - Desktop optimized

### Sections
1. **Welcome Banner**: Personalizzato con insight mensile
2. **Quick Stats** (4 cards): Collezione, partite mensili, ore settimanali, preferiti
3. **Recent Sessions**: Lista ultime 3 partite con dettagli
4. **Game Collection Grid**: MeepleCard grid con filtri e ricerca
5. **Upcoming Games**: Empty state o lista partite programmate

### Design System
- **Glassmorphism**: `bg-white/70`, `backdrop-blur-md`
- **Accent**: Amber (`bg-amber-100`, `text-amber-900`)
- **Typography**: `font-quicksand` (headings), `font-nunito` (body)
- **Components**: MeepleCard system (entity="game", variant="grid")

---

## 📋 Implementation Phases

### Phase 0: Cleanup & Preparation
**Goal**: Preparare ambiente per nuova dashboard

- [ ] **Issue #TBD-1**: Create seed data script (giochi + sessioni di test)
- [ ] **Issue #TBD-2**: Audit existing dashboard dependencies

### Phase 1: Backend API Endpoints
**Goal**: 3 nuovi endpoint per dashboard data

- [ ] **Issue #TBD-3**: `GET /api/v1/users/me/stats` - User dashboard statistics
  - Total games, monthly plays, weekly hours, monthly favorites
  - Bounded Context: Administration + SessionTracking
  - Handler: `GetUserStatsQueryHandler`

- [ ] **Issue #TBD-4**: `GET /api/v1/sessions/recent?limit=3` - Recent gaming sessions
  - Session summary with game, players, duration, rating, winner
  - Bounded Context: SessionTracking
  - Handler: `GetRecentSessionsQueryHandler`

- [ ] **Issue #TBD-5**: `GET /api/v1/users/me/games?category&sort&page&pageSize` - User game collection
  - Paginated game list with filters
  - Bounded Context: UserLibrary + GameManagement
  - Handler: `GetUserGamesQueryHandler`

### Phase 2: Frontend Components
**Goal**: Nuovi component dashboard modulari

- [ ] **Issue #TBD-6**: Create `components/dashboard/` structure
  - `quick-stats.tsx` - 4 stat cards
  - `stat-card.tsx` - Reusable stat card component
  - `recent-sessions.tsx` - Sessions list
  - `session-row.tsx` - Single session display
  - `game-collection-grid.tsx` - MeepleCard grid wrapper
  - `filter-bar.tsx` - Category/sort filters
  - `empty-states.tsx` - Empty state messages

- [ ] **Issue #TBD-7**: Create API client `lib/api/dashboard-client.ts`
  - `getUserStats()`
  - `getRecentSessions(limit)`
  - `getUserGames(params)`

- [ ] **Issue #TBD-8**: Create Zustand store `lib/stores/dashboard-store.ts`
  - State: stats, recentSessions, games, filters
  - Actions: fetch*, updateFilters

- [ ] **Issue #TBD-9**: Create main page `app/(authenticated)/page.tsx`
  - Gaming Hub layout
  - Component integration
  - Loading states

### Phase 3: Integration & Testing
**Goal**: Quality assurance completa

- [ ] **Issue #TBD-10**: Backend integration tests
  - Test endpoint `/users/me/stats`
  - Test endpoint `/sessions/recent`
  - Test endpoint `/users/me/games` with pagination/filters

- [ ] **Issue #TBD-11**: Frontend unit tests
  - Test all dashboard-v2 components
  - Test dashboard-client API calls
  - Test dashboard-store state management

- [ ] **Issue #TBD-12**: E2E tests (Playwright)
  - User login → dashboard loads
  - Stats display correctly
  - Recent sessions visible
  - Game grid with filters working
  - Empty states rendering

### Phase 4: Migration & Cleanup
**Goal**: Sostituire vecchia dashboard e cleanup

- [ ] **Issue #TBD-13**: Deprecate old dashboard
  - Remove `/dashboard` route
  - Remove `components/dashboard/DashboardHub`
  - Update navigation links
  - Add redirect `/dashboard` → `/`

- [ ] **Issue #TBD-14**: Documentation updates
  - Update docs/07-frontend/
  - Add dashboard-v2 component docs
  - Update navigation/routing docs

---

## 🔧 Technical Stack

### Backend
- **Bounded Contexts**: Administration, SessionTracking, UserLibrary, GameManagement
- **Pattern**: CQRS (Query + Handler + Validator)
- **Caching**: HybridCache for stats (5min TTL)
- **Validation**: FluentValidation for query params

### Frontend
- **Framework**: Next.js 16 App Router
- **Components**: shadcn/ui + MeepleCard system
- **State**: Zustand + React Query
- **Styling**: Tailwind 4 + Glassmorphism
- **Testing**: Vitest + Playwright

### API Contracts

**UserStatsDto**:
```csharp
{
  "totalGames": 47,
  "monthlyPlays": 12,
  "monthlyPlaysChange": 15,  // Percentage
  "weeklyPlayTime": "08:30:00",
  "monthlyFavorites": 3
}
```

**SessionSummaryDto**:
```csharp
{
  "id": "guid",
  "gameTitle": "Catan",
  "gameImageUrl": "url",
  "playedAt": "2026-02-17T10:30:00Z",
  "playerCount": 4,
  "duration": "01:30:00",
  "rating": 9,
  "winner": "Marco"
}
```

**GameDto** (paginato):
```csharp
{
  "items": [...],
  "totalCount": 47,
  "page": 1,
  "pageSize": 20
}
```

---

## 📊 Success Criteria

- [ ] Homepage (`/`) mostra Gaming Hub per utenti loggati
- [ ] Tutti gli stats caricano correttamente
- [ ] Sessioni recenti visualizzate con dati real-time
- [ ] Game grid con filtri funzionanti
- [ ] Empty states gestiti gracefully
- [ ] Responsive desktop layout (1920px ottimizzato)
- [ ] Test coverage: Backend >90%, Frontend >85%
- [ ] Vecchia dashboard rimossa completamente
- [ ] Navigazione aggiornata

---

## 🔗 Dependencies

- **MeepleCard System** (#3820): ✅ Completato
- **Game Collection API**: Verificare esistenza endpoints
- **Session Tracking**: Verificare se backend ha già tracking

---

## 📈 Estimated Effort

| Phase | Issues | Estimate |
|-------|--------|----------|
| Phase 0 | 2 | 2h |
| Phase 1 | 3 | 6h |
| Phase 2 | 4 | 8h |
| Phase 3 | 3 | 6h |
| Phase 4 | 2 | 3h |
| **Total** | **14** | **~25h** |

---

## 🚀 Next Steps

1. Assegnare Epic ID su GitHub
2. Creare tutte le issue collegate
3. Seed data preparation (Phase 0)
4. Backend implementation (Phase 1)
5. Frontend components (Phase 2)
6. Testing & validation (Phase 3)
7. Migration & cleanup (Phase 4)
