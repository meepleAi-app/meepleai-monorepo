# Navbar + FAB + Bento Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ripristinare FloatingActionPill nelle 4 pagine principali, aggiungere dropdown avatar alla navbar, attivare il bento grid della dashboard con 6 widget e gestire il first-run con Welcome Hero.

**Architecture:** `FloatingActionPill` è un componente `position:fixed` parametrizzato via `page` prop. La dashboard usa il `useDashboardStore` Zustand già esistente — basta aggiungere `fetchRecentSessions` e `fetchTrendingGames` all'`useEffect` e rendere i 6 widget già definiti nel file. Il `WelcomeHero` è un componente separato che si attiva quando `stats.totalGames === 0 && recentSessions.length === 0`.

**Tech Stack:** React 19, Next.js 16 App Router, Tailwind CSS v4, Zustand, Lucide React, `useAuth` (AuthProvider), `useRouter` (next/navigation)

---

## File Map

| File | Operazione |
|------|-----------|
| `apps/web/src/components/layout/FloatingActionPill.tsx` | **Crea** — FAB contestuale desktop+mobile |
| `apps/web/src/components/dashboard/WelcomeHero.tsx` | **Crea** — Banner primo accesso |
| `apps/web/src/components/layout/AppNavbar.tsx` | **Modifica** — avatar dropdown |
| `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | **Modifica** — colori CSS token + bento grid + welcome mode |
| `apps/web/src/app/(authenticated)/library/_content.tsx` | **Modifica** — aggiunge FAB |
| `apps/web/src/app/(authenticated)/sessions/_content.tsx` | **Modifica** — sostituisce mobile FAB con FloatingActionPill |
| `apps/web/src/app/(chat)/chat/page.tsx` | **Modifica** — aggiunge FAB |
| `apps/web/src/components/layout/__tests__/AppNavbar.test.tsx` | **Modifica** — test dropdown avatar |
| `apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx` | **Crea** — test FAB |

---

## Task 1: Crea `FloatingActionPill`

**Files:**
- Create: `apps/web/src/components/layout/FloatingActionPill.tsx`
- Test: `apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx`

- [ ] **Step 1.1: Crea il file componente**

```tsx
// apps/web/src/components/layout/FloatingActionPill.tsx
'use client';

import { LayoutGrid, Plus, Search, SortAsc } from 'lucide-react';
import { useRouter } from 'next/navigation';

export type FabPage = 'dashboard' | 'library' | 'sessions' | 'chat';

export interface FloatingActionPillProps {
  page: FabPage;
  onSearch?: () => void;
  onSort?: () => void;
  onToggleView?: () => void;
}

const PAGE_CONFIG: Record<FabPage, { label: string; cta: string; href: string }> = {
  dashboard: { label: '🏠 Dashboard', cta: '+ Aggiungi gioco', href: '/library?action=add' },
  library: { label: '📚 La mia libreria', cta: '+ Aggiungi', href: '/library?action=add' },
  sessions: { label: '🎯 Sessioni', cta: '▶ Nuova sessione', href: '/sessions/new' },
  chat: { label: '💬 Chat AI', cta: '+ Nuova chat', href: '/chat/new' },
};

export function FloatingActionPill({
  page,
  onSearch,
  onSort,
  onToggleView,
}: FloatingActionPillProps) {
  const router = useRouter();
  const { label, cta, href } = PAGE_CONFIG[page];
  const hasSecondary = Boolean(onSearch ?? onSort ?? onToggleView);

  return (
    <>
      {/* ── Desktop: pillola centrata ── */}
      <div className="hidden lg:flex fixed bottom-6 left-1/2 -translate-x-1/2 z-50 items-center gap-2 px-4 py-2 bg-[rgba(30,41,59,0.85)] backdrop-blur-md border border-white/10 rounded-[40px] shadow-[0_8px_32px_rgba(0,0,0,0.4)]">
        <span className="text-xs font-quicksand font-bold text-muted-foreground pr-1">
          {label}
        </span>
        <div className="w-px h-5 bg-white/10" />
        <button
          onClick={() => router.push(href)}
          className="text-xs font-nunito font-bold text-white px-3 py-1.5 rounded-full hover:opacity-90 transition-opacity"
          style={{ background: 'hsl(var(--e-game))' }}
        >
          {cta}
        </button>
        {onSearch && (
          <button
            onClick={onSearch}
            className="w-7 h-7 rounded-full bg-white/5 flex items-center justify-center text-muted-foreground hover:bg-white/10 transition-colors"
            aria-label="Cerca"
          >
            <Search className="w-3.5 h-3.5" />
          </button>
        )}
        {onSort && (
          <button
            onClick={onSort}
            className="w-7 h-7 rounded-full bg-white/5 flex items-center justify-center text-muted-foreground hover:bg-white/10 transition-colors"
            aria-label="Ordina"
          >
            <SortAsc className="w-3.5 h-3.5" />
          </button>
        )}
        {onToggleView && (
          <button
            onClick={onToggleView}
            className="w-7 h-7 rounded-full bg-white/5 flex items-center justify-center text-muted-foreground hover:bg-white/10 transition-colors"
            aria-label="Cambia vista"
          >
            <LayoutGrid className="w-3.5 h-3.5" />
          </button>
        )}
      </div>

      {/* ── Mobile: action bar + FAB circolare ── */}
      <div className="lg:hidden">
        {hasSecondary && (
          <div className="fixed bottom-0 left-0 right-0 h-16 z-50 bg-[rgba(15,23,42,0.95)] backdrop-blur-md border-t border-white/[0.08] flex items-center justify-around px-6">
            {onSearch && (
              <button
                onClick={onSearch}
                className="flex flex-col items-center gap-0.5 text-muted-foreground"
                aria-label="Cerca"
              >
                <Search className="w-5 h-5" />
                <span className="text-[10px] font-nunito">Cerca</span>
              </button>
            )}
            {onSort && (
              <button
                onClick={onSort}
                className="flex flex-col items-center gap-0.5 text-muted-foreground"
                aria-label="Ordina"
              >
                <SortAsc className="w-5 h-5" />
                <span className="text-[10px] font-nunito">Ordina</span>
              </button>
            )}
            {onToggleView && (
              <button
                onClick={onToggleView}
                className="flex flex-col items-center gap-0.5 text-muted-foreground"
                aria-label="Vista"
              >
                <LayoutGrid className="w-5 h-5" />
                <span className="text-[10px] font-nunito">Vista</span>
              </button>
            )}
          </div>
        )}
        <button
          onClick={() => router.push(href)}
          className="fixed bottom-20 right-4 z-50 w-14 h-14 rounded-full text-white flex items-center justify-center hover:opacity-90 transition-opacity"
          style={{
            background: 'hsl(var(--e-game))',
            boxShadow: '0 4px 16px hsl(var(--e-game) / 0.4)',
          }}
          aria-label={cta}
        >
          <Plus className="w-6 h-6" />
        </button>
      </div>
    </>
  );
}
```

- [ ] **Step 1.2: Crea il test**

```tsx
// apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';

import { FloatingActionPill } from '@/components/layout/FloatingActionPill';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: mockPush }) }));

beforeEach(() => mockPush.mockClear());

describe('FloatingActionPill', () => {
  it('renders desktop pill with correct label for "sessions" page', () => {
    render(<FloatingActionPill page="sessions" />);
    expect(screen.getAllByText(/🎯 Sessioni/)[0]).toBeInTheDocument();
  });

  it('renders desktop CTA for "chat" page', () => {
    render(<FloatingActionPill page="chat" />);
    const buttons = screen.getAllByText('+ Nuova chat');
    expect(buttons.length).toBeGreaterThan(0);
  });

  it('navigates to correct href when desktop CTA clicked', () => {
    render(<FloatingActionPill page="sessions" />);
    // desktop pill button
    const buttons = screen.getAllByText('▶ Nuova sessione');
    fireEvent.click(buttons[0]);
    expect(mockPush).toHaveBeenCalledWith('/sessions/new');
  });

  it('does not render secondary icon buttons when no callbacks provided', () => {
    render(<FloatingActionPill page="library" />);
    expect(screen.queryByLabelText('Cerca')).not.toBeInTheDocument();
    expect(screen.queryByLabelText('Ordina')).not.toBeInTheDocument();
  });

  it('renders search button when onSearch callback provided', () => {
    const onSearch = vi.fn();
    render(<FloatingActionPill page="library" onSearch={onSearch} />);
    const searchBtns = screen.getAllByLabelText('Cerca');
    expect(searchBtns.length).toBeGreaterThan(0);
    fireEvent.click(searchBtns[0]);
    expect(onSearch).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 1.3: Verifica i test passano**

```bash
cd apps/web && pnpm test --reporter=verbose src/__tests__/components/layout/FloatingActionPill.test.tsx
```

Atteso: 5 test PASS

- [ ] **Step 1.4: Commit**

```bash
git add apps/web/src/components/layout/FloatingActionPill.tsx \
        apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx
git commit -m "feat(layout): add FloatingActionPill component with desktop pill and mobile FAB"
```

---

## Task 2: Avatar Dropdown in `AppNavbar`

**Files:**
- Modify: `apps/web/src/components/layout/AppNavbar.tsx`
- Modify: `apps/web/src/components/layout/__tests__/AppNavbar.test.tsx`

- [ ] **Step 2.1: Aggiorna `AppNavbar.tsx`**

Sostituisci il contenuto completo di `apps/web/src/components/layout/AppNavbar.tsx`:

```tsx
'use client';

import { useState } from 'react';

import { LogOut, Menu, Settings, User, X } from 'lucide-react';
import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

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
  const router = useRouter();
  const { user, logout } = useAuth();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [dropdownOpen, setDropdownOpen] = useState(false);

  const initials =
    user?.displayName
      ?.split(' ')
      .map(n => n[0])
      .join('')
      .toUpperCase()
      .slice(0, 2) ?? 'U';

  async function handleLogout() {
    setDropdownOpen(false);
    await logout();
    router.push('/login');
  }

  return (
    <header className="sticky top-0 z-50 h-[52px] bg-card/95 backdrop-blur-md border-b border-border/50">
      <div className="h-full max-w-[1400px] mx-auto px-4 flex items-center justify-between">
        {/* Logo */}
        <div className="flex items-center gap-3 shrink-0">
          <div className="w-8 h-8">
            <svg viewBox="0 0 32 32" className="w-full h-full">
              <defs>
                <linearGradient id="meepleLogoGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                  <stop offset="0%" stopColor="hsl(var(--e-game))" />
                  <stop offset="100%" stopColor="hsl(var(--e-player))" />
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
          {NAV_LINKS.map(link => (
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

        {/* Right: Avatar dropdown + mobile toggle */}
        <div className="flex items-center gap-2">
          {/* Avatar with dropdown */}
          <div className="relative">
            <button
              onClick={() => setDropdownOpen(v => !v)}
              aria-label="Menu utente"
              aria-expanded={dropdownOpen}
              className="rounded-full focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
            >
              <Avatar className="w-8 h-8 border-2 border-[hsl(var(--e-player))]/50">
                <AvatarImage src="" alt={user?.displayName ?? 'User'} />
                <AvatarFallback className="bg-[hsl(var(--e-player))] text-white text-xs font-bold">
                  {initials}
                </AvatarFallback>
              </Avatar>
            </button>

            {dropdownOpen && (
              <>
                {/* Backdrop — closes dropdown on outside click */}
                <div
                  className="fixed inset-0 z-40"
                  onClick={() => setDropdownOpen(false)}
                  aria-hidden="true"
                />
                {/* Dropdown panel */}
                <div className="absolute right-0 top-full mt-2 w-48 z-50 bg-card border border-border rounded-xl shadow-xl py-1 overflow-hidden">
                  <div className="px-3 py-2 border-b border-border/50">
                    <p className="text-xs font-quicksand font-bold text-foreground truncate">
                      {user?.displayName ?? 'Utente'}
                    </p>
                    <p className="text-[11px] text-muted-foreground truncate">
                      {user?.email ?? ''}
                    </p>
                  </div>
                  <button
                    onClick={() => { setDropdownOpen(false); router.push('/profile'); }}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-foreground hover:bg-muted/50 transition-colors font-nunito"
                  >
                    <User className="w-4 h-4 text-muted-foreground" />
                    Profilo
                  </button>
                  <button
                    onClick={() => { setDropdownOpen(false); router.push('/settings'); }}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm text-foreground hover:bg-muted/50 transition-colors font-nunito"
                  >
                    <Settings className="w-4 h-4 text-muted-foreground" />
                    Impostazioni
                  </button>
                  <div className="border-t border-border/50 my-1" />
                  <button
                    onClick={handleLogout}
                    className="w-full flex items-center gap-2.5 px-3 py-2 text-sm hover:bg-muted/50 transition-colors font-nunito"
                    style={{ color: 'hsl(var(--e-event))' }}
                  >
                    <LogOut className="w-4 h-4" />
                    Esci
                  </button>
                </div>
              </>
            )}
          </div>

          {/* Mobile hamburger */}
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
            {NAV_LINKS.map(link => (
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

- [ ] **Step 2.2: Aggiorna `AppNavbar.test.tsx`** — aggiungi test dropdown, preserva tutti i test esistenti

```tsx
// apps/web/src/components/layout/__tests__/AppNavbar.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';

import { AppNavbar } from '../AppNavbar';

const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: mockPush }),
}));
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { displayName: 'Mario Rossi', id: '1', email: 'mario@test.com', role: 'User' },
    logout: vi.fn().mockResolvedValue(undefined),
  }),
}));

beforeEach(() => mockPush.mockClear());

describe('AppNavbar', () => {
  it('renders all 4 nav links', () => {
    render(<AppNavbar />);
    expect(screen.getByRole('link', { name: 'Dashboard' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Libreria' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Sessioni' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'AI Chat' })).toBeInTheDocument();
  });

  it('highlights active link when pathname matches', () => {
    render(<AppNavbar />);
    const dashboardLink = screen.getByRole('link', { name: 'Dashboard' });
    expect(dashboardLink.className).toContain('bg-secondary');
  });

  it('non-active links have muted-foreground style', () => {
    render(<AppNavbar />);
    const librariaLink = screen.getByRole('link', { name: 'Libreria' });
    expect(librariaLink.className).toContain('text-muted-foreground');
  });

  it('shows user initials in avatar', () => {
    render(<AppNavbar />);
    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('renders MeepleAI logo text', () => {
    render(<AppNavbar />);
    expect(screen.getByText('AI')).toBeInTheDocument();
  });

  it('dropdown is closed by default', () => {
    render(<AppNavbar />);
    expect(screen.queryByText('Profilo')).not.toBeInTheDocument();
    expect(screen.queryByText('Esci')).not.toBeInTheDocument();
  });

  it('clicking avatar opens dropdown with Profilo, Impostazioni, Esci', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    expect(screen.getByText('Profilo')).toBeInTheDocument();
    expect(screen.getByText('Impostazioni')).toBeInTheDocument();
    expect(screen.getByText('Esci')).toBeInTheDocument();
  });

  it('dropdown shows user display name and email', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    expect(screen.getByText('Mario Rossi')).toBeInTheDocument();
    expect(screen.getByText('mario@test.com')).toBeInTheDocument();
  });

  it('clicking Profilo navigates to /profile and closes dropdown', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    fireEvent.click(screen.getByText('Profilo'));
    expect(mockPush).toHaveBeenCalledWith('/profile');
    expect(screen.queryByText('Profilo')).not.toBeInTheDocument();
  });

  it('clicking backdrop closes dropdown', () => {
    render(<AppNavbar />);
    fireEvent.click(screen.getByRole('button', { name: 'Menu utente' }));
    expect(screen.getByText('Esci')).toBeInTheDocument();
    // Click the backdrop (aria-hidden div)
    const backdrop = document.querySelector('[aria-hidden="true"]');
    if (backdrop) fireEvent.click(backdrop);
    expect(screen.queryByText('Esci')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2.3: Verifica test**

```bash
cd apps/web && pnpm test --reporter=verbose src/components/layout/__tests__/AppNavbar.test.tsx
```

Atteso: 10 test PASS (5 originali + 5 nuovi)

- [ ] **Step 2.4: Commit**

```bash
git add apps/web/src/components/layout/AppNavbar.tsx \
        apps/web/src/components/layout/__tests__/AppNavbar.test.tsx
git commit -m "feat(navbar): add avatar dropdown with Profilo, Impostazioni, Esci"
```

---

## Task 3: Crea `WelcomeHero`

**Files:**
- Create: `apps/web/src/components/dashboard/WelcomeHero.tsx`

- [ ] **Step 3.1: Crea il componente**

```tsx
// apps/web/src/components/dashboard/WelcomeHero.tsx
'use client';

import { useRouter } from 'next/navigation';

interface WelcomeHeroProps {
  firstName: string;
}

export function WelcomeHero({ firstName }: WelcomeHeroProps) {
  const router = useRouter();

  return (
    <div
      className="col-span-6 lg:col-span-12 row-span-3 rounded-xl border overflow-hidden p-5 flex items-center gap-5 relative"
      style={{
        background:
          'linear-gradient(135deg, hsl(var(--e-game) / 0.12) 0%, hsl(var(--e-session) / 0.06) 100%)',
        borderColor: 'hsl(var(--e-game) / 0.3)',
      }}
    >
      {/* Cerchio decorativo */}
      <div
        className="absolute -top-10 -right-10 w-40 h-40 rounded-full pointer-events-none"
        style={{ background: 'hsl(var(--e-game) / 0.05)' }}
      />

      {/* Avatar iniziale */}
      <div
        className="w-12 h-12 rounded-full flex items-center justify-center font-quicksand font-extrabold text-xl text-white shrink-0"
        style={{
          background: 'linear-gradient(135deg, hsl(var(--e-player)), hsl(var(--e-game)))',
        }}
      >
        {firstName[0]?.toUpperCase() ?? 'M'}
      </div>

      {/* Testo e azioni */}
      <div className="flex-1 min-w-0 relative z-10">
        <p className="font-quicksand font-extrabold text-lg text-foreground">
          Benvenuto in MeepleAI, {firstName}! 👋
        </p>
        <p className="font-nunito text-sm text-muted-foreground mt-0.5 mb-3">
          Il tuo assistente AI per giochi da tavolo. Inizia aggiungendo i giochi che possiedi.
        </p>
        <div className="flex flex-wrap gap-2">
          <button
            onClick={() => router.push('/library?action=add')}
            className="font-nunito font-bold text-xs text-white px-4 py-2 rounded-full hover:opacity-90 transition-opacity"
            style={{ background: 'hsl(var(--e-game))' }}
          >
            ➕ Aggiungi un gioco
          </button>
          <button
            onClick={() => router.push('/library?tab=catalogo')}
            className="font-nunito font-bold text-xs text-muted-foreground px-4 py-2 rounded-full border border-border hover:bg-muted/30 transition-colors"
          >
            🔍 Sfoglia catalogo
          </button>
          <button
            onClick={() => router.push('/chat/new')}
            className="font-nunito font-bold text-xs text-muted-foreground px-4 py-2 rounded-full border border-border hover:bg-muted/30 transition-colors"
          >
            🤖 Prova l&apos;AI Chat
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 3.2: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori

- [ ] **Step 3.3: Commit**

```bash
git add apps/web/src/components/dashboard/WelcomeHero.tsx
git commit -m "feat(dashboard): add WelcomeHero component for first-run empty state"
```

---

## Task 4: Aggiorna `dashboard-client.tsx` — token CSS + bento + welcome mode

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Questa è la modifica più estesa. Sostituisci l'intero contenuto del file.

- [ ] **Step 4.1: Sostituisci `dashboard-client.tsx`**

```tsx
// apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx
'use client';

/**
 * Dashboard Bento — Layout con 6 widget
 *
 * Desktop: 12-col bento grid con widget variabili
 * - LiveSession (8×2), KPI Stats (4×2)
 * - Library (6×4), Chat (6×4)
 * - Leaderboard (6×3), Trending (6×3)
 *
 * First-run (totalGames=0 && no recent sessions):
 * - WelcomeHero (12×3) al posto di LiveSession+KPI
 * - Library empty CTA, Trending reale, Chat empty CTA, Sessions empty CTA
 */

import { useEffect } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { WelcomeHero } from '@/components/dashboard/WelcomeHero';
import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
import { StatsRow } from '@/components/dashboard/StatsRow';
import type { SessionSummaryDto, TrendingGameDto, UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { cn } from '@/lib/utils';

// ─── Entity color tokens (CSS custom properties) ──────────────────────────────

const C = {
  game: 'hsl(var(--e-game))',
  player: 'hsl(var(--e-player))',
  session: 'hsl(var(--e-session))',
  chat: 'hsl(var(--e-chat))',
  kb: 'hsl(var(--e-kb))',
  event: 'hsl(var(--e-event))',
  success: 'hsl(var(--e-success))',
} as const;

// ─── Helpers ──────────────────────────────────────────────────────────────────

function parsePlayTimeHours(timeSpan: string): string {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  const hours = parseInt(parts[0] ?? '0', 10);
  const minutes = parseInt(parts[1] ?? '0', 10);
  if (hours === 0) return `${minutes}m`;
  return `${hours}h`;
}

// ─── Bento Widget ─────────────────────────────────────────────────────────────

const COL_SPAN: Record<number, string> = {
  2: 'col-span-2',
  3: 'col-span-3',
  4: 'col-span-4',
  6: 'col-span-6',
};
const LG_COL_SPAN: Record<number, string> = {
  2: 'lg:col-span-2',
  3: 'lg:col-span-3',
  4: 'lg:col-span-4',
  6: 'lg:col-span-6',
  8: 'lg:col-span-8',
  12: 'lg:col-span-12',
};
const ROW_SPAN: Record<number, string> = {
  1: 'row-span-1',
  2: 'row-span-2',
  3: 'row-span-3',
  4: 'row-span-4',
  5: 'row-span-5',
  6: 'row-span-6',
};

interface BentoWidgetProps {
  colSpan: number;
  tabletColSpan?: number;
  rowSpan: number;
  accentColor?: string;
  accentBg?: string;
  className?: string;
  children: React.ReactNode;
  onClick?: () => void;
}

function BentoWidget({
  colSpan,
  tabletColSpan,
  rowSpan,
  accentColor,
  accentBg,
  className,
  children,
  onClick,
}: BentoWidgetProps) {
  const tc = tabletColSpan ?? Math.min(colSpan, 6);
  return (
    <div
      className={cn(
        COL_SPAN[tc] ?? `col-span-${tc}`,
        LG_COL_SPAN[colSpan] ?? `lg:col-span-${colSpan}`,
        ROW_SPAN[rowSpan] ?? `row-span-${rowSpan}`,
        'rounded-xl border border-border/60 bg-card overflow-hidden p-3',
        'transition-colors duration-150',
        onClick && 'cursor-pointer hover:bg-muted/30 hover:border-border',
        className
      )}
      style={{
        ...(accentColor ? { borderLeft: `3px solid ${accentColor}` } : {}),
        ...(accentBg ? { background: accentBg } : {}),
      }}
      onClick={onClick}
      {...(onClick
        ? {
            role: 'button' as const,
            tabIndex: 0,
            onKeyDown: (e: React.KeyboardEvent) => {
              if (e.key === 'Enter' || e.key === ' ') onClick();
            },
          }
        : {})}
    >
      {children}
    </div>
  );
}

function WidgetLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="font-nunito text-[9px] uppercase tracking-widest text-muted-foreground/50 mb-1.5">
      {children}
    </p>
  );
}

// ─── Empty state helper ───────────────────────────────────────────────────────

function WidgetEmptyState({
  icon,
  text,
  ctaLabel,
  ctaColor,
  href,
}: {
  icon: string;
  text: string;
  ctaLabel: string;
  ctaColor: string;
  href: string;
}) {
  const router = useRouter();
  return (
    <div className="flex-1 flex flex-col items-center justify-center gap-2 text-center p-4">
      <span className="text-2xl opacity-40">{icon}</span>
      <p className="font-nunito text-[11px] text-muted-foreground leading-snug">{text}</p>
      <button
        onClick={() => router.push(href)}
        className="font-nunito text-[11px] font-bold px-3 py-1 rounded-full border transition-colors hover:opacity-80"
        style={{ borderColor: `${ctaColor}66`, color: ctaColor }}
      >
        {ctaLabel}
      </button>
    </div>
  );
}

// ─── Live Session Widget (8×2) ────────────────────────────────────────────────

function LiveSessionWidget({
  session,
  isLoading,
}: {
  session: SessionSummaryDto | undefined;
  isLoading: boolean;
}) {
  const router = useRouter();

  if (isLoading) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} accentColor={C.success} className="animate-pulse">
        <div className="h-full" />
      </BentoWidget>
    );
  }

  if (!session) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="border-dashed flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base text-foreground">
            Nessuna sessione attiva
          </p>
          <p className="font-nunito text-sm text-muted-foreground mt-0.5">
            Avvia una nuova partita per vederla qui
          </p>
        </div>
        <Link
          href="/sessions/new"
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold font-nunito text-white transition-opacity hover:opacity-90"
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
      accentBg="hsl(var(--e-success) / 0.04)"
      className="flex flex-col justify-between"
      onClick={() => router.push(`/sessions/${session.id}`)}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Sessione Recente</WidgetLabel>
      </div>
      <div className="flex items-center gap-3">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center text-2xl shrink-0 overflow-hidden"
          style={{ background: 'rgba(255,255,255,0.06)' }}
        >
          {session.gameImageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={session.gameImageUrl}
              alt={session.gameName}
              className="w-full h-full object-cover"
            />
          ) : (
            '🎲'
          )}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-extrabold text-base leading-tight truncate">
            {session.gameName}
          </p>
          <p className="font-nunito text-[11px] text-muted-foreground mt-0.5">
            {session.playerCount} giocatori
            {session.winnerName ? ` · Vincitore: ${session.winnerName}` : ''}
          </p>
        </div>
        <span
          className="shrink-0 px-3.5 py-1.5 rounded-lg text-[11px] font-bold font-nunito text-white"
          style={{ background: C.game }}
          aria-hidden="true"
        >
          Vai →
        </span>
      </div>
    </BentoWidget>
  );
}

// ─── KPI Stats Widget (4×2) ───────────────────────────────────────────────────

function KpiStatsWidget({
  stats,
  isLoading,
}: {
  stats: { totalGames: number; monthlyPlays: number; weeklyPlayTime: string; monthlyFavorites: number } | null;
  isLoading: boolean;
}) {
  return (
    <BentoWidget colSpan={4} tabletColSpan={6} rowSpan={2}>
      <WidgetLabel>Le tue statistiche</WidgetLabel>
      {isLoading ? (
        <div className="grid grid-cols-2 gap-2 mt-1">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-8 rounded bg-muted/40 animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-x-3 gap-y-2 mt-1">
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.game }}>
              {stats?.totalGames ?? '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">giochi</p>
          </div>
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.session }}>
              {stats?.monthlyPlays ?? '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">partite/mese</p>
          </div>
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.chat }}>
              {stats ? parsePlayTimeHours(stats.weeklyPlayTime) : '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">ore/sett.</p>
          </div>
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.success }}>
              {stats?.monthlyFavorites ?? '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">preferiti</p>
          </div>
        </div>
      )}
    </BentoWidget>
  );
}

// ─── Library Widget (6×4) ─────────────────────────────────────────────────────

function LibraryWidget({
  games,
  totalCount,
  isLoading,
}: {
  games: UserGameDto[];
  totalCount: number;
  isLoading: boolean;
}) {
  const router = useRouter();

  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      className="flex flex-col gap-0"
      onClick={() => router.push('/library')}
    >
      <WidgetLabel>La Tua Libreria</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 animate-pulse"
            >
              <div className="w-7 h-7 rounded-md bg-muted/60 shrink-0" />
              <div className="flex-1 h-3 rounded bg-muted/60" />
              <div className="w-8 h-3 rounded bg-muted/40" />
            </div>
          ))
        ) : games.length === 0 ? (
          <WidgetEmptyState
            icon="🎲"
            text="La tua libreria è vuota. Aggiungi i giochi che possiedi."
            ctaLabel="+ Aggiungi gioco"
            ctaColor={C.game}
            href="/library?action=add"
          />
        ) : (
          games.slice(0, 6).map(game => (
            <div
              key={game.id}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0 group/row"
              onClick={e => {
                e.stopPropagation();
                router.push(`/library/${game.id}`);
              }}
            >
              {(game.thumbnailUrl ?? game.imageUrl) ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={game.thumbnailUrl ?? game.imageUrl ?? ''}
                  alt={game.title}
                  className="w-7 h-7 rounded-md object-cover shrink-0"
                />
              ) : (
                <div
                  className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center text-sm"
                  style={{ background: 'hsl(var(--e-game) / 0.13)' }}
                >
                  🎲
                </div>
              )}
              <span className="font-quicksand font-semibold text-[11px] flex-1 truncate text-foreground group-hover/row:text-primary transition-colors">
                {game.title}
              </span>
              {game.averageRating !== null && game.averageRating !== undefined && (
                <span
                  className="font-mono text-[9px] font-semibold shrink-0"
                  style={{ color: C.game }}
                >
                  ★ {game.averageRating.toFixed(1)}
                </span>
              )}
            </div>
          ))
        )}
      </div>
      {games.length > 0 && (
        <p className="font-nunito text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>
          Vedi tutti {totalCount} →
        </p>
      )}
    </BentoWidget>
  );
}

// ─── Chat Preview Widget (6×4) ────────────────────────────────────────────────

function ChatPreviewWidget() {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      accentColor={C.chat}
      className="flex flex-col"
      onClick={() => router.push('/chat')}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Chat AI</WidgetLabel>
        <span
          className="text-[9px] font-bold font-nunito rounded-full px-2 py-0.5"
          style={{ background: 'hsl(var(--e-chat) / 0.13)', color: C.chat }}
        >
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col gap-1.5 overflow-hidden justify-end">
        <div
          className="self-end max-w-[80%] rounded-lg px-2.5 py-1.5 text-[11px] font-nunito"
          style={{ background: 'hsl(var(--e-game) / 0.09)', border: '1px solid hsl(var(--e-game) / 0.13)' }}
        >
          Quante strade posso costruire?
        </div>
        <div
          className="self-start max-w-[90%] rounded-lg px-2.5 py-1.5 text-[11px] font-nunito text-muted-foreground"
          style={{
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
        >
          Puoi costruire tutte le strade che vuoi, purché tu abbia le risorse.{' '}
          <span
            className="font-mono text-[8px] rounded px-1 py-0.5 cursor-pointer"
            style={{ background: 'hsl(var(--e-chat) / 0.09)', color: C.chat }}
          >
            p.8
          </span>
        </div>
      </div>
      <div
        className="mt-auto pt-2 flex gap-1.5"
        onClick={e => {
          e.stopPropagation();
          router.push('/chat');
        }}
      >
        <div
          className="flex-1 h-7 rounded-lg flex items-center px-2.5 text-[11px] font-nunito text-muted-foreground/50"
          style={{
            background: 'rgba(255,255,255,0.03)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
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

// ─── Leaderboard Widget (6×3) ─────────────────────────────────────────────────

function LeaderboardWidget({ sessions }: { sessions: SessionSummaryDto[] }) {
  const winners = sessions
    .filter(s => s.winnerName)
    .reduce<Record<string, number>>((acc, s) => {
      const key = s.winnerName ?? '';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

  const sorted = Object.entries(winners)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 4);

  const medals = ['🥇', '🥈', '🥉', '4️⃣'];
  const avatarColors = [C.game, C.player, C.event, C.session];

  return (
    <BentoWidget colSpan={6} rowSpan={3} accentColor={C.event} className="flex flex-col">
      <WidgetLabel>Classifica Gruppo</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {sorted.length === 0 ? (
          <WidgetEmptyState
            icon="🏆"
            text="Gioca partite con amici per vedere la classifica."
            ctaLabel="▶ Nuova sessione"
            ctaColor={C.session}
            href="/sessions/new"
          />
        ) : (
          sorted.map(([name, wins], i) => (
            <div
              key={name}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0"
            >
              <span className="text-sm w-5 text-center shrink-0">{medals[i]}</span>
              <div
                className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0"
                style={{ background: avatarColors[i] }}
              >
                {name[0]?.toUpperCase()}
              </div>
              <span className="flex-1 font-quicksand font-semibold text-[11px] truncate">
                {name}
              </span>
              <span className="font-mono text-[9px] text-muted-foreground shrink-0">
                {wins} vitt.
              </span>
            </div>
          ))
        )}
      </div>
    </BentoWidget>
  );
}

// ─── Trending Widget (6×3) ────────────────────────────────────────────────────

function TrendingWidget({ games, isLoading }: { games: TrendingGameDto[]; isLoading: boolean }) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={3}
      accentColor={C.kb}
      className="flex flex-col"
      onClick={() => router.push('/library?tab=catalogo')}
    >
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      <div className="flex gap-3 mt-1 overflow-hidden flex-1">
        {isLoading
          ? Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="flex flex-col items-center gap-1 shrink-0">
                <div className="w-9 h-12 rounded-md bg-muted/60 animate-pulse" />
                <div className="w-9 h-2 rounded bg-muted/40 animate-pulse" />
              </div>
            ))
          : games.slice(0, 6).map(game => (
              <div
                key={game.gameId}
                className="flex flex-col items-center gap-1 cursor-pointer shrink-0 group/card"
                onClick={e => {
                  e.stopPropagation();
                  router.push(`/games/${game.gameId}`);
                }}
              >
                {game.thumbnailUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={game.thumbnailUrl}
                    alt={game.title}
                    className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all"
                  />
                ) : (
                  <div
                    className="w-9 h-12 rounded-md flex items-center justify-center text-lg"
                    style={{ background: 'rgba(255,255,255,0.06)' }}
                  >
                    🎲
                  </div>
                )}
                <span className="font-quicksand text-[8px] font-bold text-center w-9 truncate">
                  {game.title}
                </span>
              </div>
            ))}
      </div>
    </BentoWidget>
  );
}

// ─── Dashboard Client ─────────────────────────────────────────────────────────

export function DashboardClient() {
  const { user } = useAuth();
  const {
    fetchStats,
    fetchGames,
    fetchRecentSessions,
    fetchTrendingGames,
    updateFilters,
    stats,
    games,
    totalGamesCount,
    recentSessions,
    trendingGames,
    isLoadingStats,
    isLoadingGames,
    isLoadingTrending,
    isLoadingSessions,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(10);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 6, page: 1 });
    fetchGames();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- store actions are stable Zustand references
  }, []);

  const firstName =
    user?.displayName?.split(' ')[0] ?? user?.displayName ?? 'Giocatore';

  // New user: stats loaded, no games, no sessions
  const isNewUser =
    !isLoadingStats &&
    stats !== null &&
    stats.totalGames === 0 &&
    recentSessions.length === 0;

  return (
    <div className="max-w-[1400px] mx-auto px-4 py-6 space-y-4 pb-24">
      {/* Stats row — sempre visibile */}
      <StatsRow />

      {/* Bento grid */}
      <div className="grid grid-cols-6 lg:grid-cols-12 auto-rows-[48px] gap-3">
        {isNewUser ? (
          <>
            {/* Welcome Hero — full width */}
            <WelcomeHero firstName={firstName} />

            {/* Library — empty CTA */}
            <LibraryWidget games={[]} totalCount={0} isLoading={false} />

            {/* Trending — always has real data */}
            <TrendingWidget games={trendingGames} isLoading={isLoadingTrending} />

            {/* Chat — empty CTA */}
            <ChatPreviewWidget />

            {/* Leaderboard — empty CTA */}
            <LeaderboardWidget sessions={[]} />
          </>
        ) : (
          <>
            {/* LiveSession / Sessione recente */}
            <LiveSessionWidget
              session={recentSessions[0]}
              isLoading={isLoadingSessions}
            />

            {/* KPI Stats */}
            <KpiStatsWidget stats={stats} isLoading={isLoadingStats} />

            {/* Library */}
            <LibraryWidget
              games={games}
              totalCount={totalGamesCount}
              isLoading={isLoadingGames}
            />

            {/* Chat */}
            <ChatPreviewWidget />

            {/* Leaderboard */}
            <LeaderboardWidget sessions={recentSessions} />

            {/* Trending */}
            <TrendingWidget games={trendingGames} isLoading={isLoadingTrending} />
          </>
        )}
      </div>

      {/* FAB contestuale */}
      <FloatingActionPill page="dashboard" />
    </div>
  );
}
```

- [ ] **Step 4.2: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori. Se ci sono errori di tipo su `stats`, controllare che `UserStatsDto` abbia `monthlyFavorites: number`. Se manca, usare `stats?.monthlyFavorites ?? 0` e aggiungere il fallback.

- [ ] **Step 4.3: Commit**

```bash
git add apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx
git commit -m "feat(dashboard): activate bento grid with 6 widgets, CSS tokens, welcome mode"
```

---

## Task 5: Aggiungi FAB a Library

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/_content.tsx`

- [ ] **Step 5.1: Aggiungi import e FAB a `LibraryContent`**

In `apps/web/src/app/(authenticated)/library/_content.tsx`, aggiungi l'import:

```tsx
import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
```

Poi in `LibraryContent`, dopo `<AddGameDrawerController />`, aggiungi:

```tsx
<FloatingActionPill page="library" />
```

Il risultato finale della funzione `LibraryContent`:

```tsx
export function LibraryContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab');
  const { drawCard } = useCardHand();

  useEffect(() => {
    drawCard({
      id: 'section-library',
      entity: 'game',
      title: 'Library',
      href: '/library',
    });
  }, [drawCard]);

  return (
    <>
      {tab === 'wishlist' ? (
        <WishlistPageClient />
      ) : tab === 'catalogo' ? (
        <PublicLibraryPageClient />
      ) : (
        <PersonalLibraryPageClient />
      )}
      <AddGameDrawerController />
      <FloatingActionPill page="library" />
    </>
  );
}
```

- [ ] **Step 5.2: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori

- [ ] **Step 5.3: Commit**

```bash
git add apps/web/src/app/(authenticated)/library/_content.tsx
git commit -m "feat(library): add FloatingActionPill"
```

---

## Task 6: Sostituisci mobile FAB con FloatingActionPill in Sessions

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/_content.tsx`

- [ ] **Step 6.1: Modifica `SessionsContent`**

Aggiungi l'import in cima al file:

```tsx
import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
```

Rimuovi le seguenti sezioni dal JSX:

1. Il blocco `<div className="hidden lg:block"><NewSessionCta /></div>` (dentro il tab "active")
2. Il `<Link href="/sessions/new" className="fixed bottom-20 right-4 lg:hidden ..." aria-label="Nuova sessione">` alla fine del return

Aggiungi prima del `</div>` finale del return (a livello fratello del `<div className="space-y-6 container mx-auto px-4 py-6">`):

```tsx
<FloatingActionPill page="sessions" />
```

Il blocco return diventa (solo le sezioni modificate mostrate):

```tsx
return (
  <div className="space-y-6 container mx-auto px-4 py-6 pb-24">
    {/* Header */}
    ...

    {/* Active Tab */}
    {tab === 'active' && (
      <div className="space-y-4">
        {/* In-progress sessions */}
        ...
        {/* Paused sessions */}
        ...
        {/* RIMOSSO: NewSessionCta desktop */}
        {/* Other active sessions */}
        ...
        {/* Empty state */}
        ...
      </div>
    )}

    {/* History Tab */}
    ...

    {/* RIMOSSO: Link mobile FAB */}
    {/* AGGIUNTO: FloatingActionPill */}
    <FloatingActionPill page="sessions" />
  </div>
);
```

Nota: aggiungi anche `pb-24` al div contenitore per evitare che il FAB copra l'ultimo elemento.

- [ ] **Step 6.2: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori

- [ ] **Step 6.3: Commit**

```bash
git add apps/web/src/app/(authenticated)/sessions/_content.tsx
git commit -m "feat(sessions): replace mobile FAB Link with FloatingActionPill"
```

---

## Task 7: Aggiungi FAB a Chat

**Files:**
- Modify: `apps/web/src/app/(chat)/chat/page.tsx`

- [ ] **Step 7.1: Aggiungi import e FAB**

In `apps/web/src/app/(chat)/chat/page.tsx`, aggiungi l'import:

```tsx
import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
```

Nella funzione `ChatListPage`, nel blocco desktop (`hidden lg:flex ...`), aggiungi `<FloatingActionPill page="chat" />` dopo il tag `</div>` che chiude l'intera struttura desktop+mobile. Il componente va a livello del fragment `<>`:

```tsx
return (
  <>
    {/* Mobile */}
    <div className="lg:hidden h-dvh">
      <ChatListMobile />
    </div>

    {/* Desktop */}
    <div className="hidden lg:flex min-h-[calc(100vh-52px)]">
      {/* sidebar ... */}
      {/* main ... */}
    </div>

    {/* FAB — visibile su entrambi desktop e mobile */}
    <FloatingActionPill page="chat" />
  </>
);
```

- [ ] **Step 7.2: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori

- [ ] **Step 7.3: Commit**

```bash
git add apps/web/src/app/(chat)/chat/page.tsx
git commit -m "feat(chat): add FloatingActionPill"
```

---

## Task 8: Verifica finale

- [ ] **Step 8.1: Esegui tutti i test**

```bash
cd apps/web && pnpm test
```

Atteso: tutti i test esistenti PASS, nessuna regressione

- [ ] **Step 8.2: Typecheck completo**

```bash
cd apps/web && pnpm typecheck
```

Atteso: 0 errori

- [ ] **Step 8.3: Lint**

```bash
cd apps/web && pnpm lint
```

Atteso: 0 errori, 0 warning bloccanti

- [ ] **Step 8.4: Commit di chiusura**

```bash
git add -A
git commit -m "chore(dashboard): final typecheck and lint pass — navbar, FAB, bento complete"
```

---

## Checklist criteri di accettazione

- [ ] `AppNavbar` mostra dropdown avatar funzionante con Logout
- [ ] `FloatingActionPill` visibile su Dashboard, Library, Sessions, Chat
- [ ] Desktop: pill centrata con backdrop-blur; Mobile: FAB circle `bottom-20 right-4`
- [ ] Dashboard mostra tutti e 6 i widget con token CSS (`hsl(var(--e-*))`) e font Quicksand/Nunito
- [ ] Nuovo utente (0 giochi, 0 sessioni) vede Welcome Hero + Trending reale
- [ ] Utente con dati vede dashboard completa senza Welcome Hero
- [ ] Nessun colore hardcoded nei file modificati (no `hsl(25,95%,45%)`, no `#111827`)
- [ ] `pnpm typecheck` e `pnpm lint` passano senza errori
