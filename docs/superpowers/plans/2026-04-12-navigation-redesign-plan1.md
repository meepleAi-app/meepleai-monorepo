# Navigation Redesign — Piano 1: Foundation + Desktop HandRail

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship `useCardHand` store (FIFO + pin), `HandRail` (desktop left rail), `ActionPill` (sostituisce `FloatingActionPill`), e `useNavBreadcrumb` come layer di navigazione desktop testabile e funzionante.

**Architecture:** `useCardHand` è un Zustand store con persist (solo pinned in localStorage). `HandRail` legge lo store e renderizza entity-chip colorati in una left rail collassabile (64→200px). `ActionPill` sostituisce `FloatingActionPill`: legge `useNavBreadcrumb` per il breadcrumb e deriva la CTA contestuale dal pathname. `ContextualHandSidebar` (right panel di sessione) e `MobileBottomBar` (nav statico) non vengono toccati in questo piano.

**Tech Stack:** Next.js App Router, React 19, Zustand 5 (con persist), TypeScript 5, Tailwind 4, Vitest + Testing Library

---

## File Structure

**File da creare:**
- `apps/web/src/lib/stores/card-hand-store.ts` — useCardHand Zustand store (FIFO + pin + localStorage per solo pinned)
- `apps/web/src/lib/hooks/useNavBreadcrumb.ts` — hook che deriva BreadcrumbSegment[] da usePathname + useCardHand
- `apps/web/src/components/layout/HandRail/HandRailCard.tsx` — single entity chip nel rail
- `apps/web/src/components/layout/HandRail/HandRail.tsx` — left rail (dark bg, 64px→200px on hover, hidden su mobile)
- `apps/web/src/components/layout/HandRail/index.ts` — re-export
- `apps/web/src/components/layout/ActionPill/ActionPill.tsx` — floating glassmorphism pill
- `apps/web/src/components/layout/ActionPill/index.ts` — re-export
- `apps/web/src/__tests__/stores/card-hand-store.test.ts`
- `apps/web/src/__tests__/components/layout/HandRail.test.tsx`
- `apps/web/src/__tests__/components/layout/ActionPill.test.tsx`

**File da modificare:**
- `apps/web/src/components/layout/UserShell/DesktopShell.tsx` — aggiunge HandRail e ActionPill
- `apps/web/src/__tests__/components/layout/FloatingActionPill.test.tsx` — aggiorna import (FloatingActionPill resta ma non viene più usato in DesktopShell)

---

## Task 1: `useCardHand` store

**Files:**
- Create: `apps/web/src/lib/stores/card-hand-store.ts`

- [ ] **Step 1: Crea il file store**

```ts
// apps/web/src/lib/stores/card-hand-store.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card'

export interface HandCard {
  id: string              // `${entityType}:${entityId}`
  entityType: MeepleEntityType
  entityId: string
  label: string
  sublabel?: string
  href: string
  pinned: boolean
  addedAt: number
}

interface CardHandStore {
  cards: HandCard[]
  drawCard: (card: Omit<HandCard, 'addedAt'>) => void
  discardCard: (id: string) => void
  pinCard: (id: string) => void
  unpinCard: (id: string) => void
  clearHand: () => void
}

const MAX_CARDS = 10

function evict(cards: HandCard[]): HandCard[] {
  if (cards.length <= MAX_CARDS) return cards
  const oldest = [...cards]
    .filter(c => !c.pinned)
    .sort((a, b) => a.addedAt - b.addedAt)[0]
  if (!oldest) return cards // all pinned
  return cards.filter(c => c.id !== oldest.id)
}

export const useCardHand = create<CardHandStore>()(
  persist(
    (set) => ({
      cards: [],
      drawCard: (card) =>
        set((s) => {
          const existing = s.cards.find(c => c.id === card.id)
          if (existing) {
            return {
              cards: evict(
                s.cards.map(c =>
                  c.id === card.id ? { ...c, ...card, addedAt: Date.now() } : c
                )
              ),
            }
          }
          return { cards: evict([...s.cards, { ...card, addedAt: Date.now() }]) }
        }),
      discardCard: (id) => set((s) => ({ cards: s.cards.filter(c => c.id !== id) })),
      pinCard: (id) =>
        set((s) => ({ cards: s.cards.map(c => c.id === id ? { ...c, pinned: true } : c) })),
      unpinCard: (id) =>
        set((s) => ({ cards: s.cards.map(c => c.id === id ? { ...c, pinned: false } : c) })),
      clearHand: () => set({ cards: [] }),
    }),
    {
      name: 'meepleai:hand',
      partialize: (state) => ({ cards: state.cards.filter(c => c.pinned) }),
    }
  )
)

export const selectPinnedCards = (s: CardHandStore) =>
  s.cards.filter(c => c.pinned)

export const selectRecentCards = (s: CardHandStore) =>
  s.cards.filter(c => !c.pinned).sort((a, b) => b.addedAt - a.addedAt)
```

- [ ] **Step 2: Verifica typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | grep card-hand`
Expected: nessun errore su card-hand-store.ts

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/stores/card-hand-store.ts
git commit -m "feat(nav): add useCardHand store (FIFO + pin + localStorage)"
```

---

## Task 2: `useCardHand` tests

**Files:**
- Create: `apps/web/src/__tests__/stores/card-hand-store.test.ts`

- [ ] **Step 1: Scrivi il file di test**

```ts
// apps/web/src/__tests__/stores/card-hand-store.test.ts
import { describe, it, expect, beforeEach } from 'vitest'
import { useCardHand, selectPinnedCards, selectRecentCards } from '@/lib/stores/card-hand-store'
import type { HandCard } from '@/lib/stores/card-hand-store'

function makeCard(id: string, overrides: Partial<Omit<HandCard, 'addedAt'>> = {}): Omit<HandCard, 'addedAt'> {
  const [entityType, entityId] = id.split(':') as [HandCard['entityType'], string]
  return {
    id,
    entityType,
    entityId,
    label: overrides.label ?? `Card ${entityId}`,
    href: `/games/${entityId}`,
    pinned: false,
    ...overrides,
  }
}

beforeEach(() => {
  useCardHand.setState({ cards: [] })
})

describe('useCardHand — drawCard', () => {
  it('aggiunge una card', () => {
    useCardHand.getState().drawCard(makeCard('game:1', { label: 'Catan' }))
    expect(useCardHand.getState().cards).toHaveLength(1)
    expect(useCardHand.getState().cards[0].label).toBe('Catan')
  })

  it('aggiornare una card esistente la porta in cima (addedAt più recente)', () => {
    useCardHand.getState().drawCard(makeCard('game:1', { label: 'Catan' }))
    const firstTime = useCardHand.getState().cards[0].addedAt
    useCardHand.getState().drawCard(makeCard('game:1', { label: 'Catan v2' }))
    const cards = useCardHand.getState().cards
    expect(cards).toHaveLength(1)
    expect(cards[0].label).toBe('Catan v2')
    expect(cards[0].addedAt).toBeGreaterThanOrEqual(firstTime)
  })

  it('evict la card non-pinned più vecchia quando si supera MAX (10)', () => {
    for (let i = 1; i <= 10; i++) {
      useCardHand.getState().drawCard(makeCard(`game:${i}`))
    }
    expect(useCardHand.getState().cards).toHaveLength(10)
    useCardHand.getState().drawCard(makeCard('game:11'))
    expect(useCardHand.getState().cards).toHaveLength(10)
    expect(useCardHand.getState().cards.find(c => c.id === 'game:1')).toBeUndefined()
    expect(useCardHand.getState().cards.find(c => c.id === 'game:11')).toBeDefined()
  })

  it('NON evict card pinned anche se è la più vecchia', () => {
    for (let i = 1; i <= 10; i++) {
      useCardHand.getState().drawCard(makeCard(`game:${i}`))
    }
    useCardHand.getState().pinCard('game:1')
    useCardHand.getState().drawCard(makeCard('game:11'))
    expect(useCardHand.getState().cards.find(c => c.id === 'game:1')).toBeDefined()
    expect(useCardHand.getState().cards.find(c => c.id === 'game:2')).toBeUndefined()
  })
})

describe('useCardHand — discardCard', () => {
  it('rimuove la card', () => {
    useCardHand.getState().drawCard(makeCard('game:1'))
    useCardHand.getState().discardCard('game:1')
    expect(useCardHand.getState().cards).toHaveLength(0)
  })
})

describe('useCardHand — pinCard / unpinCard', () => {
  it('pin e unpin', () => {
    useCardHand.getState().drawCard(makeCard('game:1'))
    useCardHand.getState().pinCard('game:1')
    expect(useCardHand.getState().cards[0].pinned).toBe(true)
    useCardHand.getState().unpinCard('game:1')
    expect(useCardHand.getState().cards[0].pinned).toBe(false)
  })
})

describe('useCardHand — clearHand', () => {
  it('svuota tutte le card', () => {
    useCardHand.getState().drawCard(makeCard('game:1'))
    useCardHand.getState().drawCard(makeCard('game:2'))
    useCardHand.getState().clearHand()
    expect(useCardHand.getState().cards).toHaveLength(0)
  })
})

describe('selectors', () => {
  it('selectPinnedCards restituisce solo pinned', () => {
    useCardHand.getState().drawCard(makeCard('game:1'))
    useCardHand.getState().drawCard(makeCard('game:2'))
    useCardHand.getState().pinCard('game:1')
    expect(selectPinnedCards(useCardHand.getState())).toHaveLength(1)
    expect(selectPinnedCards(useCardHand.getState())[0].id).toBe('game:1')
  })

  it('selectRecentCards restituisce solo non-pinned, ordinati per addedAt desc', () => {
    useCardHand.getState().drawCard(makeCard('game:1'))
    useCardHand.getState().drawCard(makeCard('game:2'))
    useCardHand.getState().pinCard('game:1')
    const recent = selectRecentCards(useCardHand.getState())
    expect(recent).toHaveLength(1)
    expect(recent[0].id).toBe('game:2')
  })
})
```

- [ ] **Step 2: Esegui i test**

Run: `cd apps/web && pnpm test -- --reporter=verbose src/__tests__/stores/card-hand-store.test.ts`
Expected: 9 test PASS

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/stores/card-hand-store.test.ts
git commit -m "test(nav): add useCardHand store tests"
```

---

## Task 3: `HandRailCard` e `HandRail`

**Files:**
- Create: `apps/web/src/components/layout/HandRail/HandRailCard.tsx`
- Create: `apps/web/src/components/layout/HandRail/HandRail.tsx`
- Create: `apps/web/src/components/layout/HandRail/index.ts`

- [ ] **Step 1: Crea `HandRailCard.tsx`**

```tsx
// apps/web/src/components/layout/HandRail/HandRailCard.tsx
'use client'

import Link from 'next/link'
import { entityHsl, entityIcon } from '@/components/ui/data-display/meeple-card/tokens'
import { cn } from '@/lib/utils'
import type { HandCard } from '@/lib/stores/card-hand-store'

interface HandRailCardProps {
  card: HandCard
  expanded: boolean
  active: boolean
}

export function HandRailCard({ card, expanded, active }: HandRailCardProps) {
  const color = entityHsl(card.entityType)
  const icon = entityIcon[card.entityType]

  return (
    <Link
      href={card.href}
      title={card.label}
      aria-label={card.label}
      aria-current={active ? 'page' : undefined}
      data-testid={`rail-card-${card.id}`}
      className={cn(
        'flex items-center gap-2 w-full px-1 py-1 rounded-lg border',
        'transition-all duration-150 overflow-hidden min-h-[36px]',
        active
          ? 'bg-white/8 border-white/14'
          : 'border-transparent hover:bg-white/7 hover:border-white/10'
      )}
    >
      <span
        className="flex-shrink-0 w-[26px] h-[18px] rounded flex items-center justify-center text-[9px] font-bold"
        style={{
          background: entityHsl(card.entityType, 0.14),
          borderLeft: `2.5px solid ${color}`,
        }}
        aria-hidden="true"
      >
        {icon}
      </span>

      <span
        className={cn(
          'flex flex-col overflow-hidden transition-[opacity,max-width] duration-150',
          expanded ? 'opacity-100 max-w-[140px]' : 'opacity-0 max-w-0'
        )}
      >
        <span className="text-[9.5px] font-bold text-white/80 truncate leading-tight">
          {card.label}
        </span>
        {card.sublabel && (
          <span className="text-[7.5px] text-white/38 truncate">{card.sublabel}</span>
        )}
      </span>

      {expanded && card.pinned && (
        <span className="ml-auto text-[7px] text-white/30 flex-shrink-0" aria-hidden="true">
          📌
        </span>
      )}
    </Link>
  )
}
```

- [ ] **Step 2: Crea `HandRail.tsx`**

```tsx
// apps/web/src/components/layout/HandRail/HandRail.tsx
'use client'

import { useState } from 'react'
import { usePathname } from 'next/navigation'
import { useCardHand, selectPinnedCards, selectRecentCards } from '@/lib/stores/card-hand-store'
import { HandRailCard } from './HandRailCard'
import { cn } from '@/lib/utils'

export function HandRail() {
  const [expanded, setExpanded] = useState(false)
  const pathname = usePathname()
  const pinned = useCardHand(selectPinnedCards)
  const recent = useCardHand(selectRecentCards)

  if (pinned.length === 0 && recent.length === 0) return null

  return (
    <aside
      data-testid="hand-rail"
      aria-label="Mano di navigazione"
      className={cn(
        'hidden md:flex flex-col flex-shrink-0',
        'h-[calc(100dvh-56px)] sticky top-[56px]',
        'bg-[hsl(220,15%,11%)] border-r border-white/5',
        'transition-[width] duration-200 ease-out overflow-hidden',
        expanded ? 'w-[200px]' : 'w-[64px]'
      )}
      onMouseEnter={() => setExpanded(true)}
      onMouseLeave={() => setExpanded(false)}
    >
      <div className="flex flex-col gap-1 p-[5px] flex-1 overflow-y-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {pinned.length > 0 && (
          <>
            <span
              className={cn(
                'text-[7.5px] font-[800] tracking-[0.1em] uppercase text-white/28 px-[3px] mt-1',
                'transition-opacity duration-150',
                expanded ? 'opacity-100' : 'opacity-0'
              )}
            >
              📌 Fissate
            </span>
            {pinned.map((card) => (
              <HandRailCard
                key={card.id}
                card={card}
                expanded={expanded}
                active={pathname === card.href || pathname.startsWith(card.href + '/')}
              />
            ))}
            <div className="h-px bg-white/5 my-1" />
          </>
        )}

        {recent.length > 0 && (
          <>
            <span
              className={cn(
                'text-[7.5px] font-[800] tracking-[0.1em] uppercase text-white/28 px-[3px]',
                'transition-opacity duration-150',
                expanded ? 'opacity-100' : 'opacity-0'
              )}
            >
              🕐 Recenti
            </span>
            {recent.map((card) => (
              <HandRailCard
                key={card.id}
                card={card}
                expanded={expanded}
                active={pathname === card.href || pathname.startsWith(card.href + '/')}
              />
            ))}
          </>
        )}
      </div>
    </aside>
  )
}
```

- [ ] **Step 3: Crea `index.ts`**

```ts
// apps/web/src/components/layout/HandRail/index.ts
export { HandRail } from './HandRail'
export { HandRailCard } from './HandRailCard'
```

- [ ] **Step 4: Typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | grep -i "handrail\|hand-rail"`
Expected: nessun errore

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/HandRail/
git commit -m "feat(nav): add HandRail + HandRailCard components"
```

---

## Task 4: `HandRail` tests

**Files:**
- Create: `apps/web/src/__tests__/components/layout/HandRail.test.tsx`

- [ ] **Step 1: Scrivi il file di test**

```tsx
// apps/web/src/__tests__/components/layout/HandRail.test.tsx
import { render, screen } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { HandRail } from '@/components/layout/HandRail'
import { useCardHand } from '@/lib/stores/card-hand-store'

const mockPathname = vi.fn(() => '/dashboard')
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}))

// next/link stub
vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode; [k: string]: unknown }) =>
    <a href={href} {...props}>{children}</a>,
}))

beforeEach(() => {
  useCardHand.setState({ cards: [] })
  mockPathname.mockReturnValue('/dashboard')
})

describe('HandRail', () => {
  it('non renderizza nulla con la mano vuota', () => {
    const { container } = render(<HandRail />)
    expect(container.firstChild).toBeNull()
  })

  it('renderizza dopo drawCard', () => {
    useCardHand.getState().drawCard({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
    })
    render(<HandRail />)
    expect(screen.getByTestId('hand-rail')).toBeInTheDocument()
    expect(screen.getByTestId('rail-card-game:abc')).toBeInTheDocument()
  })

  it('imposta aria-current="page" sulla card attiva', () => {
    mockPathname.mockReturnValue('/games/abc')
    useCardHand.getState().drawCard({
      id: 'game:abc',
      entityType: 'game',
      entityId: 'abc',
      label: 'Catan',
      href: '/games/abc',
      pinned: false,
    })
    render(<HandRail />)
    const link = screen.getByTestId('rail-card-game:abc')
    expect(link).toHaveAttribute('aria-current', 'page')
  })

  it('separa pinned da recent con sezione labels', () => {
    useCardHand.getState().drawCard({
      id: 'game:1', entityType: 'game', entityId: '1',
      label: 'Pinned', href: '/games/1', pinned: false,
    })
    useCardHand.getState().pinCard('game:1')
    useCardHand.getState().drawCard({
      id: 'game:2', entityType: 'game', entityId: '2',
      label: 'Recent', href: '/games/2', pinned: false,
    })
    render(<HandRail />)
    expect(screen.getByText(/Fissate/i)).toBeInTheDocument()
    expect(screen.getByText(/Recenti/i)).toBeInTheDocument()
  })
})
```

- [ ] **Step 2: Esegui i test**

Run: `cd apps/web && pnpm test -- --reporter=verbose src/__tests__/components/layout/HandRail.test.tsx`
Expected: 4 test PASS

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/__tests__/components/layout/HandRail.test.tsx
git commit -m "test(nav): add HandRail tests"
```

---

## Task 5: `useNavBreadcrumb` + tests

**Files:**
- Create: `apps/web/src/lib/hooks/useNavBreadcrumb.ts`
- Create: `apps/web/src/__tests__/hooks/useNavBreadcrumb.test.ts`

- [ ] **Step 1: Crea il hook**

```ts
// apps/web/src/lib/hooks/useNavBreadcrumb.ts
'use client'

import { usePathname } from 'next/navigation'
import { useCardHand } from '@/lib/stores/card-hand-store'
import { entityHsl } from '@/components/ui/data-display/meeple-card/tokens'
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card'

export interface BreadcrumbSegment {
  label: string
  href: string
  entityType?: MeepleEntityType
  color?: string
}

const ROUTE_LABELS: Record<string, string> = {
  '/dashboard': 'Dashboard',
  '/library': 'Libreria',
  '/sessions': 'Sessioni',
  '/agents': 'Agenti',
  '/toolkit': 'Toolkit',
  '/chat': 'Chat',
}

export function useNavBreadcrumb(): BreadcrumbSegment[] {
  const pathname = usePathname()
  const cards = useCardHand((s) => s.cards)

  const segments: BreadcrumbSegment[] = []
  const parts = pathname.split('/').filter(Boolean)
  let accumulated = ''

  for (const part of parts) {
    accumulated = `${accumulated}/${part}`
    const card = cards.find(
      (c) => c.href === accumulated || pathname.startsWith(c.href + '/')
    )
    if (card && !segments.find((s) => s.href === accumulated)) {
      segments.push({
        label: card.label,
        href: accumulated,
        entityType: card.entityType,
        color: entityHsl(card.entityType),
      })
    } else if (ROUTE_LABELS[accumulated] && !segments.find((s) => s.href === accumulated)) {
      segments.push({ label: ROUTE_LABELS[accumulated], href: accumulated })
    }
  }

  if (segments.length === 0 && ROUTE_LABELS[pathname]) {
    segments.push({ label: ROUTE_LABELS[pathname], href: pathname })
  }

  return segments
}
```

- [ ] **Step 2: Scrivi i test**

```ts
// apps/web/src/__tests__/hooks/useNavBreadcrumb.test.ts
import { describe, it, expect, beforeEach, vi } from 'vitest'
import { renderHook } from '@testing-library/react'
import { useNavBreadcrumb } from '@/lib/hooks/useNavBreadcrumb'
import { useCardHand } from '@/lib/stores/card-hand-store'

const mockPathname = vi.fn()
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}))

beforeEach(() => {
  useCardHand.setState({ cards: [] })
})

describe('useNavBreadcrumb', () => {
  it('restituisce segmento per route nota', () => {
    mockPathname.mockReturnValue('/library')
    const { result } = renderHook(() => useNavBreadcrumb())
    expect(result.current).toHaveLength(1)
    expect(result.current[0].label).toBe('Libreria')
    expect(result.current[0].href).toBe('/library')
  })

  it('restituisce segmento entity da card in mano', () => {
    mockPathname.mockReturnValue('/games/abc')
    useCardHand.getState().drawCard({
      id: 'game:abc', entityType: 'game', entityId: 'abc',
      label: 'Catan', href: '/games/abc', pinned: false,
    })
    const { result } = renderHook(() => useNavBreadcrumb())
    expect(result.current.some((s) => s.label === 'Catan')).toBe(true)
    expect(result.current.find((s) => s.label === 'Catan')?.entityType).toBe('game')
    expect(result.current.find((s) => s.label === 'Catan')?.color).toBeTruthy()
  })

  it('lista vuota per route sconosciuta senza card', () => {
    mockPathname.mockReturnValue('/unknown/path')
    const { result } = renderHook(() => useNavBreadcrumb())
    expect(result.current).toHaveLength(0)
  })

  it('dashboard restituisce "Dashboard"', () => {
    mockPathname.mockReturnValue('/dashboard')
    const { result } = renderHook(() => useNavBreadcrumb())
    expect(result.current[0].label).toBe('Dashboard')
  })
})
```

- [ ] **Step 3: Esegui i test**

Run: `cd apps/web && pnpm test -- --reporter=verbose src/__tests__/hooks/useNavBreadcrumb.test.ts`
Expected: 4 test PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/lib/hooks/useNavBreadcrumb.ts apps/web/src/__tests__/hooks/useNavBreadcrumb.test.ts
git commit -m "feat(nav): add useNavBreadcrumb hook"
```

---

## Task 6: `ActionPill` + tests

**Files:**
- Create: `apps/web/src/components/layout/ActionPill/ActionPill.tsx`
- Create: `apps/web/src/components/layout/ActionPill/index.ts`
- Create: `apps/web/src/__tests__/components/layout/ActionPill.test.tsx`

- [ ] **Step 1: Crea `ActionPill.tsx`**

```tsx
// apps/web/src/components/layout/ActionPill/ActionPill.tsx
'use client'

import Link from 'next/link'
import { usePathname } from 'next/navigation'
import { useNavBreadcrumb } from '@/lib/hooks/useNavBreadcrumb'
import { cn } from '@/lib/utils'

interface CTAConfig {
  label: string
  href: string
}

function resolveCTA(pathname: string): CTAConfig | null {
  if (pathname === '/dashboard') {
    return { label: '+ Aggiungi gioco', href: '/library?action=add' }
  }
  if (pathname === '/library' || pathname.startsWith('/library')) {
    return { label: '+ Aggiungi', href: '/library?action=add' }
  }
  if (/^\/games\/[^/]+$/.test(pathname)) {
    return { label: '▶ Nuova sessione', href: `${pathname}/sessions/new` }
  }
  if (/^\/games\/[^/]+\/sessions$/.test(pathname)) {
    return { label: '▶ Nuova sessione', href: `${pathname}/new` }
  }
  if (/^\/games\/[^/]+\/kb$/.test(pathname)) {
    return { label: '↑ Carica PDF', href: `${pathname}/upload` }
  }
  if (/^\/agents\/[^/]+$/.test(pathname)) {
    return { label: '💬 Inizia chat', href: `${pathname}/chat` }
  }
  if (pathname === '/sessions' || pathname.startsWith('/sessions')) {
    return { label: '▶ Nuova sessione', href: '/sessions/new' }
  }
  return null
}

export function ActionPill({ className }: { className?: string }) {
  const pathname = usePathname()
  const segments = useNavBreadcrumb()
  const cta = resolveCTA(pathname)

  if (segments.length === 0 && !cta) return null

  return (
    <div
      data-testid="action-pill"
      className={cn(
        'fixed bottom-6 left-1/2 -translate-x-1/2 z-50',
        'hidden lg:flex items-center gap-3',
        'px-4 py-2 rounded-full',
        'bg-[rgba(15,20,40,0.82)] backdrop-blur-[12px]',
        'shadow-[0_8px_32px_rgba(0,0,0,0.4)] border border-white/8',
        className
      )}
    >
      {segments.length > 0 && (
        <nav aria-label="Breadcrumb">
          <ol className="flex items-center gap-1 m-0 p-0 list-none">
            {segments.map((seg, i) => (
              <li key={seg.href} className="flex items-center gap-1">
                {i > 0 && (
                  <span className="text-white/22 text-[8.5px]" aria-hidden="true">
                    ›
                  </span>
                )}
                <Link
                  href={seg.href}
                  className="text-[10.5px] font-semibold transition-colors hover:opacity-90"
                  style={{
                    color:
                      i === segments.length - 1 && seg.color
                        ? seg.color
                        : 'rgba(255,255,255,0.55)',
                  }}
                  aria-current={i === segments.length - 1 ? 'page' : undefined}
                >
                  {seg.label}
                </Link>
              </li>
            ))}
          </ol>
        </nav>
      )}

      {cta && (
        <Link
          href={cta.href}
          data-testid="action-pill-cta"
          className={cn(
            'px-3 py-[5px] rounded-full text-[10.5px] font-bold text-white',
            'bg-[hsl(25,95%,45%)] hover:bg-[hsl(25,95%,40%)]',
            'shadow-[0_2px_8px_hsla(25,95%,45%,0.4)] transition-colors'
          )}
        >
          {cta.label}
        </Link>
      )}
    </div>
  )
}
```

- [ ] **Step 2: Crea `index.ts`**

```ts
// apps/web/src/components/layout/ActionPill/index.ts
export { ActionPill } from './ActionPill'
```

- [ ] **Step 3: Scrivi i test**

```tsx
// apps/web/src/__tests__/components/layout/ActionPill.test.tsx
import { render, screen } from '@testing-library/react'
import { vi, describe, it, expect, beforeEach } from 'vitest'
import { ActionPill } from '@/components/layout/ActionPill'
import { useCardHand } from '@/lib/stores/card-hand-store'

const mockPathname = vi.fn()
vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
}))
vi.mock('next/link', () => ({
  default: ({ href, children, ...props }: { href: string; children: React.ReactNode; [k: string]: unknown }) =>
    <a href={href} {...props}>{children}</a>,
}))

beforeEach(() => {
  useCardHand.setState({ cards: [] })
})

describe('ActionPill', () => {
  it('non renderizza se path sconosciuto e nessuna card', () => {
    mockPathname.mockReturnValue('/unknown')
    const { container } = render(<ActionPill />)
    expect(container.firstChild).toBeNull()
  })

  it('renderizza CTA "▶ Nuova sessione" su /sessions', () => {
    mockPathname.mockReturnValue('/sessions')
    render(<ActionPill />)
    expect(screen.getByTestId('action-pill-cta')).toHaveTextContent('▶ Nuova sessione')
  })

  it('renderizza CTA "↑ Carica PDF" su /games/:id/kb', () => {
    mockPathname.mockReturnValue('/games/abc/kb')
    render(<ActionPill />)
    expect(screen.getByTestId('action-pill-cta')).toHaveTextContent('↑ Carica PDF')
  })

  it('renderizza CTA "💬 Inizia chat" su /agents/:id', () => {
    mockPathname.mockReturnValue('/agents/expert-1')
    render(<ActionPill />)
    expect(screen.getByTestId('action-pill-cta')).toHaveTextContent('💬 Inizia chat')
  })

  it('renderizza breadcrumb con entity label se card in mano', () => {
    mockPathname.mockReturnValue('/games/abc')
    useCardHand.getState().drawCard({
      id: 'game:abc', entityType: 'game', entityId: 'abc',
      label: 'Catan', href: '/games/abc', pinned: false,
    })
    render(<ActionPill />)
    expect(screen.getByText('Catan')).toBeInTheDocument()
    expect(screen.getByRole('navigation', { name: 'Breadcrumb' })).toBeInTheDocument()
  })

  it('renderizza breadcrumb "Libreria" su /library', () => {
    mockPathname.mockReturnValue('/library')
    render(<ActionPill />)
    expect(screen.getByText('Libreria')).toBeInTheDocument()
  })
})
```

- [ ] **Step 4: Esegui i test**

Run: `cd apps/web && pnpm test -- --reporter=verbose src/__tests__/components/layout/ActionPill.test.tsx`
Expected: 6 test PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/ActionPill/ apps/web/src/__tests__/components/layout/ActionPill.test.tsx
git commit -m "feat(nav): add ActionPill component (replaces FloatingActionPill in shell)"
```

---

## Task 7: Wire in DesktopShell + rimuovi FloatingActionPill dalle pagine

**Files:**
- Modify: `apps/web/src/components/layout/UserShell/DesktopShell.tsx`

- [ ] **Step 1: Trova tutte le pagine che usano ancora `FloatingActionPill`**

Run: `cd apps/web && grep -r "FloatingActionPill" src --include="*.tsx" -l`

Per ogni file trovato, rimuovi il `<FloatingActionPill page="..." />` e il relativo import. Il componente `FloatingActionPill.tsx` NON viene eliminato (ci sono test su di esso) — viene solo de-usato nel template shell.

- [ ] **Step 2: Modifica `DesktopShell.tsx`**

```tsx
// apps/web/src/components/layout/UserShell/DesktopShell.tsx
'use client';

import type { ReactNode } from 'react';

import { ChatSlideOverPanel } from '@/components/chat/panel/ChatSlideOverPanel';
import { ActionPill } from '@/components/layout/ActionPill';
import { HandRail } from '@/components/layout/HandRail';
import { MobileBottomBar } from '@/components/layout/MobileBottomBar';

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
          <main className="flex-1 min-w-0 overflow-y-auto relative">
            {children}
            <ActionPill />
          </main>
        </div>
      </div>
      <ChatSlideOverPanel />
      <MobileBottomBar />
    </div>
  );
}
```

- [ ] **Step 3: Typecheck full**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errori

- [ ] **Step 4: Test suite completa**

Run: `cd apps/web && pnpm test -- --reporter=verbose src/__tests__/stores/card-hand-store.test.ts src/__tests__/components/layout/HandRail.test.tsx src/__tests__/hooks/useNavBreadcrumb.test.ts src/__tests__/components/layout/ActionPill.test.tsx`
Expected: tutti PASS

- [ ] **Step 5: Smoke test manuale**

Run: `cd infra && make dev-core`

Naviga su `http://localhost:3000/library`. Verifica che:
- HandRail non appare (mano vuota — corretto)
- ActionPill mostra "Libreria" + "+ Aggiungi" nella parte bassa del desktop
- La sessione mobile bottom bar è ancora presente

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/layout/UserShell/DesktopShell.tsx
git commit -m "feat(nav): wire HandRail + ActionPill in DesktopShell, remove FloatingActionPill from shell"
```

---

## Note sui Piani 2 e 3

**Piano 2 — Mobile ActionBar + HandDrawer** (da scrivere dopo che Piano 1 è mergiato):
- `ActionBar` sostituisce `MobileBottomBar` per navigazione generale (mano mini + CTA)
- `HandDrawer` bottom sheet con entity-specific tabs (usa `useCascadeNavigationStore.openDrawer`)
- `DrawerContent` switch per entity: game, session, agent, kb, toolkit, event, player
- Wiring in `DesktopShell` (mobile breakpoint)

**Piano 3 — MeepleCard ManaPips + Focus + Dashboard Grid** (da scrivere dopo Piano 2):
- `ManaPips` sub-component (pip colorati in grid/compact variant)
- `FocusCard` variant + aggiunta a `MeepleCardVariant` in `types.ts`
- `DashboardClient.tsx` — replace `.game-card` con `MeepleCard variant="grid"`
- Action → landing state: `router.push(href, { state: { drawerOpen, drawerTab } })`
