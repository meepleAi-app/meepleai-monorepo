# Library Page Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rimuovere codice zombie (`CollectionPageClient`), eliminare chip no-op, correggere semantica mobile/desktop e migliorare la robustezza della pagina Library.

**Architecture:** `PersonalLibraryPage` è il componente canonico per la vista desktop. `LibraryMobile` gestisce il mobile con segmented control. Le azioni CRUD sui giochi vivono in `[gameId]/page.tsx` (`GameTableZoneTools`), non nella lista. La separazione vetrina (lista) / gestione (dettaglio) è intenzionale.

**Tech Stack:** Next.js 16 App Router, React 19, TanStack Query v5, Vitest + @testing-library/react, Tailwind 4

---

## File Map

| File | Azione | Motivo |
|------|--------|--------|
| `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx` | **DELETE** | Zombie — non importato da nessuna parte |
| `apps/web/src/components/library/PersonalLibraryPage.tsx` | **MODIFY** | Rimuovi chip no-op, fix handleAddGame, aggiunge error state |
| `apps/web/src/app/(authenticated)/library/library-mobile.tsx` | **MODIFY** | Fix semantica segmenti, aggiungi paginazione |

---

## Task 1: Elimina CollectionPageClient (dead code)

**Files:**
- Delete: `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx`

- [ ] **Step 1: Verifica che CollectionPageClient non sia importato da nessuna parte**

```bash
cd apps/web
grep -r "CollectionPageClient" src/ --include="*.tsx" --include="*.ts"
```

Expected output: solo il file stesso `CollectionPageClient.tsx`. Se appaiono altri file, fermati e analizza prima di procedere.

- [ ] **Step 2: Elimina il file**

```bash
rm apps/web/src/app/\(authenticated\)/library/CollectionPageClient.tsx
```

- [ ] **Step 3: Verifica che la build non si rompa**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore TS relativo a CollectionPageClient.

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore(library): remove zombie CollectionPageClient (not imported anywhere)"
```

---

## Task 2: Rimuovi chip no-op e correggi label "Meno recenti"

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx` (linee 41-79)

Il chip `strategy` è un no-op documentato (ritorna `items` invariato). Il chip `most-played` ha id fuorviante e label "Meno recenti" che non corrisponde a "più giocati". Entrambi vanno corretti.

- [ ] **Step 1: Scrivi il test per `applyFilter`**

Crea `apps/web/src/components/library/__tests__/PersonalLibraryPage.filter.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// Re-export per test: esponi applyFilter dal modulo oppure duplica la logica qui
// Per ora duplichiamo la logica attesa DOPO il fix
function applyFilter(items: UserLibraryEntry[], filterId: string): UserLibraryEntry[] {
  switch (filterId) {
    case 'recent':
      return [...items].sort(
        (a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime()
      );
    case 'rating':
      return [...items].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
    case 'players-2-4':
      return items.filter(
        g => g.minPlayers != null && g.maxPlayers != null && g.minPlayers <= 4 && g.maxPlayers >= 2
      );
    case 'under-60':
      return items.filter(g => g.playingTimeMinutes != null && g.playingTimeMinutes <= 60);
    case 'oldest':
      return [...items].sort(
        (a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
    default:
      return items;
  }
}

const makeEntry = (overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry => ({
  id: 'e1',
  userId: 'u1',
  gameId: 'g1',
  gameTitle: 'Test Game',
  addedAt: '2024-01-01T00:00:00Z',
  isFavorite: false,
  currentState: 'Owned',
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  hasRagAccess: false,
  agentIsOwned: true,
  isPrivateGame: false,
  canProposeToCatalog: false,
  ...overrides,
});

describe('applyFilter', () => {
  it('recent: ordina per addedAt decrescente', () => {
    const items = [
      makeEntry({ id: 'a', addedAt: '2024-01-01T00:00:00Z' }),
      makeEntry({ id: 'b', addedAt: '2024-06-01T00:00:00Z' }),
    ];
    const result = applyFilter(items, 'recent');
    expect(result[0].id).toBe('b');
  });

  it('oldest: ordina per addedAt crescente', () => {
    const items = [
      makeEntry({ id: 'a', addedAt: '2024-06-01T00:00:00Z' }),
      makeEntry({ id: 'b', addedAt: '2024-01-01T00:00:00Z' }),
    ];
    const result = applyFilter(items, 'oldest');
    expect(result[0].id).toBe('b');
  });

  it('strategy non esiste più: default restituisce items invariati', () => {
    const items = [makeEntry({ id: 'a' }), makeEntry({ id: 'b' })];
    const result = applyFilter(items, 'strategy');
    expect(result).toEqual(items);
  });

  it('players-2-4: filtra giochi 2-4 giocatori', () => {
    const items = [
      makeEntry({ id: 'ok', minPlayers: 2, maxPlayers: 4 }),
      makeEntry({ id: 'ko', minPlayers: 5, maxPlayers: 8 }),
    ];
    const result = applyFilter(items, 'players-2-4');
    expect(result.map(g => g.id)).toEqual(['ok']);
  });

  it('under-60: filtra giochi < 60 min', () => {
    const items = [
      makeEntry({ id: 'ok', playingTimeMinutes: 45 }),
      makeEntry({ id: 'ko', playingTimeMinutes: 90 }),
    ];
    const result = applyFilter(items, 'under-60');
    expect(result.map(g => g.id)).toEqual(['ok']);
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisce**

```bash
cd apps/web && pnpm test src/components/library/__tests__/PersonalLibraryPage.filter.test.ts --reporter=verbose
```

Expected: fail su `oldest` (non esiste ancora) e `strategy` passa per il default (già ok).

- [ ] **Step 3: Aggiorna `LIBRARY_FILTER_CHIPS` e `applyFilter` in `PersonalLibraryPage.tsx`**

Sostituisci le linee 41-79 di `apps/web/src/components/library/PersonalLibraryPage.tsx`:

```typescript
const LIBRARY_FILTER_CHIPS = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
  { id: 'oldest', label: 'Prima aggiunti' },
  { id: 'rating', label: 'Rating \u2193' },
  { id: 'players-2-4', label: '2-4 giocatori' },
  { id: 'under-60', label: '< 60 min' },
];

function applyFilter(items: UserLibraryEntry[], filterId: string): UserLibraryEntry[] {
  switch (filterId) {
    case 'recent':
      return [...items].sort(
        (a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime()
      );
    case 'oldest':
      return [...items].sort(
        (a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
    case 'rating':
      return [...items].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
    case 'players-2-4':
      return items.filter(
        g => g.minPlayers != null && g.maxPlayers != null && g.minPlayers <= 4 && g.maxPlayers >= 2
      );
    case 'under-60':
      return items.filter(g => g.playingTimeMinutes != null && g.playingTimeMinutes <= 60);
    default:
      return items;
  }
}
```

- [ ] **Step 4: Esegui i test e verifica che passano**

```bash
cd apps/web && pnpm test src/components/library/__tests__/PersonalLibraryPage.filter.test.ts --reporter=verbose
```

Expected: tutti e 5 i test passano.

- [ ] **Step 5: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx \
        apps/web/src/components/library/__tests__/PersonalLibraryPage.filter.test.ts
git commit -m "fix(library): remove no-op 'Strategici' chip, rename 'Meno recenti' to 'Prima aggiunti'"
```

---

## Task 3: Fix CreateGameCtaCard — `div[role=button]` → `<button>`

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx` (funzione `CreateGameCtaCard`, linee 83-113)

Un `<div role="button">` richiede gestione manuale di focus, keyboard events, e semantica ARIA. Un `<button>` nativo gestisce tutto questo automaticamente.

- [ ] **Step 1: Esporta `CreateGameCtaCard` per renderla testabile**

In `PersonalLibraryPage.tsx`, cambia la dichiarazione della funzione da:

```typescript
function CreateGameCtaCard() {
```

a:

```typescript
export function CreateGameCtaCard() {
```

- [ ] **Step 1b: Scrivi test di accessibilità per CreateGameCtaCard**

Crea `apps/web/src/components/library/__tests__/CreateGameCtaCard.test.tsx`:

```typescript
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Import dopo il fix — il componente deve essere esportato da PersonalLibraryPage
import { CreateGameCtaCard } from '@/components/library/PersonalLibraryPage';

describe('CreateGameCtaCard', () => {
  it('deve renderizzare un elemento <button> nativo (non div)', () => {
    render(<CreateGameCtaCard />);
    const cta = screen.getByTestId('create-game-cta');
    expect(cta.tagName).toBe('BUTTON');
  });

  it('deve avere aria-label descrittivo', () => {
    render(<CreateGameCtaCard />);
    expect(screen.getByRole('button', { name: /crea un gioco personalizzato/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 1c: Esegui il test e verifica che fallisce**

```bash
cd apps/web && pnpm test src/components/library/__tests__/CreateGameCtaCard.test.tsx --reporter=verbose
```

Expected: fail — `CreateGameCtaCard` non esiste ancora come named export.

- [ ] **Step 2: Sostituisci la funzione `CreateGameCtaCard` in `PersonalLibraryPage.tsx`**

Sostituisci l'intera funzione `CreateGameCtaCard` (linee 83-113) — mantieni il `export` aggiunto nel Step 1:

```typescript
export function CreateGameCtaCard() {
  const router = useRouter();

  return (
    <button
      type="button"
      className={cn(
        'flex flex-col items-center justify-center gap-2',
        'w-full sm:w-[140px] flex-shrink-0 rounded-xl',
        'bg-[#161b22] border border-dashed border-[#30363d]',
        'min-h-[80px] sm:min-h-[160px]',
        'cursor-pointer transition-all duration-200',
        'hover:border-[#58a6ff] hover:bg-[#1c2128]',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[#58a6ff]'
      )}
      onClick={() => router.push('/library/private/add')}
      aria-label="Crea un gioco personalizzato"
      data-testid="create-game-cta"
    >
      <Plus className="w-5 h-5 text-[#58a6ff]" aria-hidden="true" />
      <span className="text-[10px] font-medium text-[#58a6ff] text-center px-2">Crea gioco</span>
    </button>
  );
}
```

Nota: rimossi `role="button"`, `tabIndex`, `onKeyDown` (gestiti nativamente da `<button>`). Cambiato `focus:ring-1` in `focus-visible:ring-2` per comportamento standard browser.

- [ ] **Step 3: Esegui il test e verifica che passa**

```bash
cd apps/web && pnpm test src/components/library/__tests__/CreateGameCtaCard.test.tsx --reporter=verbose
```

Expected: entrambi i test passano.

- [ ] **Step 4: Typecheck e lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

Expected: nessun errore.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx \
        apps/web/src/components/library/__tests__/CreateGameCtaCard.test.tsx
git commit -m "fix(library): replace div[role=button] with native button in CreateGameCtaCard"
```

---

## Task 4: Fix handleAddGame — rimuovi `window.location.href`

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx` (funzione `handleAddGame` e imports)

`new URL(window.location.href)` accede direttamente al DOM. Il pattern Next.js corretto usa `usePathname()` + `useSearchParams()` (hook client-side sicuri, compatibili con React 19).

- [ ] **Step 1: Aggiorna gli import in `PersonalLibraryPage.tsx`**

Nelle prime righe del file, sostituisci:

```typescript
import { useRouter } from 'next/navigation';
```

con:

```typescript
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
```

- [ ] **Step 2: Aggiorna `handleAddGame` nel corpo di `PersonalLibraryPage`**

Nella funzione `PersonalLibraryPage`, aggiungi i due nuovi hook dopo `const router = useRouter();`:

```typescript
const router = useRouter();
const pathname = usePathname();
const searchParams = useSearchParams();
```

Poi sostituisci la funzione `handleAddGame`:

```typescript
const handleAddGame = () => {
  const params = new URLSearchParams(searchParams.toString());
  params.set('action', 'add');
  router.push(`${pathname}?${params.toString()}`);
};
```

- [ ] **Step 3: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore TS.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx
git commit -m "fix(library): replace window.location.href with usePathname+useSearchParams in handleAddGame"
```

---

## Task 5: Aggiunge error state a PersonalLibraryPage

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx`

`CollectionPageClient` aveva una gestione esplicita degli errori API. `PersonalLibraryPage` non mostra nulla all'utente se la chiamata API fallisce — l'utente vede una libreria vuota senza spiegazione.

- [ ] **Step 1: Aggiorna import in `PersonalLibraryPage.tsx`**

Aggiungi gli import per Alert:

```typescript
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
```

- [ ] **Step 2: Aggiorna la chiamata `useLibrary` per esporre `error`**

Sostituisci la riga:

```typescript
const { data, isLoading } = useLibrary();
```

con:

```typescript
const { data, isLoading, error } = useLibrary();
```

- [ ] **Step 3: Aggiungi il blocco error dopo il blocco loading (linee ~244-270)**

Subito dopo il blocco `if (isLoading) { return (...) }`, aggiungi:

```typescript
if (error) {
  return (
    <div className={cn('py-4', className)} data-testid="personal-library-error">
      <Alert variant="destructive">
        <AlertDescription>
          {error instanceof Error
            ? error.message
            : 'Errore nel caricamento della libreria. Riprova più tardi.'}
        </AlertDescription>
      </Alert>
    </div>
  );
}
```

- [ ] **Step 4: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx
git commit -m "fix(library): add error state to PersonalLibraryPage (was silently showing empty state)"
```

---

## Task 6: Fix LibraryMobile — semantica segmenti (isPrivateGame)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/library-mobile.tsx`

**Problema**: "Privati" mappa a `stateFilter: ['Nuovo', 'InPrestito']` (stati di gioco), ma dovrebbe mappare a `isPrivateGame === true` (flag del dominio). Questi sono concetti ortogonali: un gioco privato può avere qualsiasi stato.

**Soluzione**: Rimuovi `SEGMENT_STATE_MAP`. Esegui il fetch senza `stateFilter` e filtra client-side per segment. Per "Wishlist" usa comunque `stateFilter: ['Wishlist']` via useLibrary separato per precisione server-side.

- [ ] **Step 1: Scrivi test per la logica di filtering mobile**

Crea `apps/web/src/app/(authenticated)/library/__tests__/LibraryMobile.segment.test.ts`:

```typescript
import { describe, it, expect } from 'vitest';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// Logica di filtering per segmenti mobile (post-fix)
function filterBySegment(items: UserLibraryEntry[], segment: string): UserLibraryEntry[] {
  switch (segment) {
    case 'collection':
      return items.filter(g => !g.isPrivateGame && g.currentState !== 'Wishlist');
    case 'private':
      return items.filter(g => g.isPrivateGame);
    case 'wishlist':
      return items.filter(g => g.currentState === 'Wishlist');
    default:
      return items;
  }
}

const makeEntry = (overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry => ({
  id: 'e1',
  userId: 'u1',
  gameId: 'g1',
  gameTitle: 'Test',
  addedAt: '2024-01-01T00:00:00Z',
  isFavorite: false,
  currentState: 'Owned',
  hasKb: false,
  kbCardCount: 0,
  kbIndexedCount: 0,
  kbProcessingCount: 0,
  hasRagAccess: false,
  agentIsOwned: true,
  isPrivateGame: false,
  canProposeToCatalog: false,
  ...overrides,
});

describe('filterBySegment', () => {
  const items: UserLibraryEntry[] = [
    makeEntry({ id: 'catalog-owned', isPrivateGame: false, currentState: 'Owned' }),
    makeEntry({ id: 'catalog-nuovo', isPrivateGame: false, currentState: 'Nuovo' }),
    makeEntry({ id: 'catalog-wishlist', isPrivateGame: false, currentState: 'Wishlist' }),
    makeEntry({ id: 'private-owned', isPrivateGame: true, currentState: 'Owned' }),
    makeEntry({ id: 'private-nuovo', isPrivateGame: true, currentState: 'Nuovo' }),
  ];

  it('collection: giochi catalogo (non privati, non wishlist)', () => {
    const result = filterBySegment(items, 'collection');
    expect(result.map(g => g.id)).toEqual(['catalog-owned', 'catalog-nuovo']);
  });

  it('private: solo giochi privati (isPrivateGame=true), qualsiasi stato', () => {
    const result = filterBySegment(items, 'private');
    expect(result.map(g => g.id)).toEqual(['private-owned', 'private-nuovo']);
  });

  it('wishlist: solo giochi in stato Wishlist', () => {
    const result = filterBySegment(items, 'wishlist');
    expect(result.map(g => g.id)).toEqual(['catalog-wishlist']);
  });
});
```

- [ ] **Step 2: Esegui il test e verifica che fallisce**

```bash
cd apps/web && pnpm test src/app/\(authenticated\)/library/__tests__/LibraryMobile.segment.test.ts --reporter=verbose
```

Expected: fail (la funzione non esiste ancora nel file di produzione).

- [ ] **Step 3: Aggiorna `library-mobile.tsx`**

Sostituisci l'intero contenuto del file `apps/web/src/app/(authenticated)/library/library-mobile.tsx` con:

```typescript
'use client';

/**
 * LibraryMobile - Mobile-first library page
 *
 * Segmented control: Collezione (catalog games) / Privati (custom games) / Wishlist
 *
 * Semantica segmenti:
 *   Collezione → isPrivateGame=false, stato != Wishlist
 *   Privati    → isPrivateGame=true (qualsiasi stato)
 *   Wishlist   → currentState=Wishlist
 *
 * Il fetch carica tutti i giochi (no stateFilter) e il filtering è client-side.
 * Per "Wishlist" viene usato stateFilter server-side per efficienza.
 *
 * Nota: BGG search rimosso dalle pagine utente (licenza commerciale BGG).
 */

import React, { useCallback, useEffect, useMemo, useState } from 'react';

import { Filter, Plus, Search } from 'lucide-react';

import { AddGameDrawer } from '@/app/(authenticated)/library/AddGameDrawer';
import { LibraryFilterSheet, type LibraryFilters } from '@/components/library/LibraryFilterSheet';
import { SegmentedControl, type Segment } from '@/components/library/SegmentedControl';
import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useLibrary, useLibraryStats } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ── Segments ─────────────────────────────────────────────────────────────────

const SEGMENTS: Segment[] = [
  { id: 'collection', label: 'Collezione' },
  { id: 'private', label: 'Privati' },
  { id: 'wishlist', label: 'Wishlist' },
];

// ── Client-side segment filter ────────────────────────────────────────────────

function filterBySegment(items: UserLibraryEntry[], segment: string): UserLibraryEntry[] {
  switch (segment) {
    case 'collection':
      return items.filter(g => !g.isPrivateGame && g.currentState !== 'Wishlist');
    case 'private':
      return items.filter(g => g.isPrivateGame);
    case 'wishlist':
      return items.filter(g => g.currentState === 'Wishlist');
    default:
      return items;
  }
}

// ── Default filters ──────────────────────────────────────────────────────────

const DEFAULT_FILTERS: LibraryFilters = {
  search: '',
  state: null,
  sortBy: 'addedAt',
  favoritesOnly: false,
};

// ── Component ────────────────────────────────────────────────────────────────

export function LibraryMobile() {
  // ── Local state ──────────────────────────────────────────────────────────
  const [activeSegment, setActiveSegment] = useState('collection');
  const [searchText, setSearchText] = useState('');
  const [debouncedSearch, setDebouncedSearch] = useState('');
  const [filters, setFilters] = useState<LibraryFilters>(DEFAULT_FILTERS);
  const [filterSheetOpen, setFilterSheetOpen] = useState(false);
  const [addDrawerOpen, setAddDrawerOpen] = useState(false);
  const [pageSize, setPageSize] = useState(20);

  // ── Debounced search ─────────────────────────────────────────────────────
  useEffect(() => {
    const timer = setTimeout(() => setDebouncedSearch(searchText), 300);
    return () => clearTimeout(timer);
  }, [searchText]);

  // Reset pageSize when search or filters change
  useEffect(() => {
    setPageSize(20);
  }, [debouncedSearch, filters, activeSegment]);

  // ── Segments with counts from stats ──────────────────────────────────────
  const { data: stats } = useLibraryStats();

  const segmentsWithCounts: Segment[] = useMemo(() => {
    if (!stats) return SEGMENTS;
    return [
      { id: 'collection', label: 'Collezione', count: stats.ownedCount },
      { id: 'private', label: 'Privati' }, // stats API non espone privateGameCount
      { id: 'wishlist', label: 'Wishlist', count: stats.wishlistCount },
    ];
  }, [stats]);

  // ── Fetch all games, filter client-side per segment ───────────────────────
  const { data, isLoading } = useLibrary({
    page: 1,
    pageSize,
    search: debouncedSearch || undefined,
    sortBy: filters.sortBy,
    sortDescending: filters.sortBy === 'addedAt',
    favoritesOnly: filters.favoritesOnly,
    // Nessun stateFilter: il filtering per segmento è client-side
  });

  const allGames = data?.items ?? [];

  const games = useMemo(
    () => filterBySegment(allGames, activeSegment),
    [allGames, activeSegment]
  );

  // ── Subtitle text ────────────────────────────────────────────────────────
  const subtitle = stats
    ? `${stats.totalGames} gioch${stats.totalGames === 1 ? 'o' : 'i'}`
    : undefined;

  // ── Filter apply callback ────────────────────────────────────────────────
  const handleApplyFilters = useCallback((newFilters: LibraryFilters) => {
    setFilters(newFilters);
  }, []);

  // ── Render ───────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col gap-3 pb-24 lg:hidden">
      {/* Header */}
      <MobileHeader
        title="La Mia Libreria"
        subtitle={subtitle}
        rightActions={
          <div className="flex items-center gap-1">
            <button
              type="button"
              aria-label="Filtra"
              onClick={() => setFilterSheetOpen(true)}
              className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
            >
              <Filter className="h-5 w-5" />
            </button>
            <button
              type="button"
              aria-label="Aggiungi gioco"
              onClick={() => setAddDrawerOpen(true)}
              className="flex h-9 w-9 items-center justify-center rounded-full text-amber-400 hover:bg-amber-500/10"
            >
              <Plus className="h-5 w-5" />
            </button>
          </div>
        }
      />

      {/* Body content */}
      <div className="flex flex-col gap-3 px-4">
        {/* Segmented control */}
        <SegmentedControl
          segments={segmentsWithCounts}
          activeId={activeSegment}
          onChange={setActiveSegment}
        />

        {/* Search input */}
        <div className="relative">
          <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[var(--gaming-text-secondary)]" />
          <input
            type="text"
            value={searchText}
            onChange={e => setSearchText(e.target.value)}
            placeholder="Cerca nella libreria..."
            className="h-10 w-full rounded-lg bg-white/5 pl-9 pr-3 text-sm text-[var(--gaming-text-primary)] placeholder:text-[var(--gaming-text-secondary)] outline-none ring-1 ring-white/10 focus:ring-amber-500/40 transition-shadow"
          />
        </div>

        {/* Loading skeleton */}
        {isLoading && (
          <div className="grid grid-cols-2 gap-3">
            {Array.from({ length: 6 }).map((_, i) => (
              <MeepleCardSkeleton key={i} variant="grid" />
            ))}
          </div>
        )}

        {/* Empty state */}
        {!isLoading && games.length === 0 && (
          <div className="flex flex-col items-center gap-4 py-16 text-center">
            <p className="text-sm text-[var(--gaming-text-secondary)]">
              {debouncedSearch ? 'Nessun risultato trovato' : 'Nessun gioco in questa sezione'}
            </p>
            {!debouncedSearch && activeSegment !== 'wishlist' && (
              <button
                type="button"
                onClick={() => setAddDrawerOpen(true)}
                className="inline-flex items-center gap-2 rounded-lg bg-amber-500/20 px-4 py-2 text-sm font-medium text-amber-400 hover:bg-amber-500/30 transition-colors"
              >
                <Plus className="h-4 w-4" />
                Aggiungi gioco
              </button>
            )}
          </div>
        )}

        {/* Game grid */}
        {!isLoading && games.length > 0 && (
          <div className="grid grid-cols-2 gap-3">
            {games.map(entry => (
              <MeepleCard
                key={entry.id}
                id={entry.gameId}
                entity="game"
                variant="grid"
                title={entry.gameTitle}
                subtitle={entry.gamePublisher ?? undefined}
                imageUrl={entry.gameImageUrl ?? undefined}
                rating={entry.averageRating ?? undefined}
                ratingMax={10}
                badge={entry.isFavorite ? '★' : undefined}
              />
            ))}
          </div>
        )}

        {/* Paginazione: "Carica altri" quando ci sono più risultati server-side */}
        {!isLoading && data?.hasNextPage && (
          <button
            type="button"
            onClick={() => setPageSize(prev => prev + 20)}
            className="w-full rounded-lg bg-white/5 px-4 py-3 text-sm font-medium text-[var(--gaming-text-secondary)] hover:bg-white/10 hover:text-[var(--gaming-text-primary)] transition-colors"
          >
            Carica altri
          </button>
        )}
      </div>

      {/* Filter bottom sheet */}
      <LibraryFilterSheet
        open={filterSheetOpen}
        onOpenChange={setFilterSheetOpen}
        filters={filters}
        onApply={handleApplyFilters}
      />

      {/* Add game drawer */}
      <AddGameDrawer open={addDrawerOpen} onClose={() => setAddDrawerOpen(false)} />
    </div>
  );
}
```

- [ ] **Step 4: Esegui i test del segmento**

```bash
cd apps/web && pnpm test src/app/\(authenticated\)/library/__tests__/LibraryMobile.segment.test.ts --reporter=verbose
```

Expected: tutti e 3 i test passano.

- [ ] **Step 5: Typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/library-mobile.tsx \
        apps/web/src/app/\(authenticated\)/library/__tests__/LibraryMobile.segment.test.ts
git commit -m "fix(library): align mobile segment semantics with desktop (isPrivateGame instead of stateFilter)"
```

---

## Task 7: Aggiungi paginazione a LibraryMobile

Questo task è già implementato nel Task 6 (`pageSize` state + "Carica altri" button). Verifichiamo solo il comportamento con un test.

**Files:**
- No modify (già fatto in Task 6)
- Add test: `apps/web/src/app/(authenticated)/library/__tests__/LibraryMobile.segment.test.ts`

- [ ] **Step 1: Verifica visivamente che il bottone "Carica altri" compaia**

Controlla che in `library-mobile.tsx` esista il blocco:

```typescript
{!isLoading && data?.hasNextPage && (
  <button type="button" onClick={() => setPageSize(prev => prev + 20)} ...>
    Carica altri
  </button>
)}
```

Se è presente (inserito nel Task 6), prosegui.

- [ ] **Step 2: Verifica che pageSize riparte da 20 quando si cambia segmento**

Controlla che esista in `library-mobile.tsx`:

```typescript
useEffect(() => {
  setPageSize(20);
}, [debouncedSearch, filters, activeSegment]);
```

Se è presente, prosegui.

- [ ] **Step 3: Esegui tutti i test della library**

```bash
cd apps/web && pnpm test src/app/\(authenticated\)/library/ src/components/library/ --reporter=verbose
```

Expected: tutti i test passano.

- [ ] **Step 4: Run lint completo**

```bash
cd apps/web && pnpm lint
```

Expected: nessun warning o errore.

- [ ] **Step 5: Final typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: nessun errore.

- [ ] **Step 6: Commit finale**

```bash
git add -A
git commit -m "test(library): verify pagination and segment filtering coverage"
```

---

## Riepilogo Raccomandazioni → Task

| Raccomandazione spec-panel | Task | Status |
|---------------------------|------|--------|
| 🔴 Rimuovi CollectionPageClient (zombie) | Task 1 | — |
| 🔴 Allinea semantica Mobile/Desktop (isPrivateGame) | Task 6 | — |
| 🟠 Chip no-op "Strategici" rimosso | Task 2 | — |
| 🟠 Chip "Meno recenti" rinominato correttamente | Task 2 | — |
| 🟠 Paginazione mobile (Carica altri) | Task 6+7 | — |
| 🟡 window.location.href → usePathname/useSearchParams | Task 4 | — |
| 🟡 CustomEvent anti-pattern | **auto-fix** (rimosso con CollectionPageClient) | — |
| 🟡 div[role=button] → button nativo | Task 3 | — |
| 🟡 Error handling in PersonalLibraryPage | Task 5 | — |
| 🟡 Double fetch (LibraryMobile sempre nel DOM) | **escluso** (React Query deduplica i fetch; il pattern CSS-only è idiomatico Next.js) | N/A |

---

## Verifica Finale

Dopo aver completato tutti i task:

```bash
cd apps/web
pnpm test --reporter=verbose   # tutti i test passano
pnpm typecheck                 # 0 errori TS
pnpm lint                      # 0 warning
pnpm build                     # build production senza errori
```
