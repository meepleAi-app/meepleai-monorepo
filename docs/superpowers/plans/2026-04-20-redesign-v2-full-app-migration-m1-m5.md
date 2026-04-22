# Redesign V2 — Full App Migration M1-M5 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire **completamente** l'interfaccia consumer (non-admin) con il Design System v1 in `admin-mockups/design_files/`, usando strategia **big-bang** su branch `redesign/v2`, coprendo ~65% dell'app (le aree già mocked). M6-M7 verranno aggiunti dopo le nuove sessioni Claude Design (vedi brief `2026-04-20-claude-design-missing-pages-brief.md`).

**Architecture:** Big-bang migration su `redesign/v2`. Hybrid responsive (`md:` breakpoint 768px): drawer mobile + page desktop. Primitivi in `apps/web/src/components/ui/v2/` — le rotte vecchie restano fino alla sostituzione finale. Tailwind 4 `@theme` in `globals.css` (NON `tailwind.config.js`). Stack: Next.js 16, React 19, Tailwind 4, Zustand 5, React Query 5, Vitest 3, Playwright, MSW, `vaul` mobile, Radix Dialog desktop.

**Tech Stack:** Next.js 16.2.4, React 19.2.4, Tailwind 4, Zustand 5, React Query 5, Vitest 3 + jest-axe, Playwright, MSW, framer-motion 12, vaul 1.x, shadcn/ui Radix Dialog.

**Depends on:** `2026-04-20-redesign-v2-library-pilot-plan.md` (M0 Foundation + M1 Primitivi Library-scoped + M2 Library pilot — da completare prima di M3).

**Excluded from this plan (deferred):**
- Auth / Register / Forgot-password (no mockups)
- Settings (no mockups)
- Notifications (no mockups)
- Public landing / pricing / terms (no mockups)
- Game nights creation + edit (no mockups)
- Play records / history (no mockups)
- Session creation wizard (no mockups)
- Editor / Pipeline / n8n (no mockups)
- Calendar (no mockups)

Queste verranno coperte da M6-M7-M8 dopo le nuove sessioni Claude Design.

---

## Scope Coverage

| Milestone | Aree app | Mockup sorgente |
|---|---|---|
| **M1** (done via pilot) | Foundation tokens + fonts + theme toggle | `tokens.css`, `components.css` |
| **M2** (done via pilot) | Primitivi Library-scoped (EntityChip, EntityPip, EntityCard, Drawer) | `01-screens.html` + `mobile-app.jsx` |
| **M3 Core Screens** | Home feed, Games catalog + detail, Players list + detail, Agents, KB viewer, Chat threads, Events list, Toolkit, Sessions list | `01-screens.html` + `mobile-app.jsx` |
| **M4 Desktop patterns** | Split view, Sidebar+Drawer, Hero+Tabs per 3 aree rappresentative | `02-desktop-patterns.html` |
| **M5 Session Mode live** | Session bottom bar dedicato, timer, scoring, transitions, drawer swipe-between | `03-drawer-variants.html` + mobile session screen |

---

## File Structure

### M3 — Core Screens (estende primitivi M2)

**Primitivi aggiuntivi (shared):**
- Crea: `apps/web/src/components/ui/v2/connection-bar/connection-bar.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/connection-bar/build-connections.ts` (entity-agnostic builder)
- Crea: `apps/web/src/components/ui/v2/recents-bar/recents-bar.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/hub-nav/hub-nav.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/phone-frame/phone-frame.tsx` (dev-only, Storybook)
- Crea: `apps/web/src/components/ui/v2/section-label/section-label.tsx`
- Crea: `apps/web/src/components/ui/v2/stat-tile/stat-tile.tsx`
- Crea: `apps/web/src/components/ui/v2/cover-placeholder/cover-placeholder.tsx`

**Home feed:**
- Crea: `apps/web/src/app/(authenticated)/home/v2/page.tsx`
- Crea: `apps/web/src/components/home/v2/HomeMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/home/v2/HomeDesktop.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/home/v2/ActivityFeed.tsx`

**Games catalog + detail:**
- Crea: `apps/web/src/app/(authenticated)/games/v2/page.tsx`
- Crea: `apps/web/src/app/(authenticated)/games/v2/[id]/page.tsx`
- Crea: `apps/web/src/components/games/v2/GamesCatalogMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/games/v2/GamesCatalogDesktop.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/games/v2/GameDetailMobile.tsx`
- Crea: `apps/web/src/components/games/v2/GameDetailDesktop.tsx`
- Crea: `apps/web/src/components/games/v2/GameGraph.tsx` (relations drawer)

**Players:**
- Crea: `apps/web/src/app/(authenticated)/players/v2/page.tsx`
- Crea: `apps/web/src/app/(authenticated)/players/v2/[id]/page.tsx`
- Crea: `apps/web/src/components/players/v2/PlayerListMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/players/v2/PlayerListDesktop.tsx`
- Crea: `apps/web/src/components/players/v2/PlayerDetailDrawer.tsx`
- Crea: `apps/web/src/components/players/v2/PlayerDetailPage.tsx`

**Agents:**
- Crea: `apps/web/src/app/(authenticated)/agents/v2/page.tsx`
- Crea: `apps/web/src/app/(authenticated)/agents/v2/[id]/page.tsx`
- Crea: `apps/web/src/components/agents/v2/AgentsListMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/agents/v2/AgentCharacterSheetV2.tsx`
- Crea: `apps/web/src/components/agents/v2/AgentChatInline.tsx`

**KB viewer:**
- Crea: `apps/web/src/app/(authenticated)/kb/v2/page.tsx`
- Crea: `apps/web/src/app/(authenticated)/kb/v2/[id]/page.tsx`
- Crea: `apps/web/src/components/kb/v2/KbListMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/kb/v2/KbViewerMobile.tsx`
- Crea: `apps/web/src/components/kb/v2/KbViewerDesktop.tsx`
- Crea: `apps/web/src/components/kb/v2/ChunkCard.tsx`

**Chat threads:**
- Crea: `apps/web/src/app/(authenticated)/chat/v2/page.tsx`
- Crea: `apps/web/src/app/(authenticated)/chat/v2/[threadId]/page.tsx`
- Crea: `apps/web/src/components/chat/v2/ChatListMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/chat/v2/ChatThreadMobile.tsx`
- Crea: `apps/web/src/components/chat/v2/ChatThreadDesktop.tsx`

**Events list:**
- Crea: `apps/web/src/app/(authenticated)/events/v2/page.tsx`
- Crea: `apps/web/src/components/events/v2/EventsListMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/events/v2/EventsListDesktop.tsx`
- Crea: `apps/web/src/components/events/v2/EventCard.tsx`

**Toolkit:**
- Crea: `apps/web/src/app/(authenticated)/toolkit/v2/page.tsx`
- Crea: `apps/web/src/components/toolkit/v2/ToolkitMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/toolkit/v2/ToolkitDesktop.tsx`

**Sessions list:**
- Crea: `apps/web/src/app/(authenticated)/sessions/v2/page.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionsListMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionsListDesktop.tsx`

### M4 — Desktop patterns (riusa core screens)

- Crea: `apps/web/src/components/ui/v2/layouts/SplitViewLayout.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/layouts/SidebarDrawerLayout.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/layouts/HeroTabsLayout.tsx` + `.test.tsx`
- Modifica: `apps/web/src/app/(authenticated)/games/v2/page.tsx` (wraps GamesCatalogDesktop in SplitViewLayout)
- Modifica: `apps/web/src/app/(authenticated)/kb/v2/[id]/page.tsx` (wraps in SidebarDrawerLayout)
- Modifica: `apps/web/src/app/(authenticated)/home/v2/page.tsx` (wraps HomeDesktop in HeroTabsLayout)

### M5 — Session Mode live

- Crea: `apps/web/src/app/(authenticated)/sessions/v2/[id]/page.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionLiveMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionLiveDesktop.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionBottomBar.tsx` (entity-colored, CTA timer/score/next-turn)
- Crea: `apps/web/src/components/sessions/v2/SessionTimer.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionScoring.tsx`
- Crea: `apps/web/src/components/sessions/v2/SessionTransition.tsx`
- Modifica: `apps/web/src/components/ui/v2/drawer/drawer.tsx` (aggiunge swipe-between-entities variant)

### Final cutover — Big-bang rename (task finale)

- Modifica: tutte le rotte `apps/web/src/app/(authenticated)/<area>/page.tsx` → archivia in `page.v1.tsx.bak`
- Rename: `<area>/v2/page.tsx` → `<area>/page.tsx`
- Modifica: redirects in `next.config.mjs` per retrocompatibilità link esterni (`/library/v2` → `/library`)
- Modifica: `apps/web/src/components/MiniNav.tsx` → usa nuovo `hub-nav` component
- Modifica: `apps/web/src/components/MobileBottomBar.tsx` → sostituzione completa
- Crea: `apps/web/e2e/v2-smoke.spec.ts` — Playwright smoke su tutte le rotte migrate

---

## M3 — Core Screens

### Task 8: Connection bar primitive (shared across M3)

**Files:**
- Create: `apps/web/src/components/ui/v2/connection-bar/connection-bar.tsx`
- Create: `apps/web/src/components/ui/v2/connection-bar/connection-bar.test.tsx`
- Create: `apps/web/src/components/ui/v2/connection-bar/build-connections.ts`

- [ ] **Step 1: Write the failing test for `ConnectionBar`**

```tsx
// connection-bar.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { ConnectionBar } from './connection-bar';

describe('ConnectionBar', () => {
  it('renders entity pips with counts', () => {
    const onPipClick = vi.fn();
    render(
      <ConnectionBar
        connections={[
          { entity: 'game', count: 3 },
          { entity: 'session', count: 5 },
          { entity: 'player', count: 0 },
        ]}
        onPipClick={onPipClick}
      />
    );
    expect(screen.getByRole('button', { name: /game.*3/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /session.*5/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /player.*0/i })).toBeDisabled();
  });

  it('calls onPipClick with entity type when clicked', () => {
    const onPipClick = vi.fn();
    render(
      <ConnectionBar
        connections={[{ entity: 'game', count: 3 }]}
        onPipClick={onPipClick}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /game/i }));
    expect(onPipClick).toHaveBeenCalledWith('game');
  });
});
```

- [ ] **Step 2: Run test — expect fail (module not found)**

Run: `pnpm test connection-bar --run`
Expected: FAIL — cannot find module

- [ ] **Step 3: Implement `ConnectionBar`**

```tsx
// connection-bar.tsx
import type { JSX } from 'react';
import type { EntityType } from '../entity-tokens';
import { EntityPip } from '../entity-pip/entity-pip';

export type Connection = {
  entity: EntityType;
  count: number;
};

export type ConnectionBarProps = {
  connections: Connection[];
  onPipClick: (entity: EntityType) => void;
  className?: string;
};

export function ConnectionBar({ connections, onPipClick, className }: ConnectionBarProps): JSX.Element {
  return (
    <div
      className={['flex items-center gap-2 py-2 overflow-x-auto', className].filter(Boolean).join(' ')}
      role="toolbar"
      aria-label="Connessioni entità"
    >
      {connections.map(({ entity, count }) => (
        <EntityPip
          key={entity}
          entity={entity}
          count={count}
          disabled={count === 0}
          onClick={() => onPipClick(entity)}
        />
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect pass**

Run: `pnpm test connection-bar --run`
Expected: PASS 2/2

- [ ] **Step 5: Add `build-connections.ts` entity builder**

```ts
// build-connections.ts
import type { EntityType } from '../entity-tokens';
import type { Connection } from './connection-bar';

export type EntityRelationCounts = Partial<Record<EntityType, number>>;

export function buildConnections(counts: EntityRelationCounts): Connection[] {
  const ordered: EntityType[] = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'];
  return ordered
    .filter((e) => counts[e] !== undefined)
    .map((entity) => ({ entity, count: counts[entity] ?? 0 }));
}
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/connection-bar/
git commit -m "feat(ui-v2): add ConnectionBar primitive with entity pips"
```

---

### Task 9: RecentsBar primitive

**Files:**
- Create: `apps/web/src/components/ui/v2/recents-bar/recents-bar.tsx`
- Create: `apps/web/src/components/ui/v2/recents-bar/recents-bar.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { RecentsBar } from './recents-bar';

describe('RecentsBar', () => {
  it('renders recent entity pills', () => {
    const onClick = vi.fn();
    render(
      <RecentsBar
        items={[
          { id: '1', entity: 'game', label: 'Catan' },
          { id: '2', entity: 'session', label: 'Friday' },
        ]}
        onItemClick={onClick}
      />
    );
    expect(screen.getByRole('button', { name: /catan/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /friday/i })).toBeInTheDocument();
  });

  it('limits to 8 items max', () => {
    const items = Array.from({ length: 12 }, (_, i) => ({
      id: String(i),
      entity: 'game' as const,
      label: `G${i}`,
    }));
    render(<RecentsBar items={items} onItemClick={vi.fn()} />);
    expect(screen.getAllByRole('button')).toHaveLength(8);
  });
});
```

- [ ] **Step 2: Run test — expect fail**

Run: `pnpm test recents-bar --run`
Expected: FAIL

- [ ] **Step 3: Implement `RecentsBar`**

```tsx
// recents-bar.tsx
import type { JSX } from 'react';
import type { EntityType } from '../entity-tokens';
import { EntityChip } from '../entity-chip/entity-chip';

export type RecentItem = {
  id: string;
  entity: EntityType;
  label: string;
};

export type RecentsBarProps = {
  items: RecentItem[];
  onItemClick: (item: RecentItem) => void;
  max?: number;
  className?: string;
};

export function RecentsBar({ items, onItemClick, max = 8, className }: RecentsBarProps): JSX.Element {
  const visible = items.slice(0, max);
  return (
    <div
      className={['flex items-center gap-2 px-4 py-2 overflow-x-auto', className].filter(Boolean).join(' ')}
      role="list"
      aria-label="Entità recenti"
    >
      {visible.map((item) => (
        <button
          key={item.id}
          type="button"
          onClick={() => onItemClick(item)}
          className="flex-shrink-0"
          role="listitem"
        >
          <EntityChip entity={item.entity} label={item.label} />
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect pass**

Run: `pnpm test recents-bar --run`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/v2/recents-bar/
git commit -m "feat(ui-v2): add RecentsBar with max items limit"
```

---

### Task 10: HubNav + SectionLabel + StatTile + CoverPlaceholder

**Files:**
- Create: `apps/web/src/components/ui/v2/hub-nav/hub-nav.tsx` + `.test.tsx`
- Create: `apps/web/src/components/ui/v2/section-label/section-label.tsx`
- Create: `apps/web/src/components/ui/v2/stat-tile/stat-tile.tsx`
- Create: `apps/web/src/components/ui/v2/cover-placeholder/cover-placeholder.tsx`

- [ ] **Step 1: Write HubNav test**

```tsx
import { render, screen } from '@testing-library/react';
import { HubNav } from './hub-nav';

describe('HubNav', () => {
  it('renders brand + nav links + active state', () => {
    render(
      <HubNav
        brandLabel="MeepleAI"
        links={[
          { href: '/home', label: 'Home' },
          { href: '/games', label: 'Games' },
        ]}
        activeHref="/games"
      />
    );
    expect(screen.getByText('MeepleAI')).toBeInTheDocument();
    const gamesLink = screen.getByRole('link', { name: 'Games' });
    expect(gamesLink).toHaveAttribute('aria-current', 'page');
  });
});
```

- [ ] **Step 2: Run fail → Implement → Pass**

Implementation mirrors `00-hub.html:115-124` with Next.js `<Link>` and `aria-current` for accessibility.

```tsx
// hub-nav.tsx
import Link from 'next/link';
import type { JSX } from 'react';

export type HubNavLink = { href: string; label: string };
export type HubNavProps = {
  brandLabel: string;
  links: HubNavLink[];
  activeHref: string;
};

export function HubNav({ brandLabel, links, activeHref }: HubNavProps): JSX.Element {
  return (
    <nav className="sticky top-0 z-20 flex items-center gap-4 border-b border-[var(--border)] bg-[var(--glass-bg)] px-6 py-3 backdrop-blur-lg">
      <div className="flex items-center gap-2 font-display font-bold">
        <span className="flex h-7 w-7 items-center justify-center rounded-lg bg-gradient-to-br from-[hsl(var(--c-game))] to-[hsl(var(--c-event))] text-white">
          M
        </span>
        <span>{brandLabel}</span>
      </div>
      <div className="flex-1" />
      {links.map((link) => {
        const isActive = link.href === activeHref;
        return (
          <Link
            key={link.href}
            href={link.href}
            aria-current={isActive ? 'page' : undefined}
            className={[
              'rounded-md px-3 py-1.5 text-sm font-semibold transition',
              isActive
                ? 'bg-[hsl(var(--c-game)/0.12)] text-[hsl(var(--c-game))]'
                : 'text-[var(--text-sec)] hover:bg-[var(--bg-hover)] hover:text-[var(--text)]',
            ].join(' ')}
          >
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}
```

- [ ] **Step 3: Implement SectionLabel, StatTile, CoverPlaceholder**

Port direct da `components.css:90-95` e `00-hub.html:35-40`. Semplici, no test dedicati (coperti da integration tests).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/v2/hub-nav/ apps/web/src/components/ui/v2/section-label/ apps/web/src/components/ui/v2/stat-tile/ apps/web/src/components/ui/v2/cover-placeholder/
git commit -m "feat(ui-v2): add HubNav, SectionLabel, StatTile, CoverPlaceholder"
```

---

### Task 11: Home feed mobile

**Files:**
- Create: `apps/web/src/app/(authenticated)/home/v2/page.tsx`
- Create: `apps/web/src/components/home/v2/HomeMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/home/v2/ActivityFeed.tsx`

**Reference:** `mobile-app.jsx` Home screen — entity grouping, activity stream, quick actions.

- [ ] **Step 1: Write `HomeMobile.test.tsx`**

```tsx
import { render, screen } from '@testing-library/react';
import { HomeMobile } from './HomeMobile';

const mockActivity = [
  { id: 'a1', kind: 'session-started', entity: 'session' as const, title: 'Catan', time: '2h fa' },
  { id: 'a2', kind: 'agent-reply', entity: 'agent' as const, title: 'Rulebook Agent', time: '1d fa' },
];

describe('HomeMobile', () => {
  it('renders activity feed with entity-colored items', () => {
    render(<HomeMobile activity={mockActivity} onItemClick={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Rulebook Agent')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run fail → Implement**

```tsx
// HomeMobile.tsx
'use client';
import type { JSX } from 'react';
import { RecentsBar } from '@/components/ui/v2/recents-bar/recents-bar';
import { ActivityFeed, type ActivityItem } from './ActivityFeed';
import { useCascadeNavigationStore } from '@/stores/cascade-navigation-store';

export type HomeMobileProps = {
  activity: ActivityItem[];
  onItemClick: (item: ActivityItem) => void;
};

export function HomeMobile({ activity, onItemClick }: HomeMobileProps): JSX.Element {
  const recents = useCascadeNavigationStore((s) => s.recentEntities);
  return (
    <div className="flex min-h-screen flex-col bg-[var(--bg)] text-[var(--text)]">
      <header className="px-6 pt-6">
        <h1 className="font-display text-3xl font-bold">Ciao 👋</h1>
        <p className="text-sm text-[var(--text-sec)]">Cosa giochiamo oggi?</p>
      </header>
      <RecentsBar
        items={recents.map((r) => ({ id: r.id, entity: r.entityType, label: r.label }))}
        onItemClick={(r) => onItemClick({ id: r.id, kind: 'nav', entity: r.entity, title: r.label, time: '' })}
      />
      <ActivityFeed items={activity} onItemClick={onItemClick} />
    </div>
  );
}
```

```tsx
// ActivityFeed.tsx
import type { JSX } from 'react';
import type { EntityType } from '@/components/ui/v2/entity-tokens';
import { EntityChip } from '@/components/ui/v2/entity-chip/entity-chip';

export type ActivityItem = {
  id: string;
  kind: string;
  entity: EntityType;
  title: string;
  time: string;
};

export type ActivityFeedProps = {
  items: ActivityItem[];
  onItemClick: (item: ActivityItem) => void;
};

export function ActivityFeed({ items, onItemClick }: ActivityFeedProps): JSX.Element {
  return (
    <ul className="flex flex-col gap-3 px-6 pb-24" role="list">
      {items.map((item) => (
        <li key={item.id}>
          <button
            type="button"
            onClick={() => onItemClick(item)}
            className="flex w-full items-center gap-3 rounded-xl border border-[var(--border)] bg-[var(--bg-card)] p-3 text-left transition hover:border-[hsl(var(--e)/0.4)]"
            data-entity={item.entity}
          >
            <EntityChip entity={item.entity} label={item.kind} />
            <div className="flex-1">
              <div className="font-display font-semibold">{item.title}</div>
              <div className="font-mono text-xs text-[var(--text-muted)]">{item.time}</div>
            </div>
          </button>
        </li>
      ))}
    </ul>
  );
}
```

- [ ] **Step 3: Implement `page.tsx`**

```tsx
// page.tsx
import { HomeMobile } from '@/components/home/v2/HomeMobile';
// TODO: fetch activity via React Query (refer a existing activity endpoint)

export default function HomeV2Page() {
  // Placeholder: wire to real API in separate task
  return <HomeMobile activity={[]} onItemClick={() => {}} />;
}
```

- [ ] **Step 4: Run test → PASS**

Run: `pnpm test HomeMobile --run`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/home/v2/ apps/web/src/components/home/v2/
git commit -m "feat(home-v2): add mobile home feed with activity stream"
```

---

### Task 12: Home feed desktop + wire real API

**Files:**
- Create: `apps/web/src/components/home/v2/HomeDesktop.tsx` + `.test.tsx`
- Modifica: `apps/web/src/app/(authenticated)/home/v2/page.tsx` (responsive switch + React Query)

- [ ] **Step 1: Write `HomeDesktop.test.tsx`**

```tsx
import { render, screen } from '@testing-library/react';
import { HomeDesktop } from './HomeDesktop';

describe('HomeDesktop', () => {
  it('renders hero + activity grid', () => {
    render(<HomeDesktop activity={[]} stats={{ gamesPlayed: 12, sessionsThisMonth: 3 }} onItemClick={vi.fn()} />);
    expect(screen.getByText(/12/)).toBeInTheDocument();
    expect(screen.getByText(/3/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement HomeDesktop with StatTile row + grid + HubNav**

```tsx
'use client';
import type { JSX } from 'react';
import { HubNav } from '@/components/ui/v2/hub-nav/hub-nav';
import { StatTile } from '@/components/ui/v2/stat-tile/stat-tile';
import { ActivityFeed, type ActivityItem } from './ActivityFeed';

export type HomeDesktopProps = {
  activity: ActivityItem[];
  stats: { gamesPlayed: number; sessionsThisMonth: number };
  onItemClick: (item: ActivityItem) => void;
};

export function HomeDesktop({ activity, stats, onItemClick }: HomeDesktopProps): JSX.Element {
  return (
    <div className="min-h-screen bg-[var(--bg)] text-[var(--text)]">
      <HubNav
        brandLabel="MeepleAI"
        links={[
          { href: '/home', label: 'Home' },
          { href: '/games', label: 'Games' },
          { href: '/sessions', label: 'Sessions' },
          { href: '/agents', label: 'Agents' },
        ]}
        activeHref="/home"
      />
      <section className="mx-auto max-w-[1400px] px-6 pt-12">
        <p className="font-mono text-xs uppercase tracking-wider text-[var(--text-muted)]">Benvenuto</p>
        <h1 className="font-display text-5xl font-bold tracking-tight">Il tuo tavolo, oggi</h1>
      </section>
      <div className="mx-auto grid max-w-[1400px] grid-cols-2 gap-4 px-6 pt-8 md:grid-cols-4">
        <StatTile value={stats.gamesPlayed} label="Giochi giocati" accent="game" />
        <StatTile value={stats.sessionsThisMonth} label="Sessioni questo mese" accent="session" />
      </div>
      <section className="mx-auto max-w-[1400px] px-6 pt-10 pb-20">
        <ActivityFeed items={activity} onItemClick={onItemClick} />
      </section>
    </div>
  );
}
```

- [ ] **Step 3: Wire real API in page.tsx (responsive)**

```tsx
'use client';
import { useQuery } from '@tanstack/react-query';
import { HomeMobile } from '@/components/home/v2/HomeMobile';
import { HomeDesktop } from '@/components/home/v2/HomeDesktop';
import { useMediaQuery } from '@/hooks/useMediaQuery';

export default function HomeV2Page() {
  const isDesktop = useMediaQuery('(min-width: 768px)');
  const { data } = useQuery({
    queryKey: ['home', 'activity'],
    queryFn: async () => {
      const res = await fetch('/api/v1/home/activity');
      if (!res.ok) throw new Error('Failed');
      return res.json();
    },
  });

  if (isDesktop) {
    return <HomeDesktop activity={data?.activity ?? []} stats={data?.stats ?? { gamesPlayed: 0, sessionsThisMonth: 0 }} onItemClick={() => {}} />;
  }
  return <HomeMobile activity={data?.activity ?? []} onItemClick={() => {}} />;
}
```

- [ ] **Step 4: Run tests → PASS**

Run: `pnpm test home/v2 --run`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/home/v2/page.tsx apps/web/src/components/home/v2/HomeDesktop.tsx
git commit -m "feat(home-v2): add desktop layout with stats + responsive switch"
```

---

### Task 13: Games catalog (mobile + desktop + detail)

**Files:**
- Create: `apps/web/src/app/(authenticated)/games/v2/page.tsx`
- Create: `apps/web/src/app/(authenticated)/games/v2/[id]/page.tsx`
- Create: `apps/web/src/components/games/v2/GamesCatalogMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/games/v2/GamesCatalogDesktop.tsx` + `.test.tsx`
- Create: `apps/web/src/components/games/v2/GameDetailMobile.tsx`
- Create: `apps/web/src/components/games/v2/GameDetailDesktop.tsx`
- Create: `apps/web/src/components/games/v2/GameGraph.tsx`

Pattern: segue M2 Library ma applicato a catalog (niente ownership — tutti i giochi community).

- [ ] **Step 1: Catalog mobile — test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { GamesCatalogMobile } from './GamesCatalogMobile';

const games = [
  { id: '1', title: 'Catan', players: '3-4', duration: '60min', cover: null },
  { id: '2', title: 'Gloomhaven', players: '1-4', duration: '120min', cover: null },
];

describe('GamesCatalogMobile', () => {
  it('renders grid of game cards', () => {
    render(<GamesCatalogMobile games={games} onGameClick={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
  });

  it('filters games by search', () => {
    render(<GamesCatalogMobile games={games} onGameClick={vi.fn()} />);
    fireEvent.change(screen.getByRole('searchbox'), { target: { value: 'catan' } });
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.queryByText('Gloomhaven')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run fail → Implement mobile + desktop + detail pages → Run pass**

Detail screens usano `ConnectionBar` per pip (players, sessions, kb, agents) + pattern drawer mobile / split-view desktop.

```tsx
// GameDetailDesktop.tsx (estratto chiave)
<div className="grid grid-cols-[320px_1fr] gap-8">
  <aside>
    <CoverPlaceholder gameTitle={game.title} entity="game" />
    <ConnectionBar
      connections={buildConnections({
        player: game.playersCount,
        session: game.sessionsCount,
        kb: game.kbCount,
        agent: game.agentsCount,
      })}
      onPipClick={(e) => openDeckStack(e, game.id)}
    />
  </aside>
  <main>
    <h1 className="font-display text-4xl font-bold">{game.title}</h1>
    <p className="text-[var(--text-sec)]">{game.description}</p>
    {/* Tabs: Overview / Sessions / KB / Agents */}
  </main>
</div>
```

- [ ] **Step 3: GameGraph component (relations drawer)**

Renderizza connessioni entity (games, players, sessions) come graph inline. Usa `<canvas>` o SVG semplice — niente libs pesanti.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/games/v2/ apps/web/src/components/games/v2/
git commit -m "feat(games-v2): add catalog mobile+desktop + detail + relations graph"
```

---

### Task 14: Players list + detail

**Files:**
- Create: `apps/web/src/app/(authenticated)/players/v2/page.tsx`
- Create: `apps/web/src/app/(authenticated)/players/v2/[id]/page.tsx`
- Create: `apps/web/src/components/players/v2/PlayerListMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/players/v2/PlayerListDesktop.tsx`
- Create: `apps/web/src/components/players/v2/PlayerDetailDrawer.tsx`
- Create: `apps/web/src/components/players/v2/PlayerDetailPage.tsx`

Pattern: list responsive (mobile card stack, desktop split-view), detail come drawer mobile / page desktop. Entity color `--c-player`.

- [ ] **Step 1-5: Follow TDD pattern as Task 13**

Commit finale:
```bash
git commit -m "feat(players-v2): add list + detail with player entity accent"
```

---

### Task 15: Agents list + character sheet + inline chat

**Files:**
- Create: `apps/web/src/app/(authenticated)/agents/v2/page.tsx`
- Create: `apps/web/src/app/(authenticated)/agents/v2/[id]/page.tsx`
- Create: `apps/web/src/components/agents/v2/AgentsListMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/agents/v2/AgentCharacterSheetV2.tsx`
- Create: `apps/web/src/components/agents/v2/AgentChatInline.tsx`

Riusa pattern `SessionChatWidget` (S3 session inline) ma con entity color `--c-agent`. `AgentCharacterSheetV2` mostra: avatar, role, KB sources pip, recent threads pip, "Parla con agente" CTA.

- [ ] **Step 1-5: Follow TDD pattern**

Commit:
```bash
git commit -m "feat(agents-v2): add list + character sheet + inline chat"
```

---

### Task 16: KB viewer list + detail

**Files:**
- Create: `apps/web/src/app/(authenticated)/kb/v2/page.tsx`
- Create: `apps/web/src/app/(authenticated)/kb/v2/[id]/page.tsx`
- Create: `apps/web/src/components/kb/v2/KbListMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/kb/v2/KbViewerMobile.tsx`
- Create: `apps/web/src/components/kb/v2/KbViewerDesktop.tsx`
- Create: `apps/web/src/components/kb/v2/ChunkCard.tsx`

Entity color `--c-kb`. KbViewerDesktop usa Sidebar+Drawer pattern (chunks list a sx, chunk detail a dx). ChunkCard mostra source page + snippet + highlight.

- [ ] **Step 1-5: Follow TDD pattern**

Commit:
```bash
git commit -m "feat(kb-v2): add list + viewer with chunk detail"
```

---

### Task 17: Chat threads list + thread view

**Files:**
- Create: `apps/web/src/app/(authenticated)/chat/v2/page.tsx`
- Create: `apps/web/src/app/(authenticated)/chat/v2/[threadId]/page.tsx`
- Create: `apps/web/src/components/chat/v2/ChatListMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/chat/v2/ChatThreadMobile.tsx`
- Create: `apps/web/src/components/chat/v2/ChatThreadDesktop.tsx`

Entity color `--c-chat`. Thread view ha message bubbles entity-colored per sender type (user = neutral, agent = `--c-agent`, system = `--c-kb`). Input bar sticky bottom.

- [ ] **Step 1-5: Follow TDD pattern**

Commit:
```bash
git commit -m "feat(chat-v2): add threads list + thread view with entity bubbles"
```

---

### Task 18: Events list + Toolkit + Sessions list

**Files:**
- Create: `apps/web/src/app/(authenticated)/events/v2/page.tsx`
- Create: `apps/web/src/components/events/v2/EventsListMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/events/v2/EventsListDesktop.tsx`
- Create: `apps/web/src/components/events/v2/EventCard.tsx`
- Create: `apps/web/src/app/(authenticated)/toolkit/v2/page.tsx`
- Create: `apps/web/src/components/toolkit/v2/ToolkitMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/toolkit/v2/ToolkitDesktop.tsx`
- Create: `apps/web/src/app/(authenticated)/sessions/v2/page.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionsListMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionsListDesktop.tsx`

Tre liste parallele con stesso pattern. Entity colors: `--c-event`, `--c-toolkit`, `--c-session`.

- [ ] **Step 1-5: Follow TDD pattern per ogni area, commit separati**

```bash
git commit -m "feat(events-v2): add list mobile+desktop"
git commit -m "feat(toolkit-v2): add list mobile+desktop"
git commit -m "feat(sessions-v2): add list mobile+desktop"
```

---

## M4 — Desktop patterns

### Task 19: Layout primitives (SplitView, SidebarDrawer, HeroTabs)

**Files:**
- Create: `apps/web/src/components/ui/v2/layouts/SplitViewLayout.tsx` + `.test.tsx`
- Create: `apps/web/src/components/ui/v2/layouts/SidebarDrawerLayout.tsx` + `.test.tsx`
- Create: `apps/web/src/components/ui/v2/layouts/HeroTabsLayout.tsx` + `.test.tsx`

**Reference:** `02-desktop-patterns.html` — 3 esempi canonici.

- [ ] **Step 1: SplitViewLayout test**

```tsx
import { render, screen } from '@testing-library/react';
import { SplitViewLayout } from './SplitViewLayout';

describe('SplitViewLayout', () => {
  it('renders list on left and detail on right', () => {
    render(
      <SplitViewLayout
        list={<div>List</div>}
        detail={<div>Detail</div>}
      />
    );
    expect(screen.getByText('List')).toBeInTheDocument();
    expect(screen.getByText('Detail')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement SplitViewLayout**

```tsx
import type { JSX, ReactNode } from 'react';

export type SplitViewLayoutProps = {
  list: ReactNode;
  detail: ReactNode;
  listWidth?: string;
};

export function SplitViewLayout({ list, detail, listWidth = '360px' }: SplitViewLayoutProps): JSX.Element {
  return (
    <div className="grid min-h-[calc(100vh-60px)] grid-cols-[var(--list-w)_1fr]" style={{ ['--list-w' as string]: listWidth }}>
      <aside className="border-r border-[var(--border)] overflow-y-auto">{list}</aside>
      <main className="overflow-y-auto">{detail}</main>
    </div>
  );
}
```

- [ ] **Step 3: SidebarDrawerLayout (Notion-style)**

```tsx
export function SidebarDrawerLayout({ sidebar, main, drawer }: {
  sidebar: ReactNode;
  main: ReactNode;
  drawer?: ReactNode;
}): JSX.Element {
  return (
    <div className="grid min-h-screen grid-cols-[240px_1fr_auto]">
      <aside className="border-r border-[var(--border)]">{sidebar}</aside>
      <main className="overflow-y-auto">{main}</main>
      {drawer && <aside className="w-[360px] border-l border-[var(--border)]">{drawer}</aside>}
    </div>
  );
}
```

- [ ] **Step 4: HeroTabsLayout (Vercel-style)**

```tsx
export function HeroTabsLayout({ hero, tabs, content }: {
  hero: ReactNode;
  tabs: ReactNode;
  content: ReactNode;
}): JSX.Element {
  return (
    <div className="min-h-screen">
      <section className="border-b border-[var(--border)] px-6 py-8">{hero}</section>
      <nav className="sticky top-0 z-10 border-b border-[var(--border)] bg-[var(--glass-bg)] backdrop-blur-lg">{tabs}</nav>
      <section className="px-6 py-6">{content}</section>
    </div>
  );
}
```

- [ ] **Step 5: Apply to 3 areas**

```bash
# Modify pages to wrap in layouts
# games/v2/page.tsx → SplitViewLayout
# kb/v2/[id]/page.tsx → SidebarDrawerLayout
# home/v2/page.tsx → HeroTabsLayout (desktop variant)
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/layouts/
git commit -m "feat(ui-v2): add SplitView, SidebarDrawer, HeroTabs layouts + apply to 3 areas"
```

---

## M5 — Session Mode live

### Task 20: SessionBottomBar + SessionTimer + SessionScoring

**Files:**
- Create: `apps/web/src/components/sessions/v2/SessionBottomBar.tsx` + `.test.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionTimer.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionScoring.tsx`

- [ ] **Step 1: SessionBottomBar test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { SessionBottomBar } from './SessionBottomBar';

describe('SessionBottomBar', () => {
  it('renders timer + score + next-turn CTA', () => {
    const onNextTurn = vi.fn();
    render(
      <SessionBottomBar
        elapsedSeconds={120}
        currentScore={15}
        onNextTurn={onNextTurn}
        onOpenTimer={vi.fn()}
        onOpenScoring={vi.fn()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /turno successivo/i }));
    expect(onNextTurn).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Implement SessionBottomBar**

```tsx
'use client';
import type { JSX } from 'react';
import { formatDuration } from '@/lib/format-duration';

export type SessionBottomBarProps = {
  elapsedSeconds: number;
  currentScore: number;
  onNextTurn: () => void;
  onOpenTimer: () => void;
  onOpenScoring: () => void;
};

export function SessionBottomBar({
  elapsedSeconds,
  currentScore,
  onNextTurn,
  onOpenTimer,
  onOpenScoring,
}: SessionBottomBarProps): JSX.Element {
  return (
    <div
      className="fixed inset-x-0 bottom-0 z-40 flex items-center gap-3 border-t border-[var(--border)] bg-[var(--bg-card)] px-4 py-3 shadow-drawer"
      role="toolbar"
      aria-label="Session controls"
      data-session-mode="live"
    >
      <button
        type="button"
        onClick={onOpenTimer}
        className="flex flex-col items-start rounded-md px-3 py-1.5 hover:bg-[var(--bg-hover)]"
      >
        <span className="font-mono text-xs text-[var(--text-muted)]">TIMER</span>
        <span className="font-display font-bold">{formatDuration(elapsedSeconds)}</span>
      </button>
      <button
        type="button"
        onClick={onOpenScoring}
        className="flex flex-col items-start rounded-md px-3 py-1.5 hover:bg-[var(--bg-hover)]"
      >
        <span className="font-mono text-xs text-[var(--text-muted)]">SCORE</span>
        <span className="font-display font-bold">{currentScore}</span>
      </button>
      <div className="flex-1" />
      <button
        type="button"
        onClick={onNextTurn}
        className="rounded-md bg-[hsl(var(--c-session))] px-5 py-2 font-display font-bold text-white hover:brightness-110"
      >
        Turno successivo →
      </button>
    </div>
  );
}
```

- [ ] **Step 3: SessionTimer + SessionScoring components**

Timer: `useEffect` interval, start/pause/reset. Scoring: input matrix per player con delta +/-.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/sessions/v2/
git commit -m "feat(session-live): add bottom bar + timer + scoring components"
```

---

### Task 21: SessionLiveMobile + SessionLiveDesktop + SessionTransition

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/v2/[id]/page.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionLiveMobile.tsx` + `.test.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionLiveDesktop.tsx`
- Create: `apps/web/src/components/sessions/v2/SessionTransition.tsx`

Mobile: phone-frame con SessionBottomBar fisso, drawer sopra per scoring/timer detail. Desktop: split-view session panel + connection bar players/agents/kb.

- [ ] **Step 1-5: Follow TDD pattern**

```bash
git commit -m "feat(session-live): add mobile + desktop live views + transition dialog"
```

---

### Task 22: Drawer swipe-between-entities variant

**Files:**
- Modifica: `apps/web/src/components/ui/v2/drawer/drawer.tsx` (aggiunge `swipeable` prop + gesture)
- Create: `apps/web/src/components/ui/v2/drawer/drawer-swipe.test.tsx`

**Reference:** `03-drawer-variants.html` — swipe-between-entities pattern.

- [ ] **Step 1: Write swipe test**

```tsx
import { render, fireEvent, screen } from '@testing-library/react';
import { Drawer } from './drawer';

describe('Drawer swipe-between-entities', () => {
  it('advances to next entity on swipe-left', async () => {
    const onNext = vi.fn();
    render(
      <Drawer open swipeable onSwipeLeft={onNext} onClose={vi.fn()}>
        <div data-testid="content">Current</div>
      </Drawer>
    );
    // Simulate swipe via pointer events (vaul pattern)
    const content = screen.getByTestId('content');
    fireEvent.pointerDown(content, { clientX: 300, clientY: 400 });
    fireEvent.pointerMove(content, { clientX: 50, clientY: 400 });
    fireEvent.pointerUp(content, { clientX: 50, clientY: 400 });
    expect(onNext).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Extend Drawer with gesture hook**

Usa `vaul` built-in drag handlers o `@use-gesture/react` se preferito. Soglia swipe: 100px horizontal delta.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/v2/drawer/
git commit -m "feat(ui-v2): add swipe-between-entities variant to Drawer"
```

---

## Final cutover — Big-bang rename

### Task 23: Rename v2 routes to primary + archive v1

**Files:**
- Rename: `apps/web/src/app/(authenticated)/<area>/v2/page.tsx` → `apps/web/src/app/(authenticated)/<area>/page.tsx`
- Archive: `apps/web/src/app/(authenticated)/<area>/page.tsx` → `apps/web/src/app/(authenticated)/<area>/page.v1.tsx.bak`
- Modifica: `apps/web/src/components/MiniNav.tsx` → usa `HubNav`
- Modifica: `apps/web/src/components/MobileBottomBar.tsx` → sostituzione con v2 pattern
- Modifica: `next.config.mjs` → redirect rules per `/library/v2` → `/library`

- [ ] **Step 1: Dry-run rename script**

```bash
cd apps/web
node scripts/v2-cutover.js --dry-run
```

Script da creare in `scripts/v2-cutover.js`: enumera rotte migrate, genera lista rename, verifica che `v2/page.tsx` esista per ogni area.

- [ ] **Step 2: Apply cutover**

```bash
node scripts/v2-cutover.js --apply
```

- [ ] **Step 3: Update imports + remove .bak placeholder routes**

Grep/replace import paths se cambiano. Le `.bak` restano in git per reference; non vengono servite (Next.js ignora `.bak`).

- [ ] **Step 4: Full test suite**

```bash
pnpm typecheck
pnpm lint
pnpm test
pnpm test:e2e --grep "v2-smoke"
```

Expected: tutto verde.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(redesign-v2): big-bang cutover — v2 diventa primary, v1 archiviato"
```

---

### Task 24: E2E smoke test + bundle size check

**Files:**
- Create: `apps/web/e2e/v2-smoke.spec.ts`
- Modifica: `apps/web/bundle-size-baseline.json` (nuova baseline post-cutover)

- [ ] **Step 1: E2E smoke test**

```ts
// v2-smoke.spec.ts
import { test, expect } from '@playwright/test';

const routes = [
  '/home', '/games', '/library', '/players', '/agents',
  '/kb', '/chat', '/events', '/toolkit', '/sessions',
];

for (const route of routes) {
  test(`smoke: ${route} renders`, async ({ page }) => {
    await page.goto(route);
    await expect(page.locator('body')).toBeVisible();
    await expect(page.locator('nav')).toBeVisible();
    // Verify v2 design tokens applied
    const bg = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
    expect(bg).toMatch(/rgb\(247, 243, 238\)|rgb\(20, 16, 10\)/); // light or dark
  });
}
```

- [ ] **Step 2: Bundle size check**

```bash
cd apps/web && pnpm build
node scripts/check-bundle-size.js
```

Update `bundle-size-baseline.json` se incremento accettabile (<15% vs pre-v2).

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/v2-smoke.spec.ts apps/web/bundle-size-baseline.json
git commit -m "test(redesign-v2): add smoke E2E + update bundle baseline post-cutover"
```

---

## Milestone completion checklist

### M3 complete when:
- [ ] 9 aree core migrate (home, games, players, agents, kb, chat, events, toolkit, sessions)
- [ ] ConnectionBar + RecentsBar + HubNav primitivi usati consistentemente
- [ ] Tutti i test unit + component passano
- [ ] Visual review su mobile 380px + desktop 1400px per ogni area

### M4 complete when:
- [ ] 3 layout primitives implementati + applicati a 3 aree rappresentative
- [ ] Desktop responsive review completato
- [ ] A11y audit: keyboard nav + screen reader labels

### M5 complete when:
- [ ] Session bottom bar funzionante con timer reale + scoring persistito
- [ ] Drawer swipe-between-entities testato su touch device
- [ ] Session transition dialog copre: next-game, end-session, save-draft

### Cutover complete when:
- [ ] `pnpm typecheck && pnpm lint && pnpm test && pnpm test:e2e` tutto verde
- [ ] E2E smoke su 10 rotte primary passa
- [ ] Bundle size entro soglia (<15% incremento)
- [ ] Zero warning console in dev/prod build
- [ ] PR da `redesign/v2` → `main-dev` aperta con summary M1-M5

---

## Out-of-scope (M6-M7, post-Claude-Design)

Queste aree NON sono in questo plan. Verranno affrontate in plan separati dopo consegna mockup (vedi `docs/superpowers/specs/2026-04-20-claude-design-missing-pages-brief.md`):

- **M6**: Auth flow, Settings, Notifications, Public pages
- **M7**: Game nights create/edit, Play records, Session creation wizard
- **M8 (optional)**: Editor/Pipeline, Calendar

## Risk register

| Rischio | Mitigazione |
|---|---|
| Big-bang rompe flussi utente | Cutover solo dopo M1-M5 completi + smoke E2E verde + manual QA 30min |
| Bundle size esplode | Check ogni milestone, lazy-load layouts non-critical |
| Desktop pattern coexistence | SplitView/Sidebar/HeroTabs sono mutex — ogni area sceglie UNA |
| Entity color conflict in componenti misti (es. session che contiene game) | Pattern: wrapper usa entity primaria, children inline entity-chip per ibridi |
| v1 pagine `.bak` committate causa confusione | Aggiungi `.bak` a `.eslintignore`; documenta in CLAUDE.md che sono archivio temp |

## Execution handoff

**Subagent-Driven Development** è la modalità raccomandata — tasks 8-24 sono sequenziali ma ogni task è self-contained (fresh subagent per task + two-stage review).

Avvio esecuzione con:
```
superpowers:subagent-driven-development
plan: docs/superpowers/plans/2026-04-20-redesign-v2-full-app-migration-m1-m5.md
start_task: 8
```

**Dependency**: attendere completamento `2026-04-20-redesign-v2-library-pilot-plan.md` Tasks 7-19 prima di iniziare Task 8 di questo plan (i primitivi M2 sono prerequisite).
