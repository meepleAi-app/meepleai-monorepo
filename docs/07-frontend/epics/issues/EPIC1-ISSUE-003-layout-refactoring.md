# Issue #TBD-003: Dashboard Hub Layout Refactoring

**Epic**: EPIC-DH-001 (Dashboard Hub Core)
**Type**: Frontend - Refactoring
**Priority**: P0 - Critical
**Story Points**: 3
**Assignee**: Frontend Team
**Sprint**: N+1

---

## 📋 Description

Refactoring completo del layout dashboard da "recent games grid focus" a "multi-section hub" con integrazione API aggregata e componenti modulari.

**Context**:
Il dashboard attuale (Issue #1836) mostra principalmente recent games grid con sezioni basilari. Il nuovo hub deve fornire snapshot multi-dominio (Library, Sessions, Chat) con collegamenti espliciti a pagine dedicate.

---

## 🎯 Acceptance Criteria

### Functional Requirements
- [ ] Layout integra `useQuery` hook per `/api/v1/dashboard`
- [ ] Sezioni visibili (in ordine):
  1. Hero Section (Greeting + Stats 4-col)
  2. Active Sessions Widget (max 2 sessioni)
  3. Two-column layout:
     - Left: Library Snapshot (quota + top 3)
     - Right: Activity Feed Timeline (ultimi 5)
  4. Chat History (ultimi 5 thread)
  5. Quick Actions Grid (5 azioni)
- [ ] Loading states: Skeleton UI per ogni sezione
- [ ] Error boundaries: Fallimento parziale non blocca intero dashboard
- [ ] Empty states: Messaggi appropriati con CTA quando dati assenti

### Responsive Requirements
- [ ] Mobile (< 640px): Single column, sezioni stacked
- [ ] Tablet (640-1024px): Two column layout
- [ ] Desktop (> 1024px): Asymmetric 3-column layout (sidebar + main + aside)
- [ ] Smooth transitions tra breakpoints

### Performance Requirements
- [ ] Time to Interactive < 2s
- [ ] Largest Contentful Paint < 2.5s
- [ ] Cumulative Layout Shift < 0.1
- [ ] Bundle size increase < 30KB (code splitting)

---

## 🔧 Technical Implementation

### File Structure
```
apps/web/src/app/(public)/dashboard/
├── page.tsx                      # Route entry (refactored)
├── dashboard-hub.tsx             # Main hub component (NEW)
├── components/
│   ├── HeroSection.tsx          # Greeting + 4 stats cards (NEW)
│   ├── StatsCard.tsx            # Individual stat card (NEW)
│   ├── ActiveSessionsWidget.tsx # Existing (Issue #2617)
│   ├── LibrarySnapshot.tsx      # NEW (Issue #5)
│   ├── ActivityFeed.tsx         # NEW (Issue #4)
│   ├── ChatHistoryWidget.tsx    # Existing (refactor)
│   └── QuickActionsGrid.tsx     # Enhanced (Issue #6)
└── hooks/
    └── useDashboardData.ts      # TanStack Query hook (NEW)
```

### Main Component Implementation

```tsx
// dashboard-hub.tsx
'use client';

import { useDashboardData } from './hooks/useDashboardData';
import { HeroSection } from './components/HeroSection';
import { ActiveSessionsWidget } from './components/ActiveSessionsWidget';
import { LibrarySnapshot } from './components/LibrarySnapshot';
import { ActivityFeed } from './components/ActivityFeed';
import { ChatHistoryWidget } from './components/ChatHistoryWidget';
import { QuickActionsGrid } from './components/QuickActionsGrid';
import { DashboardSkeleton } from './components/DashboardSkeleton';
import { DashboardError } from './components/DashboardError';

export function DashboardHub() {
  const { data, isLoading, error } = useDashboardData();

  if (isLoading) return <DashboardSkeleton />;
  if (error) return <DashboardError error={error} />;
  if (!data) return <DashboardError error={new Error('No data available')} />;

  return (
    <div className="min-h-screen bg-gradient-to-br from-stone-50 via-amber-50/30 to-emerald-50/20 pb-24 md:pb-0">
      {/* Hero Section: Greeting + 4 Stats Cards */}
      <HeroSection user={data.user} stats={data.stats} />

      <div className="container mx-auto px-4 py-8 space-y-8">
        {/* Active Sessions */}
        <ActiveSessionsWidget sessions={data.activeSessions} />

        {/* Two-column layout: Library + Activity */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <LibrarySnapshot snapshot={data.librarySnapshot} />
          <ActivityFeed activities={data.recentActivity} />
        </div>

        {/* Chat History */}
        <ChatHistoryWidget threads={data.chatHistory} />

        {/* Quick Actions */}
        <QuickActionsGrid />
      </div>
    </div>
  );
}
```

### TanStack Query Hook

```tsx
// hooks/useDashboardData.ts
import { useQuery } from '@tanstack/react-query';

interface DashboardData {
  user: UserProfile;
  stats: UserStats;
  activeSessions: GameSession[];
  librarySnapshot: LibrarySnapshot;
  recentActivity: Activity[];
  chatHistory: ChatThread[];
}

export function useDashboardData() {
  return useQuery<DashboardData>({
    queryKey: ['dashboard'],
    queryFn: async () => {
      const response = await fetch('/api/v1/dashboard', {
        headers: {
          'Authorization': `Bearer ${getToken()}`,
        },
      });

      if (!response.ok) {
        throw new Error(`Dashboard API error: ${response.statusText}`);
      }

      return response.json();
    },
    staleTime: 5 * 60 * 1000, // 5 minutes (match backend cache)
    retry: 2,
    refetchOnWindowFocus: true,
  });
}
```

### Error Boundary Implementation

```tsx
// components/DashboardError.tsx
export function DashboardError({ error }: { error: Error }) {
  return (
    <Alert variant="destructive">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>Errore Dashboard</AlertTitle>
      <AlertDescription>
        Impossibile caricare i dati del dashboard.
        <Button onClick={() => window.location.reload()} className="mt-4">
          Ricarica Pagina
        </Button>
      </AlertDescription>
    </Alert>
  );
}
```

---

## 🧪 Testing Requirements

### Unit Tests (Vitest)
```tsx
describe('DashboardHub', () => {
  it('renders all sections when data available', () => {
    const { getByText } = render(<DashboardHub />, {
      wrapper: createQueryWrapper(mockDashboardData),
    });

    expect(getByText('Ciao, Marco!')).toBeInTheDocument();
    expect(getByText('127')).toBeInTheDocument(); // Library count
    expect(getByText('Catan')).toBeInTheDocument(); // Active session
  });

  it('shows skeleton when loading', () => {
    const { getByTestId } = render(<DashboardHub />, {
      wrapper: createQueryWrapper(null, { isLoading: true }),
    });

    expect(getByTestId('dashboard-skeleton')).toBeInTheDocument();
  });

  it('handles API error gracefully', () => {
    const { getByText } = render(<DashboardHub />, {
      wrapper: createQueryWrapper(null, { error: new Error('API failed') }),
    });

    expect(getByText(/Errore Dashboard/i)).toBeInTheDocument();
  });
});
```

### Integration Tests
- [ ] Dashboard loads with real API call (mocked backend)
- [ ] All sections populate with correct data
- [ ] Links navigate to correct pages
- [ ] Cache invalidation works (mutate after user action)

### Visual Regression (Chromatic)
- [ ] Dashboard fully loaded (desktop 1440px)
- [ ] Dashboard fully loaded (mobile 375px)
- [ ] Dashboard loading state (skeleton)
- [ ] Dashboard error state
- [ ] Dashboard empty states (no games, no sessions)

---

## 📦 Deliverables

- [ ] `dashboard-hub.tsx` - Main component
- [ ] `useDashboardData.ts` - Query hook
- [ ] `DashboardSkeleton.tsx` - Loading state
- [ ] `DashboardError.tsx` - Error boundary
- [ ] Updated `page.tsx` - Route integration
- [ ] Storybook stories for all new components
- [ ] Unit tests (> 85% coverage)
- [ ] Integration tests
- [ ] Visual regression tests

---

## 🔗 Dependencies

**Requires**:
- Issue #1 completed (`GET /api/v1/dashboard` available)
- Existing components: ActiveSessionsWidget, ChatHistoryWidget
- TanStack Query configured (already in project)

**Blocks**:
- Issue #7 (Responsive optimization - builds on this layout)
- Issue #8 (Integration tests - needs layout ready)

---

## 🚨 Edge Cases

- **First-time user** (no data): Show empty states con CTA "Inizia con MeepleAI"
- **API partial failure**: Error boundary per sezione (se Library fails, Sessions funziona)
- **Slow network**: Extended loading states (timeout 10s before error)
- **Token expired mid-session**: Redirect to login con return URL
- **Mobile landscape**: Adjust grid (2-col instead of 1-col)

---

## ✅ Definition of Done

- [ ] Code merged to `main-dev` after PR approval
- [ ] All tests passing (unit + integration + visual)
- [ ] Lighthouse Performance > 90
- [ ] Peer review completed (2 approvals)
- [ ] Storybook documentation published
- [ ] QA validation completed
- [ ] Feature flag enabled on staging
- [ ] Demo to stakeholders completed

---

**Created**: 2026-01-21
**Estimated Hours**: 16-20h
**Related Issues**: #1 (API), #4, #5, #6 (Components), #7 (Responsive)
