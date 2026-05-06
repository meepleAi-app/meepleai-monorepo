# Dashboard CX/IX/MX Improvements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix 13 issues from the spec panel review: 3 critical (CX = C1–C3), 5 important (IX = I1–I5), 5 maintenance (MX = M1–M5) on the Gaming Hub Dashboard at `/dashboard`.

**Architecture:** Three-phase approach. Phase 1 (CX) patches critical runtime issues in-place. Phase 2 (IX) refactors the data layer. Phase 3 (MX) decomposes the 729-line monolith into individually-testable widget modules and cleans dead code.

**Tech Stack:** Next.js 16, React 19, Zustand 4, TypeScript 5, Vitest + @testing-library/react + @testing-library/user-event, Tailwind 4

---

## File Structure

### New Files

| File | Responsibility |
|------|----------------|
| `apps/web/src/app/(authenticated)/dashboard/widgets/dashboard-colors.ts` | `C` color constants (centralised, M4) |
| `apps/web/src/app/(authenticated)/dashboard/widgets/BentoWidget.tsx` | Base grid container + WidgetLabel |
| `apps/web/src/app/(authenticated)/dashboard/widgets/LiveSessionWidget.tsx` | Live session card with error + retry |
| `apps/web/src/app/(authenticated)/dashboard/widgets/KpiWidget.tsx` | KPI metric tile |
| `apps/web/src/app/(authenticated)/dashboard/widgets/LibraryWidget.tsx` | Library preview with error state |
| `apps/web/src/app/(authenticated)/dashboard/widgets/ChatPreviewWidget.tsx` | Chat AI honest CTA (no fake data) |
| `apps/web/src/app/(authenticated)/dashboard/widgets/LeaderboardWidget.tsx` | Group leaderboard |
| `apps/web/src/app/(authenticated)/dashboard/widgets/TrendingWidget.tsx` | Trending games with error state |
| `apps/web/src/app/(authenticated)/dashboard/widgets/BentoDashboardSidebar.tsx` | 200px sidebar nav |
| `apps/web/src/app/(authenticated)/dashboard/widgets/index.ts` | Re-exports all widgets |
| `apps/web/src/app/(authenticated)/dashboard/__tests__/LiveSessionWidget.test.tsx` | Tests: loading / empty / active / error / retry |
| `apps/web/src/app/(authenticated)/dashboard/__tests__/KpiWidget.test.tsx` | Tests: value / badge / href navigation |
| `apps/web/src/app/(authenticated)/dashboard/__tests__/LeaderboardWidget.test.tsx` | Tests: empty / ranking / medal order / sort |
| `apps/web/src/lib/api/clients/aiUsageClient.ts` | AI usage API methods (extracted from dashboardClient) |

### Modified Files

| File | Change |
|------|--------|
| `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | Phase 1: error props + visibility refresh + honest chat; Phase 3: slim orchestrator |
| `apps/web/src/lib/api/dashboard-client.ts` | Remove 4 AI usage methods |
| `apps/web/src/lib/stores/dashboard-store.ts` | Extend sort types, remove `loadMore` |

---

## Setup

### Task 0: Create feature branch

- [ ] **Step 1: Create branch from main-dev**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git checkout main-dev && git pull
git checkout -b feature/dashboard-cx-ix-mx
git config branch.feature/dashboard-cx-ix-mx.parent main-dev
```

Expected: on branch `feature/dashboard-cx-ix-mx`

---

## Phase 1: CX — Critical Fixes

### Task 1: C1 — Error States in Dashboard Widgets

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Widgets `LiveSessionWidget`, `LibraryWidget`, and `TrendingWidget` silently show empty state on API failure. Add `error` + `onRetry` props so failures are visible and recoverable.

- [ ] **Step 1: Update `LiveSessionWidget` function signature and add error branch**

In `dashboard-client.tsx`, replace the `LiveSessionWidget` function with:

```tsx
function LiveSessionWidget({
  session,
  isLoading,
  error,
  onRetry,
}: {
  session: SessionSummaryDto | undefined;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const router = useRouter();

  if (isLoading) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} accentColor={C.success} className="animate-pulse">
        <div className="h-full" />
      </BentoWidget>
    );
  }

  if (error) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base" style={{ color: 'hsl(0,72%,51%)' }}>
            Errore nel caricamento
          </p>
          <p className="text-sm text-muted-foreground mt-0.5">{error}</p>
        </div>
        <button
          onClick={e => { e.stopPropagation(); onRetry(); }}
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold border border-border hover:bg-muted/30 transition-colors"
        >
          Riprova
        </button>
      </BentoWidget>
    );
  }

  if (!session) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="border-dashed flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base text-foreground">Nessuna sessione attiva</p>
          <p className="text-sm text-muted-foreground mt-0.5">Avvia una nuova partita per vederla qui</p>
        </div>
        <Link
          href="/sessions/new"
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold text-white transition-opacity hover:opacity-90"
          style={{ background: C.game }}
          onClick={e => e.stopPropagation()}
        >
          Nuova partita
        </Link>
      </BentoWidget>
    );
  }

  return (
    <BentoWidget
      colSpan={8}
      rowSpan={2}
      accentColor={C.success}
      accentBg="rgba(16,185,129,0.04)"
      className="flex flex-col justify-between"
      onClick={() => router.push(`/sessions/${session.id}`)}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Sessione Live</WidgetLabel>
        <span
          className="inline-flex items-center gap-1.5 text-[10px] font-bold rounded-full px-2.5 py-0.5"
          style={{ background: 'rgba(16,185,129,0.12)', color: C.success, border: '1px solid rgba(16,185,129,0.2)' }}
        >
          <span className="w-1.5 h-1.5 rounded-full animate-pulse" style={{ background: C.success }} />
          IN CORSO
        </span>
      </div>
      <div className="flex items-center gap-3">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center text-2xl shrink-0 overflow-hidden"
          style={{ background: 'rgba(255,255,255,0.06)' }}
        >
          {session.gameImageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={session.gameImageUrl} alt={session.gameName} className="w-full h-full object-cover" />
          ) : '🎲'}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-extrabold text-base leading-tight truncate">{session.gameName}</p>
          <p className="text-[11px] text-muted-foreground mt-0.5">
            {session.playerCount} giocatori{session.winnerName ? ` · Vincitore: ${session.winnerName}` : ''}
          </p>
        </div>
        <span className="shrink-0 px-3.5 py-1.5 rounded-lg text-[11px] font-bold text-white" style={{ background: C.game }} aria-hidden="true">
          Entra →
        </span>
      </div>
    </BentoWidget>
  );
}
```

- [ ] **Step 2: Update `LibraryWidget` with error branch**

Replace the `LibraryWidget` function signature and add error branch after `isLoading`:

```tsx
function LibraryWidget({
  games, totalCount, isLoading, error, onRetry,
}: {
  games: UserGameDto[];
  totalCount: number;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const router = useRouter();
  return (
    <BentoWidget colSpan={6} rowSpan={4} className="flex flex-col gap-0" onClick={() => router.push('/library')}>
      <WidgetLabel>La Tua Libreria</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="flex items-center gap-2 py-1.5 border-b border-border/40 animate-pulse">
              <div className="w-7 h-7 rounded-md bg-muted/60 shrink-0" />
              <div className="flex-1 h-3 rounded bg-muted/60" />
              <div className="w-8 h-3 rounded bg-muted/40" />
            </div>
          ))
        ) : error ? (
          <div className="flex flex-col items-center justify-center flex-1 gap-2 py-4 text-center">
            <p className="text-[11px] text-muted-foreground">Errore nel caricamento giochi</p>
            <button
              onClick={e => { e.stopPropagation(); onRetry(); }}
              className="text-[10px] font-bold px-3 py-1.5 rounded-lg border border-border hover:bg-muted/30 transition-colors"
            >
              Riprova
            </button>
          </div>
        ) : games.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">Nessun gioco in libreria ancora</p>
        ) : (
          games.slice(0, 6).map(game => (
            <div
              key={game.id}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0 group/row"
              onClick={e => { e.stopPropagation(); router.push(`/library/${game.id}`); }}
            >
              {(game.thumbnailUrl ?? game.imageUrl) ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={game.thumbnailUrl ?? game.imageUrl ?? ''} alt={game.title} className="w-7 h-7 rounded-md object-cover shrink-0" />
              ) : (
                <div className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center text-sm" style={{ background: `${C.game}22` }}>🎲</div>
              )}
              <span className="font-quicksand font-semibold text-[11px] flex-1 truncate text-foreground group-hover/row:text-primary transition-colors">{game.title}</span>
              {game.averageRating !== null && game.averageRating !== undefined && (
                <span className="font-mono text-[9px] font-semibold shrink-0" style={{ color: C.game }}>★ {game.averageRating.toFixed(1)}</span>
              )}
            </div>
          ))
        )}
      </div>
      <p className="text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>Vedi tutti {totalCount} →</p>
    </BentoWidget>
  );
}
```

- [ ] **Step 3: Update `TrendingWidget` with error branch**

Replace `TrendingWidget` function:

```tsx
function TrendingWidget({
  games, isLoading, error, onRetry,
}: {
  games: TrendingGameDto[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const router = useRouter();
  return (
    <BentoWidget colSpan={6} rowSpan={2} accentColor={C.kb} className="flex flex-col" onClick={() => router.push('/games')}>
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      {error ? (
        <div className="flex items-center gap-2 mt-1">
          <p className="text-[10px] text-muted-foreground flex-1">Dati non disponibili</p>
          <button
            onClick={e => { e.stopPropagation(); onRetry(); }}
            className="text-[9px] font-bold px-2 py-1 rounded border border-border hover:bg-muted/30 transition-colors"
          >
            Riprova
          </button>
        </div>
      ) : (
        <div className="flex gap-3 mt-1 overflow-hidden">
          {isLoading
            ? Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="flex flex-col items-center gap-1 shrink-0">
                  <div className="w-9 h-12 rounded-md bg-muted/60 animate-pulse" />
                  <div className="w-9 h-2 rounded bg-muted/40 animate-pulse" />
                </div>
              ))
            : games.slice(0, 6).map(game => (
                <div key={game.gameId} className="flex flex-col items-center gap-1 cursor-pointer shrink-0 group/card" onClick={e => { e.stopPropagation(); router.push(`/games/${game.gameId}`); }}>
                  {game.thumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img src={game.thumbnailUrl} alt={game.title} className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all" />
                  ) : (
                    <div className="w-9 h-12 rounded-md flex items-center justify-center text-lg" style={{ background: 'rgba(255,255,255,0.06)' }}>🎲</div>
                  )}
                  <span className="font-quicksand text-[8px] font-bold text-center w-9 truncate">{game.title}</span>
                </div>
              ))}
        </div>
      )}
    </BentoWidget>
  );
}
```

- [ ] **Step 4: Wire error props + new store fields in `DashboardClient`**

Update the destructured fields and widget usages in `DashboardClient`:

```tsx
export function DashboardClient() {
  const {
    stats, isLoadingStats, fetchStats,
    recentSessions, isLoadingSessions, sessionsError, fetchRecentSessions,
    updateFilters,
    games, isLoadingGames, gamesError, fetchGames,
    totalGamesCount,
    trendingGames, isLoadingTrending, trendingError, fetchTrendingGames,
  } = useDashboardStore();
  // ... rest unchanged except widget props below
```

Pass error props to widgets (replace the three widget usages):

```tsx
<LiveSessionWidget
  session={latestSession}
  isLoading={isLoadingSessions}
  error={sessionsError}
  onRetry={() => fetchRecentSessions(8)}
/>

<LibraryWidget
  games={games}
  totalCount={totalGamesCount || totalGames}
  isLoading={isLoadingGames}
  error={gamesError}
  onRetry={fetchGames}
/>

<TrendingWidget
  games={trendingGames}
  isLoading={isLoadingTrending}
  error={trendingError}
  onRetry={() => fetchTrendingGames(6)}
/>
```

- [ ] **Step 5: Type-check**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errors

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "feat(dashboard): C1 — add error states with retry to LiveSession/Library/Trending widgets"
```

---

### Task 2: C2 — Live Session Visibility Refresh

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

- [ ] **Step 1: Add second `useEffect` for visibility refresh**

In `dashboard-client.tsx`, add after the existing `useEffect`:

```tsx
useEffect(() => {
  const handleVisibilityChange = () => {
    if (!document.hidden) fetchRecentSessions(8);
  };
  document.addEventListener('visibilitychange', handleVisibilityChange);
  return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
  // fetchRecentSessions is a stable Zustand action reference
  // eslint-disable-next-line react-hooks/exhaustive-deps
}, []);
```

- [ ] **Step 2: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "feat(dashboard): C2 — refresh live session on tab focus via visibilitychange"
```

---

### Task 3: C3 — ChatPreview Honest CTA

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

- [ ] **Step 1: Replace `ChatPreviewWidget` with honest CTA**

Replace the entire `ChatPreviewWidget` function in `dashboard-client.tsx`:

```tsx
function ChatPreviewWidget() {
  const router = useRouter();
  return (
    <BentoWidget colSpan={6} rowSpan={4} accentColor={C.chat} className="flex flex-col" onClick={() => router.push('/chat')}>
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Chat AI</WidgetLabel>
        <span className="text-[9px] font-bold rounded-full px-2 py-0.5" style={{ background: `${C.chat}20`, color: C.chat }}>
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center px-4">
        <div className="w-12 h-12 rounded-2xl flex items-center justify-center text-2xl" style={{ background: `${C.chat}15` }}>
          💬
        </div>
        <div>
          <p className="font-quicksand font-bold text-sm text-foreground">Chiedi all&apos;AI</p>
          <p className="text-[11px] text-muted-foreground mt-0.5">
            Regole, strategie e suggerimenti per i tuoi giochi da tavolo
          </p>
        </div>
      </div>
      <div className="mt-auto pt-2 flex gap-1.5" onClick={e => { e.stopPropagation(); router.push('/chat'); }}>
        <div
          className="flex-1 h-7 rounded-lg flex items-center px-2.5 text-[11px] text-muted-foreground/50"
          style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.07)' }}
        >
          Fai una domanda…
        </div>
        <button
          className="w-7 h-7 rounded-lg flex items-center justify-center text-white text-sm"
          style={{ background: C.chat }}
          aria-label="Vai alla chat"
        >
          ↑
        </button>
      </div>
    </BentoWidget>
  );
}
```

- [ ] **Step 2: Type-check and commit**

```bash
cd apps/web && pnpm typecheck && pnpm lint
git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "fix(dashboard): C3 — replace hardcoded fake chat conversation with honest CTA"
```

---

## Phase 2: IX — Important Refactors

### Task 4: I1 — Extract AI Usage Client

**Files:**
- Create: `apps/web/src/lib/api/clients/aiUsageClient.ts`
- Modify: `apps/web/src/lib/api/dashboard-client.ts`

- [ ] **Step 1: Verify no external callers**

```bash
cd apps/web && grep -r "getMyAiUsage" src/ --include="*.ts" --include="*.tsx" -l
```

Expected: only `src/lib/api/dashboard-client.ts`

- [ ] **Step 2: Create `apps/web/src/lib/api/clients/aiUsageClient.ts`**

```typescript
/**
 * AI Usage API Client
 * Issue #94 / Issue #5484
 *
 * Extracted from dashboard-client.ts — AI usage belongs to a different
 * bounded context than the Gaming Hub Dashboard.
 */

import { HttpClient } from '../core/httpClient';
import {
  UserAiUsageDtoSchema,
  type UserAiUsageDto,
  AiUsageSummaryDtoSchema,
  type AiUsageSummaryDto,
  AiUsageDistributionsDtoSchema,
  type AiUsageDistributionsDto,
  AiUsageRecentDtoSchema,
  type AiUsageRecentDto,
} from '../schemas/ai-usage.schemas';

const httpClient = new HttpClient();

export const aiUsageClient = {
  /** Get current user's AI usage statistics. @param days Lookback period (default 30) */
  async getMyAiUsage(days = 30): Promise<UserAiUsageDto> {
    const response = await httpClient.get<UserAiUsageDto>(
      `/api/v1/users/me/ai-usage?days=${days}`,
      UserAiUsageDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage');
    return response;
  },

  /** Get multi-period AI usage summary (today/7d/30d) */
  async getMyAiUsageSummary(): Promise<AiUsageSummaryDto> {
    const response = await httpClient.get<AiUsageSummaryDto>(
      '/api/v1/users/me/ai-usage/summary',
      AiUsageSummaryDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage summary');
    return response;
  },

  /** Get AI usage distributions (model, provider, operation). @param days default 30 */
  async getMyAiUsageDistributions(days = 30): Promise<AiUsageDistributionsDto> {
    const response = await httpClient.get<AiUsageDistributionsDto>(
      `/api/v1/users/me/ai-usage/distributions?days=${days}`,
      AiUsageDistributionsDtoSchema
    );
    if (!response) throw new Error('Failed to fetch AI usage distributions');
    return response;
  },

  /** Get recent AI requests paginated. @param page default 1, @param pageSize default 20 */
  async getMyAiUsageRecent(page = 1, pageSize = 20): Promise<AiUsageRecentDto> {
    const response = await httpClient.get<AiUsageRecentDto>(
      `/api/v1/users/me/ai-usage/recent?page=${page}&pageSize=${pageSize}`,
      AiUsageRecentDtoSchema
    );
    if (!response) throw new Error('Failed to fetch recent AI requests');
    return response;
  },
};
```

- [ ] **Step 3: Remove AI usage methods and their imports from `dashboard-client.ts`**

In `apps/web/src/lib/api/dashboard-client.ts`:
1. Remove lines 10–18 (the 5 AI usage schema imports)
2. Remove the 4 methods: `getMyAiUsage`, `getMyAiUsageSummary`, `getMyAiUsageDistributions`, `getMyAiUsageRecent`

The file header should become:

```typescript
/**
 * Gaming Hub Dashboard API Client - Issue #4582
 * Epic #4575: Gaming Hub Dashboard - Phase 2
 */

import { HttpClient } from './core/httpClient';

const httpClient = new HttpClient();
```

- [ ] **Step 4: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/lib/api/clients/aiUsageClient.ts apps/web/src/lib/api/dashboard-client.ts
git commit -m "refactor(api): I1 — extract AI usage methods from dashboardClient into aiUsageClient (SRP)"
```

---

### Task 5: I2+I4 — Fix Sort Types + Remove Dead `loadMore`

**Files:**
- Modify: `apps/web/src/lib/stores/dashboard-store.ts`

- [ ] **Step 1: Verify `loadMore` is never called**

```bash
cd apps/web && grep -r "loadMore" src/ --include="*.tsx" --include="*.ts" -l
```

Expected: only `src/lib/stores/dashboard-store.ts`

- [ ] **Step 2: Update `DashboardFilters.sort` and remove `loadMore`**

In `dashboard-store.ts`:

```typescript
// Change sort type in DashboardFilters (line ~25):
sort: 'alphabetical' | 'playCount' | 'lastPlayed' | 'rating';

// Remove from DashboardState interface:
// DELETE: loadMore: () => Promise<void>;

// Remove from create() body — delete the entire loadMore block:
// DELETE:
//   loadMore: async () => {
//     const { filters: _filters } = get();
//     set(state => ({ filters: { ...state.filters, page: state.filters.page + 1 } }));
//     await get().fetchGames();
//   },
```

- [ ] **Step 3: Run store tests**

```bash
cd apps/web && pnpm test src/lib/stores/__tests__/dashboard-store.test.ts
```

Expected: all pass

- [ ] **Step 4: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/lib/stores/dashboard-store.ts
git commit -m "refactor(store): I2+I4 — align sort types with API, remove unused loadMore action"
```

---

## Phase 3: MX — Maintenance

### Task 6: M4 — Centralise Color Constants

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/widgets/dashboard-colors.ts`

- [ ] **Step 1: Create `dashboard-colors.ts`**

```typescript
/**
 * Dashboard entity color tokens.
 * All widget files import from here — single source of truth.
 *
 * M4 note: These are component-scoped HSL values. A future migration to
 * CSS custom properties would swap these for var(--color-*) references.
 */
export const C = {
  game: 'hsl(25,95%,45%)',
  player: 'hsl(262,83%,58%)',
  session: 'hsl(240,60%,55%)',
  chat: 'hsl(220,80%,55%)',
  kb: 'hsl(174,60%,40%)',
  event: 'hsl(350,89%,60%)',
  agent: 'hsl(38,92%,50%)',
  success: 'hsl(142,70%,45%)',
} as const;

export type DashboardColor = (typeof C)[keyof typeof C];
```

- [ ] **Step 2: Update `dashboard-client.tsx` to import `C`**

Remove the `const C = { ... } as const;` block from `dashboard-client.tsx` and add at the top:

```tsx
import { C } from './widgets/dashboard-colors';
```

- [ ] **Step 3: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/\(authenticated\)/dashboard/widgets/dashboard-colors.ts apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "refactor(dashboard): M4 — centralise color constants into dashboard-colors.ts"
```

---

### Task 7: M1 — Extract `BentoWidget` Primitive

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/widgets/BentoWidget.tsx`

- [ ] **Step 1: Create `BentoWidget.tsx`**

```tsx
'use client';

import React from 'react';

import { cn } from '@/lib/utils';

const COL_SPAN: Record<number, string> = { 2: 'col-span-2', 3: 'col-span-3', 4: 'col-span-4', 6: 'col-span-6' };
const LG_COL_SPAN: Record<number, string> = { 2: 'lg:col-span-2', 3: 'lg:col-span-3', 4: 'lg:col-span-4', 6: 'lg:col-span-6', 8: 'lg:col-span-8', 12: 'lg:col-span-12' };
const ROW_SPAN: Record<number, string> = { 1: 'row-span-1', 2: 'row-span-2', 3: 'row-span-3', 4: 'row-span-4', 5: 'row-span-5', 6: 'row-span-6' };

export interface BentoWidgetProps {
  colSpan: number;
  /** col-span on the 6-col tablet grid. Defaults to min(colSpan, 6). */
  tabletColSpan?: number;
  rowSpan: number;
  accentColor?: string;
  accentBg?: string;
  className?: string;
  children: React.ReactNode;
  onClick?: () => void;
}

export function BentoWidget({ colSpan, tabletColSpan, rowSpan, accentColor, accentBg, className, children, onClick }: BentoWidgetProps) {
  const tc = tabletColSpan ?? Math.min(colSpan, 6);
  return (
    <div
      className={cn(
        COL_SPAN[tc] ?? `col-span-${tc}`,
        LG_COL_SPAN[colSpan] ?? `lg:col-span-${colSpan}`,
        ROW_SPAN[rowSpan] ?? `row-span-${rowSpan}`,
        'rounded-xl border border-border/60 bg-card overflow-hidden p-3 transition-colors duration-150',
        onClick && 'cursor-pointer hover:bg-muted/30 hover:border-border',
        className
      )}
      style={{
        ...(accentColor ? { borderLeft: `3px solid ${accentColor}` } : {}),
        ...(accentBg ? { background: accentBg } : {}),
      }}
      onClick={onClick}
      {...(onClick ? { role: 'button' as const, tabIndex: 0, onKeyDown: (e: React.KeyboardEvent) => { if (e.key === 'Enter' || e.key === ' ') onClick(); } } : {})}
    >
      {children}
    </div>
  );
}

export function WidgetLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/50 mb-1.5">
      {children}
    </p>
  );
}
```

- [ ] **Step 2: Update `dashboard-client.tsx` — remove inline `BentoWidget`/`WidgetLabel`, add import**

Remove the `COL_SPAN`, `LG_COL_SPAN`, `ROW_SPAN` constants, the `BentoWidgetProps` interface, and the `BentoWidget` and `WidgetLabel` functions from `dashboard-client.tsx`. Add:

```tsx
import { BentoWidget, WidgetLabel } from './widgets/BentoWidget';
```

- [ ] **Step 3: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/\(authenticated\)/dashboard/widgets/BentoWidget.tsx apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "refactor(dashboard): M1 — extract BentoWidget + WidgetLabel into widgets/"
```

---

### Task 8: M1+I5 — Extract and Test `LiveSessionWidget`

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/widgets/LiveSessionWidget.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/LiveSessionWidget.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/app/(authenticated)/dashboard/__tests__/LiveSessionWidget.test.tsx`:

```tsx
import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { LiveSessionWidget } from '../widgets/LiveSessionWidget';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

const SESSION: SessionSummaryDto = {
  id: 'sess-1',
  gameName: 'Catan',
  sessionDate: '2026-04-01T20:00:00Z',
  playerCount: 4,
  winnerName: 'Marco',
};

describe('LiveSessionWidget', () => {
  beforeEach(() => mockPush.mockClear());

  it('renders skeleton when loading', () => {
    render(<LiveSessionWidget session={undefined} isLoading={true} error={null} onRetry={vi.fn()} />);
    expect(screen.queryByText('Sessione Live')).not.toBeInTheDocument();
    expect(screen.queryByText('Nessuna sessione attiva')).not.toBeInTheDocument();
  });

  it('renders empty state when no session', () => {
    render(<LiveSessionWidget session={undefined} isLoading={false} error={null} onRetry={vi.fn()} />);
    expect(screen.getByText('Nessuna sessione attiva')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nuova partita' })).toBeInTheDocument();
  });

  it('shows error message and retry button', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<LiveSessionWidget session={undefined} isLoading={false} error="Errore di rete" onRetry={onRetry} />);
    expect(screen.getByText('Errore nel caricamento')).toBeInTheDocument();
    expect(screen.getByText('Errore di rete')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Riprova' }));
    expect(onRetry).toHaveBeenCalledOnce();
  });

  it('renders session info when active', () => {
    render(<LiveSessionWidget session={SESSION} isLoading={false} error={null} onRetry={vi.fn()} />);
    expect(screen.getByText('Sessione Live')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText(/4 giocatori/)).toBeInTheDocument();
    expect(screen.getByText(/Vincitore: Marco/)).toBeInTheDocument();
  });

  it('navigates to session on click', async () => {
    const user = userEvent.setup();
    render(<LiveSessionWidget session={SESSION} isLoading={false} error={null} onRetry={vi.fn()} />);
    await user.click(screen.getByText('Catan'));
    expect(mockPush).toHaveBeenCalledWith('/sessions/sess-1');
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/LiveSessionWidget.test.tsx
```

Expected: FAIL — `Cannot find module '../widgets/LiveSessionWidget'`

- [ ] **Step 3: Create `widgets/LiveSessionWidget.tsx`**

```tsx
'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

interface LiveSessionWidgetProps {
  session: SessionSummaryDto | undefined;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

export function LiveSessionWidget({ session, isLoading, error, onRetry }: LiveSessionWidgetProps) {
  const router = useRouter();

  if (isLoading) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} accentColor={C.success} className="animate-pulse">
        <div className="h-full" />
      </BentoWidget>
    );
  }

  if (error) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base" style={{ color: 'hsl(0,72%,51%)' }}>
            Errore nel caricamento
          </p>
          <p className="text-sm text-muted-foreground mt-0.5">{error}</p>
        </div>
        <button
          onClick={e => { e.stopPropagation(); onRetry(); }}
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold border border-border hover:bg-muted/30 transition-colors"
        >
          Riprova
        </button>
      </BentoWidget>
    );
  }

  if (!session) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="border-dashed flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base text-foreground">Nessuna sessione attiva</p>
          <p className="text-sm text-muted-foreground mt-0.5">Avvia una nuova partita per vederla qui</p>
        </div>
        <Link
          href="/sessions/new"
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold text-white transition-opacity hover:opacity-90"
          style={{ background: C.game }}
          onClick={e => e.stopPropagation()}
        >
          Nuova partita
        </Link>
      </BentoWidget>
    );
  }

  return (
    <BentoWidget
      colSpan={8}
      rowSpan={2}
      accentColor={C.success}
      accentBg="rgba(16,185,129,0.04)"
      className="flex flex-col justify-between"
      onClick={() => router.push(`/sessions/${session.id}`)}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Sessione Live</WidgetLabel>
        <span
          className="inline-flex items-center gap-1.5 text-[10px] font-bold rounded-full px-2.5 py-0.5"
          style={{ background: 'rgba(16,185,129,0.12)', color: C.success, border: '1px solid rgba(16,185,129,0.2)' }}
        >
          <span className="w-1.5 h-1.5 rounded-full animate-pulse" style={{ background: C.success }} />
          IN CORSO
        </span>
      </div>
      <div className="flex items-center gap-3">
        <div className="w-11 h-11 rounded-xl flex items-center justify-center text-2xl shrink-0 overflow-hidden" style={{ background: 'rgba(255,255,255,0.06)' }}>
          {session.gameImageUrl
            // eslint-disable-next-line @next/next/no-img-element
            ? <img src={session.gameImageUrl} alt={session.gameName} className="w-full h-full object-cover" />
            : '🎲'}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-extrabold text-base leading-tight truncate">{session.gameName}</p>
          <p className="text-[11px] text-muted-foreground mt-0.5">
            {session.playerCount} giocatori{session.winnerName ? ` · Vincitore: ${session.winnerName}` : ''}
          </p>
        </div>
        <span className="shrink-0 px-3.5 py-1.5 rounded-lg text-[11px] font-bold text-white" style={{ background: C.game }} aria-hidden="true">
          Entra →
        </span>
      </div>
    </BentoWidget>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/LiveSessionWidget.test.tsx
```

Expected: 5 passed

- [ ] **Step 5: Remove inline `LiveSessionWidget` from `dashboard-client.tsx` and import from widgets**

Delete the `LiveSessionWidget` function body from `dashboard-client.tsx`. Add:

```tsx
import { LiveSessionWidget } from './widgets/LiveSessionWidget';
```

- [ ] **Step 6: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/\(authenticated\)/dashboard/
git commit -m "refactor(dashboard): M1+I5 — extract LiveSessionWidget with 5 tests"
```

---

### Task 9: M1+I5 — Extract and Test `KpiWidget`

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/widgets/KpiWidget.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/KpiWidget.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/app/(authenticated)/dashboard/__tests__/KpiWidget.test.tsx`:

```tsx
import React from 'react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { KpiWidget } from '../widgets/KpiWidget';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }));

describe('KpiWidget', () => {
  beforeEach(() => mockPush.mockClear());

  it('renders label and value', () => {
    render(<KpiWidget label="Partite (mese)" value={12} accentColor="hsl(25,95%,45%)" colSpan={4} rowSpan={2} />);
    expect(screen.getByText('Partite (mese)')).toBeInTheDocument();
    expect(screen.getByText('12')).toBeInTheDocument();
  });

  it('renders badge with emerald styles when badgePositive', () => {
    render(<KpiWidget label="x" value={1} badge="+15%" badgePositive={true} accentColor="hsl(25,95%,45%)" colSpan={4} rowSpan={2} />);
    expect(screen.getByText('+15%').className).toContain('emerald');
  });

  it('renders badge with neutral styles when not badgePositive', () => {
    render(<KpiWidget label="x" value={1} badge="-5%" badgePositive={false} accentColor="hsl(25,95%,45%)" colSpan={4} rowSpan={2} />);
    expect(screen.getByText('-5%').className).not.toContain('emerald');
  });

  it('navigates to href when clicked', async () => {
    const user = userEvent.setup();
    render(<KpiWidget label="Sessioni" value={42} accentColor="hsl(25,95%,45%)" colSpan={4} rowSpan={2} href="/sessions" />);
    await user.click(screen.getByText('42'));
    expect(mockPush).toHaveBeenCalledWith('/sessions');
  });

  it('renders sub text when no badge', () => {
    render(<KpiWidget label="Ore" value="3h" sub="questa settimana" accentColor="hsl(240,60%,55%)" colSpan={3} rowSpan={2} />);
    expect(screen.getByText('questa settimana')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/KpiWidget.test.tsx
```

- [ ] **Step 3: Create `widgets/KpiWidget.tsx`**

```tsx
'use client';

import { useRouter } from 'next/navigation';

import { cn } from '@/lib/utils';

import { BentoWidget, WidgetLabel } from './BentoWidget';

interface KpiWidgetProps {
  label: string;
  value: string | number;
  badge?: string;
  badgePositive?: boolean;
  sub?: string;
  accentColor: string;
  colSpan: number;
  tabletColSpan?: number;
  rowSpan: number;
  href?: string;
}

export function KpiWidget({ label, value, badge, badgePositive, sub, accentColor, colSpan, tabletColSpan, rowSpan, href }: KpiWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget colSpan={colSpan} tabletColSpan={tabletColSpan} rowSpan={rowSpan} accentColor={accentColor} onClick={href ? () => router.push(href) : undefined}>
      <WidgetLabel>{label}</WidgetLabel>
      <p className="font-quicksand text-[26px] font-extrabold leading-none tracking-tight" style={{ color: accentColor }}>
        {value}
      </p>
      {badge && (
        <span className={cn('inline-block mt-1.5 text-[10px] font-bold px-2 py-0.5 rounded-full', badgePositive ? 'bg-emerald-500/15 text-emerald-400' : 'bg-muted/60 text-muted-foreground')}>
          {badge}
        </span>
      )}
      {sub && !badge && <p className="text-[10px] text-muted-foreground mt-1">{sub}</p>}
    </BentoWidget>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/KpiWidget.test.tsx
```

Expected: 5 passed

- [ ] **Step 5: Remove inline `KpiWidget` from `dashboard-client.tsx`, add import**

```tsx
import { KpiWidget } from './widgets/KpiWidget';
```

- [ ] **Step 6: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/\(authenticated\)/dashboard/
git commit -m "refactor(dashboard): M1+I5 — extract KpiWidget with 5 tests"
```

---

### Task 10: M1+I5 — Extract and Test `LeaderboardWidget`

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/widgets/LeaderboardWidget.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/LeaderboardWidget.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/app/(authenticated)/dashboard/__tests__/LeaderboardWidget.test.tsx`:

```tsx
import React from 'react';
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LeaderboardWidget } from '../widgets/LeaderboardWidget';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

const s = (id: string, winner?: string): SessionSummaryDto => ({
  id, gameName: 'Catan', sessionDate: '2026-04-01T20:00:00Z', playerCount: 3, winnerName: winner,
});

describe('LeaderboardWidget', () => {
  it('renders empty state when no winners', () => {
    render(<LeaderboardWidget sessions={[s('1'), s('2')]} />);
    expect(screen.getByText('Gioca partite con amici per vedere la classifica')).toBeInTheDocument();
  });

  it('renders ranked winners with medals', () => {
    render(<LeaderboardWidget sessions={[s('1', 'Marco'), s('2', 'Marco'), s('3', 'Sara')]} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('Sara')).toBeInTheDocument();
    expect(screen.getByText('🥇')).toBeInTheDocument();
    expect(screen.getByText('🥈')).toBeInTheDocument();
  });

  it('shows win counts', () => {
    render(<LeaderboardWidget sessions={[s('1', 'Marco'), s('2', 'Marco'), s('3', 'Sara')]} />);
    expect(screen.getByText('2 vitt.')).toBeInTheDocument();
    expect(screen.getByText('1 vitt.')).toBeInTheDocument();
  });

  it('limits to 4 players', () => {
    const sessions = ['A', 'B', 'C', 'D', 'E'].map((name, i) => s(String(i), name));
    render(<LeaderboardWidget sessions={sessions} />);
    expect(screen.queryByText('E')).not.toBeInTheDocument();
  });

  it('sorts by wins descending', () => {
    render(<LeaderboardWidget sessions={[s('1', 'Rare'), s('2', 'Top'), s('3', 'Top'), s('4', 'Top')]} />);
    const items = screen.getAllByText(/vitt\./);
    expect(items[0]?.textContent).toBe('3 vitt.');
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/LeaderboardWidget.test.tsx
```

- [ ] **Step 3: Create `widgets/LeaderboardWidget.tsx`**

```tsx
'use client';

import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

const MEDALS = ['🥇', '🥈', '🥉', '4️⃣'] as const;
const AVATAR_COLORS = [C.game, C.player, C.event, C.session] as const;

interface LeaderboardWidgetProps {
  sessions: SessionSummaryDto[];
}

export function LeaderboardWidget({ sessions }: LeaderboardWidgetProps) {
  const winners = sessions
    .filter(s => s.winnerName)
    .reduce<Record<string, number>>((acc, s) => {
      const key = s.winnerName ?? '';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

  const sorted = Object.entries(winners).sort(([, a], [, b]) => b - a).slice(0, 4);

  return (
    <BentoWidget colSpan={6} rowSpan={3} accentColor={C.event} className="flex flex-col">
      {/* I3: derived from the 8 most recent sessions — label documents scope */}
      <WidgetLabel>Classifica Gruppo (ultime partite)</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {sorted.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">
            Gioca partite con amici per vedere la classifica
          </p>
        ) : (
          sorted.map(([name, wins], i) => (
            <div key={name} className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0">
              <span className="text-sm w-5 text-center shrink-0">{MEDALS[i]}</span>
              <div className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0" style={{ background: AVATAR_COLORS[i] }}>
                {name[0]?.toUpperCase()}
              </div>
              <span className="flex-1 font-quicksand font-semibold text-[11px] truncate">{name}</span>
              <span className="font-mono text-[9px] text-muted-foreground shrink-0">{wins} vitt.</span>
            </div>
          ))
        )}
      </div>
    </BentoWidget>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/LeaderboardWidget.test.tsx
```

Expected: 5 passed

- [ ] **Step 5: Remove inline `LeaderboardWidget` from `dashboard-client.tsx`, add import**

```tsx
import { LeaderboardWidget } from './widgets/LeaderboardWidget';
```

- [ ] **Step 6: Type-check and commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/app/\(authenticated\)/dashboard/
git commit -m "refactor(dashboard): M1+I5 — extract LeaderboardWidget with 5 tests (I3 scope documented)"
```

---

### Task 11: M1 — Extract Remaining Widgets + Slim Orchestrator

**Files:**
- Create: `widgets/LibraryWidget.tsx`, `widgets/ChatPreviewWidget.tsx`, `widgets/TrendingWidget.tsx`, `widgets/BentoDashboardSidebar.tsx`, `widgets/index.ts`
- Modify: `dashboard-client.tsx` (replace with slim orchestrator)

- [ ] **Step 1: Create `widgets/LibraryWidget.tsx`**

```tsx
'use client';

import { useRouter } from 'next/navigation';

import type { UserGameDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

interface LibraryWidgetProps {
  games: UserGameDto[];
  totalCount: number;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

export function LibraryWidget({ games, totalCount, isLoading, error, onRetry }: LibraryWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget colSpan={6} rowSpan={4} className="flex flex-col gap-0" onClick={() => router.push('/library')}>
      <WidgetLabel>La Tua Libreria</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="flex items-center gap-2 py-1.5 border-b border-border/40 animate-pulse">
              <div className="w-7 h-7 rounded-md bg-muted/60 shrink-0" />
              <div className="flex-1 h-3 rounded bg-muted/60" />
              <div className="w-8 h-3 rounded bg-muted/40" />
            </div>
          ))
        ) : error ? (
          <div className="flex flex-col items-center justify-center flex-1 gap-2 py-4 text-center">
            <p className="text-[11px] text-muted-foreground">Errore nel caricamento giochi</p>
            <button
              onClick={e => { e.stopPropagation(); onRetry(); }}
              className="text-[10px] font-bold px-3 py-1.5 rounded-lg border border-border hover:bg-muted/30 transition-colors"
            >
              Riprova
            </button>
          </div>
        ) : games.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">Nessun gioco in libreria ancora</p>
        ) : (
          games.slice(0, 6).map(game => (
            <div key={game.id} className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0 group/row" onClick={e => { e.stopPropagation(); router.push(`/library/${game.id}`); }}>
              {(game.thumbnailUrl ?? game.imageUrl) ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img src={game.thumbnailUrl ?? game.imageUrl ?? ''} alt={game.title} className="w-7 h-7 rounded-md object-cover shrink-0" />
              ) : (
                <div className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center text-sm" style={{ background: `${C.game}22` }}>🎲</div>
              )}
              <span className="font-quicksand font-semibold text-[11px] flex-1 truncate text-foreground group-hover/row:text-primary transition-colors">{game.title}</span>
              {game.averageRating !== null && game.averageRating !== undefined && (
                <span className="font-mono text-[9px] font-semibold shrink-0" style={{ color: C.game }}>★ {game.averageRating.toFixed(1)}</span>
              )}
            </div>
          ))
        )}
      </div>
      <p className="text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>Vedi tutti {totalCount} →</p>
    </BentoWidget>
  );
}
```

- [ ] **Step 2: Create `widgets/ChatPreviewWidget.tsx`**

```tsx
'use client';

import { useRouter } from 'next/navigation';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

export function ChatPreviewWidget() {
  const router = useRouter();
  return (
    <BentoWidget colSpan={6} rowSpan={4} accentColor={C.chat} className="flex flex-col" onClick={() => router.push('/chat')}>
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Chat AI</WidgetLabel>
        <span className="text-[9px] font-bold rounded-full px-2 py-0.5" style={{ background: `${C.chat}20`, color: C.chat }}>
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center px-4">
        <div className="w-12 h-12 rounded-2xl flex items-center justify-center text-2xl" style={{ background: `${C.chat}15` }}>💬</div>
        <div>
          <p className="font-quicksand font-bold text-sm text-foreground">Chiedi all&apos;AI</p>
          <p className="text-[11px] text-muted-foreground mt-0.5">Regole, strategie e suggerimenti per i tuoi giochi da tavolo</p>
        </div>
      </div>
      <div className="mt-auto pt-2 flex gap-1.5" onClick={e => { e.stopPropagation(); router.push('/chat'); }}>
        <div className="flex-1 h-7 rounded-lg flex items-center px-2.5 text-[11px] text-muted-foreground/50" style={{ background: 'rgba(255,255,255,0.03)', border: '1px solid rgba(255,255,255,0.07)' }}>
          Fai una domanda…
        </div>
        <button className="w-7 h-7 rounded-lg flex items-center justify-center text-white text-sm" style={{ background: C.chat }} aria-label="Vai alla chat">↑</button>
      </div>
    </BentoWidget>
  );
}
```

- [ ] **Step 3: Create `widgets/TrendingWidget.tsx`**

```tsx
'use client';

import { useRouter } from 'next/navigation';

import type { TrendingGameDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

interface TrendingWidgetProps {
  games: TrendingGameDto[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

export function TrendingWidget({ games, isLoading, error, onRetry }: TrendingWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget colSpan={6} rowSpan={2} accentColor={C.kb} className="flex flex-col" onClick={() => router.push('/games')}>
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      {error ? (
        <div className="flex items-center gap-2 mt-1">
          <p className="text-[10px] text-muted-foreground flex-1">Dati non disponibili</p>
          <button onClick={e => { e.stopPropagation(); onRetry(); }} className="text-[9px] font-bold px-2 py-1 rounded border border-border hover:bg-muted/30 transition-colors">Riprova</button>
        </div>
      ) : (
        <div className="flex gap-3 mt-1 overflow-hidden">
          {isLoading
            ? Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="flex flex-col items-center gap-1 shrink-0">
                  <div className="w-9 h-12 rounded-md bg-muted/60 animate-pulse" />
                  <div className="w-9 h-2 rounded bg-muted/40 animate-pulse" />
                </div>
              ))
            : games.slice(0, 6).map(game => (
                <div key={game.gameId} className="flex flex-col items-center gap-1 cursor-pointer shrink-0 group/card" onClick={e => { e.stopPropagation(); router.push(`/games/${game.gameId}`); }}>
                  {game.thumbnailUrl
                    // eslint-disable-next-line @next/next/no-img-element
                    ? <img src={game.thumbnailUrl} alt={game.title} className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all" />
                    : <div className="w-9 h-12 rounded-md flex items-center justify-center text-lg" style={{ background: 'rgba(255,255,255,0.06)' }}>🎲</div>
                  }
                  <span className="font-quicksand text-[8px] font-bold text-center w-9 truncate">{game.title}</span>
                </div>
              ))}
        </div>
      )}
    </BentoWidget>
  );
}
```

- [ ] **Step 4: Create `widgets/BentoDashboardSidebar.tsx`**

```tsx
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

const SIDEBAR_NAV = [
  { icon: '🏠', label: 'Dashboard', href: '/dashboard' },
  { icon: '📚', label: 'Libreria', href: '/library?tab=collection' },
  { icon: '🎲', label: 'Sessioni', href: '/sessions' },
  { icon: '💬', label: 'Chat AI', href: '/chat' },
  { icon: '📄', label: 'Regole KB', href: '/library?tab=private' },
  { icon: '👥', label: 'Giocatori', href: '/players' },
];

const SIDEBAR_MANAGE = [
  { icon: '📊', label: 'Analytics', href: '/play-records' },
  { icon: '⚙️', label: 'Impostazioni', href: '/settings' },
];

export function BentoDashboardSidebar() {
  const pathname = usePathname();
  const isActive = (href: string) => {
    const path = href.split('?')[0] ?? '';
    return path === '/dashboard' ? pathname === '/dashboard' : pathname.startsWith(path);
  };

  return (
    <aside className="hidden lg:flex w-[200px] min-w-[200px] h-full bg-card border-r border-border/40 flex-col py-3 px-2 overflow-y-auto shrink-0">
      <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/40 px-2 pb-1 pt-1">Navigazione</p>
      {SIDEBAR_NAV.map(item => (
        <Link key={item.href} href={item.href} className={cn('flex items-center gap-2 px-2 py-1.5 rounded-lg font-quicksand text-[12px] font-semibold transition-colors', isActive(item.href) ? 'text-[hsl(25,95%,45%)] bg-[rgba(245,130,31,0.1)]' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40')}>
          <span className="text-sm w-5 text-center shrink-0">{item.icon}</span>
          <span>{item.label}</span>
        </Link>
      ))}
      <div className="h-px bg-border/40 my-2 mx-2" />
      <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/40 px-2 pb-1">Gestione</p>
      {SIDEBAR_MANAGE.map(item => (
        <Link key={item.href} href={item.href} className={cn('flex items-center gap-2 px-2 py-1.5 rounded-lg font-quicksand text-[12px] font-semibold transition-colors', isActive(item.href) ? 'text-[hsl(25,95%,45%)] bg-[rgba(245,130,31,0.1)]' : 'text-muted-foreground hover:text-foreground hover:bg-muted/40')}>
          <span className="text-sm w-5 text-center shrink-0">{item.icon}</span>
          <span>{item.label}</span>
        </Link>
      ))}
    </aside>
  );
}
```

- [ ] **Step 5: Create `widgets/index.ts`**

```typescript
export { BentoWidget, WidgetLabel } from './BentoWidget';
export type { BentoWidgetProps } from './BentoWidget';
export { C } from './dashboard-colors';
export { LiveSessionWidget } from './LiveSessionWidget';
export { KpiWidget } from './KpiWidget';
export { LibraryWidget } from './LibraryWidget';
export { ChatPreviewWidget } from './ChatPreviewWidget';
export { LeaderboardWidget } from './LeaderboardWidget';
export { TrendingWidget } from './TrendingWidget';
export { BentoDashboardSidebar } from './BentoDashboardSidebar';
```

- [ ] **Step 6: Replace `dashboard-client.tsx` with slim orchestrator**

Overwrite the entire file:

```tsx
'use client';

/**
 * Dashboard Bento — Slim Orchestrator
 *
 * Composes the 12-column bento grid from individual widget modules in ./widgets/.
 * 60px row-height, 8px gap, 200px sidebar (lg+).
 */

import { useEffect } from 'react';

import { OnboardingFlow } from '@/components/dashboard/OnboardingFlow';
import { useDashboardStore } from '@/lib/stores/dashboard-store';

import {
  BentoDashboardSidebar,
  C,
  ChatPreviewWidget,
  KpiWidget,
  LeaderboardWidget,
  LibraryWidget,
  LiveSessionWidget,
  TrendingWidget,
} from './widgets';

function parsePlayTimeHours(timeSpan: string): string {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  const hours = parseInt(parts[0] ?? '0', 10);
  const minutes = parseInt(parts[1] ?? '0', 10);
  return hours === 0 ? `${minutes}m` : `${hours}h`;
}

export function DashboardClient() {
  const {
    stats, isLoadingStats, fetchStats,
    recentSessions, isLoadingSessions, sessionsError, fetchRecentSessions,
    updateFilters,
    games, isLoadingGames, gamesError, fetchGames,
    totalGamesCount,
    trendingGames, isLoadingTrending, trendingError, fetchTrendingGames,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(8);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 8, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    const onVisibility = () => { if (!document.hidden) fetchRecentSessions(8); };
    document.addEventListener('visibilitychange', onVisibility);
    return () => document.removeEventListener('visibilitychange', onVisibility);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const latestSession = recentSessions[0];
  const monthlyPlays = stats?.monthlyPlays ?? 0;
  const playsChange = stats?.monthlyPlaysChange;
  const weeklyHours = stats ? parsePlayTimeHours(stats.weeklyPlayTime) : '—';
  const totalGames = stats?.totalGames ?? 0;

  return (
    <div className="flex h-full bg-background overflow-hidden">
      <BentoDashboardSidebar />
      <div className="flex-1 overflow-y-auto p-3.5">
        <div className="grid grid-cols-6 lg:grid-cols-12" style={{ gridAutoRows: '60px', gap: '8px' }}>

          {/* Onboarding — renders once (localStorage-gated), then self-dismisses */}
          <div className="col-span-6 lg:col-span-12">
            <OnboardingFlow />
          </div>

          {/* Row 1-2: Live Session (8×2) + Partite KPI (4×2) */}
          <LiveSessionWidget session={latestSession} isLoading={isLoadingSessions} error={sessionsError} onRetry={() => fetchRecentSessions(8)} />
          <KpiWidget
            label="Partite (mese)"
            value={isLoadingStats ? '…' : monthlyPlays}
            badge={playsChange !== null && playsChange !== undefined && playsChange !== 0 ? `${playsChange > 0 ? '+' : ''}${playsChange}%` : undefined}
            badgePositive={(playsChange ?? 0) > 0}
            accentColor={C.game}
            colSpan={4}
            tabletColSpan={6}
            rowSpan={2}
            href="/sessions"
          />

          {/* Row 3-6: Library (6×4) + Ore KPI (3×2) + Giochi KPI (3×2) */}
          <LibraryWidget games={games} totalCount={totalGamesCount || totalGames} isLoading={isLoadingGames} error={gamesError} onRetry={fetchGames} />
          <KpiWidget label="Ore sett." value={isLoadingStats ? '…' : weeklyHours} sub="questa settimana" accentColor={C.session} colSpan={3} rowSpan={2} />
          <KpiWidget label="Giochi in lib." value={isLoadingStats ? '…' : totalGames} accentColor={C.event} colSpan={3} rowSpan={2} href="/library" />

          {/* Row 5-8: Chat AI (6×4) */}
          <ChatPreviewWidget />

          {/* Row 7-9: Leaderboard (6×3) */}
          <LeaderboardWidget sessions={recentSessions} />

          {/* Row 9-10: Trending (6×2) */}
          <TrendingWidget games={trendingGames} isLoading={isLoadingTrending} error={trendingError} onRetry={() => fetchTrendingGames(6)} />
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: Run all dashboard tests**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/ && pnpm typecheck
```

Expected: 15 tests pass, 0 type errors

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/
git commit -m "refactor(dashboard): M1+M2 — decompose into widgets/, integrate OnboardingFlow, slim orchestrator"
```

---

### Task 12: M3 — Dead Code Audit

**Files:**
- Potentially delete `apps/web/src/components/dashboard/v2/`

- [ ] **Step 1: Check if v2/ has any importers**

```bash
cd apps/web && grep -r "from.*dashboard/v2" src/ --include="*.tsx" --include="*.ts" -l
```

If output is **empty**: proceed to Step 2.
If files appear: **stop** — do not delete v2/. Document the finding and skip Step 2.

- [ ] **Step 2: Delete v2/ if unused**

Only if Step 1 returned no files:

```bash
cd apps/web && rm -rf src/components/dashboard/v2
```

- [ ] **Step 3: Check other potentially orphaned dashboard components**

```bash
cd apps/web && for comp in stat-card activity-feed game-collection-grid filter-bar empty-states; do
  count=$(grep -r "from.*dashboard/$comp" src/ --include="*.tsx" --include="*.ts" -l | wc -l)
  echo "$comp: $count importers"
done
```

Delete any component showing `0 importers` along with its `__tests__/` counterpart.

- [ ] **Step 4: Type-check**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errors (no broken imports)

- [ ] **Step 5: Commit**

```bash
git add -A apps/web/src/components/dashboard/
git commit -m "chore(dashboard): M3 — remove unused components identified by import audit"
```

---

## Wrap-up

### Task 13: Final check and PR

- [ ] **Step 1: Full test run**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/ src/lib/stores/__tests__/dashboard-store.test.ts
```

Expected: all pass

- [ ] **Step 2: Typecheck + lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Expected: 0 errors

- [ ] **Step 3: Open PR**

```bash
git push -u origin feature/dashboard-cx-ix-mx
gh pr create \
  --base main-dev \
  --title "feat(dashboard): CX/IX/MX spec panel improvements" \
  --body "$(cat <<'EOF'
## Summary
- **CX:** Error states + retry on 3 widgets; visibility refresh for live session; honest chat CTA
- **IX:** Extract aiUsageClient (SRP); align sort types with API; remove dead loadMore
- **MX:** Decompose 729-line monolith into 9 widget files; integrate OnboardingFlow; dead code audit

## Issues addressed
C1, C2, C3, I1, I2, I4, I5, M1, M2, M3, M4

## Test plan
- [ ] `pnpm test src/app/(authenticated)/dashboard/` — 15 widget tests pass
- [ ] `pnpm typecheck` — 0 errors
- [ ] Manual: error states visible on API failure, retry works, session refreshes on tab focus

🤖 Generated with Claude Code
EOF
)"
```

---

## Self-Review

| Issue | Task | Status |
|-------|------|--------|
| C1 error states | Task 1 + Tasks 8–11 | ✅ all 3 widgets |
| C2 visibility refresh | Task 2 | ✅ |
| C3 fake chat | Task 3 | ✅ |
| I1 SRP aiUsageClient | Task 4 | ✅ |
| I2 sort types | Task 5 | ✅ |
| I3 leaderboard scope | Task 10 (documented via label) | ✅ documented |
| I4 dead loadMore | Task 5 | ✅ |
| I5 widget tests (15 total) | Tasks 8, 9, 10 | ✅ |
| M1 decompose monolith | Tasks 6–11 | ✅ 9 widget files |
| M2 OnboardingFlow | Task 11 (in orchestrator) | ✅ |
| M3 dead code audit | Task 12 | ✅ |
| M4 colors centralised | Task 6 | ✅ |
| M5 semantic comment | Task 10 (WidgetLabel note) | ✅ |

**Placeholder check:** No TBDs, TODOs, or "implement later" present.

**Type consistency:** `LiveSessionWidget`, `KpiWidget`, `LeaderboardWidget` export names match across definition → test import → orchestrator import. `C` imported from `./widgets/dashboard-colors` in all 7 widget files.
