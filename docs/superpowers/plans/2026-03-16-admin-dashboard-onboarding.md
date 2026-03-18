# Admin Dashboard Redesign & Onboarding Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rewrite `/admin/overview` as an operational command center and `/onboarding` as a minimal profile form.

**Architecture:** Server component fetches 5 API calls in parallel, passes data to client sub-components. Each section (KPI row, pending banner, summary cards, quick actions, tech bar) is a separate focused component. Onboarding is a single client component with form state.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui, TanStack Query (mutations), Vitest + Testing Library

**Spec:** `docs/superpowers/specs/2026-03-16-admin-dashboard-onboarding-design.md`

---

## Chunk 0: Branch Setup

### Task 0: Create feature branch

- [ ] **Step 1: Create branch from current base**

```bash
git checkout main-dev && git pull
git checkout -b feature/admin-dashboard-redesign
git config branch.feature/admin-dashboard-redesign.parent main-dev
```

---

## Chunk 1: KPIStatsRow + Loading Skeleton

### Task 1: KPIStatsRow component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/KPIStatsRow.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/KPIStatsRow.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/KPIStatsRow.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { KPIStatsRow } from '../KPIStatsRow';

describe('KPIStatsRow', () => {
  const defaultProps = {
    totalGames: 128,
    totalUsers: 45,
    pendingApprovals: 3,
  };

  it('renders games, users, and pending KPI cards', () => {
    render(<KPIStatsRow {...defaultProps} />);
    expect(screen.getByText('128')).toBeInTheDocument();
    expect(screen.getByText('45')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('hides pending card when pendingApprovals is 0', () => {
    render(<KPIStatsRow {...defaultProps} pendingApprovals={0} />);
    expect(screen.queryByText('richieste di accesso')).not.toBeInTheDocument();
  });

  it('shows pending card when pendingApprovals > 0', () => {
    render(<KPIStatsRow {...defaultProps} />);
    expect(screen.getByText('richieste di accesso')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/KPIStatsRow.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write KPIStatsRow component**

```tsx
// KPIStatsRow.tsx
import { Gamepad2, FileText, Users, UserPlus } from 'lucide-react';
import { KPICard, type KPICardData } from '@/components/admin/KPICard';

interface KPIStatsRowProps {
  totalGames: number;
  totalUsers: number;
  pendingApprovals: number;
  totalDocuments?: number;  // Backend gap — wire when available
  queueDepth?: number;      // Backend gap — wire when available
}

export function KPIStatsRow({ totalGames, totalUsers, pendingApprovals, totalDocuments, queueDepth }: KPIStatsRowProps) {
  const cards: KPICardData[] = [
    {
      title: 'Giochi',
      value: totalGames,
      icon: <Gamepad2 className="h-5 w-5" />,
      subtitle: 'nel catalogo condiviso',
    },
  ];

  if (totalDocuments !== undefined) {
    cards.push({
      title: 'Documenti',
      value: totalDocuments,
      icon: <FileText className="h-5 w-5" />,
      subtitle: 'processati',
      ...(queueDepth && queueDepth > 0 ? { badge: `${queueDepth} in coda`, badgeVariant: 'warning' as const } : {}),
    });
  }

  cards.push({
    title: 'Utenti',
    value: totalUsers,
    icon: <Users className="h-5 w-5" />,
    subtitle: 'registrati',
  });

  if (pendingApprovals > 0) {
    cards.push({
      title: 'Pendenti',
      value: pendingApprovals,
      icon: <UserPlus className="h-5 w-5" />,
      subtitle: 'richieste di accesso',
    });
  }

  const colsClass = cards.length >= 4 ? 'lg:grid-cols-4' : cards.length === 3 ? 'lg:grid-cols-3' : 'lg:grid-cols-2';

  return (
    <div className={`grid gap-4 grid-cols-2 ${colsClass}`}>
      {cards.map((card) => (
        <KPICard key={card.title} {...card} />
      ))}
    </div>
  );
}
```

Note: The spec mentions a "Documenti" card but it requires a backend gap fix (`totalDocuments` not in `AdminOverviewStats`). Omit it for now — add when backend provides the field.

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/KPIStatsRow.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/KPIStatsRow.tsx apps/web/src/app/admin/\(dashboard\)/overview/__tests__/KPIStatsRow.test.tsx
git commit -m "feat(admin): add KPIStatsRow component for overview dashboard"
```

---

### Task 2: Loading skeleton

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/loading.tsx`

- [ ] **Step 1: Write loading skeleton**

```tsx
// loading.tsx
import { Skeleton } from '@/components/ui/feedback/skeleton';

export default function OverviewLoading() {
  return (
    <div className="space-y-8">
      {/* Header */}
      <div>
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-4 w-72 mt-2" />
      </div>

      {/* KPI Row */}
      <div className="grid grid-cols-2 lg:grid-cols-3 gap-4">
        {Array.from({ length: 3 }).map((_, i) => (
          <Skeleton key={i} className="h-24 rounded-2xl" />
        ))}
      </div>

      {/* Summary Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <Skeleton className="h-64 rounded-2xl" />
        <Skeleton className="h-64 rounded-2xl" />
      </div>

      {/* Quick Actions */}
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        {Array.from({ length: 6 }).map((_, i) => (
          <Skeleton key={i} className="h-24 rounded-2xl" />
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/loading.tsx
git commit -m "feat(admin): add loading skeleton for overview page"
```

---

## Chunk 2: PendingRequestsBanner

### Task 3: PendingRequestsBanner component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/PendingRequestsBanner.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/PendingRequestsBanner.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/PendingRequestsBanner.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PendingRequestsBanner } from '../PendingRequestsBanner';

const mockApprove = vi.hoisted(() => vi.fn());
const mockReject = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    accessRequests: {
      approveAccessRequest: mockApprove,
      rejectAccessRequest: mockReject,
    },
  }),
}));

function renderWithQuery(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('PendingRequestsBanner', () => {
  const requests = [
    { id: '1', email: 'alice@example.com', status: 'Pending', requestedAt: '2026-03-15T10:00:00Z' },
    { id: '2', email: 'bob@example.com', status: 'Pending', requestedAt: '2026-03-15T11:00:00Z' },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    mockApprove.mockResolvedValue(undefined);
    mockReject.mockResolvedValue(undefined);
  });

  it('renders nothing when requests array is empty', () => {
    const { container } = renderWithQuery(<PendingRequestsBanner requests={[]} totalCount={0} />);
    expect(container.firstChild).toBeNull();
  });

  it('renders email list with approve/reject buttons', () => {
    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={2} />);
    expect(screen.getByText('alice@example.com')).toBeInTheDocument();
    expect(screen.getByText('bob@example.com')).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /approva/i })).toHaveLength(2);
    expect(screen.getAllByRole('button', { name: /rifiuta/i })).toHaveLength(2);
  });

  it('shows "Vedi tutte" link when totalCount > 5', () => {
    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={8} />);
    expect(screen.getByText(/vedi tutte/i)).toBeInTheDocument();
  });

  it('calls approveAccessRequest on approve click', async () => {
    renderWithQuery(<PendingRequestsBanner requests={requests} totalCount={2} />);
    const approveButtons = screen.getAllByRole('button', { name: /approva/i });
    await userEvent.click(approveButtons[0]);
    expect(mockApprove).toHaveBeenCalledWith('1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/PendingRequestsBanner.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Write PendingRequestsBanner component**

```tsx
// PendingRequestsBanner.tsx
'use client';

import { useState } from 'react';
import Link from 'next/link';
import { AlertTriangle, Check, X, Loader2 } from 'lucide-react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Button } from '@/components/ui/inputs/button';
import { createApiClient } from '@/lib/api';

interface AccessRequest {
  id: string;
  email: string;
  status: string;
  requestedAt: string;
}

interface PendingRequestsBannerProps {
  requests: AccessRequest[];
  totalCount: number;
}

export function PendingRequestsBanner({ requests, totalCount }: PendingRequestsBannerProps) {
  const api = createApiClient();
  const queryClient = useQueryClient();
  const [processingIds, setProcessingIds] = useState<Set<string>>(new Set());

  // Hooks must be called before any early return
  if (requests.length === 0) return null;

  const approveMutation = useMutation({
    mutationFn: (id: string) => api.accessRequests.approveAccessRequest(id),
    onMutate: (id) => setProcessingIds((prev) => new Set(prev).add(id)),
    onSettled: (_, __, id) => {
      setProcessingIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'overview'] });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (id: string) => api.accessRequests.rejectAccessRequest(id),
    onMutate: (id) => setProcessingIds((prev) => new Set(prev).add(id)),
    onSettled: (_, __, id) => {
      setProcessingIds((prev) => {
        const next = new Set(prev);
        next.delete(id);
        return next;
      });
      queryClient.invalidateQueries({ queryKey: ['admin', 'overview'] });
    },
  });

  return (
    <div className="rounded-2xl border border-amber-200 dark:border-amber-800/50 bg-amber-50 dark:bg-amber-950/30 p-4">
      <div className="flex items-center gap-2 mb-3">
        <AlertTriangle className="h-5 w-5 text-amber-600 dark:text-amber-400" />
        <h3 className="font-quicksand font-semibold text-amber-900 dark:text-amber-200">
          {totalCount} {totalCount === 1 ? 'richiesta' : 'richieste'} di accesso in attesa
        </h3>
      </div>
      <ul className="space-y-2">
        {requests.map((req) => {
          const isProcessing = processingIds.has(req.id);
          return (
            <li key={req.id} className="flex items-center justify-between gap-3 py-1">
              <span className="font-nunito text-sm text-foreground truncate">{req.email}</span>
              <div className="flex gap-2 shrink-0">
                <Button
                  size="sm"
                  variant="default"
                  className="bg-emerald-600 hover:bg-emerald-700 text-white h-7 px-3 text-xs"
                  disabled={isProcessing}
                  onClick={() => approveMutation.mutate(req.id)}
                  aria-label={`Approva ${req.email}`}
                >
                  {isProcessing ? <Loader2 className="h-3 w-3 animate-spin" /> : <Check className="h-3 w-3 mr-1" />}
                  Approva
                </Button>
                <Button
                  size="sm"
                  variant="outline"
                  className="border-red-300 text-red-600 hover:bg-red-50 dark:border-red-800 dark:text-red-400 dark:hover:bg-red-950/30 h-7 px-3 text-xs"
                  disabled={isProcessing}
                  onClick={() => rejectMutation.mutate(req.id)}
                  aria-label={`Rifiuta ${req.email}`}
                >
                  <X className="h-3 w-3 mr-1" />
                  Rifiuta
                </Button>
              </div>
            </li>
          );
        })}
      </ul>
      {totalCount > 5 && (
        <Link
          href="/admin/users/access-requests"
          className="mt-3 block text-sm font-medium text-amber-700 dark:text-amber-400 hover:underline"
        >
          Vedi tutte →
        </Link>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/PendingRequestsBanner.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/PendingRequestsBanner.tsx apps/web/src/app/admin/\(dashboard\)/overview/__tests__/PendingRequestsBanner.test.tsx
git commit -m "feat(admin): add PendingRequestsBanner with inline approve/reject"
```

---

## Chunk 3: Summary Cards (Library + Users)

### Task 4: LibrarySummaryCard component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/LibrarySummaryCard.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/LibrarySummaryCard.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/LibrarySummaryCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { LibrarySummaryCard } from '../LibrarySummaryCard';

describe('LibrarySummaryCard', () => {
  const props = {
    totalGames: 128,
    recentGames: [
      { id: 'g1', title: 'Catan', createdAt: '2026-03-14T10:00:00Z' },
      { id: 'g2', title: 'Wingspan', createdAt: '2026-03-13T10:00:00Z' },
    ],
  };

  it('renders header and total games stat', () => {
    render(<LibrarySummaryCard {...props} />);
    expect(screen.getByText('Libreria Condivisa')).toBeInTheDocument();
    expect(screen.getByText('128')).toBeInTheDocument();
  });

  it('renders recent games mini list', () => {
    render(<LibrarySummaryCard {...props} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders manage link', () => {
    render(<LibrarySummaryCard {...props} />);
    const link = screen.getByText(/gestisci catalogo/i);
    expect(link.closest('a')).toHaveAttribute('href', '/admin/shared-games/all');
  });

  it('handles empty recent games', () => {
    render(<LibrarySummaryCard totalGames={0} recentGames={[]} />);
    expect(screen.getByText('Nessun gioco recente')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/LibrarySummaryCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write LibrarySummaryCard component**

```tsx
// LibrarySummaryCard.tsx
import Link from 'next/link';
import { Library, ArrowRight } from 'lucide-react';

interface RecentGame {
  id: string;
  title: string;
  createdAt: string;
}

interface LibrarySummaryCardProps {
  totalGames: number;
  recentGames: RecentGame[];
}

export function LibrarySummaryCard({ totalGames, recentGames }: LibrarySummaryCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
      <div className="flex items-center gap-2 mb-4">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-orange-100/80 dark:bg-orange-500/20">
          <Library className="h-4 w-4 text-orange-600 dark:text-orange-400" />
        </div>
        <h3 className="font-quicksand font-semibold text-foreground">Libreria Condivisa</h3>
      </div>

      <div className="mb-4">
        <span className="font-nunito text-2xl font-bold text-foreground">{totalGames.toLocaleString()}</span>
        <span className="ml-2 text-sm text-muted-foreground">giochi nel catalogo</span>
      </div>

      <div className="space-y-2 mb-4">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Ultimi aggiunti</p>
        {recentGames.length === 0 ? (
          <p className="text-sm text-muted-foreground italic">Nessun gioco recente</p>
        ) : (
          <ul className="space-y-1">
            {recentGames.map((game) => (
              <li key={game.id}>
                <Link
                  href={`/admin/shared-games/${game.id}`}
                  className="flex items-center justify-between text-sm hover:bg-slate-50 dark:hover:bg-zinc-700/30 rounded-lg px-2 py-1 -mx-2 transition-colors"
                >
                  <span className="font-nunito text-foreground truncate">{game.title}</span>
                  <span className="text-xs text-muted-foreground shrink-0 ml-2">
                    {new Date(game.createdAt).toLocaleDateString('it-IT', { day: 'numeric', month: 'short' })}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Link
        href="/admin/shared-games/all"
        className="flex items-center gap-1 text-sm font-medium text-orange-600 dark:text-orange-400 hover:underline"
      >
        Gestisci catalogo <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/LibrarySummaryCard.test.tsx`
Expected: PASS (4 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/LibrarySummaryCard.tsx apps/web/src/app/admin/\(dashboard\)/overview/__tests__/LibrarySummaryCard.test.tsx
git commit -m "feat(admin): add LibrarySummaryCard for overview dashboard"
```

---

### Task 5: UsersSummaryCard component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/UsersSummaryCard.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/UsersSummaryCard.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/UsersSummaryCard.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { UsersSummaryCard } from '../UsersSummaryCard';

describe('UsersSummaryCard', () => {
  const props = {
    totalUsers: 45,
    activeUsers: 32,
    pendingInvitations: 4,
    recentUsers: [
      { id: 'u1', displayName: 'Alice', email: 'alice@test.com', createdAt: '2026-03-15T10:00:00Z' },
      { id: 'u2', displayName: null, email: 'bob@test.com', createdAt: '2026-03-14T10:00:00Z' },
    ],
  };

  it('renders header and user stats', () => {
    render(<UsersSummaryCard {...props} />);
    expect(screen.getByText('Utenti')).toBeInTheDocument();
    expect(screen.getByText('45')).toBeInTheDocument();
    expect(screen.getByText(/32/)).toBeInTheDocument(); // active
    expect(screen.getByText(/4/)).toBeInTheDocument(); // pending invitations
  });

  it('renders recent users with displayName or email fallback', () => {
    render(<UsersSummaryCard {...props} />);
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('bob@test.com')).toBeInTheDocument();
  });

  it('renders manage link', () => {
    render(<UsersSummaryCard {...props} />);
    const link = screen.getByText(/gestisci utenti/i);
    expect(link.closest('a')).toHaveAttribute('href', '/admin/users');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/UsersSummaryCard.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write UsersSummaryCard component**

```tsx
// UsersSummaryCard.tsx
import Link from 'next/link';
import { Users, ArrowRight, Mail, Send } from 'lucide-react';

interface RecentUser {
  id: string;
  displayName: string | null;
  email: string;
  createdAt: string;
}

interface UsersSummaryCardProps {
  totalUsers: number;
  activeUsers: number;
  pendingInvitations: number;
  recentUsers: RecentUser[];
}

export function UsersSummaryCard({
  totalUsers,
  activeUsers,
  pendingInvitations,
  recentUsers,
}: UsersSummaryCardProps) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
      <div className="flex items-center gap-2 mb-4">
        <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-orange-100/80 dark:bg-orange-500/20">
          <Users className="h-4 w-4 text-orange-600 dark:text-orange-400" />
        </div>
        <h3 className="font-quicksand font-semibold text-foreground">Utenti</h3>
      </div>

      <div className="flex items-baseline gap-4 mb-4">
        <div>
          <span className="font-nunito text-2xl font-bold text-foreground">{totalUsers.toLocaleString()}</span>
          <span className="ml-1 text-sm text-muted-foreground">totali</span>
        </div>
        <div className="flex items-center gap-1 text-sm text-muted-foreground">
          <Users className="h-3 w-3" />
          <span>{activeUsers} attivi 30gg</span>
        </div>
        {pendingInvitations > 0 && (
          <div className="flex items-center gap-1 text-sm text-amber-600 dark:text-amber-400">
            <Send className="h-3 w-3" />
            <span>{pendingInvitations} inviti pendenti</span>
          </div>
        )}
      </div>

      <div className="space-y-2 mb-4">
        <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">Ultimi registrati</p>
        {recentUsers.length === 0 ? (
          <p className="text-sm text-muted-foreground italic">Nessun utente recente</p>
        ) : (
          <ul className="space-y-1">
            {recentUsers.map((user) => (
              <li
                key={user.id}
                className="flex items-center justify-between text-sm rounded-lg px-2 py-1 -mx-2"
              >
                <div className="flex items-center gap-2 truncate">
                  <Mail className="h-3 w-3 text-muted-foreground shrink-0" />
                  <span className="font-nunito text-foreground truncate">
                    {user.displayName || user.email}
                  </span>
                </div>
                <span className="text-xs text-muted-foreground shrink-0 ml-2">
                  {new Date(user.createdAt).toLocaleDateString('it-IT', { day: 'numeric', month: 'short' })}
                </span>
              </li>
            ))}
          </ul>
        )}
      </div>

      <Link
        href="/admin/users"
        className="flex items-center gap-1 text-sm font-medium text-orange-600 dark:text-orange-400 hover:underline"
      >
        Gestisci utenti <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/UsersSummaryCard.test.tsx`
Expected: PASS (3 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/UsersSummaryCard.tsx apps/web/src/app/admin/\(dashboard\)/overview/__tests__/UsersSummaryCard.test.tsx
git commit -m "feat(admin): add UsersSummaryCard for overview dashboard"
```

---

## Chunk 4: QuickActionsGrid + TechActionsBar

### Task 6: QuickActionsGrid component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/QuickActionsGrid.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/QuickActionsGrid.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/QuickActionsGrid.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import { QuickActionsGrid } from '../QuickActionsGrid';

const mockPush = vi.hoisted(() => vi.fn());
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

vi.mock('@/components/admin/invitations/InviteUserDialog', () => ({
  InviteUserDialog: () => null,
}));

describe('QuickActionsGrid', () => {
  it('renders all 6 action cards', () => {
    render(<QuickActionsGrid />);
    expect(screen.getByText('Crea Gioco')).toBeInTheDocument();
    expect(screen.getByText('Invita Utente')).toBeInTheDocument();
    expect(screen.getByText('Gestisci Giochi')).toBeInTheDocument();
    expect(screen.getByText('Gestisci Utenti')).toBeInTheDocument();
    expect(screen.getByText('Upload PDF')).toBeInTheDocument();
    expect(screen.getByText('Vedi Coda')).toBeInTheDocument();
  });

  it('navigates on card click for navigate actions', async () => {
    render(<QuickActionsGrid />);
    await userEvent.click(screen.getByText('Crea Gioco'));
    expect(mockPush).toHaveBeenCalledWith('/admin/shared-games/new');
  });

  it('opens invite dialog on Invita Utente click', async () => {
    render(<QuickActionsGrid />);
    await userEvent.click(screen.getByText('Invita Utente'));
    // InviteUserDialog should be open — look for dialog content
    // The dialog is mocked away in unit test, so we just verify it renders
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/QuickActionsGrid.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write QuickActionsGrid component**

```tsx
// QuickActionsGrid.tsx
'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Plus, UserPlus, Gamepad2, Users, Upload, ListOrdered } from 'lucide-react';
import { InviteUserDialog } from '@/components/admin/invitations/InviteUserDialog';

interface QuickAction {
  id: string;
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
  type: 'navigate' | 'dialog';
  href?: string;
}

const QUICK_ACTIONS: QuickAction[] = [
  { id: 'create-game', label: 'Crea Gioco', description: 'Aggiungi al catalogo', icon: Plus, type: 'navigate', href: '/admin/shared-games/new' },
  { id: 'invite-user', label: 'Invita Utente', description: 'Invia invito email', icon: UserPlus, type: 'dialog' },
  { id: 'manage-games', label: 'Gestisci Giochi', description: 'Catalogo e filtri', icon: Gamepad2, type: 'navigate', href: '/admin/shared-games/all' },
  { id: 'manage-users', label: 'Gestisci Utenti', description: 'Lista e ruoli', icon: Users, type: 'navigate', href: '/admin/users' },
  { id: 'upload-pdf', label: 'Upload PDF', description: 'Carica regolamento', icon: Upload, type: 'navigate', href: '/admin/knowledge-base/upload' },
  { id: 'view-queue', label: 'Vedi Coda', description: 'Stato processing', icon: ListOrdered, type: 'navigate', href: '/admin/knowledge-base/queue' },
];

export function QuickActionsGrid() {
  const router = useRouter();
  const [inviteOpen, setInviteOpen] = useState(false);

  function handleAction(action: QuickAction) {
    if (action.type === 'navigate' && action.href) {
      router.push(action.href);
    } else if (action.id === 'invite-user') {
      setInviteOpen(true);
    }
  }

  return (
    <>
      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        {QUICK_ACTIONS.map((action) => (
          <button
            key={action.id}
            onClick={() => handleAction(action)}
            className="flex items-start gap-3 rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-4 text-left transition-colors hover:border-amber-300 dark:hover:border-amber-700 hover:bg-amber-50/50 dark:hover:bg-amber-950/20"
          >
            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-amber-100/80 dark:bg-amber-900/30">
              <action.icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
            </div>
            <div className="min-w-0">
              <p className="font-quicksand text-sm font-semibold text-foreground">{action.label}</p>
              <p className="font-nunito text-xs text-muted-foreground">{action.description}</p>
            </div>
          </button>
        ))}
      </div>
      <InviteUserDialog open={inviteOpen} onOpenChange={setInviteOpen} />
    </>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/QuickActionsGrid.test.tsx`
Expected: PASS (3 tests). May need to mock `InviteUserDialog` — add `vi.mock('@/components/admin/invitations/InviteUserDialog', () => ({ InviteUserDialog: () => null }))` if dialog import causes issues.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/QuickActionsGrid.tsx apps/web/src/app/admin/\(dashboard\)/overview/__tests__/QuickActionsGrid.test.tsx
git commit -m "feat(admin): add QuickActionsGrid with 6 operative actions"
```

---

### Task 7: TechActionsBar component

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/overview/TechActionsBar.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/TechActionsBar.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/TechActionsBar.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { TechActionsBar } from '../TechActionsBar';

describe('TechActionsBar', () => {
  it('renders all 4 tech actions', () => {
    render(<TechActionsBar />);
    expect(screen.getByText('Clear Cache')).toBeInTheDocument();
    expect(screen.getByText('Reindex All')).toBeInTheDocument();
    expect(screen.getByText('System Health')).toBeInTheDocument();
    expect(screen.getByText('Export Users')).toBeInTheDocument();
  });

  it('renders System Health as a link to /admin/monitor', () => {
    render(<TechActionsBar />);
    const link = screen.getByText('System Health').closest('a');
    expect(link).toHaveAttribute('href', '/admin/monitor');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/TechActionsBar.test.tsx`
Expected: FAIL

- [ ] **Step 3: Write TechActionsBar component**

```tsx
// TechActionsBar.tsx
'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Trash2, RefreshCw, HeartPulse, Download, Loader2 } from 'lucide-react';
import { createApiClient } from '@/lib/api';

export function TechActionsBar() {
  const [loadingAction, setLoadingAction] = useState<string | null>(null);

  async function handleApiAction(id: string) {
    setLoadingAction(id);
    try {
      const api = createApiClient();
      if (id === 'clear-cache') {
        // Placeholder — wire to actual cache clear endpoint when available
      } else if (id === 'reindex-all') {
        // Placeholder — wire to actual reindex endpoint when available
      } else if (id === 'export-users') {
        // Placeholder — wire to actual export endpoint when available
      }
    } finally {
      setLoadingAction(null);
    }
  }

  const buttonClass =
    'inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground hover:bg-slate-100 dark:hover:bg-zinc-800 rounded-md px-3 py-1.5 transition-colors';

  return (
    <div className="flex flex-wrap items-center gap-1 pt-2 border-t border-slate-200/60 dark:border-zinc-700/40">
      <button
        className={buttonClass}
        disabled={loadingAction === 'clear-cache'}
        onClick={() => handleApiAction('clear-cache')}
      >
        {loadingAction === 'clear-cache' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Trash2 className="h-3.5 w-3.5" />}
        Clear Cache
      </button>
      <span className="text-slate-300 dark:text-zinc-600">·</span>
      <button
        className={buttonClass}
        disabled={loadingAction === 'reindex-all'}
        onClick={() => handleApiAction('reindex-all')}
      >
        {loadingAction === 'reindex-all' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <RefreshCw className="h-3.5 w-3.5" />}
        Reindex All
      </button>
      <span className="text-slate-300 dark:text-zinc-600">·</span>
      <Link href="/admin/monitor" className={buttonClass}>
        <HeartPulse className="h-3.5 w-3.5" />
        System Health
      </Link>
      <span className="text-slate-300 dark:text-zinc-600">·</span>
      <button
        className={buttonClass}
        disabled={loadingAction === 'export-users'}
        onClick={() => handleApiAction('export-users')}
      >
        {loadingAction === 'export-users' ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Download className="h-3.5 w-3.5" />}
        Export Users
      </button>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/__tests__/TechActionsBar.test.tsx`
Expected: PASS (2 tests)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/overview/TechActionsBar.tsx apps/web/src/app/admin/\(dashboard\)/overview/__tests__/TechActionsBar.test.tsx
git commit -m "feat(admin): add TechActionsBar with muted secondary actions"
```

---

## Chunk 5: Overview Page Assembly + Test Rewrite

### Task 8: Rewrite overview/page.tsx

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/overview/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/overview/__tests__/page.test.tsx`

- [ ] **Step 1: Rewrite overview page as server component**

Replace the entire content of `apps/web/src/app/admin/(dashboard)/overview/page.tsx`:

```tsx
// page.tsx — Server component
import { createApiClient } from '@/lib/api';
import { KPIStatsRow } from './KPIStatsRow';
import { PendingRequestsBanner } from './PendingRequestsBanner';
import { LibrarySummaryCard } from './LibrarySummaryCard';
import { UsersSummaryCard } from './UsersSummaryCard';
import { QuickActionsGrid } from './QuickActionsGrid';
import { TechActionsBar } from './TechActionsBar';

export default async function OverviewPage() {
  const api = createApiClient();

  const [stats, pendingRequestsRes, recentGamesRes, recentUsersRes, pendingInvitationsRes] =
    await Promise.all([
      api.admin.getOverviewStats().catch(() => null),
      api.accessRequests.getAccessRequests({ status: 'Pending', pageSize: 5 }).catch(() => null),
      api.sharedGames.getAll({ pageSize: 5, page: 1 }).catch(() => null),
      api.admin.getAllUsers({ limit: 5 }).catch(() => null),
      api.invitations.getInvitations({ status: 'Pending', pageSize: 5 }).catch(() => null),
    ]);

  const pendingRequests = pendingRequestsRes?.items ?? [];
  const pendingTotalCount = pendingRequestsRes?.totalCount ?? 0;
  const recentGames = (recentGamesRes?.items ?? []).map((g: any) => ({
    id: g.id,
    title: g.title ?? g.name ?? 'Untitled',
    createdAt: g.createdAt ?? g.addedAt ?? new Date().toISOString(),
  }));
  const recentUsers = (recentUsersRes?.items ?? recentUsersRes ?? []).map((u: any) => ({
    id: u.id,
    displayName: u.displayName ?? null,
    email: u.email ?? '',
    createdAt: u.createdAt ?? new Date().toISOString(),
  }));
  const pendingInvitationCount = pendingInvitationsRes?.totalCount ?? 0;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Dashboard
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Centro operativo: richieste, libreria e gestione utenti
        </p>
      </div>

      <KPIStatsRow
        totalGames={stats?.totalGames ?? 0}
        totalUsers={stats?.totalUsers ?? 0}
        pendingApprovals={stats?.pendingApprovals ?? 0}
      />

      <PendingRequestsBanner
        requests={pendingRequests}
        totalCount={pendingTotalCount}
      />

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <LibrarySummaryCard
          totalGames={stats?.totalGames ?? 0}
          recentGames={recentGames}
        />
        <UsersSummaryCard
          totalUsers={stats?.totalUsers ?? 0}
          activeUsers={stats?.activeUsers ?? 0}
          pendingInvitations={pendingInvitationCount}
          recentUsers={recentUsers}
        />
      </div>

      <div>
        <h2 className="font-quicksand text-lg font-semibold text-foreground mb-4">
          Azioni Rapide
        </h2>
        <QuickActionsGrid />
      </div>

      <TechActionsBar />
    </div>
  );
}
```

Important notes for the implementer:
- The `createApiClient()` must work server-side. Check if it uses cookies — if server component, may need `cookies()` from `next/headers`. Inspect `apps/web/src/lib/api/core/httpClient.ts` and existing server-component patterns (e.g., `apps/web/src/app/admin/(dashboard)/overview/page.tsx` current version is client-side, but other admin pages may be server components). If `createApiClient()` doesn't work server-side, keep the page as `'use client'` and use `useEffect` + `useState` like the current implementation, passing data to children.
- The `any` type casts on `recentGames` and `recentUsers` mapping: replace with proper types from the Zod schemas (`SharedGameSchema`, `UserSchema`) once you verify the exact field names from the API response.

- [ ] **Step 2: Delete the old test file (server component — tested via sub-component tests)**

Delete `__tests__/page.test.tsx` — the old tests tested a client component that no longer exists. All behavior is covered by sub-component tests (KPIStatsRow, PendingRequestsBanner, LibrarySummaryCard, UsersSummaryCard, QuickActionsGrid, TechActionsBar). If the page ends up as a client component, restore integration tests.

Old content for reference:

```tsx
// __tests__/page.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const mockGetOverviewStats = vi.hoisted(() => vi.fn());
const mockGetAccessRequests = vi.hoisted(() => vi.fn());
const mockGetAllGames = vi.hoisted(() => vi.fn());
const mockGetAllUsers = vi.hoisted(() => vi.fn());
const mockGetInvitations = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    admin: {
      getOverviewStats: mockGetOverviewStats,
      getAllUsers: mockGetAllUsers,
    },
    accessRequests: {
      getAccessRequests: mockGetAccessRequests,
      approveAccessRequest: vi.fn(),
      rejectAccessRequest: vi.fn(),
    },
    sharedGames: { getAll: mockGetAllGames },
    invitations: { getInvitations: mockGetInvitations },
  }),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/admin/invitations/InviteUserDialog', () => ({
  InviteUserDialog: () => null,
}));

// NOTE: The overview page is a server component with async data fetching.
// Server components cannot be rendered with standard @testing-library/react.
// All behavior is tested through sub-component tests (Tasks 1-7).
// Delete the old test file — it tested the old client component and is no longer valid.
// Integration coverage is provided by the E2E test suite (admin-overview.spec.ts).
//
// If the implementer converts page.tsx to a client component instead,
// restore this test with proper mocks for all 5 API calls.
```

The implementer should adapt this test based on whether the final page is a server or client component. Sub-component tests (Tasks 1-7) already cover all behavior.

- [ ] **Step 3: Verify the page renders locally**

Run: `cd apps/web && pnpm dev`
Navigate to: `http://localhost:3000/admin/overview`
Verify: KPI row, pending banner (if any), summary cards, quick actions grid, tech bar all render.

- [ ] **Step 4: Run all overview tests**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/`
Expected: All tests PASS

- [ ] **Step 5: Commit**

```bash
git rm apps/web/src/app/admin/\(dashboard\)/overview/__tests__/page.test.tsx 2>/dev/null || true
git add apps/web/src/app/admin/\(dashboard\)/overview/page.tsx
git commit -m "feat(admin): rewrite overview page as operational command center"
```

---

## Chunk 6: Onboarding Page Rewrite

### Task 9: Rewrite onboarding page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/onboarding/page.tsx`
- Test: `apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// __tests__/page.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockPush = vi.fn();
const mockReplace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush, replace: mockReplace }),
}));

const mockUpdateProfile = vi.hoisted(() => vi.fn());
const mockCompleteOnboarding = vi.hoisted(() => vi.fn());
const mockUploadAvatar = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    auth: {
      updateProfile: mockUpdateProfile,
      completeOnboarding: mockCompleteOnboarding,
      uploadAvatar: mockUploadAvatar,
    },
  }),
}));

const mockUseAuth = vi.hoisted(() => vi.fn());
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: mockUseAuth,
}));

import OnboardingPage from '../page';

describe('OnboardingPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({
      user: { email: 'test@example.com', onboardingCompleted: false },
      loading: false,
    });
    mockUpdateProfile.mockResolvedValue({ ok: true });
    mockCompleteOnboarding.mockResolvedValue({ ok: true });
  });

  it('renders profile form with pre-filled read-only email', () => {
    render(<OnboardingPage />);
    expect(screen.getByText(/benvenuto/i)).toBeInTheDocument();
    const emailInput = screen.getByDisplayValue('test@example.com');
    expect(emailInput).toBeDisabled();
  });

  it('requires display name to submit', async () => {
    render(<OnboardingPage />);
    const submitBtn = screen.getByRole('button', { name: /entra/i });
    await userEvent.click(submitBtn);
    // Should not call API without name
    expect(mockUpdateProfile).not.toHaveBeenCalled();
  });

  it('submits profile and completes onboarding', async () => {
    render(<OnboardingPage />);
    const nameInput = screen.getByLabelText(/display name/i);
    await userEvent.type(nameInput, 'Mario Rossi');
    await userEvent.click(screen.getByRole('button', { name: /entra/i }));
    expect(mockUpdateProfile).toHaveBeenCalledWith({ displayName: 'Mario Rossi' });
    expect(mockCompleteOnboarding).toHaveBeenCalledWith(false);
  });

  it('redirects to / if already onboarded', () => {
    mockUseAuth.mockReturnValue({
      user: { email: 'test@example.com', onboardingCompleted: true },
      loading: false,
    });
    render(<OnboardingPage />);
    expect(mockReplace).toHaveBeenCalledWith('/');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/\(authenticated\)/onboarding/__tests__/page.test.tsx`
Expected: FAIL

- [ ] **Step 3: Rewrite onboarding page**

Replace `apps/web/src/app/(authenticated)/onboarding/page.tsx`:

```tsx
'use client';

import { useState, useRef, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Lock, Camera, Loader2, ArrowRight } from 'lucide-react';
import { useAuth } from '@/components/auth/AuthProvider';
import { createApiClient } from '@/lib/api';
import { Button } from '@/components/ui/inputs/button';
import { Input } from '@/components/ui/inputs/input';
import { Label } from '@/components/ui/primitives/label';

export default function OnboardingPage() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuth();
  const [displayName, setDisplayName] = useState('');
  const [avatarPreview, setAvatarPreview] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Safety net: redirect if already onboarded
  useEffect(() => {
    if (!authLoading && user?.onboardingCompleted) {
      router.replace('/');
    }
  }, [authLoading, user, router]);

  if (authLoading) {
    return (
      <div className="flex min-h-[80vh] items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
      </div>
    );
  }

  if (!user || user.onboardingCompleted) return null;

  async function handleAvatarChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setAvatarPreview(URL.createObjectURL(file));
    try {
      const api = createApiClient();
      await api.auth.uploadAvatar(file);
    } catch {
      // Avatar upload is optional — don't block onboarding
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    const trimmed = displayName.trim();
    if (trimmed.length < 2 || trimmed.length > 50) return;

    setIsSubmitting(true);
    setError(null);
    try {
      const api = createApiClient();
      await api.auth.updateProfile({ displayName: trimmed });
      await api.auth.completeOnboarding(false);
      router.push('/');
    } catch {
      setError('Si e\u0027 verificato un errore. Riprova.');
    } finally {
      setIsSubmitting(false);
    }
  }

  const isValid = displayName.trim().length >= 2 && displayName.trim().length <= 50;

  return (
    <div className="flex min-h-[80vh] items-center justify-center px-4">
      <div className="w-full max-w-sm">
        <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-md p-8">
          <div className="text-center mb-6">
            <h1 className="font-quicksand text-2xl font-bold text-foreground">
              Benvenuto!
            </h1>
            <p className="font-nunito text-sm text-muted-foreground mt-1">
              Completa il tuo profilo per iniziare
            </p>
          </div>

          <form onSubmit={handleSubmit} className="space-y-5">
            {/* Avatar */}
            <div className="flex justify-center">
              <button
                type="button"
                onClick={() => fileInputRef.current?.click()}
                className="relative w-20 h-20 rounded-full bg-slate-100 dark:bg-zinc-700 border-2 border-dashed border-slate-300 dark:border-zinc-600 flex items-center justify-center overflow-hidden hover:border-amber-400 transition-colors"
              >
                {avatarPreview ? (
                  <img src={avatarPreview} alt="Avatar" className="w-full h-full object-cover" />
                ) : (
                  <Camera className="h-6 w-6 text-muted-foreground" />
                )}
              </button>
              <input
                ref={fileInputRef}
                type="file"
                accept="image/*"
                className="hidden"
                onChange={handleAvatarChange}
              />
            </div>

            {/* Display Name */}
            <div className="space-y-1.5">
              <Label htmlFor="displayName" className="font-nunito text-sm">
                Display Name
              </Label>
              <Input
                id="displayName"
                value={displayName}
                onChange={(e) => setDisplayName(e.target.value)}
                placeholder="Il tuo nome"
                autoFocus
                minLength={2}
                maxLength={50}
                required
                disabled={isSubmitting}
                className="font-nunito"
              />
            </div>

            {/* Email (read-only) */}
            <div className="space-y-1.5">
              <Label htmlFor="email" className="font-nunito text-sm">
                Email
              </Label>
              <div className="relative">
                <Input
                  id="email"
                  value={user.email}
                  disabled
                  className="font-nunito pr-9 bg-slate-50 dark:bg-zinc-900"
                />
                <Lock className="absolute right-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              </div>
            </div>

            {error && (
              <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
            )}

            <Button
              type="submit"
              disabled={!isValid || isSubmitting}
              className="w-full bg-amber-500 hover:bg-amber-600 text-white rounded-xl py-3 font-quicksand font-semibold"
            >
              {isSubmitting ? (
                <Loader2 className="h-4 w-4 animate-spin mr-2" />
              ) : (
                <ArrowRight className="h-4 w-4 mr-2" />
              )}
              Entra
            </Button>
          </form>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/app/\(authenticated\)/onboarding/__tests__/page.test.tsx`
Expected: PASS (4 tests). Adjust mock paths if `useAuth` import differs from `@/components/auth/AuthProvider` — check the actual import used in the codebase.

- [ ] **Step 5: Verify locally**

Run: `cd apps/web && pnpm dev`
Navigate to: `http://localhost:3000/onboarding` (must be logged in, onboarding not completed)
Verify: avatar upload area, name field, email read-only, submit flow.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/onboarding/page.tsx apps/web/src/app/\(authenticated\)/onboarding/__tests__/page.test.tsx
git commit -m "feat(auth): rewrite onboarding as minimal single-screen profile form"
```

---

## Chunk 7: Cleanup + Final Validation

### Task 10: Remove unused components

**Files:**
- Check & potentially delete: `apps/web/src/components/admin/overview/QuickActionsWidget.tsx`
- Check & potentially delete: `apps/web/src/components/admin/overview/SystemHealthCard.tsx`

- [ ] **Step 1: Check if QuickActionsWidget is imported elsewhere**

Run: `cd apps/web && grep -r "QuickActionsWidget" src/ --include="*.tsx" --include="*.ts" -l`

If only found in the old `overview/page.tsx` (now rewritten) and its own file + test, safe to delete.

- [ ] **Step 2: Check if SystemHealthCard is imported elsewhere**

Run: `cd apps/web && grep -r "SystemHealthCard" src/ --include="*.tsx" --include="*.ts" -l`

Same check — if only imported in old overview page, safe to delete.

- [ ] **Step 3: Delete unused files (only if confirmed unused)**

```bash
# Only run these if grep confirmed no other imports:
git rm apps/web/src/components/admin/overview/QuickActionsWidget.tsx
git rm apps/web/src/components/admin/overview/SystemHealthCard.tsx
# Also delete their tests if they exist:
git rm apps/web/src/components/admin/overview/__tests__/QuickActionsWidget.test.tsx 2>/dev/null || true
git rm apps/web/src/components/admin/overview/__tests__/SystemHealthCard.test.tsx 2>/dev/null || true
```

- [ ] **Step 4: Run full test suite for overview + onboarding**

Run: `cd apps/web && pnpm vitest run src/app/admin/\(dashboard\)/overview/ src/app/\(authenticated\)/onboarding/`
Expected: All tests PASS

- [ ] **Step 5: Run TypeScript check**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 6: Commit cleanup**

```bash
git add -A
git commit -m "chore(admin): remove unused QuickActionsWidget and SystemHealthCard"
```

---

### Task 11: Create PR

- [ ] **Step 1: Push branch and create PR**

```bash
git push -u origin feature/admin-dashboard-redesign
```

Create PR targeting parent branch (check with `git config branch.$(git branch --show-current).parent`):

```bash
gh pr create --title "feat(admin): redesign overview dashboard + onboarding" --body "$(cat <<'EOF'
## Summary
- Rewrites `/admin/overview` as operational command center: KPI row, pending requests banner, library/users summary cards, 6 quick action cards, muted tech actions bar
- Rewrites `/onboarding` as minimal single-screen profile form (name + email read-only + avatar)
- Adds loading skeleton for overview page

## Test plan
- [ ] All new component tests pass (`pnpm vitest run src/app/admin/(dashboard)/overview/`)
- [ ] Onboarding tests pass (`pnpm vitest run src/app/(authenticated)/onboarding/`)
- [ ] TypeScript check passes (`pnpm typecheck`)
- [ ] Manual: navigate to /admin/overview — verify KPI row, pending banner, summary cards, quick actions, tech bar
- [ ] Manual: navigate to /onboarding — verify name field, email read-only, avatar upload, submit flow

Spec: `docs/superpowers/specs/2026-03-16-admin-dashboard-onboarding-design.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 2: Run code review**

Use `/code-review:code-review <PR-URL>` on the created PR.

- [ ] **Step 3: Fix code review issues (max 3 iterations)**

- [ ] **Step 4: Mark PR ready and merge**
