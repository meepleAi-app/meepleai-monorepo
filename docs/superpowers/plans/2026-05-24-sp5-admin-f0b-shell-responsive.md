# SP5 Admin F0b — Shell Sidebar-Responsive (desktop) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere una **sidebar admin persistente su desktop (≥lg)** che riusa la config nav a 4 gruppi di F0a, mantenendo il drawer off-canvas su mobile, estraendo la resa della nav in un componente condiviso `AdminNavList` (DRY tra drawer e sidebar).

**Architecture:** Estrai `NavLink` + `isPathActive` + la resa dei gruppi dal drawer in un componente condiviso `AdminNavList`. Crea `AdminSidebar` (visibile solo ≥lg via `hidden lg:flex`) che filtra `ADMIN_NAV_GROUPS` per ruolo e rende `AdminNavList`. Modifica `AdminShell` per disporre `TopBar` sopra e, sotto, una riga flex `[AdminSidebar | main]`. Il drawer mobile resta invariato (riusa anch'esso `AdminNavList`).

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · Vitest + React Testing Library · lucide-react · Tailwind (breakpoint `lg` = 1024px). Niente nuove dipendenze.

**Depends on:** **F0a** (`apps/web/src/components/layout/admin-nav/admin-nav-config.ts` + `filter-nav-by-role.ts`). Esegui F0a prima.

**Spec di riferimento:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §4.3 (shell ibrida sidebar desktop + drawer mobile).

**Note di contesto (dall'esplorazione):**
- Pattern di sidebar persistente già nel codebase: `CardRack` e `ContextualHandSidebar` usano `hidden md:flex` + larghezza fissa. F0b usa `lg` (1024px) perché la nav admin è più densa.
- `AdminShell` oggi è `flex flex-col` (TopBar + main + drawer overlay).
- Il drawer (`AdminSideDrawer`) dopo F0a rende i 4 gruppi inline; F0b sposta quella resa in `AdminNavList` riusabile.

---

## File Structure

- **Create** `apps/web/src/components/layout/admin-nav/AdminNavList.tsx` — resa condivisa: `NavLink` + `isPathActive` + map dei gruppi. Consumato da drawer (mobile) e sidebar (desktop).
- **Create** `apps/web/src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx` — test resa gruppi + active state.
- **Create** `apps/web/src/components/layout/AdminSidebar/AdminSidebar.tsx` — sidebar desktop persistente (`hidden lg:flex`).
- **Create** `apps/web/src/components/layout/AdminSidebar/__tests__/AdminSidebar.test.tsx` — test resa + filtro ruolo + classe responsive.
- **Modify** `apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx` — usa `AdminNavList` al posto della resa inline (introdotta in F0a).
- **Modify** `apps/web/src/components/layout/AdminShell/AdminShell.tsx` — riga flex `[AdminSidebar | main]` sotto la TopBar.

---

## Task 1: Estrai `AdminNavList` condiviso

Sposta `NavLink`, `isPathActive` e la resa dei gruppi (introdotta in F0a, Task 3, step 3c) in un componente riusabile.

**Files:**
- Create: `apps/web/src/components/layout/admin-nav/AdminNavList.tsx`
- Test: `apps/web/src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx`
- Modify: `apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx`

- [ ] **Step 1: Scrivi il test di `AdminNavList`**

```tsx
// apps/web/src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import type { AdminNavGroup } from '../admin-nav-config';
import { AdminNavList } from '../AdminNavList';

const GROUPS: AdminNavGroup[] = [
  {
    id: 'A',
    label: 'Admin Console',
    icon: () => null,
    items: [{ label: 'Dashboard', href: '/admin/overview', icon: () => null }],
  },
];

describe('AdminNavList', () => {
  it('renders group label and item link', () => {
    render(<AdminNavList groups={GROUPS} pathname="/admin/overview" />);
    expect(screen.getByText('Admin Console')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute('href', '/admin/overview');
  });

  it('marks the active item with aria-current', () => {
    render(<AdminNavList groups={GROUPS} pathname="/admin/overview" />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute('aria-current', 'page');
  });

  it('does not mark a non-active item', () => {
    render(<AdminNavList groups={GROUPS} pathname="/admin/users" />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).not.toHaveAttribute('aria-current');
  });

  it('calls onNavigate when a link is clicked', () => {
    const onNavigate = vi.fn();
    render(<AdminNavList groups={GROUPS} pathname="/admin/users" onNavigate={onNavigate} />);
    screen.getByRole('link', { name: /Dashboard/i }).click();
    expect(onNavigate).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx`
Expected: FAIL — `Failed to resolve import "../AdminNavList"`.

- [ ] **Step 3: Crea `AdminNavList.tsx`**

```tsx
// apps/web/src/components/layout/admin-nav/AdminNavList.tsx
'use client';

import Link from 'next/link';

import type { AdminNavGroup, AdminNavItem } from './admin-nav-config';

export function isPathActive(pathname: string, href: string): boolean {
  const hrefPath = href.split('?')[0];
  return pathname === hrefPath || pathname.startsWith(hrefPath + '/');
}

interface NavLinkProps {
  item: AdminNavItem;
  pathname: string;
  onClick?: () => void;
}

function NavLink({ item, pathname, onClick }: NavLinkProps) {
  const Icon = item.icon;
  const active = isPathActive(pathname, item.href);

  return (
    <Link
      href={item.href}
      onClick={onClick}
      aria-current={active ? 'page' : undefined}
      className={[
        'flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors',
        active
          ? 'bg-[hsla(25,95%,45%,0.12)] text-[hsl(var(--c-game-text))]'
          : 'text-foreground/70 hover:bg-muted hover:text-foreground',
      ].join(' ')}
    >
      <Icon className="h-4 w-4 shrink-0" />
      <span>{item.label}</span>
    </Link>
  );
}

export interface AdminNavListProps {
  groups: AdminNavGroup[];
  pathname: string;
  onNavigate?: () => void;
}

/**
 * Shared rendering of the admin navigation groups. Used by both the mobile
 * drawer (AdminSideDrawer) and the desktop sidebar (AdminSidebar). Receives
 * already-filtered groups; does not read the user/role itself.
 */
export function AdminNavList({ groups, pathname, onNavigate }: AdminNavListProps) {
  return (
    <nav className="flex flex-col gap-0.5">
      {groups.map(group => (
        <div key={group.id} className="flex flex-col gap-0.5">
          <div className="flex items-center gap-2 px-3 py-1.5 mt-2">
            <group.icon className="h-3.5 w-3.5 text-muted-foreground" />
            <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
              {group.label}
            </span>
          </div>
          {group.items.map(item => (
            <NavLink key={item.href} item={item} pathname={pathname} onClick={onNavigate} />
          ))}
        </div>
      ))}
    </nav>
  );
}
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `cd apps/web && pnpm vitest run src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx`
Expected: PASS (4 test).

- [ ] **Step 5: Refactor `AdminSideDrawer` per usare `AdminNavList`**

Nel drawer, importa `AdminNavList`, calcola i gruppi filtrati come in F0a, e sostituisci il `<nav>...</nav>` (che dopo F0a contiene il `.map` dei gruppi inline) con `<AdminNavList groups={visibleGroups} pathname={pathname} onNavigate={onClose} />`. Mantieni il link "Torna all'app" e l'header utente. Rimuovi il `NavLink` locale e `isPathActive` locale (ora importati/usati da `AdminNavList`).

```tsx
// In cima (import):
import { AdminNavList } from '@/components/layout/admin-nav/AdminNavList';

// Dentro il body scrollabile, al posto del <nav> inline:
<div className="flex-1 overflow-y-auto px-3 py-3">
  <Link
    href="/dashboard"
    onClick={onClose}
    className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium transition-colors mb-2"
    style={{ color: 'hsl(var(--c-kb-text))' }}
  >
    <ChevronLeft className="h-4 w-4 shrink-0" />
    <span>Torna all&apos;app</span>
  </Link>
  <div className="border-t mb-1" />
  <AdminNavList groups={visibleGroups} pathname={pathname} onNavigate={onClose} />
</div>
```

- [ ] **Step 6: Esegui i test del drawer (F0a) per verificare nessuna regressione**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminSideDrawer`
Expected: PASS — i test di F0a (4 gruppi, filtro Staging Access, aria-current) passano ancora con la resa estratta.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/layout/admin-nav/AdminNavList.tsx apps/web/src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx
git commit -m "refactor(admin): extract shared AdminNavList from drawer"
```

---

## Task 2: Crea `AdminSidebar` (desktop persistente)

**Files:**
- Create: `apps/web/src/components/layout/AdminSidebar/AdminSidebar.tsx`
- Test: `apps/web/src/components/layout/AdminSidebar/__tests__/AdminSidebar.test.tsx`

- [ ] **Step 1: Scrivi il test di `AdminSidebar`**

```tsx
// apps/web/src/components/layout/AdminSidebar/__tests__/AdminSidebar.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminSidebar } from '../AdminSidebar';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminSidebar', () => {
  beforeEach(() => mockUseCurrentUser.mockReset());

  it('renders the four group labels for an admin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSidebar />);
    expect(screen.getByText('Admin Console')).toBeInTheDocument();
    expect(screen.getByText('AI Tooling & Data Quality')).toBeInTheDocument();
  });

  it('hides superadmin-only items from an admin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSidebar />);
    expect(screen.queryByRole('link', { name: /Staging Access/i })).not.toBeInTheDocument();
  });

  it('is hidden below lg and shown at lg+ (responsive class)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSidebar />);
    const aside = screen.getByRole('navigation', { name: /admin sidebar/i }).closest('aside');
    expect(aside).toHaveClass('hidden');
    expect(aside).toHaveClass('lg:flex');
  });

  it('renders nothing meaningful for a null user (no groups)', () => {
    mockUseCurrentUser.mockReturnValue({ data: null });
    render(<AdminSidebar />);
    expect(screen.queryByText('Admin Console')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminSidebar/__tests__/AdminSidebar.test.tsx`
Expected: FAIL — `Failed to resolve import "../AdminSidebar"`.

- [ ] **Step 3: Crea `AdminSidebar.tsx`**

```tsx
// apps/web/src/components/layout/AdminSidebar/AdminSidebar.tsx
'use client';

import { usePathname } from 'next/navigation';

import { ADMIN_NAV_GROUPS } from '@/components/layout/admin-nav/admin-nav-config';
import { AdminNavList } from '@/components/layout/admin-nav/AdminNavList';
import { filterNavByRole } from '@/components/layout/admin-nav/filter-nav-by-role';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

/**
 * Persistent admin sidebar for desktop (>=lg). On smaller screens it is hidden
 * (`hidden lg:flex`) and navigation is provided by AdminSideDrawer instead.
 */
export function AdminSidebar() {
  const { data: user } = useCurrentUser();
  const pathname = usePathname();
  const visibleGroups = filterNavByRole(ADMIN_NAV_GROUPS, user ?? null);

  return (
    <aside className="hidden lg:flex w-[280px] shrink-0 flex-col border-r bg-background overflow-y-auto">
      <div className="px-3 py-3">
        <AdminNavList
          groups={visibleGroups}
          pathname={pathname}
          // no onNavigate: desktop sidebar stays open, links navigate in place
        />
      </div>
      {/* aria-label so tests/AT can target the persistent nav distinctly */}
      <span className="sr-only" aria-hidden="true" />
    </aside>
  );
}
```

> NOTA: `AdminNavList` rende un `<nav>`. Per soddisfare il test `getByRole('navigation', { name: /admin sidebar/i })`, aggiungi `aria-label="Admin sidebar"` al `<nav>` quando serve. Implementazione: estendi `AdminNavListProps` con un opzionale `ariaLabel?: string` e applicalo a `<nav aria-label={ariaLabel}>`. Aggiorna `AdminNavList.tsx` (Task 1, step 3) di conseguenza e passa `ariaLabel="Admin sidebar"` qui. Il drawer può passare `ariaLabel="Admin navigation"`.

- [ ] **Step 3b: Aggiungi `ariaLabel` a `AdminNavList`**

In `AdminNavList.tsx`, estendi le props e applica:

```tsx
export interface AdminNavListProps {
  groups: AdminNavGroup[];
  pathname: string;
  onNavigate?: () => void;
  ariaLabel?: string;
}

export function AdminNavList({ groups, pathname, onNavigate, ariaLabel }: AdminNavListProps) {
  return (
    <nav aria-label={ariaLabel} className="flex flex-col gap-0.5">
      {/* ...unchanged... */}
    </nav>
  );
}
```

In `AdminSidebar.tsx` passa `ariaLabel="Admin sidebar"`; rimuovi lo `<span className="sr-only" />` segnaposto.

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminSidebar/__tests__/AdminSidebar.test.tsx`
Expected: PASS (4 test).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/AdminSidebar apps/web/src/components/layout/admin-nav/AdminNavList.tsx apps/web/src/components/layout/admin-nav/__tests__/AdminNavList.test.tsx
git commit -m "feat(admin): add persistent desktop AdminSidebar"
```

---

## Task 3: `AdminShell` con riga flex `[sidebar | main]`

**Files:**
- Modify: `apps/web/src/components/layout/AdminShell/AdminShell.tsx`
- Test: `apps/web/src/components/layout/AdminShell/__tests__/AdminShell.test.tsx`

- [ ] **Step 1: Scrivi il test di `AdminShell`**

```tsx
// apps/web/src/components/layout/AdminShell/__tests__/AdminShell.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminShell } from '../AdminShell';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));
vi.mock('next/navigation', () => ({ usePathname: () => '/admin/overview' }));
// Keep TopBar / SearchOverlay / DashboardEngineProvider light for the test:
vi.mock('@/components/layout/UserShell/TopBar', () => ({ TopBar: () => <div data-testid="topbar" /> }));
vi.mock('@/components/layout/SearchOverlay', () => ({ SearchOverlay: () => null }));
vi.mock('@/components/dashboard', () => ({
  DashboardEngineProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe('AdminShell', () => {
  beforeEach(() => mockUseCurrentUser.mockReset());

  it('renders the persistent sidebar and the main content', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminShell><p>page body</p></AdminShell>);
    expect(screen.getByRole('navigation', { name: /admin sidebar/i })).toBeInTheDocument();
    expect(screen.getByText('page body')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminShell/__tests__/AdminShell.test.tsx`
Expected: FAIL — la shell attuale non rende `AdminSidebar` (nessuna `navigation` "Admin sidebar").

- [ ] **Step 3: Modifica `AdminShell.tsx`**

```tsx
'use client';

import { useState, type ReactNode } from 'react';

import { DashboardEngineProvider } from '@/components/dashboard';
import { AdminSidebar } from '@/components/layout/AdminSidebar/AdminSidebar';
import { AdminSideDrawer } from '@/components/layout/AdminSideDrawer/AdminSideDrawer';
import { SearchOverlay } from '@/components/layout/SearchOverlay';
import { TopBar } from '@/components/layout/UserShell/TopBar';

interface AdminShellProps {
  children: ReactNode;
}

export function AdminShell({ children }: AdminShellProps) {
  const [drawerOpen, setDrawerOpen] = useState(false);
  const [searchOpen, setSearchOpen] = useState(false);

  return (
    <div className="flex min-h-dvh flex-col bg-[var(--bg)]">
      <TopBar
        onHamburgerClick={() => setDrawerOpen(true)}
        onSearchClick={() => setSearchOpen(true)}
        adminMode
      />

      <div className="flex flex-1 overflow-hidden">
        <AdminSidebar />
        <main id="main-content" className="flex-1 overflow-y-auto overflow-x-clip">
          <DashboardEngineProvider>{children}</DashboardEngineProvider>
        </main>
      </div>

      <AdminSideDrawer open={drawerOpen} onClose={() => setDrawerOpen(false)} />
      <SearchOverlay open={searchOpen} onClose={() => setSearchOpen(false)} />
    </div>
  );
}
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminShell/__tests__/AdminShell.test.tsx`
Expected: PASS.

- [ ] **Step 5: Typecheck + lint sui file toccati**

Run: `cd apps/web && pnpm typecheck && pnpm exec eslint src/components/layout/admin-nav src/components/layout/AdminSidebar src/components/layout/AdminShell src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx`
Expected: nessun errore.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/AdminShell/AdminShell.tsx apps/web/src/components/layout/AdminShell/__tests__/AdminShell.test.tsx
git commit -m "feat(admin): persistent sidebar on desktop in AdminShell"
```

---

## Self-Review

**1. Spec coverage:** F0b copre §4.3 "shell ibrida sidebar desktop (≥lg) + drawer mobile". Il dark scoped (stesso §4.3) è F0c.

**2. Placeholder scan:** nessun TODO. `AdminNavList`/`AdminSidebar`/`AdminShell` mostrati per intero; il refinement `ariaLabel` è specificato con codice (Task 2 step 3b).

**3. Type consistency:** `AdminNavList` consuma `AdminNavGroup`/`AdminNavItem` (F0a). `filterNavByRole(groups, user)` come F0a. `AdminSidebar` e drawer entrambi: `useCurrentUser` → `filterNavByRole` → `AdminNavList`. `AdminNavListProps.ariaLabel` aggiunto in modo coerente tra Task 1 e Task 2.

**Note di rischio:**
- L'hamburger in `TopBar` (adminMode) resta attivo anche ≥lg: su desktop apre un drawer ridondante rispetto alla sidebar. Innocuo ma migliorabile (nascondere l'hamburger `lg:hidden`) in un follow-up — fuori scope F0b per non toccare `TopBar` condivisa.
- Il test responsive verifica le classi (`hidden`/`lg:flex`), non il rendering reale al breakpoint (jsdom non fa layout). Validazione visiva ≥lg/<lg demandata a review manuale / E2E in un'ondata successiva.

---

## Execution Handoff

Plan F0b salvato. Dipende da F0a. Prossimo plan Fondamenta: **F0c** (dark scoped admin).
