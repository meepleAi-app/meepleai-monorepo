# Navigation Redesign — Piano 2: ActionBar + HandDrawer + DrawerContent

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare il layer mobile della navigazione Hand-First: ActionBar (sostituisce MobileBottomBar), HandDrawer (bottom sheet per la mano), DrawerContent per ogni entity type (game, player, session, agent, kb, chat).

**Architecture:** ActionBar legge da `useCardHand` per mostrare mini-hand chips + overflow. Tapping un chip (o overflow) apre `HandDrawer` via `useCascadeNavigationStore.openDrawer()`. `HandDrawer` è mobile-only (`md:hidden`) e mostra `DrawerContent` — un switch per entity type che renderizza tabs + footer actions per ciascun tipo. La logica CTA viene estratta in `lib/utils/nav-cta.ts` condiviso con ActionPill.

**Tech Stack:** React 19, Next.js 16 App Router, Zustand 5, Tailwind 4, Vitest + @testing-library/react

---

## File Structure

### Nuovi file
- `apps/web/src/lib/utils/nav-cta.ts` — `resolveCTA(pathname)` condiviso
- `apps/web/src/components/layout/mobile/HandDrawer.tsx` — bottom sheet drawer
- `apps/web/src/components/layout/mobile/drawer/DrawerContent.tsx` — switch per entityType
- `apps/web/src/components/layout/mobile/drawer/GameDrawerContent.tsx`
- `apps/web/src/components/layout/mobile/drawer/PlayerDrawerContent.tsx`
- `apps/web/src/components/layout/mobile/drawer/SessionDrawerContent.tsx`
- `apps/web/src/components/layout/mobile/drawer/AgentDrawerContent.tsx`
- `apps/web/src/components/layout/mobile/drawer/KbDrawerContent.tsx`
- `apps/web/src/components/layout/mobile/drawer/ChatDrawerContent.tsx`
- `apps/web/src/components/layout/mobile/ActionBar.tsx`
- `apps/web/src/__tests__/lib/utils/nav-cta.test.ts`
- `apps/web/src/__tests__/components/layout/mobile/HandDrawer.test.tsx`
- `apps/web/src/__tests__/components/layout/mobile/DrawerContent.test.tsx`
- `apps/web/src/__tests__/components/layout/mobile/ActionBar.test.tsx`

### File modificati
- `apps/web/src/components/layout/ActionPill/ActionPill.tsx` — usa nav-cta.ts
- `apps/web/src/components/layout/UserShell/DesktopShell.tsx` — sostituisce MobileBottomBar con ActionBar + HandDrawer

### File rimossi
- `apps/web/src/components/layout/MobileBottomBar.tsx` — rimpiazzato interamente da ActionBar

---

## Task 1: Shared `nav-cta` utility

**Files:**
- Create: `apps/web/src/lib/utils/nav-cta.ts`
- Create: `apps/web/src/__tests__/lib/utils/nav-cta.test.ts`
- Modify: `apps/web/src/components/layout/ActionPill/ActionPill.tsx`

- [ ] **Step 1: Write the failing test**

Crea `apps/web/src/__tests__/lib/utils/nav-cta.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import { resolveCTA } from '@/lib/utils/nav-cta';

describe('resolveCTA', () => {
  it('returns aggiungi gioco CTA for /dashboard', () => {
    const cta = resolveCTA('/dashboard');
    expect(cta).toEqual({ label: '+ Aggiungi gioco', href: '/library?action=add' });
  });

  it('returns aggiungi CTA for /library', () => {
    expect(resolveCTA('/library')).toEqual({ label: '+ Aggiungi', href: '/library?action=add' });
  });

  it('returns aggiungi CTA for /library sub-path', () => {
    expect(resolveCTA('/library/123')).toEqual({ label: '+ Aggiungi', href: '/library?action=add' });
  });

  it('returns nuova sessione CTA for /games/:id', () => {
    const cta = resolveCTA('/games/abc123');
    expect(cta).toEqual({ label: '▶ Nuova sessione', href: '/games/abc123/sessions/new' });
  });

  it('returns nuova sessione CTA for /games/:id/sessions', () => {
    const cta = resolveCTA('/games/abc123/sessions');
    expect(cta).toEqual({ label: '▶ Nuova sessione', href: '/games/abc123/sessions/new' });
  });

  it('returns carica PDF CTA for /games/:id/kb', () => {
    const cta = resolveCTA('/games/abc123/kb');
    expect(cta).toEqual({ label: '↑ Carica PDF', href: '/games/abc123/kb/upload' });
  });

  it('returns chat CTA for /agents/:id', () => {
    const cta = resolveCTA('/agents/abc123');
    expect(cta).toEqual({ label: '💬 Inizia chat', href: '/agents/abc123/chat' });
  });

  it('returns nuova sessione CTA for /sessions', () => {
    expect(resolveCTA('/sessions')).toEqual({ label: '▶ Nuova sessione', href: '/sessions/new' });
  });

  it('returns null for unknown routes', () => {
    expect(resolveCTA('/unknown')).toBeNull();
    expect(resolveCTA('/')).toBeNull();
    expect(resolveCTA('/toolkit')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --reporter=verbose nav-cta
```

Expected: FAIL — `Cannot find module '@/lib/utils/nav-cta'`

- [ ] **Step 3: Crea `nav-cta.ts`**

Crea `apps/web/src/lib/utils/nav-cta.ts`:

```typescript
export interface CTAConfig {
  label: string;
  href: string;
}

/**
 * Resolves the contextual CTA for the given pathname.
 * Shared between ActionPill (desktop) and ActionBar (mobile).
 */
export function resolveCTA(pathname: string): CTAConfig | null {
  if (pathname === '/dashboard') {
    return { label: '+ Aggiungi gioco', href: '/library?action=add' };
  }
  if (pathname === '/library' || pathname.startsWith('/library')) {
    return { label: '+ Aggiungi', href: '/library?action=add' };
  }
  if (/^\/games\/[^/]+$/.test(pathname)) {
    return { label: '▶ Nuova sessione', href: `${pathname}/sessions/new` };
  }
  if (/^\/games\/[^/]+\/sessions$/.test(pathname)) {
    return { label: '▶ Nuova sessione', href: `${pathname}/new` };
  }
  if (/^\/games\/[^/]+\/kb$/.test(pathname)) {
    return { label: '↑ Carica PDF', href: `${pathname}/upload` };
  }
  if (/^\/agents\/[^/]+$/.test(pathname)) {
    return { label: '💬 Inizia chat', href: `${pathname}/chat` };
  }
  if (pathname === '/sessions' || pathname.startsWith('/sessions')) {
    return { label: '▶ Nuova sessione', href: '/sessions/new' };
  }
  return null;
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --reporter=verbose nav-cta
```

Expected: PASS — 9/9

- [ ] **Step 5: Aggiorna ActionPill.tsx per usare nav-cta**

In `apps/web/src/components/layout/ActionPill/ActionPill.tsx`, rimuovi la funzione `resolveCTA` locale e aggiungi l'import:

```typescript
// Rimuovi:
// interface CTAConfig { ... }
// function resolveCTA(pathname: string): CTAConfig | null { ... }

// Aggiungi in cima agli import:
import { resolveCTA } from '@/lib/utils/nav-cta';
```

Il file risultante deve iniziare con:

```typescript
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useNavBreadcrumb } from '@/lib/hooks/useNavBreadcrumb';
import { resolveCTA } from '@/lib/utils/nav-cta';
import { cn } from '@/lib/utils';

export function ActionPill({ className }: { className?: string }) {
  // ... resto invariato
```

- [ ] **Step 6: Run ActionPill tests per conferma regressione zero**

```bash
cd apps/web && pnpm test -- --reporter=verbose ActionPill
```

Expected: PASS — tutti i test esistenti passano

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/utils/nav-cta.ts \
        apps/web/src/__tests__/lib/utils/nav-cta.test.ts \
        apps/web/src/components/layout/ActionPill/ActionPill.tsx
git commit -m "refactor(nav): extract resolveCTA to shared nav-cta utility"
```

---

## Task 2: HandDrawer — base bottom sheet

**Files:**
- Create: `apps/web/src/components/layout/mobile/HandDrawer.tsx`
- Create: `apps/web/src/__tests__/components/layout/mobile/HandDrawer.test.tsx`

- [ ] **Step 1: Write the failing test**

Crea `apps/web/src/__tests__/components/layout/mobile/HandDrawer.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { useCardHand } from '@/lib/stores/card-hand-store';
import { HandDrawer } from '@/components/layout/mobile/HandDrawer';

// Mock DrawerContent — tested separately
vi.mock('@/components/layout/mobile/drawer/DrawerContent', () => ({
  DrawerContent: ({ entityType }: { entityType: string }) => (
    <div data-testid="drawer-content">{entityType}</div>
  ),
}));

const CLOSED_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [],
};

describe('HandDrawer', () => {
  beforeEach(() => {
    useCascadeNavigationStore.setState(CLOSED_STATE);
    useCardHand.setState({ cards: [] });
  });

  it('renders nothing when state is closed', () => {
    render(<HandDrawer />);
    expect(screen.queryByTestId('hand-drawer-panel')).toBeNull();
  });

  it('renders panel when state is drawer', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'game',
      activeEntityId: 'g1',
    });
    render(<HandDrawer />);
    expect(screen.getByTestId('hand-drawer-panel')).toBeDefined();
  });

  it('renders DrawerContent with correct entityType', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'session',
      activeEntityId: 's1',
    });
    render(<HandDrawer />);
    expect(screen.getByTestId('drawer-content')).toBeDefined();
    expect(screen.getByText('session')).toBeDefined();
  });

  it('shows entity icon in header', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'game',
      activeEntityId: 'g1',
    });
    render(<HandDrawer />);
    expect(screen.getByText('🎲')).toBeDefined();
  });

  it('closes on overlay click', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'agent',
      activeEntityId: 'a1',
    });
    render(<HandDrawer />);
    fireEvent.click(screen.getByTestId('hand-drawer-overlay'));
    expect(useCascadeNavigationStore.getState().state).toBe('closed');
  });

  it('closes on Escape key', () => {
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'kb',
      activeEntityId: 'kb1',
    });
    render(<HandDrawer />);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(useCascadeNavigationStore.getState().state).toBe('closed');
  });

  it('uses card label from hand when available', () => {
    useCardHand.setState({
      cards: [
        {
          id: 'game:g1',
          entityType: 'game',
          entityId: 'g1',
          label: 'Catan',
          href: '/games/g1',
          pinned: false,
          addedAt: 1,
        },
      ],
    });
    useCascadeNavigationStore.setState({
      ...CLOSED_STATE,
      state: 'drawer',
      activeEntityType: 'game',
      activeEntityId: 'g1',
    });
    render(<HandDrawer />);
    expect(screen.getByText('Catan')).toBeDefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --reporter=verbose HandDrawer
```

Expected: FAIL — `Cannot find module '@/components/layout/mobile/HandDrawer'`

- [ ] **Step 3: Crea `HandDrawer.tsx`**

Crea `apps/web/src/components/layout/mobile/HandDrawer.tsx`:

```typescript
'use client';

import { useEffect, useRef, useState } from 'react';
import { X } from 'lucide-react';

import { entityIcon, entityLabel } from '@/components/ui/data-display/meeple-card/tokens';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { useCardHand } from '@/lib/stores/card-hand-store';
import { cn } from '@/lib/utils';

import { DrawerContent } from './drawer/DrawerContent';

export function HandDrawer() {
  const { state, activeEntityType, activeEntityId, activeTabId, closeCascade } =
    useCascadeNavigationStore();

  const cards = useCardHand(s => s.cards);

  const [dragY, setDragY] = useState(0);
  const startY = useRef(0);
  const panelRef = useRef<HTMLDivElement>(null);

  const isOpen = state === 'drawer';

  // Close on Escape
  useEffect(() => {
    if (!isOpen) return;
    const onKeyDown = (e: KeyboardEvent) => {
      if (e.key === 'Escape') closeCascade();
    };
    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [isOpen, closeCascade]);

  // Focus panel on open
  useEffect(() => {
    if (isOpen) panelRef.current?.focus();
  }, [isOpen]);

  if (!isOpen || !activeEntityType || !activeEntityId) return null;

  // Resolve label from hand store, fallback to entityLabel
  const card = cards.find(c => c.entityId === activeEntityId && c.entityType === activeEntityType);
  const headerLabel = card?.label ?? entityLabel[activeEntityType];

  const handleTouchStart = (e: React.TouchEvent) => {
    startY.current = e.touches[0].clientY;
  };
  const handleTouchMove = (e: React.TouchEvent) => {
    const delta = e.touches[0].clientY - startY.current;
    if (delta > 0) setDragY(delta);
  };
  const handleTouchEnd = () => {
    if (dragY > 120) {
      setDragY(0);
      closeCascade();
    } else {
      setDragY(0);
    }
  };

  return (
    <>
      {/* Overlay — mobile only */}
      <div
        data-testid="hand-drawer-overlay"
        className="fixed inset-0 z-40 bg-black/60 md:hidden"
        onClick={closeCascade}
        aria-hidden="true"
      />

      {/* Panel */}
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-label={`${headerLabel} drawer`}
        data-testid="hand-drawer-panel"
        tabIndex={-1}
        className={cn(
          'fixed bottom-0 left-0 right-0 z-50 md:hidden',
          'rounded-t-[20px] bg-[hsl(222,20%,10%)]',
          'border-t border-white/10',
          'flex flex-col h-[70vh] outline-none',
          'transition-transform duration-300'
        )}
        style={{ transform: `translateY(${dragY}px)` }}
      >
        {/* Drag handle */}
        <div
          className="flex justify-center pt-2.5 pb-1 cursor-grab active:cursor-grabbing flex-shrink-0"
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
          onTouchEnd={handleTouchEnd}
          data-testid="hand-drawer-handle"
        >
          <div className="w-10 h-1 rounded-full bg-white/20" />
        </div>

        {/* Header */}
        <div className="flex items-center gap-3 px-4 pb-3 border-b border-white/8 flex-shrink-0">
          <span className="text-2xl leading-none">{entityIcon[activeEntityType]}</span>
          <div className="flex-1 min-w-0">
            <p className="text-sm font-bold text-white truncate">{headerLabel}</p>
            {card?.sublabel && (
              <p className="text-[11px] text-white/50 truncate">{card.sublabel}</p>
            )}
          </div>
          <button
            type="button"
            onClick={closeCascade}
            aria-label="Chiudi drawer"
            className="p-1.5 rounded-lg text-white/50 hover:text-white transition-colors"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Entity-specific content */}
        <DrawerContent
          entityType={activeEntityType}
          entityId={activeEntityId}
          activeTab={activeTabId ?? undefined}
          onNavigate={closeCascade}
        />
      </div>
    </>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --reporter=verbose HandDrawer
```

Expected: PASS — 7/7

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/mobile/HandDrawer.tsx \
        apps/web/src/__tests__/components/layout/mobile/HandDrawer.test.tsx
git commit -m "feat(mobile-nav): HandDrawer base bottom sheet component"
```

---

## Task 3: DrawerContent entity components

**Files:**
- Create: `apps/web/src/components/layout/mobile/drawer/DrawerContent.tsx`
- Create: `apps/web/src/components/layout/mobile/drawer/GameDrawerContent.tsx`
- Create: `apps/web/src/components/layout/mobile/drawer/PlayerDrawerContent.tsx`
- Create: `apps/web/src/components/layout/mobile/drawer/SessionDrawerContent.tsx`
- Create: `apps/web/src/components/layout/mobile/drawer/AgentDrawerContent.tsx`
- Create: `apps/web/src/components/layout/mobile/drawer/KbDrawerContent.tsx`
- Create: `apps/web/src/components/layout/mobile/drawer/ChatDrawerContent.tsx`
- Create: `apps/web/src/__tests__/components/layout/mobile/DrawerContent.test.tsx`

- [ ] **Step 1: Write the failing test**

Crea `apps/web/src/__tests__/components/layout/mobile/DrawerContent.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { DrawerContent } from '@/components/layout/mobile/drawer/DrawerContent';

const CLOSED_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [],
};

beforeEach(() => {
  useCascadeNavigationStore.setState(CLOSED_STATE);
});

describe('DrawerContent — game', () => {
  it('renders Info, Statistiche, Storico tabs', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    expect(screen.getByText('Info')).toBeDefined();
    expect(screen.getByText('Statistiche')).toBeDefined();
    expect(screen.getByText('Storico')).toBeDefined();
  });

  it('renders Gioca and Apri footer actions', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    expect(screen.getByText('▶ Gioca')).toBeDefined();
    expect(screen.getByText('↗ Apri')).toBeDefined();
  });

  it('Info tab is active by default', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    const infoTab = screen.getByRole('tab', { name: 'Info' });
    expect(infoTab.getAttribute('aria-selected')).toBe('true');
  });

  it('switches tab on click', () => {
    render(<DrawerContent entityType="game" entityId="g1" onNavigate={() => {}} />);
    fireEvent.click(screen.getByRole('tab', { name: 'Statistiche' }));
    expect(screen.getByRole('tab', { name: 'Statistiche' }).getAttribute('aria-selected')).toBe('true');
    expect(screen.getByRole('tab', { name: 'Info' }).getAttribute('aria-selected')).toBe('false');
  });

  it('respects activeTab prop', () => {
    render(<DrawerContent entityType="game" entityId="g1" activeTab="Storico" onNavigate={() => {}} />);
    expect(screen.getByRole('tab', { name: 'Storico' }).getAttribute('aria-selected')).toBe('true');
  });
});

describe('DrawerContent — session', () => {
  it('renders Live, Toolkit, Timeline tabs', () => {
    render(<DrawerContent entityType="session" entityId="s1" onNavigate={() => {}} />);
    expect(screen.getByText('Live')).toBeDefined();
    expect(screen.getByText('Toolkit')).toBeDefined();
    expect(screen.getByText('Timeline')).toBeDefined();
  });

  it('renders Riprendi and Apri footer actions', () => {
    render(<DrawerContent entityType="session" entityId="s1" onNavigate={() => {}} />);
    expect(screen.getByText('▶ Riprendi')).toBeDefined();
    expect(screen.getByText('↗ Apri')).toBeDefined();
  });
});

describe('DrawerContent — agent', () => {
  it('renders Overview, Storico, Config tabs', () => {
    render(<DrawerContent entityType="agent" entityId="a1" onNavigate={() => {}} />);
    expect(screen.getByText('Overview')).toBeDefined();
    expect(screen.getByText('Storico')).toBeDefined();
    expect(screen.getByText('Config')).toBeDefined();
  });

  it('renders Chat and Apri footer actions', () => {
    render(<DrawerContent entityType="agent" entityId="a1" onNavigate={() => {}} />);
    expect(screen.getByText('💬 Chat')).toBeDefined();
    expect(screen.getByText('↗ Apri')).toBeDefined();
  });
});

describe('DrawerContent — kb', () => {
  it('renders Overview, Preview, Citazioni tabs', () => {
    render(<DrawerContent entityType="kb" entityId="kb1" onNavigate={() => {}} />);
    expect(screen.getByText('Overview')).toBeDefined();
    expect(screen.getByText('Preview')).toBeDefined();
    expect(screen.getByText('Citazioni')).toBeDefined();
  });
});

describe('DrawerContent — chat', () => {
  it('renders Messaggi and Fonti tabs', () => {
    render(<DrawerContent entityType="chat" entityId="c1" onNavigate={() => {}} />);
    expect(screen.getByText('Messaggi')).toBeDefined();
    expect(screen.getByText('Fonti')).toBeDefined();
  });

  it('renders Continua and Archivia footer actions', () => {
    render(<DrawerContent entityType="chat" entityId="c1" onNavigate={() => {}} />);
    expect(screen.getByText('💬 Continua')).toBeDefined();
    expect(screen.getByText('📦 Archivia')).toBeDefined();
  });
});

describe('DrawerContent — player', () => {
  it('renders Profilo, Statistiche, Storico tabs', () => {
    render(<DrawerContent entityType="player" entityId="p1" onNavigate={() => {}} />);
    expect(screen.getByText('Profilo')).toBeDefined();
    expect(screen.getByText('Statistiche')).toBeDefined();
    expect(screen.getByText('Storico')).toBeDefined();
  });
});

describe('DrawerContent — unknown', () => {
  it('renders null for unknown entityType', () => {
    const { container } = render(
      <DrawerContent entityType={'unknown' as any} entityId="x" onNavigate={() => {}} />
    );
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --reporter=verbose DrawerContent
```

Expected: FAIL — `Cannot find module '@/components/layout/mobile/drawer/DrawerContent'`

- [ ] **Step 3: Crea shared DrawerTabBar helper (inline nei file entity)**

I tab usano la stessa struttura. Ogni entity component la implementa direttamente (no over-abstraction).

Interfaccia props comune per tutti gli entity content component:
```typescript
interface EntityDrawerContentProps {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void; // chiama closeCascade dal HandDrawer
}
```

- [ ] **Step 4: Crea `GameDrawerContent.tsx`**

Crea `apps/web/src/components/layout/mobile/drawer/GameDrawerContent.tsx`:

```typescript
'use client';

import { useState } from 'react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Info', 'Statistiche', 'Storico'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function GameDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Info'
  );

  return (
    <>
      {/* Tabs */}
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(25,95%,55%)] border-b-2 border-[hsl(25,95%,45%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Info' && (
          <p>Dettagli del gioco disponibili dopo il caricamento dei dati.</p>
        )}
        {tab === 'Statistiche' && (
          <p>Statistiche di gioco disponibili a breve.</p>
        )}
        {tab === 'Storico' && (
          <p>Storico delle sessioni disponibile a breve.</p>
        )}
      </div>

      {/* Footer */}
      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <Link
          href={`/games/${entityId}/sessions/new`}
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-bold text-white bg-[hsl(25,95%,45%)] rounded-xl hover:bg-[hsl(25,95%,40%)] transition-colors"
        >
          ▶ Gioca
        </Link>
        <Link
          href={`/games/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
```

- [ ] **Step 5: Crea `PlayerDrawerContent.tsx`**

Crea `apps/web/src/components/layout/mobile/drawer/PlayerDrawerContent.tsx`:

```typescript
'use client';

import { useState } from 'react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Profilo', 'Statistiche', 'Storico'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function PlayerDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Profilo'
  );

  return (
    <>
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(262,83%,68%)] border-b-2 border-[hsl(262,83%,58%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Profilo' && <p>Profilo del giocatore disponibile a breve.</p>}
        {tab === 'Statistiche' && <p>Statistiche del giocatore disponibili a breve.</p>}
        {tab === 'Storico' && <p>Storico partite disponibile a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <button
          type="button"
          className="flex-1 py-2 text-center text-[11px] font-semibold text-white/70 border border-white/15 rounded-xl hover:text-white/90 transition-colors"
        >
          📊 Confronta
        </button>
        <Link
          href={`/players/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
```

- [ ] **Step 6: Crea `SessionDrawerContent.tsx`**

Crea `apps/web/src/components/layout/mobile/drawer/SessionDrawerContent.tsx`:

```typescript
'use client';

import { useState } from 'react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Live', 'Toolkit', 'Timeline'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function SessionDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Live'
  );

  return (
    <>
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(240,60%,65%)] border-b-2 border-[hsl(240,60%,55%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Live' && <p>Stato live della sessione disponibile a breve.</p>}
        {tab === 'Toolkit' && <p>Tool attivi nella sessione disponibili a breve.</p>}
        {tab === 'Timeline' && <p>Timeline degli eventi disponibile a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <Link
          href={`/sessions/${entityId}`}
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-bold text-white bg-[hsl(240,60%,55%)] rounded-xl hover:bg-[hsl(240,60%,48%)] transition-colors"
        >
          ▶ Riprendi
        </Link>
        <Link
          href={`/sessions/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
```

- [ ] **Step 7: Crea `AgentDrawerContent.tsx`**

Crea `apps/web/src/components/layout/mobile/drawer/AgentDrawerContent.tsx`:

```typescript
'use client';

import { useState } from 'react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Overview', 'Storico', 'Config'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function AgentDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Overview'
  );

  return (
    <>
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(38,92%,60%)] border-b-2 border-[hsl(38,92%,50%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Overview' && <p>Descrizione agente disponibile a breve.</p>}
        {tab === 'Storico' && <p>Conversazioni recenti disponibili a breve.</p>}
        {tab === 'Config' && <p>Configurazione agente disponibile a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <Link
          href={`/agents/${entityId}/chat`}
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-bold text-white bg-[hsl(38,92%,50%)] rounded-xl hover:bg-[hsl(38,92%,43%)] transition-colors"
        >
          💬 Chat
        </Link>
        <Link
          href={`/agents/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
```

- [ ] **Step 8: Crea `KbDrawerContent.tsx`**

Crea `apps/web/src/components/layout/mobile/drawer/KbDrawerContent.tsx`:

```typescript
'use client';

import { useState } from 'react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Overview', 'Preview', 'Citazioni'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function KbDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Overview'
  );

  return (
    <>
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(210,40%,65%)] border-b-2 border-[hsl(210,40%,55%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Overview' && <p>Dettagli documento disponibili a breve.</p>}
        {tab === 'Preview' && <p>Preview del testo estratto disponibile a breve.</p>}
        {tab === 'Citazioni' && <p>Citazioni generate disponibili a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <button
          type="button"
          className="flex-1 py-2 text-center text-[11px] font-semibold text-white/70 border border-white/15 rounded-xl hover:text-white/90 transition-colors"
        >
          🔄 Reindex
        </button>
        <button
          type="button"
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ⬇ Download
        </button>
        <Link
          href={`/kb/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
```

- [ ] **Step 9: Crea `ChatDrawerContent.tsx`**

Crea `apps/web/src/components/layout/mobile/drawer/ChatDrawerContent.tsx`:

```typescript
'use client';

import { useState } from 'react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

const TABS = ['Messaggi', 'Fonti'] as const;
type Tab = (typeof TABS)[number];

interface Props {
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function ChatDrawerContent({ entityId, activeTab, onNavigate }: Props) {
  const [tab, setTab] = useState<Tab>(
    TABS.includes(activeTab as Tab) ? (activeTab as Tab) : 'Messaggi'
  );

  return (
    <>
      <div className="flex border-b border-white/8 px-4 flex-shrink-0" role="tablist">
        {TABS.map(t => (
          <button
            key={t}
            role="tab"
            aria-selected={tab === t}
            onClick={() => setTab(t)}
            className={cn(
              'px-3 py-2.5 text-[11px] font-semibold transition-colors',
              tab === t
                ? 'text-[hsl(220,80%,65%)] border-b-2 border-[hsl(220,80%,55%)]'
                : 'text-white/45 hover:text-white/70'
            )}
          >
            {t}
          </button>
        ))}
      </div>

      <div className="flex-1 overflow-y-auto px-4 py-3 text-sm text-white/55">
        {tab === 'Messaggi' && <p>Ultimi messaggi disponibili a breve.</p>}
        {tab === 'Fonti' && <p>Documenti citati disponibili a breve.</p>}
      </div>

      <div className="flex gap-2 px-4 py-3 border-t border-white/8 flex-shrink-0">
        <Link
          href={`/chat/${entityId}`}
          onClick={onNavigate}
          className="flex-1 py-2 text-center text-[11px] font-bold text-white bg-[hsl(220,80%,55%)] rounded-xl hover:bg-[hsl(220,80%,48%)] transition-colors"
        >
          💬 Continua
        </Link>
        <button
          type="button"
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          📦 Archivia
        </button>
        <Link
          href={`/chat/${entityId}`}
          onClick={onNavigate}
          className="px-3 py-2 text-[11px] font-semibold text-white/60 border border-white/15 rounded-xl hover:text-white/80 transition-colors"
        >
          ↗ Apri
        </Link>
      </div>
    </>
  );
}
```

- [ ] **Step 10: Crea `DrawerContent.tsx` (switch router)**

Crea `apps/web/src/components/layout/mobile/drawer/DrawerContent.tsx`:

```typescript
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

import { AgentDrawerContent } from './AgentDrawerContent';
import { ChatDrawerContent } from './ChatDrawerContent';
import { GameDrawerContent } from './GameDrawerContent';
import { KbDrawerContent } from './KbDrawerContent';
import { PlayerDrawerContent } from './PlayerDrawerContent';
import { SessionDrawerContent } from './SessionDrawerContent';

interface DrawerContentProps {
  entityType: MeepleEntityType;
  entityId: string;
  activeTab?: string;
  onNavigate: () => void;
}

export function DrawerContent({ entityType, entityId, activeTab, onNavigate }: DrawerContentProps) {
  const props = { entityId, activeTab, onNavigate };

  switch (entityType) {
    case 'game':
      return <GameDrawerContent {...props} />;
    case 'player':
      return <PlayerDrawerContent {...props} />;
    case 'session':
      return <SessionDrawerContent {...props} />;
    case 'agent':
      return <AgentDrawerContent {...props} />;
    case 'kb':
      return <KbDrawerContent {...props} />;
    case 'chat':
      return <ChatDrawerContent {...props} />;
    default:
      return null;
  }
}
```

- [ ] **Step 11: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --reporter=verbose DrawerContent
```

Expected: PASS — tutti i test DrawerContent

- [ ] **Step 12: Commit**

```bash
git add apps/web/src/components/layout/mobile/drawer/ \
        apps/web/src/__tests__/components/layout/mobile/DrawerContent.test.tsx
git commit -m "feat(mobile-nav): DrawerContent entity components (game/player/session/agent/kb/chat)"
```

---

## Task 4: ActionBar — mini-hand bottom bar

**Files:**
- Create: `apps/web/src/components/layout/mobile/ActionBar.tsx`
- Create: `apps/web/src/__tests__/components/layout/mobile/ActionBar.test.tsx`

- [ ] **Step 1: Write the failing test**

Crea `apps/web/src/__tests__/components/layout/mobile/ActionBar.test.tsx`:

```typescript
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useCardHand } from '@/lib/stores/card-hand-store';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { ActionBar } from '@/components/layout/mobile/ActionBar';

vi.mock('next/navigation', () => ({
  usePathname: vi.fn().mockReturnValue('/dashboard'),
}));

vi.mock('@/components/dashboard', () => ({
  useDashboardMode: vi.fn().mockReturnValue({ isGameMode: false, activeSessionId: null }),
}));

const CLOSED_STATE = {
  state: 'closed' as const,
  activeEntityType: null,
  activeEntityId: null,
  activeTabId: null,
  sourceEntityId: null,
  anchorRect: null,
  deckStackSkipped: false,
  drawerStack: [],
};

const GAME_CARD = (n: number) => ({
  id: `game:${n}`,
  entityType: 'game' as const,
  entityId: String(n),
  label: `Game ${n}`,
  href: `/games/${n}`,
  pinned: false,
  addedAt: n,
});

describe('ActionBar', () => {
  beforeEach(() => {
    useCardHand.setState({ cards: [] });
    useCascadeNavigationStore.setState(CLOSED_STATE);
  });

  it('renders the action bar', () => {
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar')).toBeDefined();
  });

  it('renders no chips when hand is empty', () => {
    render(<ActionBar />);
    expect(screen.queryByTestId(/action-bar-chip/)).toBeNull();
  });

  it('renders one chip per card (max 3 visible)', () => {
    useCardHand.setState({ cards: [GAME_CARD(1), GAME_CARD(2)] });
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-chip-game:1')).toBeDefined();
    expect(screen.getByTestId('action-bar-chip-game:2')).toBeDefined();
  });

  it('shows max 3 chips with overflow badge when cards > 3', () => {
    useCardHand.setState({ cards: [GAME_CARD(1), GAME_CARD(2), GAME_CARD(3), GAME_CARD(4)] });
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-chip-game:1')).toBeDefined();
    expect(screen.getByTestId('action-bar-chip-game:2')).toBeDefined();
    expect(screen.getByTestId('action-bar-chip-game:3')).toBeDefined();
    expect(screen.queryByTestId('action-bar-chip-game:4')).toBeNull();
    const overflow = screen.getByTestId('action-bar-overflow');
    expect(overflow.textContent).toContain('+1');
  });

  it('opens drawer when chip is tapped', () => {
    useCardHand.setState({ cards: [GAME_CARD(1)] });
    render(<ActionBar />);
    fireEvent.click(screen.getByTestId('action-bar-chip-game:1'));
    const storeState = useCascadeNavigationStore.getState();
    expect(storeState.state).toBe('drawer');
    expect(storeState.activeEntityType).toBe('game');
    expect(storeState.activeEntityId).toBe('1');
  });

  it('opens drawer when overflow is tapped', () => {
    useCardHand.setState({ cards: [GAME_CARD(1), GAME_CARD(2), GAME_CARD(3), GAME_CARD(4)] });
    render(<ActionBar />);
    fireEvent.click(screen.getByTestId('action-bar-overflow'));
    expect(useCascadeNavigationStore.getState().state).toBe('drawer');
  });

  it('shows CTA for /dashboard route', () => {
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-cta')).toBeDefined();
    expect(screen.getByTestId('action-bar-cta').textContent).toContain('Aggiungi');
  });

  it('renders session mode when in session', async () => {
    const { useDashboardMode } = await import('@/components/dashboard');
    vi.mocked(useDashboardMode).mockReturnValue({ isGameMode: true, activeSessionId: 'sess1' });
    render(<ActionBar />);
    expect(screen.getByTestId('action-bar-session')).toBeDefined();
    expect(screen.queryByTestId('action-bar')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --reporter=verbose ActionBar
```

Expected: FAIL — `Cannot find module '@/components/layout/mobile/ActionBar'`

- [ ] **Step 3: Crea `ActionBar.tsx`**

Crea `apps/web/src/components/layout/mobile/ActionBar.tsx`:

```typescript
'use client';

import { usePathname } from 'next/navigation';
import { BarChart3, ChevronLeft, MessageCircle, MoreHorizontal, Wrench } from 'lucide-react';

import { entityHsl, entityIcon } from '@/components/ui/data-display/meeple-card/tokens';
import { useDashboardMode } from '@/components/dashboard';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { useCardHand, selectPinnedCards, selectRecentCards } from '@/lib/stores/card-hand-store';
import { resolveCTA } from '@/lib/utils/nav-cta';
import { cn } from '@/lib/utils';

export function ActionBar() {
  const { isGameMode, activeSessionId } = useDashboardMode();
  const inSession = isGameMode && !!activeSessionId;

  if (inSession) {
    return <SessionActionBar sessionId={activeSessionId!} />;
  }

  return <NormalActionBar />;
}

function NormalActionBar() {
  const pathname = usePathname();
  const pinned = useCardHand(selectPinnedCards);
  const recent = useCardHand(selectRecentCards);
  const allCards = [...pinned, ...recent];
  const visibleChips = allCards.slice(0, 3);
  const overflowCount = Math.max(0, allCards.length - 3);

  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);
  const cta = resolveCTA(pathname);

  const handleOverflow = () => {
    const target = allCards[3] ?? allCards[0];
    if (target) openDrawer(target.entityType, target.entityId);
  };

  return (
    <nav
      role="toolbar"
      data-testid="action-bar"
      className={cn(
        'fixed bottom-0 left-0 right-0 z-30 md:hidden',
        'flex items-center justify-between gap-3',
        'px-3 pt-2 pb-[calc(env(safe-area-inset-bottom)+8px)]',
        'bg-[rgba(8,12,24,0.92)] backdrop-blur-[16px]',
        'border-t border-white/8'
      )}
    >
      {/* Mini hand */}
      <div className="flex items-center">
        {visibleChips.map((card, i) => (
          <button
            key={card.id}
            type="button"
            role="button"
            aria-label={card.label}
            onClick={() => openDrawer(card.entityType, card.entityId)}
            data-testid={`action-bar-chip-${card.id}`}
            className="flex items-center justify-center rounded transition-transform active:scale-95"
            style={{
              width: 28,
              height: 20,
              background: entityHsl(card.entityType, 0.22),
              border: `1px solid ${entityHsl(card.entityType, 0.55)}`,
              marginLeft: i > 0 ? -6 : 0,
              zIndex: 3 - i,
              position: 'relative',
            }}
          >
            <span className="text-[10px] leading-none">{entityIcon[card.entityType]}</span>
          </button>
        ))}
        {overflowCount > 0 && (
          <button
            type="button"
            onClick={handleOverflow}
            data-testid="action-bar-overflow"
            className="ml-2 text-[10px] font-bold text-white/65 hover:text-white transition-colors"
          >
            +{overflowCount} ›
          </button>
        )}
      </div>

      {/* CTA */}
      {cta ? (
        <a
          href={cta.href}
          data-testid="action-bar-cta"
          className={cn(
            'px-3 py-1.5 rounded-full',
            'text-[11px] font-bold text-white',
            'bg-[hsl(25,95%,45%)] hover:bg-[hsl(25,95%,40%)]',
            'shadow-[0_2px_8px_hsla(25,95%,45%,0.35)] transition-colors'
          )}
        >
          {cta.label}
        </a>
      ) : (
        <div className="w-10" />
      )}
    </nav>
  );
}

function SessionActionBar({ sessionId }: { sessionId: string }) {
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  return (
    <nav
      data-testid="action-bar-session"
      className={cn(
        'fixed bottom-0 left-0 right-0 z-30 md:hidden',
        'flex items-center justify-around',
        'px-2 pt-1.5 pb-[calc(env(safe-area-inset-bottom)+4px)]',
        'bg-[rgba(8,12,24,0.92)] backdrop-blur-[16px]',
        'border-t-2 border-indigo-400/60'
      )}
    >
      <button
        type="button"
        onClick={() => window.history.back()}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <ChevronLeft className="h-5 w-5" />
        <span>Back</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'live')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-indigo-400"
      >
        <BarChart3 className="h-5 w-5" />
        <span>Classifica</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'toolkit')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <Wrench className="h-5 w-5" />
        <span>Toolkit</span>
      </button>
      <button
        type="button"
        disabled
        aria-label="Chat AI (prossimamente)"
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold opacity-40 cursor-not-allowed"
      >
        <MessageCircle className="h-5 w-5" />
        <span>AI</span>
      </button>
      <button
        type="button"
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <MoreHorizontal className="h-5 w-5" />
        <span>Altro</span>
      </button>
    </nav>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --reporter=verbose ActionBar
```

Expected: PASS — tutti i test ActionBar

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/mobile/ActionBar.tsx \
        apps/web/src/__tests__/components/layout/mobile/ActionBar.test.tsx
git commit -m "feat(mobile-nav): ActionBar mini-hand + CTA + session mode"
```

---

## Task 5: Wire in DesktopShell + cleanup MobileBottomBar

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx`
- Delete: `apps/web/src/components/layout/MobileBottomBar.tsx`

- [ ] **Step 1: Verifica che nessun altro file importi MobileBottomBar**

```bash
cd apps/web && grep -r "MobileBottomBar" src/ --include="*.tsx" --include="*.ts" -l
```

Expected: solo `components/layout/UserShell/DesktopShell.tsx`

Se altri file importano MobileBottomBar, aggiornali prima.

- [ ] **Step 2: Aggiorna DesktopShell.tsx**

Sostituisci il contenuto di `apps/web/src/components/layout/UserShell/DesktopShell.tsx`:

```typescript
'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { ActionPill } from '@/components/layout/ActionPill';
import { HandRail } from '@/components/layout/HandRail';
import { ActionBar } from '@/components/layout/mobile/ActionBar';
import { HandDrawer } from '@/components/layout/mobile/HandDrawer';

import { MiniNavSlot } from './MiniNavSlot';
import { SessionBanner } from './SessionBanner';
import { TopBar } from './TopBar';

interface DesktopShellProps {
  children: ReactNode;
}

/**
 * DesktopShell — Hand-First layout.
 *
 * Layout:
 *   ┌─────────────────────────────────────────────┐
 *   │ TopBar (56px sticky)                        │
 *   ├──────────────┬──────────────────────────────┤
 *   │ HandRail     │ MiniNavSlot (48px, optional) │
 *   │ (64→200px,   ├──────────────────────────────┤
 *   │  hidden mob) │ SessionBanner (32px, session) │
 *   │              ├──────────────────────────────┤
 *   │              │ main content                  │
 *   │              │      [ActionPill floating]    │
 *   └──────────────┴──────────────────────────────┘
 *   [ActionBar — mobile only, fixed bottom]
 *   [HandDrawer — mobile only, bottom sheet]
 */
export function DesktopShell({ children }: DesktopShellProps) {
  return (
    <div className="min-h-dvh flex flex-col bg-[var(--nh-bg-base)]">
      <TopBar />
      <div className="flex flex-1 min-h-0">
        <HandRail />
        <div className="flex flex-col flex-1 min-w-0">
          <MiniNavSlot />
          <SessionBanner />
          <main className="flex-1 min-w-0 overflow-y-auto">{children}</main>
          <ActionPill />
        </div>
      </div>
      <ChatSlideOverPanel />
      <ActionBar />
      <HandDrawer />
    </div>
  );
}
```

- [ ] **Step 3: Elimina MobileBottomBar.tsx**

```bash
rm apps/web/src/components/layout/MobileBottomBar.tsx
```

- [ ] **Step 4: Run l'intera suite di test frontend**

```bash
cd apps/web && pnpm test
```

Expected: PASS — nessuna regressione. Se qualche test importa `MobileBottomBar`, aggiornarlo a `ActionBar`.

- [ ] **Step 5: TypeScript check**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore TS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UserShell/DesktopShell.tsx
git rm apps/web/src/components/layout/MobileBottomBar.tsx
git commit -m "feat(mobile-nav): wire ActionBar + HandDrawer in DesktopShell, remove MobileBottomBar"
```

---

## Checklist post-Piano 2

Dopo il merge di Piano 2, verificare:

- [ ] ActionBar visibile su mobile (md:hidden su desktop)
- [ ] Hand chips mostrano le carte correnti da useCardHand
- [ ] Tap su chip apre HandDrawer con l'entità corretta
- [ ] HandDrawer mostra i tab giusti per ogni entityType
- [ ] Footer actions del drawer navigano agli URL corretti
- [ ] Session mode (SessionActionBar) funziona come il vecchio SessionModeBar
- [ ] ActionPill (desktop) funziona ancora — resolveCTA refactoring non ha rotto nulla
- [ ] `MobileBottomBar` completamente rimosso dal progetto

---

## Scope NON incluso in Piano 2 (→ Piano 3)

- ManaPips su MeepleCard (§6.2)
- navItems chip su variante focus/featured MeepleCard (§6.3)
- `drawCard()` call sites nelle pagine entity (§2.2, §8) — popolamento automatico della mano al navigate
- Dashboard hub grid con MeepleCard (§6b)
- Entity page header con MeepleCard variant="focus"
