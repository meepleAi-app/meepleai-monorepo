# SP5 Admin F0a — Nav Consolidation (4 gruppi A/B/C/D) + Role Filter — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Estrarre la navigazione admin in una config condivisa tipizzata, riorganizzata nei 4 gruppi A/B/C/D della spec, con un campo `minRole` e un filtro per ruolo; far consumare la config (filtrata) all'`AdminSideDrawer` esistente, senza regressioni.

**Architecture:** Config nav = dati puri (array tipizzato di gruppi/voci con `minRole`). Filtro = funzione pura `filterNavByRole(groups, user)`. L'`AdminSideDrawer` smette di hard-codare le `SECTIONS` inline e consuma la config filtrata via `useCurrentUser()`. Separare dati + funzione pura dal componente li rende testabili in isolamento e riutilizzabili dalla futura sidebar desktop (F0b).

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · Vitest + React Testing Library · lucide-react. Niente nuove dipendenze.

**Spec di riferimento:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` (§4.1 IA 4 gruppi, §4.4 filtro per ruolo).

**Note di contesto (dall'audit/esplorazione):**
- Il tipo FE `UserRole` (`apps/web/src/types/auth.ts`) è `'superadmin' | 'admin' | 'editor' | 'user'` — **non** include `creator` (gap noto R2, fuori scope F0a). `minRole` usa quindi solo questi 4 valori.
- `hasRole(user, requiredRole)` (stesso file) implementa la gerarchia SuperAdmin > Admin > Editor > User e ritorna `false` se `user` è null.
- I redirect di de-duplicazione IA sono **già implementati** in `next.config.js` (Issue #5040): F0a usa i path canonici già attivi (`/admin/ai`, ecc.), non crea redirect.
- F0a include **solo le voci con route già esistenti**. Le schermate non ancora implementate (es. C secrets/emergency/budget, D sandbox/prompts) si aggiungono nelle ondate F3-F6 estendendo la stessa config.

---

## File Structure

- **Create** `apps/web/src/components/layout/admin-nav/admin-nav-config.ts` — tipi `AdminNavItem`/`AdminNavGroup` + costante `ADMIN_NAV_GROUPS` (4 gruppi, voci con route esistenti + `minRole`).
- **Create** `apps/web/src/components/layout/admin-nav/filter-nav-by-role.ts` — funzione pura `filterNavByRole(groups, user)`.
- **Create** `apps/web/src/components/layout/admin-nav/__tests__/filter-nav-by-role.test.ts` — test della funzione pura.
- **Create** `apps/web/src/components/layout/admin-nav/__tests__/admin-nav-config.test.ts` — invarianti della config (id unici, href non vuoti, minRole validi).
- **Modify** `apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx` — consuma `ADMIN_NAV_GROUPS` + `filterNavByRole`, rimuove le `SECTIONS` inline.
- **Create** `apps/web/src/components/layout/AdminSideDrawer/__tests__/AdminSideDrawer.test.tsx` — test di rendering + filtro per ruolo (non esiste oggi).

---

## Task 1: Config nav condivisa (tipi + 4 gruppi)

**Files:**
- Create: `apps/web/src/components/layout/admin-nav/admin-nav-config.ts`
- Test: `apps/web/src/components/layout/admin-nav/__tests__/admin-nav-config.test.ts`

- [ ] **Step 1: Scrivi il test delle invarianti della config**

```typescript
// apps/web/src/components/layout/admin-nav/__tests__/admin-nav-config.test.ts
import { describe, it, expect } from 'vitest';

import { ADMIN_NAV_GROUPS } from '../admin-nav-config';

const VALID_ROLES = ['superadmin', 'admin', 'editor', 'user'];

describe('ADMIN_NAV_GROUPS', () => {
  it('declares exactly the four groups A, B, C, D in order', () => {
    expect(ADMIN_NAV_GROUPS.map(g => g.id)).toEqual(['A', 'B', 'C', 'D']);
  });

  it('every group has a non-empty label and at least one item', () => {
    for (const group of ADMIN_NAV_GROUPS) {
      expect(group.label.length).toBeGreaterThan(0);
      expect(group.items.length).toBeGreaterThan(0);
    }
  });

  it('every item has a non-empty label and an /admin href', () => {
    for (const group of ADMIN_NAV_GROUPS) {
      for (const item of group.items) {
        expect(item.label.length).toBeGreaterThan(0);
        expect(item.href.startsWith('/admin')).toBe(true);
      }
    }
  });

  it('every declared minRole is a valid UserRole', () => {
    for (const group of ADMIN_NAV_GROUPS) {
      for (const item of group.items) {
        if (item.minRole !== undefined) {
          expect(VALID_ROLES).toContain(item.minRole);
        }
      }
    }
  });

  it('has no duplicate hrefs across all groups', () => {
    const hrefs = ADMIN_NAV_GROUPS.flatMap(g => g.items.map(i => i.href));
    expect(new Set(hrefs).size).toBe(hrefs.length);
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/admin-nav/__tests__/admin-nav-config.test.ts`
Expected: FAIL — `Failed to resolve import "../admin-nav-config"` (il modulo non esiste ancora).

- [ ] **Step 3: Crea il modulo di config con tipi e i 4 gruppi**

Mappa solo route già esistenti (verificate). `minRole` omesso = `'admin'` (default applicato dal filtro nel Task 2).

```typescript
// apps/web/src/components/layout/admin-nav/admin-nav-config.ts
import {
  Activity,
  BarChart2,
  Bot,
  BookOpen,
  Database,
  FileSearch,
  Gamepad2,
  Globe,
  LayoutDashboard,
  Mail,
  MonitorCheck,
  Settings,
  Shield,
  Users,
  BellRing,
  UserCheck,
  Wrench,
} from 'lucide-react';
import type { ComponentType } from 'react';

import type { UserRole } from '@/types/auth';

export interface AdminNavItem {
  label: string;
  href: string;
  icon: ComponentType<{ className?: string }>;
  /** Minimum role required to see this item. Defaults to 'admin' when omitted. */
  minRole?: UserRole;
}

export interface AdminNavGroup {
  id: 'A' | 'B' | 'C' | 'D';
  label: string;
  icon: ComponentType<{ className?: string }>;
  items: AdminNavItem[];
}

/**
 * Canonical admin navigation, organised in the 4 groups of the SP5 consolidation
 * spec (A Admin Console / B Power-User Tools / C Platform & Operations /
 * D AI Tooling & Data Quality).
 *
 * Only routes that already exist are listed here. New screens land in waves
 * F3-F6 by extending this array. Paths use the canonical hubs already enforced
 * by next.config.js redirects (Issue #5040).
 */
export const ADMIN_NAV_GROUPS: AdminNavGroup[] = [
  {
    id: 'A',
    label: 'Admin Console',
    icon: LayoutDashboard,
    items: [
      { label: 'Dashboard', href: '/admin/overview', icon: LayoutDashboard },
      { label: 'Activity Feed', href: '/admin/overview/activity', icon: Activity },
      { label: 'System Health', href: '/admin/overview/system', icon: MonitorCheck },
      { label: 'Users', href: '/admin/users', icon: Users },
      { label: 'Content', href: '/admin/content', icon: Gamepad2 },
      { label: 'AI / RAG', href: '/admin/ai', icon: Bot },
      { label: 'Knowledge Base', href: '/admin/knowledge-base', icon: BookOpen },
      { label: 'Catalog Ingestion', href: '/admin/catalog-ingestion', icon: Database },
      { label: 'Config', href: '/admin/config', icon: Settings },
      { label: 'Monitor', href: '/admin/monitor', icon: MonitorCheck },
      { label: 'Notifications', href: '/admin/notifications/compose', icon: BellRing },
    ],
  },
  {
    id: 'B',
    label: 'Power-User Tools',
    icon: Wrench,
    items: [
      { label: 'Giochi', href: '/admin/shared-games/all', icon: Gamepad2 },
      { label: 'Email Templates', href: '/admin/content/email-templates', icon: Mail },
      { label: 'Invitations', href: '/admin/users/invitations', icon: Mail },
      { label: 'Ruoli & Permessi', href: '/admin/users/roles', icon: Shield },
    ],
  },
  {
    id: 'C',
    label: 'Platform & Operations',
    icon: Globe,
    items: [
      { label: 'Providers', href: '/admin/providers', icon: Globe },
      { label: 'Analytics', href: '/admin/analytics', icon: BarChart2 },
      { label: 'Staging Access', href: '/admin/staging-access', icon: Shield, minRole: 'superadmin' },
    ],
  },
  {
    id: 'D',
    label: 'AI Tooling & Data Quality',
    icon: Bot,
    items: [
      { label: 'Agent Definitions', href: '/admin/agents/definitions', icon: Database },
      { label: 'RAG Inspector', href: '/admin/agents/inspector', icon: FileSearch },
      { label: 'RAG Quality', href: '/admin/rag-quality', icon: BarChart2 },
      { label: 'Mechanic Extractor', href: '/admin/knowledge-base/mechanic-extractor', icon: Wrench },
      { label: 'A/B Testing', href: '/admin/agents/ab-testing', icon: BarChart2 },
      { label: 'Agent Usage', href: '/admin/agents/usage', icon: BarChart2 },
      { label: 'Access Requests', href: '/admin/users/access-requests', icon: UserCheck },
    ],
  },
];
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `cd apps/web && pnpm vitest run src/components/layout/admin-nav/__tests__/admin-nav-config.test.ts`
Expected: PASS (5 test).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/admin-nav/admin-nav-config.ts apps/web/src/components/layout/admin-nav/__tests__/admin-nav-config.test.ts
git commit -m "feat(admin): add shared admin nav config (4 groups A/B/C/D)"
```

---

## Task 2: Filtro per ruolo (funzione pura)

**Files:**
- Create: `apps/web/src/components/layout/admin-nav/filter-nav-by-role.ts`
- Test: `apps/web/src/components/layout/admin-nav/__tests__/filter-nav-by-role.test.ts`

- [ ] **Step 1: Scrivi il test della funzione di filtro**

```typescript
// apps/web/src/components/layout/admin-nav/__tests__/filter-nav-by-role.test.ts
import { describe, it, expect } from 'vitest';

import type { AuthUser } from '@/types/auth';

import type { AdminNavGroup } from '../admin-nav-config';
import { filterNavByRole } from '../filter-nav-by-role';

function user(role: string): AuthUser {
  return { id: 'u1', email: 'a@b.c', role };
}

const GROUPS: AdminNavGroup[] = [
  {
    id: 'A',
    label: 'Admin Console',
    icon: () => null,
    items: [
      { label: 'Dashboard', href: '/admin/overview', icon: () => null }, // default admin
      { label: 'Secrets', href: '/admin/secrets', icon: () => null, minRole: 'superadmin' },
    ],
  },
  {
    id: 'B',
    label: 'Editor Tools',
    icon: () => null,
    items: [{ label: 'Editor', href: '/admin/editor', icon: () => null, minRole: 'editor' }],
  },
];

describe('filterNavByRole', () => {
  it('superadmin sees every item', () => {
    const result = filterNavByRole(GROUPS, user('superadmin'));
    const hrefs = result.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toEqual(['/admin/overview', '/admin/secrets', '/admin/editor']);
  });

  it('admin sees admin+editor items but not superadmin-only', () => {
    const result = filterNavByRole(GROUPS, user('admin'));
    const hrefs = result.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toEqual(['/admin/overview', '/admin/editor']);
  });

  it('editor sees only editor item; admin-default item is hidden', () => {
    const result = filterNavByRole(GROUPS, user('editor'));
    const hrefs = result.flatMap(g => g.items.map(i => i.href));
    expect(hrefs).toEqual(['/admin/editor']);
  });

  it('drops groups that become empty after filtering', () => {
    const result = filterNavByRole(GROUPS, user('editor'));
    expect(result.map(g => g.id)).toEqual(['B']);
  });

  it('returns no groups for a null user', () => {
    expect(filterNavByRole(GROUPS, null)).toEqual([]);
  });

  it('treats an item without minRole as requiring admin', () => {
    const result = filterNavByRole(GROUPS, user('user'));
    expect(result).toEqual([]);
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/admin-nav/__tests__/filter-nav-by-role.test.ts`
Expected: FAIL — `Failed to resolve import "../filter-nav-by-role"`.

- [ ] **Step 3: Implementa la funzione pura**

```typescript
// apps/web/src/components/layout/admin-nav/filter-nav-by-role.ts
import type { AuthUser } from '@/types/auth';
import { hasRole } from '@/types/auth';

import type { AdminNavGroup } from './admin-nav-config';

const DEFAULT_MIN_ROLE = 'admin' as const;

/**
 * Returns a copy of `groups` keeping only the items the user is allowed to see
 * (per item `minRole`, defaulting to 'admin'), and dropping groups left empty.
 * Pure: does not mutate the input.
 */
export function filterNavByRole(
  groups: AdminNavGroup[],
  user: AuthUser | null
): AdminNavGroup[] {
  return groups
    .map(group => ({
      ...group,
      items: group.items.filter(item => hasRole(user, item.minRole ?? DEFAULT_MIN_ROLE)),
    }))
    .filter(group => group.items.length > 0);
}
```

- [ ] **Step 4: Esegui il test e verifica che passi**

Run: `cd apps/web && pnpm vitest run src/components/layout/admin-nav/__tests__/filter-nav-by-role.test.ts`
Expected: PASS (6 test).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/admin-nav/filter-nav-by-role.ts apps/web/src/components/layout/admin-nav/__tests__/filter-nav-by-role.test.ts
git commit -m "feat(admin): add role-based nav filter (pure fn)"
```

---

## Task 3: AdminSideDrawer consuma la config filtrata

Sostituisce le `SECTIONS`/`SYSTEM_ITEMS`/`ANALYTICS_ITEMS` inline e la loro resa con i 4 gruppi filtrati per ruolo. Mantiene il pattern visivo esistente (`NavLink`, label di gruppo, active state via `usePathname`). Rimuove i collassabili `CollapsibleSection`/System/Analytics (le voci confluiscono nei 4 gruppi).

**Files:**
- Modify: `apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx`
- Test: `apps/web/src/components/layout/AdminSideDrawer/__tests__/AdminSideDrawer.test.tsx`

- [ ] **Step 1: Scrivi il test del drawer (rendering + filtro ruolo)**

```tsx
// apps/web/src/components/layout/AdminSideDrawer/__tests__/AdminSideDrawer.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { AdminSideDrawer } from '../AdminSideDrawer';

const mockUseCurrentUser = vi.fn();

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/overview',
}));

describe('AdminSideDrawer', () => {
  beforeEach(() => {
    mockUseCurrentUser.mockReset();
  });

  it('renders nothing when closed', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    const { container } = render(<AdminSideDrawer open={false} onClose={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });

  it('renders the four group labels for an admin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.getByText('Admin Console')).toBeInTheDocument();
    expect(screen.getByText('Power-User Tools')).toBeInTheDocument();
    expect(screen.getByText('Platform & Operations')).toBeInTheDocument();
    expect(screen.getByText('AI Tooling & Data Quality')).toBeInTheDocument();
  });

  it('hides superadmin-only items from an admin (Staging Access)', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.queryByRole('link', { name: /Staging Access/i })).not.toBeInTheDocument();
  });

  it('shows superadmin-only items to a superadmin', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'superadmin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.getByRole('link', { name: /Staging Access/i })).toBeInTheDocument();
  });

  it('marks the active route with aria-current', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    render(<AdminSideDrawer open onClose={() => {}} />);
    expect(screen.getByRole('link', { name: /Dashboard/i })).toHaveAttribute('aria-current', 'page');
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminSideDrawer/__tests__/AdminSideDrawer.test.tsx`
Expected: FAIL — il drawer attuale rende "Overview"/"Content"/"AI"/"Users" e mostra tutto, quindi i test sui 4 gruppi e sul filtro Staging Access falliscono.

- [ ] **Step 3: Refactor del drawer per consumare la config filtrata**

Sostituisci l'intero blocco `Nav definitions` (le costanti `SECTIONS`, `SYSTEM_ITEMS`, `ANALYTICS_ITEMS` alle righe 54-116) e la resa delle sezioni dentro `<nav>` (il `.map` di `SECTIONS` + i due blocchi collassabili System/Analytics, righe 328-405). Tieni invariati: l'header utente, l'overlay, il link "Torna all'app", l'helper `isPathActive`, il sub-componente `NavLink`. Rimuovi `CollapsibleSection`, `isSectionAltroActive`, e gli `useState` `systemOpen`/`analyticsOpen` ormai inutilizzati.

3a. In testa al file, aggiungi gli import della config + filtro e rimuovi le icone non più usate inline:

```tsx
import { ADMIN_NAV_GROUPS } from '@/components/layout/admin-nav/admin-nav-config';
import { filterNavByRole } from '@/components/layout/admin-nav/filter-nav-by-role';
```

3b. Dentro `AdminSideDrawer`, dopo `const pathname = usePathname();`, calcola i gruppi visibili e rimuovi gli `useState` dei collassabili:

```tsx
const visibleGroups = filterNavByRole(ADMIN_NAV_GROUPS, user ?? null);
```

3c. Sostituisci il contenuto di `<nav className="flex flex-col gap-0.5">` (dopo il link "Torna all'app" + il `<div className="border-t mb-1" />`) con la resa dei gruppi:

```tsx
{visibleGroups.map(group => (
  <div key={group.id} className="flex flex-col gap-0.5">
    <div className="flex items-center gap-2 px-3 py-1.5 mt-2">
      <group.icon className="h-3.5 w-3.5 text-muted-foreground" />
      <span className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        {group.label}
      </span>
    </div>
    {group.items.map(item => (
      <NavLink key={item.href} item={item} pathname={pathname} onClick={onClose} />
    ))}
  </div>
))}
```

3d. Verifica che `NavItem` (interfaccia locale usata da `NavLink`) sia strutturalmente compatibile con `AdminNavItem`: `NavLink` legge solo `item.icon`, `item.href`, `item.label`. `AdminNavItem` ha quegli stessi campi (più `minRole`, ignorato da `NavLink`), quindi `NavLink` accetta un `AdminNavItem` senza modifiche. Lascia `NavLink` invariato.

- [ ] **Step 4: Esegui i test del drawer e verifica che passino**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminSideDrawer/__tests__/AdminSideDrawer.test.tsx`
Expected: PASS (5 test).

- [ ] **Step 5: Esegui typecheck + lint sui file toccati**

Run: `cd apps/web && pnpm typecheck && pnpm exec eslint src/components/layout/admin-nav src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx`
Expected: nessun errore. In particolare nessun import inutilizzato (le icone rimosse) e nessuna violazione `local/no-hardcoded-color-utility`.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/AdminSideDrawer/AdminSideDrawer.tsx apps/web/src/components/layout/AdminSideDrawer/__tests__/AdminSideDrawer.test.tsx
git commit -m "refactor(admin): drive AdminSideDrawer from shared 4-group nav config + role filter"
```

---

## Self-Review

**1. Spec coverage:** F0a copre §4.1 (4 gruppi A/B/C/D) e §4.4 (filtro per ruolo) limitatamente alle route esistenti. Shell desktop persistente (§4.3) → F0b. Dark scoped (§4.3) → F0c. Redirect (§4.2) → già fatti (#5040). i18n delle label → non in scope F0a (label inline come oggi), da valutare in un'ondata.

**2. Placeholder scan:** nessun TODO/TBD; ogni step ha codice o comando completo; le icone nuove (`Wrench`) sono importate da lucide-react (già dipendenza).

**3. Type consistency:** `AdminNavItem`/`AdminNavGroup` definiti in Task 1, usati identici in Task 2 e 3. `filterNavByRole(groups, user)` firma costante. `hasRole`/`AuthUser`/`UserRole` da `@/types/auth` (firme verificate). `NavLink` invariato consuma `AdminNavItem` (superset strutturale di `NavItem`).

**Nota di rischio:** la rimozione dei collassabili System/Analytics cambia la UX del drawer (tutte le voci ora visibili sotto i 4 gruppi, niente accordion). È intenzionale (consolidamento IA spec §4.1) ma è un cambiamento visibile — segnalare in PR per review del designer.

---

## Execution Handoff

Plan salvato. Sotto-plan successivi della fase Fondamenta: **F0b** (shell sidebar-responsive desktop, riusa `ADMIN_NAV_GROUPS`), **F0c** (dark scoped admin).
