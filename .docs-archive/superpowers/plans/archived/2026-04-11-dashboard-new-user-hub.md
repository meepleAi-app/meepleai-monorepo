# Dashboard New User Hub — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire completamente la dashboard utente esistente con il layout Hub-block basato sul mockup `dashboard-new-user-mockup.html`: sezione Giochi (con fallback catalogo quando la libreria è vuota), Sessioni (empty state + CTA), Agenti (empty state + dual CTA) e Toolkit carousel.

**Architecture:** `DashboardClient` orchestrates five section components (GreetingRow, GamesHubBlock, SessionsHubBlock, AgentsHubBlock, ToolkitHubBlock). Each HubBlock is self-contained with its own search toolbar + filter chips. GamesHubBlock reads `useLibraryStats` to decide empty vs populated state and `useSharedGames` for catalog fallback. Sessions/Agents are static empty states for this sprint.

**Tech Stack:** Next.js 16 App Router, React 19, TanStack Query v5 (`useLibraryStats`, `useSharedGames`, `useAddGameToLibrary`), Tailwind 4, Vitest + React Testing Library.

---

## File Map

### DELETE (old sections no longer needed)
- `apps/web/src/app/(authenticated)/dashboard/dashboard-mobile.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/KpiStrip.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/ContinueCarousel.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/ChatRecentCards.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/FriendsRow.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/HeroLiveSession.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/KpiStrip.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/ContinueCarousel.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/ChatRecentCards.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/FriendsRow.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/HeroLiveSession.test.tsx`

### MODIFY
- `apps/web/src/app/(authenticated)/dashboard/sections/GreetingRow.tsx` — rimuovere prop `stats`, semplificare
- `apps/web/src/app/(authenticated)/dashboard/__tests__/GreetingRow.test.tsx` — aggiornare per nuova firma
- `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx` — riscrittura completa
- `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx` — riscrittura completa
- `apps/web/src/app/(authenticated)/dashboard/page.tsx` — rimuovere split mobile/desktop

### CREATE
- `apps/web/src/app/(authenticated)/dashboard/sections/HubBlock.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/GamesHubBlock.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/SessionsHubBlock.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/AgentsHubBlock.tsx`
- `apps/web/src/app/(authenticated)/dashboard/sections/ToolkitHubBlock.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/GamesHubBlock.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/SessionsHubBlock.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/AgentsHubBlock.test.tsx`
- `apps/web/src/app/(authenticated)/dashboard/__tests__/ToolkitHubBlock.test.tsx`

---

## Task 0: Delete vecchi file

**Files:** Delete tutte le sezioni/test dell'elenco DELETE sopra.

- [ ] **Step 1: Elimina i file obsoleti**

```bash
cd apps/web/src/app/\(authenticated\)/dashboard
rm dashboard-mobile.tsx
rm sections/KpiStrip.tsx sections/ContinueCarousel.tsx
rm sections/ChatRecentCards.tsx sections/FriendsRow.tsx sections/HeroLiveSession.tsx
rm __tests__/KpiStrip.test.tsx __tests__/ContinueCarousel.test.tsx
rm __tests__/ChatRecentCards.test.tsx __tests__/FriendsRow.test.tsx
rm __tests__/HeroLiveSession.test.tsx
```

- [ ] **Step 2: Verifica che i file siano stati eliminati**

```bash
ls sections/ __tests__/
```

Expected: nessun riferimento a KpiStrip, ContinueCarousel, ChatRecentCards, FriendsRow, HeroLiveSession, dashboard-mobile.

- [ ] **Step 3: Commit**

```bash
git add -A apps/web/src/app/\(authenticated\)/dashboard/
git commit -m "chore(dashboard): remove old dashboard sections (KpiStrip, ContinueCarousel, ChatRecentCards, FriendsRow, HeroLiveSession, DashboardMobile)"
```

---

## Task 1: Semplifica GreetingRow

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/sections/GreetingRow.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/GreetingRow.test.tsx`

- [ ] **Step 1: Riscrivi il test con la nuova firma (senza `stats`)**

```tsx
// apps/web/src/app/(authenticated)/dashboard/__tests__/GreetingRow.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GreetingRow } from '../sections/GreetingRow';

describe('GreetingRow', () => {
  it('renders greeting with user display name', () => {
    render(<GreetingRow displayName="Aaron" subtitle="La tua tavola da gioco" />);
    expect(screen.getByText(/Ciao/)).toBeInTheDocument();
    expect(screen.getByText('Aaron')).toBeInTheDocument();
  });

  it('renders subtitle', () => {
    render(<GreetingRow displayName="Marco" subtitle="La tua tavola da gioco" />);
    expect(screen.getByText('La tua tavola da gioco')).toBeInTheDocument();
  });

  it('does not render stats cluster (stats prop removed)', () => {
    const { container } = render(<GreetingRow displayName="X" subtitle="Y" />);
    expect(container.querySelector('[data-testid="greet-stats"]')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/GreetingRow.test.tsx
```

Expected: FAIL — `GreetingRow` richiede ancora `stats` prop.

- [ ] **Step 3: Riscrivi GreetingRow senza `stats`**

```tsx
// apps/web/src/app/(authenticated)/dashboard/sections/GreetingRow.tsx
'use client';

interface GreetingRowProps {
  displayName: string;
  subtitle: string;
}

/**
 * Dashboard greeting: "Ciao {name} 👋" con sottotitolo.
 * Mockup ref: dashboard-new-user-mockup.html §greeting
 */
export function GreetingRow({ displayName, subtitle }: GreetingRowProps) {
  return (
    <div className="px-4 pt-4 pb-2">
      <h2 className="font-quicksand text-[1.2rem] font-extrabold text-[var(--nh-text-primary)]">
        Ciao,{' '}
        <span
          className="bg-clip-text text-transparent"
          style={{ backgroundImage: 'linear-gradient(135deg, hsl(25 95% 48%), hsl(38 92% 55%))' }}
        >
          {displayName}
        </span>{' '}
        <span aria-hidden>👋</span>
      </h2>
      <p className="mt-0.5 text-[0.75rem] text-[var(--nh-text-muted)]">{subtitle}</p>
    </div>
  );
}
```

- [ ] **Step 4: Esegui il test per verificare che passa**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/GreetingRow.test.tsx
```

Expected: PASS 2/2.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/sections/GreetingRow.tsx \
        apps/web/src/app/\(authenticated\)/dashboard/__tests__/GreetingRow.test.tsx
git commit -m "refactor(dashboard): simplify GreetingRow — remove stats prop"
```

---

## Task 2: HubBlock — componente base

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/sections/HubBlock.tsx`

HubBlock è il container condiviso da tutti i blocchi hub: ha un titolo sezione, una search bar e filter chips orizzontali, e accetta `children` per il contenuto specifico.

- [ ] **Step 1: Crea HubBlock**

```tsx
// apps/web/src/app/(authenticated)/dashboard/sections/HubBlock.tsx
'use client';

import type { ReactNode } from 'react';

export interface FilterChip {
  label: string;
  value: string;
}

interface HubBlockProps {
  title: string;
  searchPlaceholder?: string;
  chips?: FilterChip[];
  activeChip?: string;
  onChipChange?: (value: string) => void;
  children: ReactNode;
}

/**
 * Contenitore riusabile per i blocchi hub della dashboard.
 * Ogni blocco ha titolo sezione, barra di ricerca inline e filter chips.
 * Mockup ref: dashboard-new-user-mockup.html §hub-block + §hub-toolbar
 */
export function HubBlock({
  title,
  searchPlaceholder = 'Cerca...',
  chips = [],
  activeChip,
  onChipChange,
  children,
}: HubBlockProps) {
  return (
    <section className="px-3.5 pb-1 pt-2.5">
      {/* Titolo sezione */}
      <div className="mb-1.5 flex items-center gap-1.5">
        <span className="font-quicksand text-[0.72rem] font-extrabold uppercase tracking-[0.07em] text-[var(--nh-text-muted)]">
          {title}
        </span>
      </div>

      {/* Search toolbar */}
      <div className="mb-1.5 flex h-[30px] items-center gap-2 rounded-full border border-[var(--nh-border-default)] bg-white/70 px-3">
        <span aria-hidden className="text-[var(--nh-text-muted)]">
          🔍
        </span>
        <span className="flex-1 text-[10px] text-[var(--nh-text-muted)]">{searchPlaceholder}</span>
      </div>

      {/* Filter chips */}
      {chips.length > 0 && (
        <div className="mb-1.5 flex gap-1 overflow-x-auto [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
          {chips.map(chip => (
            <button
              key={chip.value}
              type="button"
              onClick={() => onChipChange?.(chip.value)}
              className={[
                'flex-shrink-0 rounded-[14px] border px-2.5 py-[3px] font-quicksand text-[10px] font-bold',
                chip.value === activeChip
                  ? 'border-transparent bg-[var(--nh-text-primary)] text-white'
                  : 'border-[var(--nh-border-default)] bg-white/70 text-[var(--nh-text-muted)]',
              ].join(' ')}
            >
              {chip.label}
            </button>
          ))}
        </div>
      )}

      {children}
    </section>
  );
}
```

- [ ] **Step 2: Commit (nessun test — componente strutturale puro, coperto dai test dei blocchi figli)**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/sections/HubBlock.tsx
git commit -m "feat(dashboard): add HubBlock base container component"
```

---

## Task 3: GamesHubBlock

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/sections/GamesHubBlock.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/GamesHubBlock.test.tsx`

Logica: se `libraryStats.owned === 0` mostra hint banner + grid 2 colonne di top catalog con badge KB. Ogni card ha pulsante "+ Aggiungi" che chiama `useAddGameToLibrary`.

- [ ] **Step 1: Scrivi il test che fallisce**

```tsx
// apps/web/src/app/(authenticated)/dashboard/__tests__/GamesHubBlock.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockAddGame = vi.fn();

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryStats: () => ({ data: { owned: 0 }, isLoading: false }),
  useAddGameToLibrary: () => ({ mutate: mockAddGame, isPending: false }),
}));

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: () => ({
    data: {
      items: [
        { id: 'g1', title: 'Puerto Rico', publishers: [{ id: 'p1', name: 'Alea' }], hasKnowledgeBase: true, thumbnailUrl: null },
        { id: 'g2', title: 'Paleo', publishers: [{ id: 'p2', name: 'Hans im Glück' }], hasKnowledgeBase: false, thumbnailUrl: null },
      ],
      totalCount: 2,
    },
    isLoading: false,
  }),
}));

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { GamesHubBlock } from '../sections/GamesHubBlock';

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('GamesHubBlock', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('shows catalog hint banner when library is empty', () => {
    render(<GamesHubBlock />, { wrapper: createWrapper() });
    expect(screen.getByText(/Libreria vuota/i)).toBeInTheDocument();
  });

  it('renders catalog game cards', () => {
    render(<GamesHubBlock />, { wrapper: createWrapper() });
    expect(screen.getByText('Puerto Rico')).toBeInTheDocument();
    expect(screen.getByText('Paleo')).toBeInTheDocument();
  });

  it('shows KB-yes badge for games with knowledge base', () => {
    render(<GamesHubBlock />, { wrapper: createWrapper() });
    expect(screen.getByText('KB ✓')).toBeInTheDocument();
  });

  it('shows KB-no badge for games without knowledge base', () => {
    render(<GamesHubBlock />, { wrapper: createWrapper() });
    expect(screen.getByText('KB –')).toBeInTheDocument();
  });

  it('calls addGame mutation when Aggiungi button is clicked', async () => {
    const user = userEvent.setup();
    render(<GamesHubBlock />, { wrapper: createWrapper() });
    const buttons = screen.getAllByRole('button', { name: /aggiungi/i });
    await user.click(buttons[0]);
    expect(mockAddGame).toHaveBeenCalledWith({ gameId: 'g1' });
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/GamesHubBlock.test.tsx
```

Expected: FAIL — `GamesHubBlock` non esiste.

- [ ] **Step 3: Crea GamesHubBlock**

```tsx
// apps/web/src/app/(authenticated)/dashboard/sections/GamesHubBlock.tsx
'use client';

import { useState } from 'react';

import { useAddGameToLibrary } from '@/hooks/queries/useLibrary';
import { useLibraryStats } from '@/hooks/queries/useLibrary';
import { useSharedGames } from '@/hooks/queries/useSharedGames';

import { HubBlock } from './HubBlock';

const CHIPS = [
  { label: 'Tutti', value: 'all' },
  { label: 'Recenti', value: 'recent' },
];

/**
 * Blocco Giochi della dashboard.
 * - Libreria vuota: mostra hint + top 6 giochi dal catalogo (KB-first)
 * - Libreria popolata: placeholder per future iterazioni
 * Mockup ref: dashboard-new-user-mockup.html §phone-1
 */
export function GamesHubBlock() {
  const [activeChip, setActiveChip] = useState('all');
  const { data: stats } = useLibraryStats();
  const isLibraryEmpty = !stats || stats.owned === 0;

  const { data: catalogData, isLoading } = useSharedGames(
    { hasKnowledgeBase: true, pageSize: 6 },
    isLibraryEmpty
  );

  const { mutate: addGame, isPending } = useAddGameToLibrary();

  // Sort: KB games first, then non-KB
  const catalogGames = [...(catalogData?.items ?? [])].sort((a, b) => {
    if (a.hasKnowledgeBase === b.hasKnowledgeBase) return 0;
    return a.hasKnowledgeBase ? -1 : 1;
  });

  return (
    <HubBlock
      title="🎲 Giochi"
      searchPlaceholder="Cerca giochi..."
      chips={CHIPS}
      activeChip={activeChip}
      onChipChange={setActiveChip}
    >
      {isLibraryEmpty && (
        <>
          {/* Hint banner */}
          <div className="mb-1.5 flex items-center gap-1.5 rounded-lg border border-amber-200/60 bg-amber-50 px-2.5 py-1.5 text-[9.5px] font-semibold text-[var(--nh-text-muted)]">
            <span>💡</span>
            <span>Libreria vuota — ecco i top giochi dal catalogo. Aggiungili per iniziare!</span>
          </div>

          {/* Catalog grid 2 colonne */}
          {isLoading ? (
            <div className="grid grid-cols-2 gap-1.5">
              {Array.from({ length: 4 }).map((_, i) => (
                <div
                  key={i}
                  className="h-[120px] animate-pulse rounded-xl bg-[var(--nh-border-default)]"
                />
              ))}
            </div>
          ) : (
            <div className="grid grid-cols-2 gap-[7px]">
              {catalogGames.map(game => (
                <div
                  key={game.id}
                  className="flex flex-col overflow-hidden rounded-xl border border-[var(--nh-border-default)] bg-white shadow-[var(--shadow-warm-sm)]"
                >
                  {/* Thumb */}
                  <div className="relative flex h-[68px] items-center justify-center bg-gradient-to-br from-amber-50 to-amber-100 text-[32px]">
                    🎮
                    <span
                      className={[
                        'absolute right-1 top-1 rounded-[6px] px-1 py-px font-quicksand text-[8px] font-extrabold text-white',
                        game.hasKnowledgeBase ? 'bg-green-500' : 'bg-slate-400',
                      ].join(' ')}
                    >
                      {game.hasKnowledgeBase ? 'KB ✓' : 'KB –'}
                    </span>
                  </div>
                  {/* Info */}
                  <div className="flex-1 px-[7px] py-[5px]">
                    <div className="line-clamp-2 font-quicksand text-[0.73rem] font-bold leading-tight text-[var(--nh-text-primary)]">
                      {game.title}
                    </div>
                    {game.publishers?.[0]?.name && (
                      <div className="mt-px text-[0.62rem] text-[var(--nh-text-muted)]">
                        {game.publishers[0].name}
                      </div>
                    )}
                  </div>
                  {/* Add button */}
                  <button
                    type="button"
                    disabled={isPending}
                    onClick={() => addGame({ gameId: game.id })}
                    className="mx-1.5 mb-1.5 flex h-[22px] items-center justify-center gap-1 rounded-lg bg-[hsl(25,95%,45%)] text-[10px] font-bold text-white disabled:opacity-60"
                  >
                    <span>+</span> Aggiungi
                  </button>
                </div>
              ))}
            </div>
          )}
        </>
      )}

      {!isLibraryEmpty && (
        <div className="py-4 text-center text-xs text-[var(--nh-text-muted)]">
          I tuoi giochi apparirà qui.
        </div>
      )}
    </HubBlock>
  );
}
```

- [ ] **Step 4: Esegui i test per verificare che passano**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/GamesHubBlock.test.tsx
```

Expected: PASS 5/5.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/sections/GamesHubBlock.tsx \
        apps/web/src/app/\(authenticated\)/dashboard/__tests__/GamesHubBlock.test.tsx
git commit -m "feat(dashboard): add GamesHubBlock with catalog empty state and KB badges"
```

---

## Task 4: SessionsHubBlock

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/sections/SessionsHubBlock.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/SessionsHubBlock.test.tsx`

- [ ] **Step 1: Scrivi il test che fallisce**

```tsx
// apps/web/src/app/(authenticated)/dashboard/__tests__/SessionsHubBlock.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { SessionsHubBlock } from '../sections/SessionsHubBlock';

describe('SessionsHubBlock', () => {
  it('renders the section title', () => {
    render(<SessionsHubBlock />);
    expect(screen.getByText(/Sessioni/i)).toBeInTheDocument();
  });

  it('shows empty state when no sessions', () => {
    render(<SessionsHubBlock />);
    expect(screen.getByText(/Nessuna sessione/i)).toBeInTheDocument();
  });

  it('renders the Crea sessione CTA button', () => {
    render(<SessionsHubBlock />);
    expect(screen.getByRole('button', { name: /crea sessione/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/SessionsHubBlock.test.tsx
```

Expected: FAIL — `SessionsHubBlock` non esiste.

- [ ] **Step 3: Crea SessionsHubBlock**

```tsx
// apps/web/src/app/(authenticated)/dashboard/sections/SessionsHubBlock.tsx
'use client';

import { useState } from 'react';

import { useRouter } from 'next/navigation';

import { HubBlock } from './HubBlock';

const CHIPS = [
  { label: 'Tutte', value: 'all' },
  { label: 'Attive', value: 'active' },
  { label: 'Recenti', value: 'recent' },
];

/**
 * Blocco Sessioni della dashboard — empty state con CTA.
 * Fase attuale: solo stato vuoto. Sessioni attive wiring in sprint successivo.
 * Mockup ref: dashboard-new-user-mockup.html §phone-2 sessions block
 */
export function SessionsHubBlock() {
  const [activeChip, setActiveChip] = useState('all');
  const router = useRouter();

  return (
    <HubBlock
      title="🎯 Sessioni"
      searchPlaceholder="Filtra per stato..."
      chips={CHIPS}
      activeChip={activeChip}
      onChipChange={setActiveChip}
    >
      {/* Empty state */}
      <div className="flex flex-col items-center gap-1.5 rounded-[14px] border border-dashed border-amber-300/40 bg-white/60 px-4 py-5 text-center">
        <span className="text-[28px]">🎯</span>
        <div className="font-quicksand text-[0.8rem] font-bold text-[var(--nh-text-primary)]">
          Nessuna sessione
        </div>
        <p className="max-w-[230px] text-[0.7rem] leading-[1.4] text-[var(--nh-text-muted)]">
          Inizia una nuova partita e traccia i tuoi progressi in tempo reale.
        </p>
        <button
          type="button"
          onClick={() => router.push('/sessions/new')}
          className="mt-1 inline-flex items-center gap-1.5 rounded-[14px] bg-[hsl(25,95%,45%)] px-4 py-1.5 font-quicksand text-[0.72rem] font-bold text-white shadow-[0_2px_8px_rgba(180,100,20,0.25)]"
        >
          + Crea sessione
        </button>
      </div>
    </HubBlock>
  );
}
```

- [ ] **Step 4: Esegui i test per verificare che passano**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/SessionsHubBlock.test.tsx
```

Expected: PASS 3/3.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/sections/SessionsHubBlock.tsx \
        apps/web/src/app/\(authenticated\)/dashboard/__tests__/SessionsHubBlock.test.tsx
git commit -m "feat(dashboard): add SessionsHubBlock with empty state and CTA"
```

---

## Task 5: AgentsHubBlock

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/sections/AgentsHubBlock.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/AgentsHubBlock.test.tsx`

- [ ] **Step 1: Scrivi il test che fallisce**

```tsx
// apps/web/src/app/(authenticated)/dashboard/__tests__/AgentsHubBlock.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { AgentsHubBlock } from '../sections/AgentsHubBlock';

describe('AgentsHubBlock', () => {
  it('renders the section title', () => {
    render(<AgentsHubBlock />);
    expect(screen.getByText(/Agenti AI/i)).toBeInTheDocument();
  });

  it('shows empty state text', () => {
    render(<AgentsHubBlock />);
    expect(screen.getByText(/Nessun agente attivo/i)).toBeInTheDocument();
  });

  it('renders primary CTA "Avvia chat"', () => {
    render(<AgentsHubBlock />);
    expect(screen.getByRole('button', { name: /avvia chat/i })).toBeInTheDocument();
  });

  it('renders secondary CTA "Crea agente"', () => {
    render(<AgentsHubBlock />);
    expect(screen.getByRole('button', { name: /crea agente/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/AgentsHubBlock.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Crea AgentsHubBlock**

```tsx
// apps/web/src/app/(authenticated)/dashboard/sections/AgentsHubBlock.tsx
'use client';

import { useState } from 'react';

import { useRouter } from 'next/navigation';

import { HubBlock } from './HubBlock';

const CHIPS = [
  { label: 'Tutti', value: 'all' },
  { label: 'Attivi', value: 'active' },
];

/**
 * Blocco Agenti AI della dashboard — empty state con doppia CTA.
 * Mockup ref: dashboard-new-user-mockup.html §phone-2 agents block
 */
export function AgentsHubBlock() {
  const [activeChip, setActiveChip] = useState('all');
  const router = useRouter();

  return (
    <HubBlock
      title="🤖 Agenti AI"
      searchPlaceholder="Cerca agenti..."
      chips={CHIPS}
      activeChip={activeChip}
      onChipChange={setActiveChip}
    >
      {/* Empty state */}
      <div className="flex flex-col items-center gap-1.5 rounded-[14px] border border-dashed border-amber-300/40 bg-white/60 px-4 py-5 text-center">
        <span className="text-[28px]">🤖</span>
        <div className="font-quicksand text-[0.8rem] font-bold text-[var(--nh-text-primary)]">
          Nessun agente attivo
        </div>
        <p className="max-w-[230px] text-[0.7rem] leading-[1.4] text-[var(--nh-text-muted)]">
          Avvia una chat con un agente AI per ricevere aiuto durante la partita.
        </p>
        <div className="mt-1 flex flex-wrap justify-center gap-2">
          <button
            type="button"
            onClick={() => router.push('/chat')}
            className="inline-flex items-center gap-1.5 rounded-[14px] bg-[hsl(25,95%,45%)] px-4 py-1.5 font-quicksand text-[0.72rem] font-bold text-white shadow-[0_2px_8px_rgba(180,100,20,0.25)]"
          >
            🤖 Avvia chat
          </button>
          <button
            type="button"
            onClick={() => router.push('/agents')}
            className="inline-flex items-center gap-1.5 rounded-[14px] border border-[hsl(25,95%,45%)] bg-transparent px-4 py-1.5 font-quicksand text-[0.72rem] font-bold text-[hsl(25,95%,45%)]"
          >
            + Crea agente
          </button>
        </div>
      </div>
    </HubBlock>
  );
}
```

- [ ] **Step 4: Esegui i test per verificare che passano**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/AgentsHubBlock.test.tsx
```

Expected: PASS 4/4.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/sections/AgentsHubBlock.tsx \
        apps/web/src/app/\(authenticated\)/dashboard/__tests__/AgentsHubBlock.test.tsx
git commit -m "feat(dashboard): add AgentsHubBlock with empty state and dual CTA"
```

---

## Task 6: ToolkitHubBlock

**Files:**
- Create: `apps/web/src/app/(authenticated)/dashboard/sections/ToolkitHubBlock.tsx`
- Create: `apps/web/src/app/(authenticated)/dashboard/__tests__/ToolkitHubBlock.test.tsx`

- [ ] **Step 1: Scrivi il test che fallisce**

```tsx
// apps/web/src/app/(authenticated)/dashboard/__tests__/ToolkitHubBlock.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { ToolkitHubBlock } from '../sections/ToolkitHubBlock';

describe('ToolkitHubBlock', () => {
  it('renders the section title', () => {
    render(<ToolkitHubBlock />);
    expect(screen.getByText(/Toolkit/i)).toBeInTheDocument();
  });

  it('renders the Dado tool card', () => {
    render(<ToolkitHubBlock />);
    expect(screen.getByText('Dado')).toBeInTheDocument();
  });

  it('renders the Clessidra tool card', () => {
    render(<ToolkitHubBlock />);
    expect(screen.getByText('Clessidra')).toBeInTheDocument();
  });

  it('renders the Scoreboard tool card', () => {
    render(<ToolkitHubBlock />);
    expect(screen.getByText('Scoreboard')).toBeInTheDocument();
  });

  it('renders the Token tool card', () => {
    render(<ToolkitHubBlock />);
    expect(screen.getByText('Token')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/ToolkitHubBlock.test.tsx
```

Expected: FAIL.

- [ ] **Step 3: Crea ToolkitHubBlock**

```tsx
// apps/web/src/app/(authenticated)/dashboard/sections/ToolkitHubBlock.tsx
'use client';

import { useRouter } from 'next/navigation';

interface ToolkitTool {
  id: string;
  icon: string;
  name: string;
  description: string;
  bgColor: string;
  href: string;
}

const TOOLS: ToolkitTool[] = [
  {
    id: 'dice',
    icon: '🎲',
    name: 'Dado',
    description: 'Lancia d4–d20, gruppi multipli',
    bgColor: 'hsl(25,95%,92%)',
    href: '/toolkit/dice',
  },
  {
    id: 'timer',
    icon: '⏳',
    name: 'Clessidra',
    description: 'Timer per turno, avvisi sonori',
    bgColor: 'hsl(210,80%,92%)',
    href: '/toolkit/timer',
  },
  {
    id: 'scoreboard',
    icon: '📊',
    name: 'Scoreboard',
    description: 'Punteggi multi-giocatore',
    bgColor: 'hsl(262,70%,92%)',
    href: '/toolkit/scoreboard',
  },
  {
    id: 'token',
    icon: '🔵',
    name: 'Token',
    description: 'Contatori risorse e punti',
    bgColor: 'hsl(140,60%,90%)',
    href: '/toolkit/tokens',
  },
];

/**
 * Blocco Toolkit della dashboard — carousel orizzontale di strumenti.
 * Sempre visibile, indipendente dalla libreria.
 * Mockup ref: dashboard-new-user-mockup.html §phone-3 toolkit carousel
 */
export function ToolkitHubBlock() {
  const router = useRouter();

  return (
    <section className="px-3.5 pb-4 pt-2.5">
      <div className="mb-2 flex items-center justify-between">
        <span className="font-quicksand text-[0.72rem] font-extrabold uppercase tracking-[0.07em] text-[var(--nh-text-muted)]">
          🔧 Toolkit
        </span>
        <span className="text-[9px] font-semibold text-[var(--nh-text-muted)]">
          Strumenti disponibili subito, senza libreria
        </span>
      </div>

      <div className="flex gap-2 overflow-x-auto pb-1 [scrollbar-width:none] [&::-webkit-scrollbar]:hidden">
        {TOOLS.map(tool => (
          <button
            key={tool.id}
            type="button"
            onClick={() => router.push(tool.href)}
            className="flex w-[100px] flex-shrink-0 flex-col items-center gap-1.5 rounded-xl border border-[var(--nh-border-default)] bg-white p-2.5 shadow-[var(--shadow-warm-sm)] transition-shadow hover:shadow-[var(--shadow-warm-md)]"
          >
            <div
              className="flex h-[38px] w-[38px] items-center justify-center rounded-[10px] text-[20px]"
              style={{ background: tool.bgColor }}
              aria-hidden
            >
              {tool.icon}
            </div>
            <div className="text-center font-quicksand text-[0.67rem] font-bold leading-tight text-[var(--nh-text-primary)]">
              {tool.name}
            </div>
            <div className="text-center text-[0.58rem] leading-[1.3] text-[var(--nh-text-muted)]">
              {tool.description}
            </div>
          </button>
        ))}
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Esegui i test per verificare che passano**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/ToolkitHubBlock.test.tsx
```

Expected: PASS 5/5.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/sections/ToolkitHubBlock.tsx \
        apps/web/src/app/\(authenticated\)/dashboard/__tests__/ToolkitHubBlock.test.tsx
git commit -m "feat(dashboard): add ToolkitHubBlock with static carousel (Dado, Clessidra, Scoreboard, Token)"
```

---

## Task 7: Riscrivi DashboardClient

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx`
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx`

- [ ] **Step 1: Riscrivi il test**

```tsx
// apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/components/auth/AuthProvider', () => ({
  useAuth: () => ({ user: { id: 'u1', displayName: 'Aaron' } }),
}));

vi.mock('@/lib/stores/mini-nav-config-store', () => {
  const state = { config: null, setConfig: vi.fn(), clear: vi.fn() };
  return {
    useMiniNavConfigStore: (selector?: (s: typeof state) => unknown) =>
      selector ? selector(state) : state,
  };
});

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryStats: () => ({ data: { owned: 0 }, isLoading: false }),
  useAddGameToLibrary: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: () => ({ data: { items: [], totalCount: 0 }, isLoading: false }),
}));

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { DashboardClient } from '../DashboardClient';

function createWrapper() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
}

describe('DashboardClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders greeting with user display name', () => {
    render(<DashboardClient />, { wrapper: createWrapper() });
    expect(screen.getByText('Aaron')).toBeInTheDocument();
  });

  it('renders all four hub block section titles', () => {
    render(<DashboardClient />, { wrapper: createWrapper() });
    expect(screen.getByText(/Giochi/i)).toBeInTheDocument();
    expect(screen.getByText(/Sessioni/i)).toBeInTheDocument();
    expect(screen.getByText(/Agenti AI/i)).toBeInTheDocument();
    expect(screen.getByText(/Toolkit/i)).toBeInTheDocument();
  });

  it('shows sessions empty state', () => {
    render(<DashboardClient />, { wrapper: createWrapper() });
    expect(screen.getByText(/Nessuna sessione/i)).toBeInTheDocument();
  });

  it('shows agents empty state', () => {
    render(<DashboardClient />, { wrapper: createWrapper() });
    expect(screen.getByText(/Nessun agente attivo/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui i test per verificare che falliscono**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/DashboardClient.test.tsx
```

Expected: FAIL — il vecchio DashboardClient ha componenti rimossi.

- [ ] **Step 3: Riscrivi DashboardClient**

```tsx
// apps/web/src/app/(authenticated)/dashboard/DashboardClient.tsx
'use client';

import { useEffect, useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { useMiniNavConfig } from '@/hooks/useMiniNavConfig';
import { useRecentsStore } from '@/stores/use-recents';

import { AgentsHubBlock } from './sections/AgentsHubBlock';
import { GamesHubBlock } from './sections/GamesHubBlock';
import { GreetingRow } from './sections/GreetingRow';
import { SessionsHubBlock } from './sections/SessionsHubBlock';
import { ToolkitHubBlock } from './sections/ToolkitHubBlock';

/**
 * Dashboard Hub — layout basato sul mockup dashboard-new-user-mockup.html.
 * Unico componente client per mobile e desktop: GreetingRow + 4 HubBlock.
 */
export function DashboardClient() {
  const { user } = useAuth();
  const router = useRouter();

  const miniNavConfig = useMemo(
    () => ({
      breadcrumb: 'Home',
      tabs: [{ id: 'overview', label: 'Overview', href: '/dashboard' }],
      activeTabId: 'overview',
      primaryAction: {
        label: 'Nuova partita',
        icon: '＋',
        onClick: () => router.push('/sessions/new'),
      },
    }),
    [router]
  );
  useMiniNavConfig(miniNavConfig);

  useEffect(() => {
    useRecentsStore.getState().push({
      id: 'section-dashboard',
      entity: 'game',
      title: 'Home',
      href: '/dashboard',
    });
  }, []);

  const displayName = user?.displayName ?? 'giocatore';

  return (
    <div className="flex flex-col gap-2 pb-20">
      <GreetingRow displayName={displayName} subtitle="La tua tavola da gioco" />
      <GamesHubBlock />
      <SessionsHubBlock />
      <AgentsHubBlock />
      <ToolkitHubBlock />
    </div>
  );
}
```

- [ ] **Step 4: Esegui i test per verificare che passano**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/__tests__/DashboardClient.test.tsx
```

Expected: PASS 4/4.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/DashboardClient.tsx \
        apps/web/src/app/\(authenticated\)/dashboard/__tests__/DashboardClient.test.tsx
git commit -m "feat(dashboard): rewrite DashboardClient with hub layout (GamesHubBlock, SessionsHubBlock, AgentsHubBlock, ToolkitHubBlock)"
```

---

## Task 8: Aggiorna page.tsx — rimuovi split mobile/desktop

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/page.tsx`

- [ ] **Step 1: Aggiorna page.tsx**

```tsx
// apps/web/src/app/(authenticated)/dashboard/page.tsx
import { RequireRole } from '@/components/auth/RequireRole';

import { DashboardClient } from './DashboardClient';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Gaming Hub | MeepleAI',
  description:
    'La tua tavola da gioco personale. Gestisci giochi, sessioni, agenti AI e toolkit.',
  openGraph: {
    title: 'Gaming Hub | MeepleAI',
    description: 'La tua tavola da gioco personale.',
    type: 'website',
  },
};

export const dynamic = 'force-dynamic';

export default function GamingHubDashboardPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <DashboardClient />
    </RequireRole>
  );
}
```

- [ ] **Step 2: Verifica typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 3: Esegui tutti i test della dashboard**

```bash
cd apps/web && pnpm test src/app/\\(authenticated\\)/dashboard/
```

Expected: tutti i nuovi test passano, nessun riferimento ai file eliminati.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/page.tsx
git commit -m "refactor(dashboard): remove mobile/desktop split — single DashboardClient for all breakpoints"
```

---

## Task 9: Pulizia finale e verifica

- [ ] **Step 1: Esegui lint**

```bash
cd apps/web && pnpm lint
```

Expected: 0 errori.

- [ ] **Step 2: Esegui tutti i test del progetto**

```bash
cd apps/web && pnpm test --run
```

Expected: nessun test fallisce per le modifiche di questa PR.

- [ ] **Step 3: Typecheck completo**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errors.

- [ ] **Step 4: Commit finale se necessario e push**

```bash
git push -u origin $(git branch --show-current)
```

---

## Self-Review Checklist

### Spec coverage
- [x] Greeting "Ciao, {name} 👋" + sottotitolo → Task 1 (GreetingRow)
- [x] Games HubBlock con toolbar + filter chips → Task 2+3
- [x] Catalog hint banner quando libreria vuota → Task 3 (GamesHubBlock)
- [x] Badge KB ✓ / KB – → Task 3 (GamesHubBlock)
- [x] Bottone "+ Aggiungi" → Task 3 (addGame mutation)
- [x] Sessions empty state + CTA "Crea sessione" → Task 4
- [x] Agents empty state + "Avvia chat" + "Crea agente" → Task 5
- [x] Toolkit carousel (Dado, Clessidra, Scoreboard, Token) → Task 6
- [x] HubLayout toolbar presente anche se blocco vuoto → Task 2 (HubBlock applica sempre toolbar)
- [x] Rimozione vecchie sezioni → Task 0
- [x] Rimozione split mobile/desktop → Task 8

### Gap aperti (da spec panel, non in scope di questo PR)
- G-03: dopo "+ Aggiungi" la card non scompare dalla grid (ottimistica: il mutation invalida `useLibraryStats` → ricarica al prossimo render)
- G-04: fallback se catalogo vuoto → già gestito (grid vuota, senza errori)
- Games populated state → placeholder testuale, wiring completo in sprint successivo
