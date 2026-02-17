# Gaming Hub Dashboard - Issue Breakdown

**Epic**: Gaming Hub Dashboard (Epic #TBD)
**Total Issues**: 14
**Created**: 2026-02-17

---

## Phase 0: Cleanup & Preparation (2 issues)

### Issue #TBD-1: Create seed data script for dashboard testing

**Title**: Create seed data script for dashboard testing

**Description**:
Creare script di seed per popolare il database con dati di test necessari per sviluppare e testare la Gaming Hub Dashboard.

**Acceptance Criteria**:
- [ ] Script PowerShell/Bash in `scripts/seed-dashboard-data.sh`
- [ ] Crea almeno 3 utenti di test con diverse collezioni
- [ ] Popola almeno 20 giochi per utente (mix di categorie)
- [ ] Crea 10-15 sessioni di gioco per utente con dati realistici
- [ ] Include timestamp variati (oggi, ieri, ultima settimana, ultimo mese)
- [ ] Rating variati (1-10)
- [ ] Player count realistico (1-6)
- [ ] Script idempotente (può essere rieseguito senza duplicati)
- [ ] README con istruzioni uso

**Technical Notes**:
- Usare EF Core o SQL diretto per inserimento
- Bounded Contexts: UserLibrary, GameManagement, SessionTracking
- Considerare soft-delete per testing di filtri

**Labels**: `phase-0`, `backend`, `data`, `testing`
**Estimate**: 2h

---

### Issue #TBD-2: Audit existing dashboard dependencies

**Title**: Audit existing dashboard dependencies and create migration plan

**Description**:
Analizzare la dashboard esistente (`/dashboard`) per identificare tutti i componenti, API, stores e dipendenze da migrare o deprecare.

**Acceptance Criteria**:
- [ ] Lista completa componenti in `components/dashboard/`
- [ ] Identificare API endpoints usati dalla vecchia dashboard
- [ ] Identificare stores/hooks Zustand utilizzati
- [ ] Verificare navigation links che puntano a `/dashboard`
- [ ] Creare migration plan per componenti riutilizzabili
- [ ] Documento in `docs/migration/dashboard-v1-to-v2.md`

**Technical Notes**:
- Vecchia dashboard: `app/(authenticated)/dashboard/`
- Component principale: `DashboardHub`
- Verificare se ci sono test da migrare
- Identificare shared utilities da preservare

**Labels**: `phase-0`, `frontend`, `audit`, `documentation`
**Estimate**: 1h

---

## Phase 1: Backend API Endpoints (3 issues)

### Issue #TBD-3: Implement GET /api/v1/users/me/stats endpoint

**Title**: Implement user dashboard statistics endpoint

**Description**:
Creare endpoint per recuperare statistiche aggregate dell'utente da mostrare nella sezione Quick Stats della dashboard.

**Acceptance Criteria**:
- [ ] Query: `GetUserStatsQuery` in `Application/Users/Queries/`
- [ ] Handler: `GetUserStatsQueryHandler` con caching (HybridCache 5min)
- [ ] DTO: `UserStatsDto` con tutti i campi richiesti
- [ ] Endpoint: `GET /api/v1/users/me/stats`
- [ ] Calcolo: Total games (UserLibrary count)
- [ ] Calcolo: Monthly plays (SessionTracking last 30 days)
- [ ] Calcolo: Monthly plays change percentage (vs previous month)
- [ ] Calcolo: Weekly play time (SessionTracking last 7 days sum)
- [ ] Calcolo: Monthly favorites (games con rating >=9 ultimo mese)
- [ ] Validation: User must be authenticated
- [ ] Unit tests: Handler logic
- [ ] Integration tests: API endpoint
- [ ] Swagger documentation

**Technical Specs**:
```csharp
// Application/Users/Queries/GetUserStatsQuery.cs
public record GetUserStatsQuery : IRequest<UserStatsDto>;

// DTOs/UserStatsDto.cs
public record UserStatsDto
{
    public int TotalGames { get; init; }
    public int MonthlyPlays { get; init; }
    public int MonthlyPlaysChange { get; init; } // Percentage: +15 or -10
    public TimeSpan WeeklyPlayTime { get; init; }
    public int MonthlyFavorites { get; init; }
}

// Routing/UsersEndpoints.cs
users.MapGet("/me/stats", async (IMediator mediator) =>
{
    var stats = await mediator.Send(new GetUserStatsQuery());
    return Results.Ok(stats);
})
.RequireAuthorization()
.WithTags("Users")
.WithName("GetUserStats")
.Produces<UserStatsDto>();
```

**Bounded Contexts**: Administration, SessionTracking, UserLibrary

**Labels**: `phase-1`, `backend`, `api`, `cqrs`
**Estimate**: 3h

---

### Issue #TBD-4: Implement GET /api/v1/sessions/recent endpoint

**Title**: Implement recent gaming sessions endpoint

**Description**:
Creare endpoint per recuperare le ultime N sessioni di gioco dell'utente per la sezione "Sessioni Recenti" della dashboard.

**Acceptance Criteria**:
- [ ] Query: `GetRecentSessionsQuery` con parametro `Limit` (default 3)
- [ ] Handler: `GetRecentSessionsQueryHandler` con caching (HybridCache 2min)
- [ ] DTO: `SessionSummaryDto` con dati completi sessione
- [ ] Endpoint: `GET /api/v1/sessions/recent?limit={n}`
- [ ] Ordinamento: DESC per `PlayedAt`
- [ ] Include: Game title, image, player count, duration, rating, winner
- [ ] Validation: Limit 1-20, default 3
- [ ] Unit tests: Handler + ordering
- [ ] Integration tests: API endpoint con parametri
- [ ] Swagger documentation

**Technical Specs**:
```csharp
// Application/Sessions/Queries/GetRecentSessionsQuery.cs
public record GetRecentSessionsQuery : IRequest<List<SessionSummaryDto>>
{
    public int Limit { get; init; } = 3;
}

public class GetRecentSessionsQueryValidator : AbstractValidator<GetRecentSessionsQuery>
{
    public GetRecentSessionsQueryValidator()
    {
        RuleFor(x => x.Limit)
            .GreaterThan(0)
            .LessThanOrEqualTo(20);
    }
}

// DTOs/SessionSummaryDto.cs
public record SessionSummaryDto
{
    public Guid Id { get; init; }
    public string GameTitle { get; init; }
    public string? GameImageUrl { get; init; }
    public DateTime PlayedAt { get; init; }
    public int PlayerCount { get; init; }
    public TimeSpan Duration { get; init; }
    public int? Rating { get; init; } // 1-10
    public string? Winner { get; init; }
}

// Routing/SessionsEndpoints.cs
sessions.MapGet("/recent", async (
    [FromQuery] int limit,
    IMediator mediator) =>
{
    var sessions = await mediator.Send(new GetRecentSessionsQuery { Limit = limit });
    return Results.Ok(sessions);
})
.RequireAuthorization()
.WithTags("Sessions")
.Produces<List<SessionSummaryDto>>();
```

**Bounded Context**: SessionTracking

**Labels**: `phase-1`, `backend`, `api`, `cqrs`
**Estimate**: 2h

---

### Issue #TBD-5: Implement GET /api/v1/users/me/games endpoint with filters

**Title**: Implement user game collection endpoint with pagination and filters

**Description**:
Creare endpoint per recuperare la collezione giochi dell'utente con supporto per filtri (categoria, ordinamento) e paginazione.

**Acceptance Criteria**:
- [ ] Query: `GetUserGamesQuery` con parametri filter/sort/pagination
- [ ] Handler: `GetUserGamesQueryHandler` con caching strategico
- [ ] DTO: `PagedResult<GameDto>` con metadata paginazione
- [ ] Endpoint: `GET /api/v1/users/me/games?category&sort&page&pageSize`
- [ ] Filtri supportati: category (all/strategy/family/party/solo)
- [ ] Sort supportati: alphabetical, lastPlayed, rating, playCount
- [ ] Paginazione: page (default 1), pageSize (default 20, max 100)
- [ ] Include: Game metadata completo per MeepleCard rendering
- [ ] Validation: FluentValidation per tutti i parametri
- [ ] Unit tests: Handler + filters + sorting
- [ ] Integration tests: API con varie combinazioni parametri
- [ ] Swagger documentation completa

**Technical Specs**:
```csharp
// Application/Users/Queries/GetUserGamesQuery.cs
public record GetUserGamesQuery : IRequest<PagedResult<GameDto>>
{
    public string? Category { get; init; } // null = all
    public string Sort { get; init; } = "alphabetical";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public class GetUserGamesQueryValidator : AbstractValidator<GetUserGamesQuery>
{
    public GetUserGamesQueryValidator()
    {
        RuleFor(x => x.Category)
            .Must(c => c == null || new[] { "all", "strategy", "family", "party", "solo" }.Contains(c))
            .When(x => x.Category != null);

        RuleFor(x => x.Sort)
            .Must(s => new[] { "alphabetical", "lastPlayed", "rating", "playCount" }.Contains(s));

        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

// DTOs/GameDto.cs (esistente - verificare campi necessari)
// Deve includere: Id, Title, Publisher, ImageUrl, AverageRating,
// PlayerCount (min-max), PlayingTime, Category, IsOwned, WishlistStatus

// Routing/UsersEndpoints.cs
users.MapGet("/me/games", async (
    [FromQuery] string? category,
    [FromQuery] string sort,
    [FromQuery] int page,
    [FromQuery] int pageSize,
    IMediator mediator) =>
{
    var query = new GetUserGamesQuery
    {
        Category = category,
        Sort = sort,
        Page = page,
        PageSize = pageSize
    };
    var result = await mediator.Send(query);
    return Results.Ok(result);
})
.RequireAuthorization()
.WithTags("Users")
.Produces<PagedResult<GameDto>>();
```

**Bounded Contexts**: UserLibrary, GameManagement

**Labels**: `phase-1`, `backend`, `api`, `cqrs`, `pagination`
**Estimate**: 3h

---

## Phase 2: Frontend Components (4 issues)

### Issue #TBD-6: Create dashboard-v2 component library

**Title**: Create modular dashboard-v2 components

**Description**:
Creare tutti i componenti modulari riutilizzabili per la nuova Gaming Hub Dashboard seguendo il design system glassmorphic.

**Acceptance Criteria**:
- [ ] Struttura: `components/dashboard-v2/` creata
- [ ] Component: `quick-stats.tsx` - Container 4 stat cards
- [ ] Component: `stat-card.tsx` - Single stat display (icon, value, label)
- [ ] Component: `recent-sessions.tsx` - Sessions list container
- [ ] Component: `session-row.tsx` - Single session row display
- [ ] Component: `game-collection-grid.tsx` - Grid wrapper per MeepleCard
- [ ] Component: `filter-bar.tsx` - Category/sort filters UI
- [ ] Component: `empty-states.tsx` - Empty state messages (no games, no sessions)
- [ ] Design system: Glassmorphism `bg-white/70 backdrop-blur-md`
- [ ] Typography: `font-quicksand` headings, `font-nunito` body
- [ ] Accent colors: Amber (`bg-amber-100`, `text-amber-900`)
- [ ] TypeScript: Proper typing per tutti i props
- [ ] Accessibility: ARIA labels, keyboard navigation
- [ ] Unit tests per ogni componente (Vitest + Testing Library)

**Technical Specs**:
```tsx
// components/dashboard-v2/stat-card.tsx
interface StatCardProps {
  icon: string;
  value: number | string;
  label: string;
  sublabel?: string;
}

export function StatCard({ icon, value, label, sublabel }: StatCardProps) {
  return (
    <Card className="bg-white/70 backdrop-blur-md border border-white/20">
      <CardContent className="pt-6">
        <div className="text-4xl mb-2">{icon}</div>
        <div className="text-3xl font-bold font-quicksand">{value}</div>
        <div className="text-sm text-muted-foreground font-nunito">{label}</div>
        {sublabel && <div className="text-xs text-muted-foreground mt-1">{sublabel}</div>}
      </CardContent>
    </Card>
  );
}

// components/dashboard-v2/session-row.tsx
interface SessionRowProps {
  session: SessionSummaryDto;
  onViewDetails?: (id: string) => void;
}

// components/dashboard-v2/game-collection-grid.tsx
interface GameCollectionGridProps {
  games: GameDto[];
  isLoading?: boolean;
  onLoadMore?: () => void;
}

// components/dashboard-v2/filter-bar.tsx
interface FilterBarProps {
  categories: string[];
  currentCategory: string;
  currentSort: string;
  onCategoryChange: (category: string) => void;
  onSortChange: (sort: string) => void;
}

// components/dashboard-v2/empty-states.tsx
type EmptyStateVariant = 'no-games' | 'no-sessions' | 'no-upcoming';

interface EmptyStateProps {
  variant: EmptyStateVariant;
  onAction?: () => void;
}
```

**Files to Create**:
- `components/dashboard-v2/quick-stats.tsx`
- `components/dashboard-v2/stat-card.tsx`
- `components/dashboard-v2/recent-sessions.tsx`
- `components/dashboard-v2/session-row.tsx`
- `components/dashboard-v2/game-collection-grid.tsx`
- `components/dashboard-v2/filter-bar.tsx`
- `components/dashboard-v2/empty-states.tsx`
- `components/dashboard-v2/index.ts` (barrel export)
- `components/dashboard-v2/__tests__/` (directory con test)

**Labels**: `phase-2`, `frontend`, `components`, `ui`
**Estimate**: 4h

---

### Issue #TBD-7: Create dashboard API client

**Title**: Create dashboard API client with TypeScript types

**Description**:
Creare API client TypeScript per chiamare i nuovi endpoint dashboard con type-safety completo.

**Acceptance Criteria**:
- [ ] File: `lib/api/dashboard-client.ts`
- [ ] Function: `getUserStats()` - Fetch user statistics
- [ ] Function: `getRecentSessions(limit?)` - Fetch recent sessions
- [ ] Function: `getUserGames(params)` - Fetch user games with filters/pagination
- [ ] TypeScript types matching backend DTOs
- [ ] Error handling con try/catch e error types
- [ ] JSDoc documentation per ogni function
- [ ] Usa `httpClient` esistente (con auth headers)
- [ ] Unit tests con MSW (Mock Service Worker)

**Technical Specs**:
```typescript
// lib/api/dashboard-client.ts
import { httpClient } from './http-client';

export interface UserStatsDto {
  totalGames: number;
  monthlyPlays: number;
  monthlyPlaysChange: number; // Percentage
  weeklyPlayTime: string; // TimeSpan format "HH:MM:SS"
  monthlyFavorites: number;
}

export interface SessionSummaryDto {
  id: string;
  gameTitle: string;
  gameImageUrl?: string;
  playedAt: string; // ISO 8601
  playerCount: number;
  duration: string; // TimeSpan format "HH:MM:SS"
  rating?: number; // 1-10
  winner?: string;
}

export interface GetUserGamesParams {
  category?: string;
  sort?: 'alphabetical' | 'lastPlayed' | 'rating' | 'playCount';
  page?: number;
  pageSize?: number;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export const dashboardClient = {
  async getUserStats(): Promise<UserStatsDto> {
    const response = await httpClient.get<UserStatsDto>('/api/v1/users/me/stats');
    return response.data;
  },

  async getRecentSessions(limit = 3): Promise<SessionSummaryDto[]> {
    const response = await httpClient.get<SessionSummaryDto[]>(
      `/api/v1/sessions/recent?limit=${limit}`
    );
    return response.data;
  },

  async getUserGames(params: GetUserGamesParams = {}): Promise<PagedResult<GameDto>> {
    const queryParams = new URLSearchParams();
    if (params.category) queryParams.append('category', params.category);
    if (params.sort) queryParams.append('sort', params.sort);
    if (params.page) queryParams.append('page', params.page.toString());
    if (params.pageSize) queryParams.append('pageSize', params.pageSize.toString());

    const response = await httpClient.get<PagedResult<GameDto>>(
      `/api/v1/users/me/games?${queryParams}`
    );
    return response.data;
  },
};
```

**Labels**: `phase-2`, `frontend`, `api-client`, `typescript`
**Estimate**: 2h

---

### Issue #TBD-8: Create dashboard Zustand store

**Title**: Create dashboard state management with Zustand

**Description**:
Creare Zustand store per gestire lo stato globale della dashboard (stats, sessions, games, filters).

**Acceptance Criteria**:
- [ ] File: `lib/stores/dashboard-store.ts`
- [ ] State: stats, recentSessions, games, filters, loading, error
- [ ] Action: `fetchStats()` - Load user statistics
- [ ] Action: `fetchRecentSessions()` - Load recent sessions
- [ ] Action: `fetchGames()` - Load games with current filters
- [ ] Action: `updateFilters()` - Update filter state + re-fetch
- [ ] Action: `loadMore()` - Pagination support
- [ ] Loading states per sezione (stats, sessions, games)
- [ ] Error handling per failed fetches
- [ ] TypeScript typing completo
- [ ] Unit tests con Zustand testing utils

**Technical Specs**:
```typescript
// lib/stores/dashboard-store.ts
import { create } from 'zustand';
import { dashboardClient } from '@/lib/api/dashboard-client';
import type { UserStatsDto, SessionSummaryDto, GameDto, PagedResult } from '@/lib/api/dashboard-client';

interface DashboardFilters {
  search: string;
  category: string;
  sort: 'alphabetical' | 'lastPlayed' | 'rating' | 'playCount';
  page: number;
  pageSize: number;
}

interface DashboardState {
  // State
  stats: UserStatsDto | null;
  recentSessions: SessionSummaryDto[];
  games: GameDto[];
  totalGamesCount: number;
  filters: DashboardFilters;

  // Loading states
  isLoadingStats: boolean;
  isLoadingSessions: boolean;
  isLoadingGames: boolean;

  // Errors
  statsError: string | null;
  sessionsError: string | null;
  gamesError: string | null;

  // Actions
  fetchStats: () => Promise<void>;
  fetchRecentSessions: (limit?: number) => Promise<void>;
  fetchGames: () => Promise<void>;
  updateFilters: (filters: Partial<DashboardFilters>) => void;
  loadMore: () => Promise<void>;
  reset: () => void;
}

export const useDashboardStore = create<DashboardState>((set, get) => ({
  // Initial state
  stats: null,
  recentSessions: [],
  games: [],
  totalGamesCount: 0,
  filters: {
    search: '',
    category: 'all',
    sort: 'alphabetical',
    page: 1,
    pageSize: 20,
  },
  isLoadingStats: false,
  isLoadingSessions: false,
  isLoadingGames: false,
  statsError: null,
  sessionsError: null,
  gamesError: null,

  // Actions
  fetchStats: async () => {
    set({ isLoadingStats: true, statsError: null });
    try {
      const stats = await dashboardClient.getUserStats();
      set({ stats, isLoadingStats: false });
    } catch (error) {
      set({
        statsError: error instanceof Error ? error.message : 'Failed to load stats',
        isLoadingStats: false
      });
    }
  },

  fetchRecentSessions: async (limit = 3) => {
    set({ isLoadingSessions: true, sessionsError: null });
    try {
      const sessions = await dashboardClient.getRecentSessions(limit);
      set({ recentSessions: sessions, isLoadingSessions: false });
    } catch (error) {
      set({
        sessionsError: error instanceof Error ? error.message : 'Failed to load sessions',
        isLoadingSessions: false
      });
    }
  },

  fetchGames: async () => {
    const { filters } = get();
    set({ isLoadingGames: true, gamesError: null });
    try {
      const result = await dashboardClient.getUserGames({
        category: filters.category === 'all' ? undefined : filters.category,
        sort: filters.sort,
        page: filters.page,
        pageSize: filters.pageSize,
      });
      set({
        games: result.items,
        totalGamesCount: result.totalCount,
        isLoadingGames: false
      });
    } catch (error) {
      set({
        gamesError: error instanceof Error ? error.message : 'Failed to load games',
        isLoadingGames: false
      });
    }
  },

  updateFilters: (newFilters) => {
    const { fetchGames } = get();
    set((state) => ({
      filters: { ...state.filters, ...newFilters, page: 1 } // Reset page on filter change
    }));
    fetchGames();
  },

  loadMore: async () => {
    const { filters, games, fetchGames } = get();
    set((state) => ({
      filters: { ...state.filters, page: state.filters.page + 1 }
    }));
    await fetchGames();
  },

  reset: () => {
    set({
      stats: null,
      recentSessions: [],
      games: [],
      totalGamesCount: 0,
      filters: {
        search: '',
        category: 'all',
        sort: 'alphabetical',
        page: 1,
        pageSize: 20,
      },
      isLoadingStats: false,
      isLoadingSessions: false,
      isLoadingGames: false,
      statsError: null,
      sessionsError: null,
      gamesError: null,
    });
  },
}));
```

**Labels**: `phase-2`, `frontend`, `state-management`, `zustand`
**Estimate**: 2h

---

### Issue #TBD-9: Implement Gaming Hub homepage

**Title**: Implement Gaming Hub dashboard as authenticated homepage

**Description**:
Creare la pagina principale Gaming Hub Dashboard in `app/(authenticated)/page.tsx` come homepage per utenti loggati, integrando tutti i componenti dashboard-v2.

**Acceptance Criteria**:
- [ ] File: `app/(authenticated)/page.tsx` (nuova homepage)
- [ ] Metadata: SEO ottimizzato per dashboard
- [ ] Layout: Gaming Hub design (4 sezioni principali)
- [ ] Integration: QuickStats component
- [ ] Integration: RecentSessions component
- [ ] Integration: GameCollectionGrid component
- [ ] Integration: EmptyStates per sezioni vuote
- [ ] Loading states: Skeleton screens per ogni sezione
- [ ] Error handling: Error boundaries per sezione
- [ ] Responsive: Desktop-first (1920px optimal), mobile graceful degradation
- [ ] Accessibility: ARIA labels, semantic HTML
- [ ] Unit tests: Component rendering + state management
- [ ] Visual tests: Chromatic snapshots

**Technical Specs**:
```tsx
// app/(authenticated)/page.tsx
import { Metadata } from 'next';
import { RequireRole } from '@/components/auth/RequireRole';
import { GamingHubClient } from './gaming-hub-client';

export const metadata: Metadata = {
  title: 'Gaming Hub | MeepleAI',
  description: 'Your personal board game hub. Track sessions, manage your collection, and view your gaming statistics.',
  openGraph: {
    title: 'Gaming Hub | MeepleAI',
    description: 'Your personal board game hub.',
    type: 'website',
  },
};

export const dynamic = 'force-dynamic';

export default function HomePage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <GamingHubClient />
    </RequireRole>
  );
}
```

```tsx
// app/(authenticated)/gaming-hub-client.tsx
'use client';

import { useEffect } from 'react';
import { Layout } from '@/components/layout';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import {
  QuickStats,
  RecentSessions,
  GameCollectionGrid,
  FilterBar,
  EmptyStates,
} from '@/components/dashboard-v2';

export function GamingHubClient() {
  const {
    stats,
    recentSessions,
    games,
    filters,
    isLoadingStats,
    isLoadingSessions,
    isLoadingGames,
    fetchStats,
    fetchRecentSessions,
    fetchGames,
    updateFilters,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(3);
    fetchGames();
  }, [fetchStats, fetchRecentSessions, fetchGames]);

  return (
    <Layout showActionBar>
      <div className="container mx-auto py-8 space-y-8">
        {/* Welcome Banner */}
        <div className="bg-amber-100/50 backdrop-blur-md border border-amber-200 rounded-lg p-6">
          <h1 className="text-2xl font-quicksand font-bold text-amber-900">
            Benvenuto! 👋
          </h1>
          {stats && (
            <p className="text-amber-900 font-nunito mt-2">
              Hai giocato {stats.monthlyPlays} partite questo mese
              {stats.monthlyPlaysChange > 0 && (
                <span className="text-green-700">
                  {' '}(+{stats.monthlyPlaysChange}% rispetto al mese scorso)
                </span>
              )}
            </p>
          )}
        </div>

        {/* Quick Stats */}
        <section>
          <h2 className="text-xl font-quicksand font-semibold mb-4">📊 Panoramica</h2>
          <QuickStats stats={stats} isLoading={isLoadingStats} />
        </section>

        {/* Recent Sessions */}
        <section>
          <RecentSessions
            sessions={recentSessions}
            isLoading={isLoadingSessions}
          />
        </section>

        {/* Game Collection */}
        <section>
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-xl font-quicksand font-semibold">📚 I Miei Giochi</h2>
          </div>

          <FilterBar
            categories={['all', 'strategy', 'family', 'party', 'solo']}
            currentCategory={filters.category}
            currentSort={filters.sort}
            onCategoryChange={(cat) => updateFilters({ category: cat })}
            onSortChange={(sort) => updateFilters({ sort })}
          />

          <GameCollectionGrid
            games={games}
            isLoading={isLoadingGames}
          />
        </section>

        {/* Upcoming Games - Empty State */}
        <section>
          <h2 className="text-xl font-quicksand font-semibold mb-4">
            📅 Prossime Partite
          </h2>
          <EmptyStates variant="no-upcoming" />
        </section>
      </div>
    </Layout>
  );
}
```

**Files to Create**:
- `app/(authenticated)/page.tsx`
- `app/(authenticated)/gaming-hub-client.tsx`
- `app/(authenticated)/__tests__/page.test.tsx`

**Labels**: `phase-2`, `frontend`, `page`, `integration`
**Estimate**: 2h

---

## Phase 3: Integration & Testing (3 issues)

### Issue #TBD-10: Backend integration tests for dashboard endpoints

**Title**: Integration tests for dashboard API endpoints

**Description**:
Creare test di integrazione completi per i 3 nuovi endpoint dashboard, testando tutti i casi d'uso e edge cases.

**Acceptance Criteria**:
- [ ] Test class: `UserStatsEndpointTests` in `tests/Api.Tests/Users/`
- [ ] Test class: `RecentSessionsEndpointTests` in `tests/Api.Tests/Sessions/`
- [ ] Test class: `UserGamesEndpointTests` in `tests/Api.Tests/Users/`
- [ ] Test: GET /users/me/stats returns correct calculations
- [ ] Test: Stats change calculation (month over month)
- [ ] Test: Recent sessions ordering (DESC by PlayedAt)
- [ ] Test: Recent sessions limit parameter (1-20 validation)
- [ ] Test: User games pagination (page/pageSize)
- [ ] Test: User games filtering (category filter)
- [ ] Test: User games sorting (all 4 sort options)
- [ ] Test: Unauthorized access returns 401
- [ ] Test: Empty collections handled gracefully
- [ ] Coverage: >90% per tutti gli handler
- [ ] Usa Testcontainers per PostgreSQL

**Technical Specs**:
```csharp
// tests/Api.Tests/Users/UserStatsEndpointTests.cs
public class UserStatsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task GetUserStats_WithGamesAndSessions_ReturnsCorrectStats()
    {
        // Arrange
        var userId = await SeedUserWithGames(gameCount: 10);
        await SeedSessions(userId, sessionsThisMonth: 5, sessionsLastMonth: 4);

        // Act
        var response = await Client.GetAsync("/api/v1/users/me/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await response.Content.ReadFromJsonAsync<UserStatsDto>();
        stats!.TotalGames.Should().Be(10);
        stats.MonthlyPlays.Should().Be(5);
        stats.MonthlyPlaysChange.Should().Be(25); // +25%
    }

    [Fact]
    public async Task GetUserStats_Unauthorized_Returns401()
    {
        // Arrange
        Client.DefaultRequestHeaders.Authorization = null;

        // Act
        var response = await Client.GetAsync("/api/v1/users/me/stats");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

// tests/Api.Tests/Sessions/RecentSessionsEndpointTests.cs
public class RecentSessionsEndpointTests : IntegrationTestBase
{
    [Fact]
    public async Task GetRecentSessions_ReturnsOrderedByDate()
    {
        // Arrange
        var userId = await SeedUserWithSessions(count: 5);

        // Act
        var response = await Client.GetAsync("/api/v1/sessions/recent?limit=3");

        // Assert
        var sessions = await response.Content.ReadFromJsonAsync<List<SessionSummaryDto>>();
        sessions.Should().HaveCount(3);
        sessions.Should().BeInDescendingOrder(s => s.PlayedAt);
    }

    [Theory]
    [InlineData(0, HttpStatusCode.BadRequest)]
    [InlineData(21, HttpStatusCode.BadRequest)]
    [InlineData(5, HttpStatusCode.OK)]
    public async Task GetRecentSessions_ValidatesLimit(int limit, HttpStatusCode expected)
    {
        var response = await Client.GetAsync($"/api/v1/sessions/recent?limit={limit}");
        response.StatusCode.Should().Be(expected);
    }
}

// tests/Api.Tests/Users/UserGamesEndpointTests.cs
public class UserGamesEndpointTests : IntegrationTestBase
{
    [Theory]
    [InlineData("strategy", 5)]
    [InlineData("family", 3)]
    [InlineData(null, 10)]
    public async Task GetUserGames_FiltersbyCategory(string? category, int expectedCount)
    {
        // Arrange
        await SeedUserWithCategorizedGames();
        var query = category != null ? $"?category={category}" : "";

        // Act
        var response = await Client.GetAsync($"/api/v1/users/me/games{query}");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<PagedResult<GameDto>>();
        result!.Items.Should().HaveCount(expectedCount);
    }

    [Theory]
    [InlineData("alphabetical", "Azul", "Wingspan")]
    [InlineData("rating", "Wingspan", "Azul")]
    public async Task GetUserGames_SortsCorrectly(string sort, string first, string last)
    {
        // Arrange
        await SeedUserWithGames();

        // Act
        var response = await Client.GetAsync($"/api/v1/users/me/games?sort={sort}");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<PagedResult<GameDto>>();
        result!.Items.First().Title.Should().Be(first);
        result.Items.Last().Title.Should().Be(last);
    }
}
```

**Labels**: `phase-3`, `backend`, `testing`, `integration`
**Estimate**: 3h

---

### Issue #TBD-11: Frontend unit tests for dashboard components

**Title**: Unit tests for dashboard-v2 components

**Description**:
Creare test unitari completi per tutti i componenti dashboard-v2 usando Vitest e Testing Library.

**Acceptance Criteria**:
- [ ] Test: `stat-card.test.tsx` - Rendering props, accessibilità
- [ ] Test: `quick-stats.test.tsx` - 4 cards rendering, loading state
- [ ] Test: `session-row.test.tsx` - Session data display, actions
- [ ] Test: `recent-sessions.test.tsx` - List rendering, empty state
- [ ] Test: `filter-bar.test.tsx` - Filter interactions, callbacks
- [ ] Test: `game-collection-grid.test.tsx` - MeepleCard grid, pagination
- [ ] Test: `empty-states.test.tsx` - All variants rendering
- [ ] Test: `dashboard-client.test.ts` - API calls con MSW mocking
- [ ] Test: `dashboard-store.test.ts` - Zustand actions + state updates
- [ ] Test: `gaming-hub-client.test.tsx` - Full integration
- [ ] Coverage: >85% per tutti i componenti
- [ ] Accessibilità: ARIA assertions
- [ ] Mock: MSW handlers per tutte le API

**Technical Specs**:
```tsx
// components/dashboard-v2/__tests__/stat-card.test.tsx
import { render, screen } from '@testing-library/react';
import { StatCard } from '../stat-card';

describe('StatCard', () => {
  it('renders icon, value, and label', () => {
    render(
      <StatCard
        icon="🎲"
        value={47}
        label="Giochi Collezione"
      />
    );

    expect(screen.getByText('🎲')).toBeInTheDocument();
    expect(screen.getByText('47')).toBeInTheDocument();
    expect(screen.getByText('Giochi Collezione')).toBeInTheDocument();
  });

  it('renders optional sublabel', () => {
    render(
      <StatCard
        icon="🎯"
        value={12}
        label="Partite"
        sublabel="Questo Mese"
      />
    );

    expect(screen.getByText('Questo Mese')).toBeInTheDocument();
  });

  it('handles string values', () => {
    render(
      <StatCard
        icon="⏱️"
        value="8h 30m"
        label="Giocate"
      />
    );

    expect(screen.getByText('8h 30m')).toBeInTheDocument();
  });
});

// lib/api/__tests__/dashboard-client.test.ts
import { rest } from 'msw';
import { setupServer } from 'msw/node';
import { dashboardClient } from '../dashboard-client';

const server = setupServer(
  rest.get('/api/v1/users/me/stats', (req, res, ctx) => {
    return res(ctx.json({
      totalGames: 47,
      monthlyPlays: 12,
      monthlyPlaysChange: 15,
      weeklyPlayTime: '08:30:00',
      monthlyFavorites: 3,
    }));
  })
);

beforeAll(() => server.listen());
afterEach(() => server.resetHandlers());
afterAll(() => server.close());

describe('dashboardClient', () => {
  it('getUserStats returns typed data', async () => {
    const stats = await dashboardClient.getUserStats();
    expect(stats.totalGames).toBe(47);
    expect(stats.monthlyPlays).toBe(12);
  });

  it('handles API errors', async () => {
    server.use(
      rest.get('/api/v1/users/me/stats', (req, res, ctx) => {
        return res(ctx.status(500));
      })
    );

    await expect(dashboardClient.getUserStats()).rejects.toThrow();
  });
});
```

**Labels**: `phase-3`, `frontend`, `testing`, `unit-tests`
**Estimate**: 3h

---

### Issue #TBD-12: E2E tests for Gaming Hub dashboard

**Title**: E2E tests for Gaming Hub dashboard with Playwright

**Description**:
Creare test end-to-end completi per validare il flusso completo della Gaming Hub Dashboard.

**Acceptance Criteria**:
- [ ] File: `apps/web/__tests__/e2e/gaming-hub.spec.ts`
- [ ] Test: User login → Dashboard loads with all sections
- [ ] Test: Quick stats display correctly
- [ ] Test: Recent sessions visible and formatted
- [ ] Test: Game collection grid renders MeepleCards
- [ ] Test: Filter bar changes update game grid
- [ ] Test: Sort dropdown changes game order
- [ ] Test: Pagination/infinite scroll works
- [ ] Test: Empty states render when no data
- [ ] Test: Loading states (skeleton screens) display
- [ ] Test: Error states handled gracefully
- [ ] Test: Click session → navigate to details
- [ ] Test: Click game card → navigate to game page
- [ ] Mock: API responses con `page.context().route()`
- [ ] Visual regression: Screenshots baseline

**Technical Specs**:
```typescript
// apps/web/__tests__/e2e/gaming-hub.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Gaming Hub Dashboard', () => {
  test.beforeEach(async ({ page, context }) => {
    // Set auth bypass for testing
    await page.goto('/');

    // Mock API responses at context level (CRITICAL for Next.js)
    await context.route('**/api/v1/users/me/stats', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          totalGames: 47,
          monthlyPlays: 12,
          monthlyPlaysChange: 15,
          weeklyPlayTime: '08:30:00',
          monthlyFavorites: 3,
        }),
      });
    });

    await context.route('**/api/v1/sessions/recent*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: '123',
            gameTitle: 'Catan',
            gameImageUrl: '/images/catan.jpg',
            playedAt: new Date(Date.now() - 2 * 60 * 60 * 1000).toISOString(),
            playerCount: 4,
            duration: '01:30:00',
            rating: 9,
            winner: 'Marco',
          },
        ]),
      });
    });

    await context.route('**/api/v1/users/me/games*', async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          items: [
            { id: '1', title: 'Catan', averageRating: 9.2 },
            { id: '2', title: 'Azul', averageRating: 8.5 },
          ],
          totalCount: 47,
          page: 1,
          pageSize: 20,
          totalPages: 3,
        }),
      });
    });
  });

  test('loads dashboard with all sections', async ({ page }) => {
    await page.goto('/');

    // Welcome banner
    await expect(page.getByText(/Benvenuto/)).toBeVisible();

    // Quick stats
    await expect(page.getByText('47')).toBeVisible(); // Total games
    await expect(page.getByText('Giochi Collezione')).toBeVisible();

    // Recent sessions
    await expect(page.getByText('Sessioni Recenti')).toBeVisible();
    await expect(page.getByText('Catan')).toBeVisible();

    // Game collection
    await expect(page.getByText('I Miei Giochi')).toBeVisible();
  });

  test('filter bar updates game grid', async ({ page, context }) => {
    await page.goto('/');

    // Mock filtered response
    await context.route('**/api/v1/users/me/games?category=strategy*', async (route) => {
      await route.fulfill({
        status: 200,
        body: JSON.stringify({
          items: [{ id: '1', title: 'Catan', category: 'strategy' }],
          totalCount: 15,
        }),
      });
    });

    // Click strategy filter
    await page.getByRole('button', { name: /Strategia/ }).click();

    // Verify filtered results
    await expect(page.getByText('15')).toBeVisible(); // Updated count
  });

  test('empty states render when no data', async ({ page, context }) => {
    // Mock empty responses
    await context.route('**/api/v1/sessions/recent*', async (route) => {
      await route.fulfill({ status: 200, body: JSON.stringify([]) });
    });

    await page.goto('/');

    await expect(page.getByText(/Nessuna partita programmata/)).toBeVisible();
  });
});
```

**Environment**: `PLAYWRIGHT_AUTH_BYPASS=true`

**Labels**: `phase-3`, `frontend`, `testing`, `e2e`, `playwright`
**Estimate**: 3h

---

## Phase 4: Migration & Cleanup (2 issues)

### Issue #TBD-13: Deprecate old dashboard and setup redirects

**Title**: Remove old dashboard route and setup redirects

**Description**:
Rimuovere la vecchia dashboard (`/dashboard`) e impostare redirect automatico alla nuova homepage.

**Acceptance Criteria**:
- [ ] Remove: `app/(authenticated)/dashboard/` directory completa
- [ ] Remove: `components/dashboard/DashboardHub` component
- [ ] Create: `app/(authenticated)/dashboard/page.tsx` con redirect
- [ ] Redirect: `/dashboard` → `/` (permanent 308)
- [ ] Update: Navigation links che puntavano a `/dashboard`
- [ ] Update: Breadcrumbs e menu items
- [ ] Verify: Nessun broken link in tutta l'app
- [ ] Test: Redirect funziona correttamente
- [ ] Test: Old dashboard non più accessibile
- [ ] Cleanup: Rimuovere unused imports/dependencies

**Technical Specs**:
```tsx
// app/(authenticated)/dashboard/page.tsx (nuovo - solo redirect)
import { redirect } from 'next/navigation';

export default function OldDashboardRedirect() {
  redirect('/');
}
```

```bash
# Files to remove
rm -rf apps/web/src/app/(authenticated)/dashboard/
rm -rf apps/web/src/components/dashboard/DashboardHub*

# Search for references
grep -r "/dashboard" apps/web/src/
grep -r "DashboardHub" apps/web/src/
```

**Labels**: `phase-4`, `frontend`, `cleanup`, `migration`
**Estimate**: 2h

---

### Issue #TBD-14: Documentation updates for Gaming Hub

**Title**: Update documentation for new Gaming Hub dashboard

**Description**:
Aggiornare tutta la documentazione per riflettere la nuova Gaming Hub Dashboard come homepage utenti.

**Acceptance Criteria**:
- [ ] Create: `docs/07-frontend/gaming-hub-dashboard.md` - Comprehensive guide
- [ ] Update: `docs/07-frontend/navigation-routing.md` - Homepage routing
- [ ] Update: `docs/features/dashboard-requirements.md` - New requirements
- [ ] Document: Component API per dashboard-v2
- [ ] Document: Zustand store usage patterns
- [ ] Document: API client usage examples
- [ ] Update: CLAUDE.md con Gaming Hub reference
- [ ] Screenshot: Dashboard desktop layout
- [ ] Diagram: Component hierarchy
- [ ] Diagram: Data flow (API → Store → Components)

**Documentation Structure**:
```markdown
# docs/07-frontend/gaming-hub-dashboard.md

## Overview
Gaming Hub Dashboard as homepage for authenticated users.

## Architecture
- Route: `/` (authenticated users only)
- Layout: Desktop-first, responsive
- Design: Glassmorphism + Amber accent

## Components
### Quick Stats
[Component API, props, usage examples]

### Recent Sessions
[Component API, data flow, customization]

### Game Collection Grid
[MeepleCard integration, filters, pagination]

## State Management
### Dashboard Store
[Zustand store API, actions, selectors]

## API Integration
### Endpoints Used
- GET /api/v1/users/me/stats
- GET /api/v1/sessions/recent
- GET /api/v1/users/me/games

## Testing
[Unit, integration, E2E test patterns]

## Troubleshooting
[Common issues and solutions]
```

**Labels**: `phase-4`, `documentation`
**Estimate**: 2h

---

## 📊 Issue Summary

| Phase | Issues | Total Estimate |
|-------|--------|----------------|
| Phase 0 | 2 | 3h |
| Phase 1 | 3 | 8h |
| Phase 2 | 4 | 10h |
| Phase 3 | 3 | 9h |
| Phase 4 | 2 | 4h |
| **Total** | **14** | **~34h** |

---

## 🔄 Dependencies Graph

```
Phase 0 (Preparation)
  ├─ Issue #TBD-1 (Seed data) → Enables all testing
  └─ Issue #TBD-2 (Audit) → Informs Phase 4 cleanup

Phase 1 (Backend) - Can run in parallel
  ├─ Issue #TBD-3 (Stats API)
  ├─ Issue #TBD-4 (Sessions API)
  └─ Issue #TBD-5 (Games API)
       ↓
Phase 2 (Frontend) - Sequential dependencies
  ├─ Issue #TBD-6 (Components)
  ├─ Issue #TBD-7 (API Client) → Requires Phase 1 complete
  ├─ Issue #TBD-8 (Zustand Store) → Requires #TBD-7
  └─ Issue #TBD-9 (Main Page) → Requires #TBD-6, #TBD-8
       ↓
Phase 3 (Testing) - Can run in parallel after Phase 2
  ├─ Issue #TBD-10 (Backend tests) → Requires Phase 1
  ├─ Issue #TBD-11 (Frontend tests) → Requires Phase 2
  └─ Issue #TBD-12 (E2E tests) → Requires Phase 1 + Phase 2
       ↓
Phase 4 (Migration) - Sequential
  ├─ Issue #TBD-13 (Cleanup) → Requires Phase 3 passing
  └─ Issue #TBD-14 (Docs) → Final step
```

---

## 🎯 Implementation Strategy

### Parallelization Opportunities
1. **Phase 1**: All 3 backend endpoints can be developed in parallel
2. **Phase 3**: Backend tests, frontend tests, E2E tests can run concurrently
3. **Incremental deployment**: Enable new dashboard behind feature flag

### Risk Mitigation
- Seed data first (Phase 0) ensures testability
- Keep old dashboard until Phase 4 (safe rollback)
- Comprehensive testing before migration (Phase 3)
- Documentation last ensures accuracy (Phase 4)

### Quality Gates
- ✅ **After Phase 1**: All API endpoints tested with Postman/curl
- ✅ **After Phase 2**: Component library in Storybook
- ✅ **After Phase 3**: All tests passing (>90% backend, >85% frontend)
- ✅ **After Phase 4**: Old dashboard removed, redirects working

---

## 📝 Notes

### Design Decisions
- **Homepage takeover**: `/` per utenti loggati mostra Gaming Hub (non redirect)
- **Old dashboard**: Deprecata con redirect `/dashboard` → `/`
- **MeepleCard reuse**: Massimizzare riuso sistema esistente (#3820)
- **Modular components**: Ogni sezione dashboard è componente indipendente
- **State management**: Zustand per semplicità vs Redux

### Technical Constraints
- Desktop-first: Mobile è graceful degradation, non prioritario
- Empty states: Must handle no games, no sessions gracefully
- Caching: Stats cache 5min, sessions 2min per ridurre DB load
- Pagination: Default 20 items, max 100 per performance

### Future Enhancements (Out of Scope)
- 📱 Mobile-optimized layout
- 📅 Calendario interattivo sessioni programmate
- 🤝 Social features (amici, gruppi)
- 🤖 AI recommendations widget
- 📊 Advanced analytics/graphs
- 🏆 Achievements/badges display
