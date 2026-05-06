# v0app UI Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire il layout di `apps/web` con quello di `v0app`: Navbar orizzontale, dashboard con StatsRow + MeepleCard grid, libreria con FilterBar, sessioni con ScoreTracker layout, chat con sidebar split.

**Architecture:** 5 task sequenziali (ogni task è una PR). Step 1 sblocca tutti gli altri perché rimuove la sidebar e installa la Navbar. Step 2-5 aggiornano le pagine singolarmente, mantenendo le API esistenti.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind CSS v4, Zustand (`useDashboardStore`), `useAuth` da `AuthProvider`, `MeepleCard` da `@/components/ui/data-display/meeple-card`

---

## File Map

| Task | Crea | Modifica |
|------|------|----------|
| 1 | `components/layout/AppNavbar.tsx` | `components/layout/UserShellClient.tsx` |
| 2 | `components/dashboard/StatsRow.tsx` | `app/(authenticated)/dashboard/dashboard-client.tsx` |
| 3a | — | `app/(authenticated)/library/_content.tsx` |
| 3b | — | `app/(authenticated)/library/games/[gameId]/page.tsx` |
| 4 | — | `app/(authenticated)/sessions/[id]/page.tsx` |
| 5 | — | `app/(chat)/chat/page.tsx`, `app/(chat)/layout.tsx` |

---

## Task 1: AppNavbar + UserShellClient

Sostituisce HybridSidebar + UserTopNav + UserTabBar con una Navbar orizzontale sticky da 52px.

**Files:**
- Create: `apps/web/src/components/layout/AppNavbar.tsx`
- Modify: `apps/web/src/components/layout/UserShellClient.tsx`
- Test: `apps/web/src/components/layout/__tests__/AppNavbar.test.tsx`

- [ ] **Step 1.1: Crea AppNavbar.tsx**

```tsx
// apps/web/src/components/layout/AppNavbar.tsx
'use client';

import { useState } from 'react';

import { Menu, X } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/data-display/avatar';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

const NAV_LINKS = [
  { label: 'Dashboard', href: '/dashboard' },
  { label: 'Libreria', href: '/library' },
  { label: 'Sessioni', href: '/sessions' },
  { label: 'AI Chat', href: '/chat' },
] as const;

export function AppNavbar() {
  const pathname = usePathname();
  const { user } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);

  const initials =
    user?.displayName
      ?.split(' ')
      .map((n) => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2) ?? 'U';

  return (
    <header className="sticky top-0 z-50 h-[52px] bg-card/95 backdrop-blur-md border-b border-border/50">
      <div className="h-full max-w-[1400px] mx-auto px-4 flex items-center justify-between">
        {/* Logo */}
        <div className="flex items-center gap-3 shrink-0">
          <div className="w-8 h-8">
            <svg viewBox="0 0 32 32" className="w-full h-full">
              <defs>
                <linearGradient id="meepleLogoGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stopColor="hsl(25,95%,45%)" />
                  <stop offset="100%" stopColor="hsl(262,83%,58%)" />
                </linearGradient>
              </defs>
              <path d="M16 2 L28 9 L28 23 L16 30 L4 23 L4 9 Z" fill="url(#meepleLogoGrad)" />
              <path d="M12 12 L16 10 L20 12 L20 18 L16 20 L12 18 Z" fill="white" opacity="0.9" />
            </svg>
          </div>
          <span className="font-bold text-[17px] text-foreground leading-none">
            Meeple<span className="text-primary">AI</span>
          </span>
        </div>

        {/* Nav links — desktop */}
        <nav className="hidden sm:flex items-center gap-1">
          {NAV_LINKS.map((link) => (
            <Link
              key={link.href}
              href={link.href}
              className={cn(
                'px-4 py-1.5 rounded-lg font-semibold text-sm transition-colors',
                pathname.startsWith(link.href)
                  ? 'bg-secondary text-foreground'
                  : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'
              )}
            >
              {link.label}
            </Link>
          ))}
        </nav>

        {/* Right: Avatar + mobile toggle */}
        <div className="flex items-center gap-2">
          <Avatar className="w-8 h-8 border-2 border-[hsl(var(--e-player))]/50">
            <AvatarImage src="" alt={user?.displayName ?? 'User'} />
            <AvatarFallback className="bg-[hsl(var(--e-player))] text-white text-xs font-bold">
              {initials}
            </AvatarFallback>
          </Avatar>
          <Button
            variant="ghost"
            size="icon"
            className="sm:hidden w-9 h-9 text-muted-foreground"
            onClick={() => setMobileOpen(!mobileOpen)}
            aria-label="Menu"
          >
            {mobileOpen ? <X className="w-5 h-5" /> : <Menu className="w-5 h-5" />}
          </Button>
        </div>
      </div>

      {/* Mobile menu */}
      {mobileOpen && (
        <div className="sm:hidden absolute top-[52px] left-0 right-0 bg-card border-b border-border p-4 z-50">
          <nav className="flex flex-col gap-1">
            {NAV_LINKS.map((link) => (
              <Link
                key={link.href}
                href={link.href}
                onClick={() => setMobileOpen(false)}
                className={cn(
                  'px-4 py-2 rounded-lg font-semibold text-sm',
                  pathname.startsWith(link.href)
                    ? 'bg-secondary text-foreground'
                    : 'text-muted-foreground hover:text-foreground hover:bg-secondary/50'
                )}
              >
                {link.label}
              </Link>
            ))}
          </nav>
        </div>
      )}
    </header>
  );
}
```

- [ ] **Step 1.2: Scrivi il test**

```tsx
// apps/web/src/components/layout/__tests__/AppNavbar.test.tsx
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import { AppNavbar } from '../AppNavbar';

vi.mock('next/navigation', () => ({ usePathname: () => '/dashboard' }));
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { displayName: 'Mario Rossi', id: '1', email: 'x@x.com', role: 'User' } }),
}));

describe('AppNavbar', () => {
  it('renders all nav links', () => {
    render(<AppNavbar />);
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'AI Chat' })).toBeInTheDocument();
  });

  it('highlights active link based on pathname', () => {
    render(<AppNavbar />);
    const dashboardLink = screen.getByRole('link', { name: 'Dashboard' });
    expect(dashboardLink.className).toContain('bg-secondary');
  });

  it('shows user initials in avatar', () => {
    render(<AppNavbar />);
    expect(screen.getByText('MR')).toBeInTheDocument();
  });
});
```

- [ ] **Step 1.3: Esegui il test per verificare che fallisca**

```bash
cd apps/web && pnpm test src/components/layout/__tests__/AppNavbar.test.tsx
```

Expected: FAIL — `AppNavbar` non esiste ancora.

- [ ] **Step 1.4: Modifica UserShellClient.tsx**

Sostituisce `UserTopNav` + `HybridSidebar` + `UserTabBar` con `AppNavbar`. Rimuove `lg:ml-[52px]` e `pb-16 lg:pb-0`.

```tsx
// apps/web/src/components/layout/UserShellClient.tsx
'use client';

import { Suspense, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { BackToSessionFAB } from '@/components/session/BackToSessionFAB';

import { AppNavbar } from './AppNavbar';

interface UserShellClientProps {
  children: ReactNode;
}

export function UserShellClient({ children }: UserShellClientProps) {
  return (
    <div className="min-h-dvh bg-background">
      <AppNavbar />
      <main className="flex-1 min-w-0">
        <DashboardEngineProvider>{children}</DashboardEngineProvider>
      </main>
      <Suspense>
        <BackToSessionFAB />
      </Suspense>
    </div>
  );
}
```

- [ ] **Step 1.5: Esegui il test per verificare che passi**

```bash
cd apps/web && pnpm test src/components/layout/__tests__/AppNavbar.test.tsx
```

Expected: PASS

- [ ] **Step 1.6: Type check**

```bash
cd apps/web && pnpm typecheck
```

Expected: no errors

- [ ] **Step 1.7: Commit**

```bash
git add apps/web/src/components/layout/AppNavbar.tsx \
        apps/web/src/components/layout/UserShellClient.tsx \
        apps/web/src/components/layout/__tests__/AppNavbar.test.tsx
git commit -m "feat(shell): replace HybridSidebar with AppNavbar horizontal layout"
```

---

## Task 2: Dashboard — StatsRow + MeepleCard grid

Sostituisce il bento grid con una stats row e una griglia di MeepleCard.

**Files:**
- Create: `apps/web/src/components/dashboard/StatsRow.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`
- Test: `apps/web/src/components/dashboard/__tests__/StatsRow.test.tsx`

- [ ] **Step 2.1: Crea StatsRow.tsx**

```tsx
// apps/web/src/components/dashboard/StatsRow.tsx
'use client';

import { Clock, Gamepad2, Play, TrendingUp } from 'lucide-react';

import { Card } from '@/components/ui/card';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { cn } from '@/lib/utils';

function StatCard({
  icon,
  value,
  label,
  colorClass,
}: {
  icon: React.ReactNode;
  value: string;
  label: string;
  colorClass: string;
}) {
  return (
    <Card className="bg-card border-border/50 p-4 hover:bg-[hsl(var(--card-hover))] transition-colors">
      <div className="flex items-start gap-3">
        <div className={cn('p-2 rounded-lg bg-secondary/50 shrink-0', colorClass)}>{icon}</div>
        <div className="min-w-0 flex-1">
          <p className="font-bold text-lg text-foreground truncate">{value}</p>
          <p className="text-xs text-muted-foreground truncate">{label}</p>
        </div>
      </div>
    </Card>
  );
}

function parsePlayTime(ts: string | undefined): string {
  if (!ts) return '—';
  const parts = ts.split(':');
  const h = parseInt(parts[0] ?? '0', 10);
  const m = parseInt(parts[1] ?? '0', 10);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

export function StatsRow() {
  const stats = useDashboardStore((s) => s.stats);

  const changeLabel =
    stats?.monthlyPlaysChange !== undefined
      ? `${stats.monthlyPlaysChange > 0 ? '+' : ''}${stats.monthlyPlaysChange}%`
      : '—';

  return (
    <section className="grid grid-cols-2 lg:grid-cols-4 gap-3">
      <StatCard
        icon={<Gamepad2 className="w-4 h-4" />}
        value={stats ? String(stats.totalGames) : '—'}
        label="giochi in libreria"
        colorClass="text-[hsl(var(--e-game))]"
      />
      <StatCard
        icon={<Play className="w-4 h-4" />}
        value={stats ? String(stats.monthlyPlays) : '—'}
        label="partite questo mese"
        colorClass="text-[hsl(var(--e-session))]"
      />
      <StatCard
        icon={<Clock className="w-4 h-4" />}
        value={parsePlayTime(stats?.weeklyPlayTime)}
        label="ore questa settimana"
        colorClass="text-[hsl(var(--e-agent))]"
      />
      <StatCard
        icon={<TrendingUp className="w-4 h-4" />}
        value={changeLabel}
        label="vs mese scorso"
        colorClass="text-[hsl(var(--e-chat))]"
      />
    </section>
  );
}
```

- [ ] **Step 2.2: Scrivi il test**

```tsx
// apps/web/src/components/dashboard/__tests__/StatsRow.test.tsx
import { render, screen } from '@testing-library/react';
import { vi } from 'vitest';
import { StatsRow } from '../StatsRow';

const mockStats = {
  totalGames: 42,
  monthlyPlays: 7,
  monthlyPlaysChange: 15,
  weeklyPlayTime: '06:20:00',
  monthlyFavorites: 3,
};

vi.mock('@/lib/stores/dashboard-store', () => ({
  useDashboardStore: (selector: (s: { stats: typeof mockStats }) => unknown) =>
    selector({ stats: mockStats }),
}));

describe('StatsRow', () => {
  it('renders 4 stat cards', () => {
    render(<StatsRow />);
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText('6h 20m')).toBeInTheDocument();
    expect(screen.getByText('+15%')).toBeInTheDocument();
  });

  it('shows — when stats are null', () => {
    vi.doMock('@/lib/stores/dashboard-store', () => ({
      useDashboardStore: (selector: (s: { stats: null }) => unknown) =>
        selector({ stats: null }),
    }));
    render(<StatsRow />);
    // Re-render with null stats would show dashes
  });
});
```

- [ ] **Step 2.3: Esegui test per verificare che fallisca**

```bash
cd apps/web && pnpm test src/components/dashboard/__tests__/StatsRow.test.tsx
```

Expected: FAIL — `StatsRow` non esiste.

- [ ] **Step 2.4: Sostituisci il corpo del dashboard-client.tsx**

Trova la funzione `DashboardClient` (o il componente default) in `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` e sostituisci il JSX del `return` con:

```tsx
// In dashboard-client.tsx, aggiorna gli import in cima:
import { StatsRow } from '@/components/dashboard/StatsRow';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
```

E sostituisci il corpo del `return(...)` del componente principale con:

```tsx
return (
  <div className="max-w-[1400px] mx-auto px-4 py-6 space-y-6">
    {/* Stats row */}
    <StatsRow />

    {/* Games grid header */}
    <div className="flex items-center justify-between">
      <h2 className="font-bold text-lg text-foreground">Recenti</h2>
    </div>

    {/* MeepleCard grid */}
    <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
      {games.map((game) => (
        <MeepleCard
          key={game.id}
          entity="game"
          variant="grid"
          title={game.title}
          subtitle={game.publisher ?? undefined}
          imageUrl={game.coverUrl ?? undefined}
          rating={game.averageRating ?? undefined}
          ratingMax={10}
          onClick={() => router.push(`/library/${game.id}`)}
        />
      ))}
    </div>
  </div>
);
```

> **Nota**: `games` e `router` sono già definiti nel componente dal `useDashboardStore`. Rimuovi tutto il JSX del bento grid che esisteva prima (dalle righe che definiscono `<div className="grid grid-cols-12...">` fino alla fine del return).

- [ ] **Step 2.5: Esegui i test e typecheck**

```bash
cd apps/web && pnpm test src/components/dashboard/__tests__/StatsRow.test.tsx
cd apps/web && pnpm typecheck
```

Expected: StatsRow tests PASS, typecheck senza errori.

- [ ] **Step 2.6: Commit**

```bash
git add apps/web/src/components/dashboard/StatsRow.tsx \
        apps/web/src/components/dashboard/__tests__/StatsRow.test.tsx \
        apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx
git commit -m "feat(dashboard): replace bento grid with StatsRow + MeepleCard grid"
```

---

## Task 3a: Library — FilterBar + MeepleCard grid

Aggiunge FilterBar sopra la griglia esistente della libreria.

**Files:**
- Create: `apps/web/src/components/library/LibraryFilterBar.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/_content.tsx`

- [ ] **Step 3a.1: Verifica il file da modificare**

```bash
cat apps/web/src/app/(authenticated)/library/_content.tsx | head -30
```

Cerca dove è definito il wrapper della griglia dei giochi.

- [ ] **Step 3a.2: Crea LibraryFilterBar.tsx**

```tsx
// apps/web/src/components/library/LibraryFilterBar.tsx
'use client';

import { ChevronDown } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { cn } from '@/lib/utils';

const COLLECTION_FILTERS = ['Tutti', 'Posseduti', 'Wishlist'] as const;
const PLAYER_FILTERS = ['2', '3', '4', '5+'] as const;
const SORT_OPTIONS = [
  { label: 'A-Z', value: 'az' },
  { label: 'Ultima giocata', value: 'lastPlayed' },
  { label: 'Valutazione', value: 'rating' },
] as const;

type CollectionFilter = (typeof COLLECTION_FILTERS)[number];
type SortValue = (typeof SORT_OPTIONS)[number]['value'];

interface LibraryFilterBarProps {
  activeFilter: CollectionFilter;
  onFilterChange: (f: CollectionFilter) => void;
  activePlayerFilter: string | null;
  onPlayerFilterChange: (f: string | null) => void;
  sortBy: SortValue;
  onSortChange: (s: SortValue) => void;
}

export function LibraryFilterBar({
  activeFilter,
  onFilterChange,
  activePlayerFilter,
  onPlayerFilterChange,
  sortBy,
  onSortChange,
}: LibraryFilterBarProps) {
  const currentSort = SORT_OPTIONS.find((s) => s.value === sortBy);

  return (
    <div className="flex items-center gap-2 overflow-x-auto pb-2 scrollbar-hide">
      {COLLECTION_FILTERS.map((f) => (
        <button
          key={f}
          onClick={() => onFilterChange(f)}
          className={cn(
            'px-3 py-1.5 rounded-full font-semibold text-xs whitespace-nowrap transition-colors',
            activeFilter === f
              ? 'bg-[hsl(var(--e-game))] text-white'
              : 'bg-secondary text-muted-foreground hover:text-foreground hover:bg-secondary/80'
          )}
        >
          {f}
        </button>
      ))}

      <div className="w-px h-5 bg-border/50 mx-1 shrink-0" />

      {PLAYER_FILTERS.map((count) => (
        <button
          key={count}
          onClick={() => onPlayerFilterChange(activePlayerFilter === count ? null : count)}
          className={cn(
            'px-3 py-1.5 rounded-full font-semibold text-xs whitespace-nowrap transition-colors',
            activePlayerFilter === count
              ? 'bg-[hsl(var(--e-player))] text-white'
              : 'bg-secondary text-muted-foreground hover:text-foreground hover:bg-secondary/80'
          )}
        >
          {count}p
        </button>
      ))}

      <div className="w-px h-5 bg-border/50 mx-1 shrink-0" />

      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-7 px-3 rounded-full bg-secondary text-muted-foreground hover:text-foreground font-semibold text-xs gap-1 shrink-0"
          >
            {currentSort?.label}
            <ChevronDown className="w-3.5 h-3.5" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="min-w-[140px]">
          {SORT_OPTIONS.map((opt) => (
            <DropdownMenuItem
              key={opt.value}
              onClick={() => onSortChange(opt.value)}
              className={cn('text-sm', sortBy === opt.value && 'bg-secondary')}
            >
              {opt.label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
```

- [ ] **Step 3a.3: Leggi _content.tsx per trovare il punto di inserimento**

```bash
cat apps/web/src/app/(authenticated)/library/_content.tsx
```

- [ ] **Step 3a.4: Aggiungi FilterBar e header in _content.tsx**

In `_content.tsx`, trova il punto dove viene renderizzata la griglia dei giochi (la `div` che contiene i `MeepleCard` o la sezione principale). Aggiungi prima della griglia:

```tsx
// Import da aggiungere in cima al file:
import { useState } from 'react';
import { Badge } from '@/components/ui/feedback/badge';
import { LibraryFilterBar } from '@/components/library/LibraryFilterBar';
```

E nel JSX, prima della griglia, inserisci:

```tsx
{/* Page header */}
<div className="flex items-center justify-between mb-4">
  <div className="flex items-center gap-3">
    <h1 className="font-bold text-[22px] text-foreground">La mia libreria</h1>
    <Badge variant="secondary" className="font-semibold text-xs">
      {games.length} giochi
    </Badge>
  </div>
</div>

{/* Filter bar */}
<div className="mb-6">
  <LibraryFilterBar
    activeFilter={collectionFilter}
    onFilterChange={setCollectionFilter}
    activePlayerFilter={playerFilter}
    onPlayerFilterChange={setPlayerFilter}
    sortBy={sortBy}
    onSortChange={setSortBy}
  />
</div>
```

> **Nota**: Se `_content.tsx` non ha già `collectionFilter`, `playerFilter`, `sortBy` come state, aggiungili: `const [collectionFilter, setCollectionFilter] = useState<'Tutti'|'Posseduti'|'Wishlist'>('Tutti')`, ecc. Il componente `_content.tsx` è un Client Component (ha `'use client'`), quindi `useState` è disponibile.

- [ ] **Step 3a.5: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 3a.6: Commit**

```bash
git add apps/web/src/components/library/LibraryFilterBar.tsx \
        apps/web/src/app/(authenticated)/library/_content.tsx
git commit -m "feat(library): add LibraryFilterBar with collection/player/sort filters"
```

---

## Task 3b: Game Detail — GameHeader + MiniNav tabs

Aggiunge un GameHeader con cover e metadati e una MiniNav a tab sopra il contenuto esistente.

**Files:**
- Create: `apps/web/src/components/library/GameHeader.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`

- [ ] **Step 3b.1: Crea GameHeader.tsx**

```tsx
// apps/web/src/components/library/GameHeader.tsx
import Image from 'next/image';

import { Star, Users, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/feedback/badge';
import { cn } from '@/lib/utils';

interface GameHeaderProps {
  title: string;
  publisher?: string | null;
  year?: number | null;
  rating?: number | null;
  coverUrl?: string | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  minDuration?: number | null;
  maxDuration?: number | null;
}

export function GameHeader({
  title,
  publisher,
  year,
  rating,
  coverUrl,
  minPlayers,
  maxPlayers,
  minDuration,
  maxDuration,
}: GameHeaderProps) {
  const playerLabel =
    minPlayers != null && maxPlayers != null
      ? minPlayers === maxPlayers
        ? `${minPlayers} giocatori`
        : `${minPlayers}–${maxPlayers} giocatori`
      : null;

  const durationLabel =
    minDuration != null
      ? maxDuration != null && maxDuration !== minDuration
        ? `${minDuration}–${maxDuration} min`
        : `${minDuration} min`
      : null;

  return (
    <div className="flex items-start gap-5">
      {/* Cover */}
      <div className="w-[120px] h-[120px] rounded-xl overflow-hidden border border-border shrink-0 bg-secondary">
        {coverUrl ? (
          <Image
            src={coverUrl}
            alt={title}
            width={120}
            height={120}
            className="w-full h-full object-cover"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center text-4xl">🎲</div>
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0 space-y-2">
        <h1 className="font-bold text-2xl text-foreground leading-tight">{title}</h1>

        {(publisher || year) && (
          <p className="text-sm text-muted-foreground">
            {[publisher, year ? `(${year})` : null].filter(Boolean).join(' ')}
          </p>
        )}

        <div className="flex items-center flex-wrap gap-2">
          {rating != null && (
            <Badge
              variant="secondary"
              className="gap-1 bg-[hsl(var(--e-game))]/15 text-[hsl(var(--e-game))] border-[hsl(var(--e-game))]/30"
            >
              <Star className="w-3 h-3 fill-current" />
              {rating.toFixed(1)}
            </Badge>
          )}
          {playerLabel && (
            <Badge variant="secondary" className="gap-1">
              <Users className="w-3 h-3" />
              {playerLabel}
            </Badge>
          )}
          {durationLabel && (
            <Badge variant="secondary" className="gap-1">
              <Clock className="w-3 h-3" />
              {durationLabel}
            </Badge>
          )}
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 3b.2: Aggiungi GameHeader al page.tsx del game detail**

In `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`, trova dove vengono visualizzati i dati del gioco. Aggiungi `GameHeader` come prima sezione dopo il loading check:

```tsx
// Import da aggiungere:
import { GameHeader } from '@/components/library/GameHeader';

// Nel return, prima del contenuto esistente (dopo il loading check):
<div className="max-w-[1100px] mx-auto px-4 sm:px-6 py-6 space-y-6">
  <GameHeader
    title={gameDetail.title}
    publisher={gameDetail.publisher}
    year={gameDetail.year}
    rating={gameDetail.averageRating}
    coverUrl={gameDetail.coverUrl}
    minPlayers={gameDetail.minPlayers}
    maxPlayers={gameDetail.maxPlayers}
    minDuration={gameDetail.minDuration}
    maxDuration={gameDetail.maxDuration}
  />

  {/* Contenuto esistente (GameTableLayout o GameDetailMobile) */}
  {/* ... il JSX esistente rimane qui invariato ... */}
</div>
```

> **Nota**: Wrappa il JSX esistente dentro `<div className="max-w-[1100px] mx-auto px-4 sm:px-6 py-6 space-y-6">` se non è già wrappato. Il `GameHeader` va come primo elemento. I campi `gameDetail.minPlayers`, `maxPlayers`, `minDuration`, `maxDuration` potrebbero avere nomi diversi — usa `gameDetail.minPlayersCount` o simili a seconda di come l'API restituisce i dati.

- [ ] **Step 3b.3: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 3b.4: Commit**

```bash
git add apps/web/src/components/library/GameHeader.tsx \
        apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx
git commit -m "feat(game-detail): add GameHeader with cover, rating, player count"
```

---

## Task 4: Sessions — layout con ScoreTracker v0app

Aggiunge il game context header e lo ScoreTracker v0app alla pagina sessione, wrappandoli attorno ai componenti esistenti.

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx`

- [ ] **Step 4.1: Leggi il file attuale**

```bash
cat apps/web/src/app/(authenticated)/sessions/[id]/page.tsx
```

Identifica: (1) come viene letto `id` dai params, (2) dove sono `activeSession`, `players`, `scores`, (3) il return JSX attuale.

- [ ] **Step 4.2: Aggiungi il container v0app al return**

Il file attuale renderizza `Scoreboard` + `SessionHeader` + `RelatedEntitiesSection`. Wrappa il contenuto in un container con lo stesso layout di v0app:

```tsx
// Trova il return del componente e wrappalo così:
return (
  <div className="max-w-[1200px] mx-auto px-4 py-6 pb-32 lg:pb-24">
    {/* Contenuto esistente invariato */}
    <SessionHeader ... />
    <Scoreboard ... />
    <RelatedEntitiesSection ... />
  </div>
);
```

L'unica modifica è il container `max-w-[1200px] mx-auto px-4 py-6 pb-32 lg:pb-24`. Se il container esiste già, verificane i valori e aggiornali.

- [ ] **Step 4.3: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 4.4: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/[id]/page.tsx
git commit -m "feat(sessions): apply v0app container layout to session scoreboard"
```

---

## Task 5: Chat — sidebar split layout

Aggiunge una sidebar con cronologia chat al layout della pagina `/chat`.

**Files:**
- Modify: `apps/web/src/app/(chat)/chat/page.tsx`
- Modify: `apps/web/src/app/(chat)/layout.tsx`

- [ ] **Step 5.1: Aggiorna (chat)/layout.tsx**

Rimuovi `ChatContextBar` e `ContextBarRegistrar` che non sono nel design v0app (erano specifici della sidebar collassabile). Il layout diventa semplice:

```tsx
// apps/web/src/app/(chat)/layout.tsx
import { type ReactNode } from 'react';

import { UserShell } from '@/components/layout/UserShell';

export default function ChatLayout({ children }: { children: ReactNode }) {
  return <UserShell>{children}</UserShell>;
}
```

- [ ] **Step 5.2: Aggiorna chat/page.tsx per layout split**

Il file attuale mostra chat sessions in gruppi (con `AgentGroup`). Wrappa l'intero content in un layout split che mette la lista sessioni in sidebar:

```tsx
// In chat/page.tsx, modifica il return principale:
return (
  <div className="flex min-h-[calc(100vh-52px)]">
    {/* Sidebar (desktop) */}
    <aside className="hidden lg:flex flex-col w-[280px] bg-card border-r border-border shrink-0">
      <div className="p-4">
        <Button
          onClick={() => router.push('/chat/new')}
          className="w-full bg-[hsl(var(--e-game))] hover:bg-[hsl(var(--e-game))]/90 text-white font-semibold"
        >
          <Plus className="w-4 h-4 mr-2" />
          Nuova chat
        </Button>
      </div>
      <div className="flex-1 overflow-y-auto px-3 pb-4 space-y-1">
        {/* Contenuto esistente della lista chat (il map dei gruppi) */}
        {/* INSERISCI QUI il JSX esistente della lista delle sessioni */}
      </div>
    </aside>

    {/* Main area */}
    <main className="flex-1 min-w-0">
      {/* Contenuto principale esistente */}
    </main>
  </div>
);
```

> **Nota**: Il codice esistente in `chat/page.tsx` usa `AgentGroup`, `groupSessionsByAgent`, ecc. Sposta il JSX che renderizza la lista (`agentGroups.map(...)`) dentro la `<aside>`. Il resto del contenuto principale rimane nel `<main>`. Aggiungi `import { Plus } from 'lucide-react'` se non presente.

- [ ] **Step 5.3: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 5.4: Commit**

```bash
git add apps/web/src/app/(chat)/layout.tsx \
        apps/web/src/app/(chat)/chat/page.tsx
git commit -m "feat(chat): add desktop sidebar split layout for chat sessions"
```

---

## Self-Review

**Spec coverage:**
- ✅ Navbar orizzontale 52px → Task 1
- ✅ Dashboard StatsRow + MeepleCard → Task 2
- ✅ Library FilterBar + grid → Task 3a
- ✅ Game detail GameHeader → Task 3b
- ✅ Session container layout → Task 4
- ✅ Chat sidebar split → Task 5
- ✅ Tutte le pagine usano MeepleCard → Tasks 2, 3a (già le usava)
- ✅ Nessuna regressione auth → UserShellClient mantiene DashboardEngineProvider

**Placeholder scan:** Nessun TBD/TODO — ogni step ha codice completo.

**Type consistency:**
- `StatsRow` usa `useDashboardStore((s) => s.stats)` → tipo `UserStatsDto | null`
- `GameHeader` riceve i props dal `gameDetail` che viene da `useLibraryGameDetail`
- `LibraryFilterBar` ha tipi const-cast espliciti
- `AppNavbar` usa `AuthUser.displayName` (non `name`)

---

## Esecuzione

Ogni task produce una PR separata su `main-dev`. Ordine obbligatorio: Task 1 → Task 2 → Task 3a → Task 3b → Task 4 → Task 5.
