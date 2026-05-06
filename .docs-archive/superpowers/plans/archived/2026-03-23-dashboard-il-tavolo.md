# Dashboard "Il Tavolo" — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite the `/dashboard` page as the post-login landing, using MeepleCard Grid with CoverOverlay 4-corner and ManaLinkFooter, 2-column layout (Tavolo + Sidebar), warm glassmorphism theme.

**Architecture:** New route at `/dashboard` with server component entry + client orchestrator. The client fetches data from existing Zustand store + API clients and passes props to 8 sub-components. Existing MeepleCard component system is composed — no new UI primitives needed.

**Tech Stack:** Next.js 16 App Router, React 19, Zustand, MeepleCard component system, Tailwind 4, Vitest + Testing Library.

**Spec:** `docs/superpowers/specs/2026-03-23-dashboard-il-tavolo-design.md`

---

## File Map

### New Files

| File | Responsibility |
|------|---------------|
| `apps/web/src/app/(authenticated)/dashboard/page.tsx` | Route entry — server component, renders DashboardClient |
| `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | Client orchestrator — data fetching, layout, passes props |
| `apps/web/src/components/dashboard/v2/HeroZone.tsx` | Contextual hero: active session banner or time-aware greeting |
| `apps/web/src/components/dashboard/v2/QuickStats.tsx` | 4 KPI stat cards with glassmorphism |
| `apps/web/src/components/dashboard/v2/ActiveSessions.tsx` | Session MeepleCards or empty state |
| `apps/web/src/components/dashboard/v2/RecentGames.tsx` | Game MeepleCard grid with CoverOverlay + ManaLinkFooter |
| `apps/web/src/components/dashboard/v2/YourAgents.tsx` | Agent MeepleCard grid + CTA card |
| `apps/web/src/components/dashboard/v2/RecentAgentsSidebar.tsx` | Sidebar: compact agent list |
| `apps/web/src/components/dashboard/v2/RecentChatsSidebar.tsx` | Sidebar: compact chat thread list |
| `apps/web/src/components/dashboard/v2/RecentActivitySidebar.tsx` | Sidebar: minimal activity timeline |
| `apps/web/src/components/dashboard/v2/index.ts` | Barrel export |
| `apps/web/src/components/dashboard/v2/__tests__/HeroZone.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/QuickStats.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/ActiveSessions.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/RecentGames.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/YourAgents.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/RecentAgentsSidebar.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/RecentChatsSidebar.test.tsx` | Tests |
| `apps/web/src/components/dashboard/v2/__tests__/RecentActivitySidebar.test.tsx` | Tests |

### Modified Files

| File | Change |
|------|--------|
| `apps/web/src/components/auth/AuthModal.tsx` | `redirectTo` default: `/library` → `/dashboard` |
| `apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx` | `home` tab click → `router.push('/dashboard')` |
| `apps/web/src/components/layout/UserShell/UserTabBar.tsx` | `home` tab click → `router.push('/dashboard')` |

---

## Task 1: Route + Page Shell

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/page.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`
- Create: `apps/web/src/components/dashboard/v2/index.ts`

- [ ] **Step 1: Create page.tsx (server component)**

```tsx
// apps/web/src/app/(authenticated)/dashboard/page.tsx
import { DashboardClient } from './dashboard-client';

export const metadata = { title: 'Dashboard — MeepleAI' };

export default function DashboardPage() {
  return <DashboardClient />;
}
```

- [ ] **Step 2: Create dashboard-client.tsx (skeleton with layout only)**

```tsx
// apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx
'use client';

export function DashboardClient() {
  return (
    <div className="flex gap-6 w-full max-w-7xl mx-auto px-4 py-6">
      {/* Tavolo — main column */}
      <main className="flex-1 min-w-0 flex flex-col gap-6">
        <div data-testid="hero-zone-placeholder">Hero</div>
        <div data-testid="quick-stats-placeholder">Stats</div>
        <div data-testid="active-sessions-placeholder">Sessions</div>
        <div data-testid="recent-games-placeholder">Games</div>
        <div data-testid="your-agents-placeholder">Agents</div>
      </main>

      {/* Sidebar */}
      <aside className="hidden lg:flex flex-col gap-6 w-[280px] flex-shrink-0">
        <div data-testid="sidebar-agents-placeholder">Sidebar Agents</div>
        <div data-testid="sidebar-chats-placeholder">Sidebar Chats</div>
        <div data-testid="sidebar-activity-placeholder">Sidebar Activity</div>
      </aside>
    </div>
  );
}
```

- [ ] **Step 3: Create barrel export**

```tsx
// apps/web/src/components/dashboard/v2/index.ts
export { HeroZone } from './HeroZone';
export { QuickStats } from './QuickStats';
export { ActiveSessions } from './ActiveSessions';
export { RecentGames } from './RecentGames';
export { YourAgents } from './YourAgents';
export { RecentAgentsSidebar } from './RecentAgentsSidebar';
export { RecentChatsSidebar } from './RecentChatsSidebar';
export { RecentActivitySidebar } from './RecentActivitySidebar';
```

Note: This will show import errors until each component is created. That's fine — they get resolved task by task.

- [ ] **Step 4: Verify the route loads**

Run: `cd apps/web && pnpm dev` — navigate to `http://localhost:3000/dashboard`.
Expected: See the placeholder text in 2-column layout.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/ apps/web/src/components/dashboard/v2/index.ts
git commit -m "feat(dashboard): scaffold /dashboard route with 2-column layout shell"
```

---

## Task 2: HeroZone Component

**Files:**
- Create: `apps/web/src/components/dashboard/v2/HeroZone.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/HeroZone.test.tsx`

- [ ] **Step 1: Write failing tests**

```tsx
// apps/web/src/components/dashboard/v2/__tests__/HeroZone.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HeroZone } from '../HeroZone';

describe('HeroZone', () => {
  it('renders time-aware greeting when no active session', () => {
    render(<HeroZone userName="Marco" />);
    // Should contain the user name
    expect(screen.getByText(/Marco/)).toBeInTheDocument();
  });

  it('renders active session banner when session is provided', () => {
    render(
      <HeroZone
        userName="Marco"
        activeSession={{ gameName: 'Catan', duration: '45 min', sessionId: 's1' }}
      />
    );
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
    expect(screen.getByText(/Riprendi/)).toBeInTheDocument();
  });

  it('shows current date', () => {
    render(<HeroZone userName="Marco" />);
    // Date element should exist
    expect(screen.getByTestId('hero-date')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/v2/__tests__/HeroZone.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement HeroZone**

```tsx
// apps/web/src/components/dashboard/v2/HeroZone.tsx
'use client';

import { cn } from '@/lib/utils';

interface ActiveSessionInfo {
  gameName: string;
  duration: string;
  sessionId: string;
}

interface HeroZoneProps {
  userName: string;
  activeSession?: ActiveSessionInfo;
  className?: string;
}

function getTimeGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Buongiorno';
  if (hour < 18) return 'Buon pomeriggio';
  return 'Buonasera';
}

function formatDate(): string {
  return new Date().toLocaleDateString('it-IT', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });
}

export function HeroZone({ userName, activeSession, className }: HeroZoneProps) {
  if (activeSession) {
    return (
      <div
        className={cn(
          'rounded-2xl p-6 backdrop-blur-lg',
          'bg-gradient-to-r from-emerald-500/10 to-emerald-500/5',
          'border border-emerald-500/20',
          className
        )}
        data-testid="hero-zone"
      >
        <h1 className="font-quicksand font-bold text-2xl text-foreground">
          Hai una partita in corso: {activeSession.gameName}
        </h1>
        <p className="text-muted-foreground text-sm mt-1">
          Durata: {activeSession.duration}
        </p>
        <button
          className="mt-3 px-4 py-2 rounded-lg bg-primary text-primary-foreground text-sm font-semibold hover:opacity-90 transition-opacity"
          onClick={() => {
            window.location.href = `/sessions/${activeSession.sessionId}`;
          }}
        >
          Riprendi
        </button>
      </div>
    );
  }

  return (
    <div
      className={cn(
        'rounded-2xl px-7 py-6 backdrop-blur-lg',
        'bg-gradient-to-br from-[rgba(210,105,30,0.06)] to-[rgba(210,105,30,0.02)]',
        'border border-[rgba(210,105,30,0.10)]',
        className
      )}
      data-testid="hero-zone"
    >
      <h1 className="font-quicksand font-bold text-2xl text-foreground">
        {getTimeGreeting()}, {userName} 👋
      </h1>
      <p className="text-muted-foreground text-sm mt-1 capitalize" data-testid="hero-date">
        {formatDate()}
      </p>
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/v2/__tests__/HeroZone.test.tsx`
Expected: 3 PASS

- [ ] **Step 5: Wire into DashboardClient**

In `dashboard-client.tsx`, replace `<div data-testid="hero-zone-placeholder">Hero</div>` with:
```tsx
import { HeroZone } from '@/components/dashboard/v2/HeroZone';
// ...
<HeroZone userName="Marco" />
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/dashboard/v2/HeroZone.tsx apps/web/src/components/dashboard/v2/__tests__/HeroZone.test.tsx apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "feat(dashboard): add HeroZone with time-aware greeting and session banner"
```

---

## Task 3: QuickStats Component

**Files:**
- Create: `apps/web/src/components/dashboard/v2/QuickStats.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/QuickStats.test.tsx`

- [ ] **Step 1: Write failing tests**

```tsx
// apps/web/src/components/dashboard/v2/__tests__/QuickStats.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QuickStats } from '../QuickStats';

describe('QuickStats', () => {
  const stats = {
    totalGames: 12,
    monthlyPlays: 8,
    weeklyPlaytime: '6h',
    favorites: 3,
  };

  it('renders 4 stat cards with values', () => {
    render(<QuickStats stats={stats} />);
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
    expect(screen.getByText('6h')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders loading skeletons when loading', () => {
    render(<QuickStats stats={null} loading />);
    expect(screen.getAllByTestId('stat-skeleton')).toHaveLength(4);
  });

  it('renders dash values on error', () => {
    render(<QuickStats stats={null} error />);
    expect(screen.getAllByText('—')).toHaveLength(4);
  });
});
```

- [ ] **Step 2: Run tests — expect FAIL**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/v2/__tests__/QuickStats.test.tsx`

- [ ] **Step 3: Implement QuickStats**

```tsx
// apps/web/src/components/dashboard/v2/QuickStats.tsx
'use client';

import { cn } from '@/lib/utils';

interface QuickStatsData {
  totalGames: number;
  monthlyPlays: number;
  weeklyPlaytime: string;
  favorites: number;
}

interface QuickStatsProps {
  stats: QuickStatsData | null;
  loading?: boolean;
  error?: boolean;
  className?: string;
}

const STAT_CONFIG = [
  { key: 'totalGames', label: 'Giochi' },
  { key: 'monthlyPlays', label: 'Partite / Mese' },
  { key: 'weeklyPlaytime', label: 'Tempo / Sett.' },
  { key: 'favorites', label: 'Preferiti' },
] as const;

function StatSkeleton() {
  return (
    <div
      className="rounded-xl p-4 text-center bg-[rgba(200,180,160,0.20)] animate-pulse h-[80px]"
      data-testid="stat-skeleton"
    />
  );
}

export function QuickStats({ stats, loading, error, className }: QuickStatsProps) {
  return (
    <div className={cn('grid grid-cols-2 lg:grid-cols-4 gap-3', className)} data-testid="quick-stats">
      {STAT_CONFIG.map(({ key, label }) => {
        if (loading) return <StatSkeleton key={key} />;

        const value = error || !stats ? '—' : String(stats[key]);

        return (
          <div
            key={key}
            className={cn(
              'rounded-xl p-4 text-center',
              'bg-[rgba(255,255,255,0.75)] backdrop-blur-[12px]',
              'border border-[rgba(200,180,160,0.20)]',
              'shadow-[0_2px_12px_rgba(180,120,60,0.06)]',
              'transition-all duration-200',
              'hover:-translate-y-0.5 hover:shadow-[var(--shadow-warm-md)]'
            )}
          >
            <div className="font-quicksand font-bold text-[28px] text-primary leading-none">
              {value}
            </div>
            <div className="text-[11px] font-semibold uppercase tracking-wide text-muted-foreground mt-1.5">
              {label}
            </div>
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run tests — expect PASS**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/v2/__tests__/QuickStats.test.tsx`

- [ ] **Step 5: Wire into DashboardClient**

Replace the stats placeholder. Import `useDashboardStore` and call `fetchStats()` in a `useEffect`. Pass `stats`, `isLoadingStats`, `statsError` as props.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/dashboard/v2/QuickStats.tsx apps/web/src/components/dashboard/v2/__tests__/QuickStats.test.tsx apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
git commit -m "feat(dashboard): add QuickStats glassmorphism KPI cards"
```

---

## Task 4: ActiveSessions Component

**Files:**
- Create: `apps/web/src/components/dashboard/v2/ActiveSessions.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/ActiveSessions.test.tsx`

- [ ] **Step 1: Write failing tests**

Test the empty state (primary case in Alpha) and section header rendering.

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement ActiveSessions**

Renders section header "🎮 Sessioni Attive" + dashed empty state box with CTA. When sessions array is non-empty, render MeepleCard `entity="session"` grid (future use).

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Wire into DashboardClient + Commit**

```bash
git commit -m "feat(dashboard): add ActiveSessions with empty state for Alpha"
```

---

## Task 5: RecentGames Component

**Files:**
- Create: `apps/web/src/components/dashboard/v2/RecentGames.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/RecentGames.test.tsx`

- [ ] **Step 1: Write failing tests**

Test: renders MeepleCard for each game, shows section header with "Vedi tutti" link, renders loading skeleton, renders empty state when no games.

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement RecentGames**

Key implementation details:
- Import `MeepleCard` from `@/components/ui/data-display/meeple-card`
- Grid: `grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5`
- For each game, map to MeepleCard props:
  - `entity="game"`, `variant="grid"`
  - `title={game.title}`, `subtitle={game.designer + ' · ' + game.playerCount}`
  - `imageUrl={game.imageUrl}`
  - `coverLabels={[{ text: game.title, primary: true }]}`
  - `subtypeIcons` from `game.mechanics` array
  - `stateLabel` based on game state (nuovo/giocato/preferito)
  - `linkedEntities` from game relationship data: `{ entityType, count }`
  - `metadata` with players, playtime, rating using Lucide icons
  - `loading={isLoading}` for skeleton state
  - `entityId={game.id}`, `showInfoButton`

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Wire into DashboardClient**

Use `useDashboardStore().fetchGames()` with `pageSize: 6, sort: 'lastPlayed'`. Map `UserGameDto` to MeepleCard props.

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(dashboard): add RecentGames with MeepleCard Grid + CoverOverlay + ManaLinkFooter"
```

---

## Task 6: YourAgents Component

**Files:**
- Create: `apps/web/src/components/dashboard/v2/YourAgents.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/YourAgents.test.tsx`

- [ ] **Step 1: Write failing tests**

Test: renders agent MeepleCards, shows CTA card at the end, loading skeleton, empty state.

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement YourAgents**

- MeepleCard `entity="agent"`, `variant="grid"` for each agent
- Pass `agentStatus`, `agentStats: { invocationCount, lastExecutedAt }` props
- Last slot: dashed CTA card "Crea agente" → navigate to agent wizard
- Section header: "🤖 I Tuoi Agenti" + "Gestisci →" link

- [ ] **Step 4: Run tests — expect PASS**

- [ ] **Step 5: Wire into DashboardClient + Commit**

Use `dashboardClient.getRecentAgents()`. Store result in local state (shared with sidebar).

```bash
git commit -m "feat(dashboard): add YourAgents with MeepleCard Grid + CTA card"
```

---

## Task 7: Sidebar Components (3 in 1)

**Files:**
- Create: `apps/web/src/components/dashboard/v2/RecentAgentsSidebar.tsx`
- Create: `apps/web/src/components/dashboard/v2/RecentChatsSidebar.tsx`
- Create: `apps/web/src/components/dashboard/v2/RecentActivitySidebar.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/RecentAgentsSidebar.test.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/RecentChatsSidebar.test.tsx`
- Create: `apps/web/src/components/dashboard/v2/__tests__/RecentActivitySidebar.test.tsx`

- [ ] **Step 1: Write failing tests for all 3 sidebar components**

Each test: renders items, shows section title in correct color, hides on error, shows skeleton on loading.

- [ ] **Step 2: Run tests — expect FAIL**

- [ ] **Step 3: Implement RecentAgentsSidebar**

Compact list: ManaSymbol mini avatar, name, timestamp, status dot. Section title in agent color `hsl(38 92% 50%)`. Glass item bg with hover.

- [ ] **Step 4: Implement RecentChatsSidebar**

Compact rows: thread title, meta (msg count + agent + time). Section title in chat color `hsl(220 80% 55%)`.

- [ ] **Step 5: Implement RecentActivitySidebar**

Minimal timeline: mana icon per event type, entity name highlighted in entity color, relative timestamp. Section title in muted-fg.

Use `GET /api/v1/dashboard/activity-timeline` (skip/take), NOT `/api/v1/activity/timeline`.

- [ ] **Step 6: Run all 3 test files — expect PASS**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/v2/__tests__/Recent`

- [ ] **Step 7: Wire all 3 into DashboardClient sidebar + Commit**

```bash
git commit -m "feat(dashboard): add sidebar sections — agents, chats, activity timeline"
```

---

## Task 8: DashboardClient Data Integration

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

- [ ] **Step 1: Wire all data fetching**

Replace all placeholders with real components. Add `useEffect` to fetch all data on mount:

```tsx
const { stats, isLoadingStats, statsError, fetchStats,
        recentSessions, isLoadingSessions, fetchRecentSessions,
        games, isLoadingGames, gamesError, fetchGames } = useDashboardStore();

const [agents, setAgents] = useState([]);
const [threads, setThreads] = useState([]);
const [activities, setActivities] = useState([]);
const [agentsLoading, setAgentsLoading] = useState(true);
const [chatsLoading, setChatsLoading] = useState(true);
const [activityLoading, setActivityLoading] = useState(true);

useEffect(() => {
  fetchStats();
  fetchRecentSessions(5);
  fetchGames(); // uses store filters — override pageSize/sort
  // Independent fetches for sidebar
  dashboardClient.getRecentAgents().then(setAgents).finally(() => setAgentsLoading(false));
  chatClient.getMyThreads({ limit: 4 }).then(setThreads).finally(() => setChatsLoading(false));
  dashboardClient.getActivityTimeline({ skip: 0, take: 5 }).then(setActivities).finally(() => setActivityLoading(false));
}, []);
```

- [ ] **Step 2: Map DTO fields to component props**

Create mapper functions for `UserGameDto → MeepleCard props` and `AgentDto → MeepleCard props`. Keep mappers in the client file — they're specific to this page.

- [ ] **Step 3: Verify in browser**

Run: `cd apps/web && pnpm dev` → navigate to `/dashboard`. All sections should render with real data (or empty states).

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(dashboard): integrate all data fetching in DashboardClient"
```

---

## Task 9: Navigation Wiring

**Files:**
- Modify: `apps/web/src/components/auth/AuthModal.tsx`
- Modify: `apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx`
- Modify: `apps/web/src/components/layout/UserShell/UserTabBar.tsx`

- [ ] **Step 1: Update AuthModal default redirect**

In `apps/web/src/components/auth/AuthModal.tsx` line 58, change:
```tsx
redirectTo = '/library',
```
to:
```tsx
redirectTo = '/dashboard',
```

Also update the JSDoc on line 43:
```tsx
/** URL to redirect to after successful authentication. Defaults to '/dashboard' */
```

- [ ] **Step 2: Update UserDesktopSidebar — home tab navigates to /dashboard**

In `apps/web/src/components/layout/UserShell/UserDesktopSidebar.tsx`, add router import and modify click handler:

```tsx
import { useRouter } from 'next/navigation';
// ...
export function UserDesktopSidebar() {
  const router = useRouter();
  const { activeTab, setActiveTab, setSectionTitle } = useNavigation();

  const handleTabClick = (tab: SidebarTabConfig) => {
    setActiveTab(tab.id);
    setSectionTitle(tab.sectionTitle);
    if (tab.id === 'home') {
      router.push('/dashboard');
    }
  };
  // ... rest unchanged
```

- [ ] **Step 3: Update UserTabBar — same pattern**

In `apps/web/src/components/layout/UserShell/UserTabBar.tsx`, add router import and modify:

```tsx
import { useRouter } from 'next/navigation';
// ...
export function UserTabBar() {
  const router = useRouter();
  const { activeTab, setActiveTab, setSectionTitle } = useNavigation();

  const handleTabChange = (tab: TabConfig) => {
    setActiveTab(tab.id);
    setSectionTitle(tab.sectionTitle);
    if (tab.id === 'home') {
      router.push('/dashboard');
    }
  };
  // ... rest unchanged
```

- [ ] **Step 4: Verify navigation flow**

1. Login → should land on `/dashboard`
2. Click Home tab (sidebar) → should navigate to `/dashboard`
3. Click Home tab (mobile bar) → should navigate to `/dashboard`
4. Admin login → should still land on `/admin`

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(dashboard): wire /dashboard as post-login landing + navbar home tab"
```

---

## Task 10: Responsive Layout + Polish

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

- [ ] **Step 1: Verify responsive breakpoints**

Check in browser at:
- Desktop (>1024px): 2-column, grids 3 col
- Tablet (640-1024px): 1-column, sidebar below tavolo, grids 2 col
- Mobile (<640px): 1-column, grids 1 col

The sidebar should use: `hidden lg:flex` → at tablet it stacks below via `flex-col lg:flex-row`.

- [ ] **Step 2: Fix any responsive issues**

Update DashboardClient layout classes:
```tsx
<div className="flex flex-col lg:flex-row gap-6 w-full max-w-7xl mx-auto px-4 py-6">
```

- [ ] **Step 3: Run all v2 dashboard tests**

Run: `cd apps/web && pnpm vitest run src/components/dashboard/v2/`
Expected: All PASS

- [ ] **Step 4: Run typecheck + lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`
Expected: No errors

- [ ] **Step 5: Final commit**

```bash
git commit -m "feat(dashboard): responsive layout polish and test verification"
```

---

## Task 11: Cleanup Old Dashboard

**Files:**
- Remove: `apps/web/src/app/(authenticated)/gaming-hub-client.tsx` (if no longer referenced)
- Check: Old dashboard components in `apps/web/src/components/dashboard/` for unused files

- [ ] **Step 1: Check references to gaming-hub-client.tsx**

Run: `grep -r "gaming-hub-client" apps/web/src/` to find all imports.

- [ ] **Step 2: Remove or redirect**

If `gaming-hub-client.tsx` is still imported from other routes (not just `/dashboard`), keep it. If it's only used as the old dashboard, remove it.

- [ ] **Step 3: Verify nothing breaks**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds with no errors

- [ ] **Step 4: Commit cleanup**

```bash
git commit -m "refactor(dashboard): remove old gaming-hub-client after v2 migration"
```

---

## Summary

| Task | Component | Depends On |
|------|-----------|------------|
| 1 | Route + Shell | — |
| 2 | HeroZone | 1 |
| 3 | QuickStats | 1 |
| 4 | ActiveSessions | 1 |
| 5 | RecentGames | 1 |
| 6 | YourAgents | 1 |
| 7 | Sidebar (3 components) | 1 |
| 8 | Data Integration | 2-7 |
| 9 | Navigation Wiring | 1 |
| 10 | Responsive + Polish | 8, 9 |
| 11 | Cleanup | 10 |

Tasks 2-7 are **parallelizable** (all depend only on Task 1). Task 8 integrates them. Task 9 is independent of 2-7.
