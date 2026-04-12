# Game Night MVP Hardening — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Chiudere i 3 gap residui del flusso Game Night mobile post-PR #265 con scope MVP minimale: soft-filter PDF nel selettore gioco, persistenza foto via IndexedDB, metric di health per AutoSave job.

**Architecture:**
- **F1 (frontend-only)**: Estensione del wizard `GameNightWizard` per leggere `UserGameKbStatus.isIndexed` (endpoint `/api/v1/games/{id}/knowledge-base` esistente), badge "AI pronto" / "Solo manuale", warning inline su selezione gioco non indicizzato. Nessun cambiamento backend.
- **F2 (frontend-only)**: Nuovo modulo `lib/storage/photo-store.ts` che usa `idb` (già in deps `^8.0.3`) per persistere foto come `Blob` in IndexedDB con key `photo:<sessionId>:<uuid>`. Refactor `photos/page.tsx` per leggere/scrivere via store; rimozione del bottone "Carica" placeholder.
- **F3 (backend-only)**: Nuova metric ObservableGauge `meepleai.session.autosave.last_run_age_seconds` registrata sullo `Meter` esistente `MeepleAI.Api`. Stato condiviso `IAutoSaveHealthTracker` (singleton) aggiornato dentro `AutoSaveSessionJob.Execute`. Esposizione automatica via `/metrics` Prometheus endpoint.

**Tech Stack:** Next.js 16 + React 19, `idb` 8.x, Vitest, .NET 9, Quartz.NET, OpenTelemetry Metrics, xUnit.

**Boundaries:**
- NO backend changes per F1 (solo consumo endpoint esistente)
- NO upload S3 per F2 (decisione utente: IndexedDB only)
- NO circuit breaker LLM (out of scope)
- NO E2E Playwright in questo plan (issue separata backlog #3)
- NO modifiche al `GameNightWizard` step structure (solo aggiunta badge + warning)

**Spec di riferimento:** `docs/superpowers/specs/2026-04-08-game-night-mobile-flow-spec-panel.md`

---

## File Map

### Nuovi file

```
# F1 — frontend
apps/web/src/components/game-night/GameKbBadge.tsx
apps/web/src/components/game-night/__tests__/GameKbBadge.test.tsx
apps/web/src/lib/domain-hooks/useGameKbStatus.ts
apps/web/src/lib/domain-hooks/__tests__/useGameKbStatus.test.ts

# F2 — frontend
apps/web/src/lib/storage/photo-store.ts
apps/web/src/lib/storage/__tests__/photo-store.test.ts

# F3 — backend
apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/IAutoSaveHealthTracker.cs
apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthTracker.cs
apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.SessionAutoSave.cs
tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthTrackerTests.cs
tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveSessionJobHealthTests.cs
```

### File modificati

```
# F1
apps/web/src/components/game-night/GameNightWizard.tsx
apps/web/src/lib/api/clients/knowledgeBaseClient.ts  # se manca metodo userGameKbStatus
apps/web/src/lib/api/schemas/knowledge-base.schemas.ts  # solo se schema incompleto

# F2
apps/web/src/app/(authenticated)/sessions/live/[sessionId]/photos/page.tsx

# F3
apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/AutoSaveSessionJob.cs
apps/api/src/Api/Extensions/ServiceCollectionExtensions.cs   # registrazione DI singleton (verificare nome reale extension)
apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs  # nessuna modifica se Meter è già `MeepleAI.Api`
```

> **Nota verifica path DI**: prima dello Step 5 della Phase 3, l'engineer deve trovare la classe Extension che registra i servizi SessionTracking (probabile `apps/api/src/Api/Extensions/ServiceCollectionExtensions.cs` o `BoundedContexts/SessionTracking/Infrastructure/SessionTrackingServiceCollectionExtensions.cs`) con `Glob` su `**/*ServiceCollection*.cs` e `Grep` su `AutoSaveScheduler`.

---

## PHASE 1 — F1: Soft-filter PDF nel wizard

### Task 1.1 — Hook `useGameKbStatus`

**Files:**
- Create: `apps/web/src/lib/domain-hooks/useGameKbStatus.ts`
- Create: `apps/web/src/lib/domain-hooks/__tests__/useGameKbStatus.test.ts`

- [ ] **Step 1.1.1: Verifica metodo client esistente**

Run: `grep -n "kb-status\|knowledge-base\|UserGameKbStatus" apps/web/src/lib/api/clients/knowledgeBaseClient.ts`

Expected: trovare un metodo che chiama `/api/v1/games/{gameId}/knowledge-base` e parsa `UserGameKbStatusSchema`. Se NON esiste, aggiungerlo seguendo il pattern di `getPrivateGameKbStatus` esistente. Annotare il nome esatto del metodo (es. `getUserGameKbStatus(gameId)`).

- [ ] **Step 1.1.2: Scrivi il test del hook**

```ts
// apps/web/src/lib/domain-hooks/__tests__/useGameKbStatus.test.ts
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ReactNode } from 'react';

import { useGameKbStatus } from '../useGameKbStatus';
import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';

vi.mock('@/lib/api/clients/knowledgeBaseClient', () => ({
  knowledgeBaseClient: {
    getUserGameKbStatus: vi.fn(),
  },
}));

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('useGameKbStatus', () => {
  beforeEach(() => vi.clearAllMocks());

  it('returns isIndexed=true when KB has documents', async () => {
    vi.mocked(knowledgeBaseClient.getUserGameKbStatus).mockResolvedValue({
      gameId: 'g1',
      isIndexed: true,
      documentCount: 3,
      coverageScore: 80,
      coverageLevel: 'Standard',
      suggestedQuestions: [],
    });

    const { result } = renderHook(() => useGameKbStatus('g1'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isIndexed).toBe(true);
    expect(result.current.documentCount).toBe(3);
  });

  it('returns isIndexed=false on empty KB', async () => {
    vi.mocked(knowledgeBaseClient.getUserGameKbStatus).mockResolvedValue({
      gameId: 'g2',
      isIndexed: false,
      documentCount: 0,
      coverageScore: 0,
      coverageLevel: 'None',
      suggestedQuestions: [],
    });

    const { result } = renderHook(() => useGameKbStatus('g2'), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.isIndexed).toBe(false);
  });

  it('skips fetch when gameId is null', () => {
    const { result } = renderHook(() => useGameKbStatus(null), { wrapper });
    expect(knowledgeBaseClient.getUserGameKbStatus).not.toHaveBeenCalled();
    expect(result.current.isIndexed).toBe(false);
  });
});
```

- [ ] **Step 1.1.3: Esegui i test e verifica che falliscano**

Run: `cd apps/web && pnpm test -- useGameKbStatus`
Expected: FAIL — modulo `useGameKbStatus` non esiste.

- [ ] **Step 1.1.4: Implementa il hook**

```ts
// apps/web/src/lib/domain-hooks/useGameKbStatus.ts
'use client';

import { useQuery } from '@tanstack/react-query';

import { knowledgeBaseClient } from '@/lib/api/clients/knowledgeBaseClient';

interface UseGameKbStatusResult {
  isIndexed: boolean;
  documentCount: number;
  coverageLevel: 'None' | 'Basic' | 'Standard' | 'Complete';
  isLoading: boolean;
  error: Error | null;
}

/**
 * Hook to check whether a game has an indexed knowledge base (PDF + RAG).
 * Returns isIndexed=false when gameId is null or fetch fails.
 */
export function useGameKbStatus(gameId: string | null): UseGameKbStatusResult {
  const query = useQuery({
    queryKey: ['game-kb-status', gameId],
    queryFn: () => knowledgeBaseClient.getUserGameKbStatus(gameId!),
    enabled: gameId !== null,
    staleTime: 60_000,
  });

  return {
    isIndexed: query.data?.isIndexed ?? false,
    documentCount: query.data?.documentCount ?? 0,
    coverageLevel: query.data?.coverageLevel ?? 'None',
    isLoading: query.isLoading,
    error: query.error,
  };
}
```

- [ ] **Step 1.1.5: Esegui i test e verifica che passino**

Run: `cd apps/web && pnpm test -- useGameKbStatus`
Expected: PASS (3 test).

- [ ] **Step 1.1.6: Commit**

```bash
git add apps/web/src/lib/domain-hooks/useGameKbStatus.ts apps/web/src/lib/domain-hooks/__tests__/useGameKbStatus.test.ts
git commit -m "feat(game-night): add useGameKbStatus hook for PDF-aware filter"
```

---

### Task 1.2 — Componente `GameKbBadge`

**Files:**
- Create: `apps/web/src/components/game-night/GameKbBadge.tsx`
- Create: `apps/web/src/components/game-night/__tests__/GameKbBadge.test.tsx`

- [ ] **Step 1.2.1: Scrivi il test del componente**

```tsx
// apps/web/src/components/game-night/__tests__/GameKbBadge.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { GameKbBadge } from '../GameKbBadge';

describe('GameKbBadge', () => {
  it('renders "AI pronto" when isIndexed=true', () => {
    render(<GameKbBadge isIndexed={true} />);
    expect(screen.getByText('AI pronto')).toBeInTheDocument();
    expect(screen.getByTestId('game-kb-badge')).toHaveAttribute('data-indexed', 'true');
  });

  it('renders "Solo manuale" when isIndexed=false', () => {
    render(<GameKbBadge isIndexed={false} />);
    expect(screen.getByText('Solo manuale')).toBeInTheDocument();
    expect(screen.getByTestId('game-kb-badge')).toHaveAttribute('data-indexed', 'false');
  });

  it('renders nothing while loading', () => {
    const { container } = render(<GameKbBadge isIndexed={false} isLoading={true} />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 1.2.2: Run test, verifica fallisca**

Run: `cd apps/web && pnpm test -- GameKbBadge`
Expected: FAIL — modulo non esiste.

- [ ] **Step 1.2.3: Implementa il componente**

```tsx
// apps/web/src/components/game-night/GameKbBadge.tsx
import { Sparkles, FileText } from 'lucide-react';

interface GameKbBadgeProps {
  isIndexed: boolean;
  isLoading?: boolean;
}

/**
 * Badge che indica se un gioco ha un PDF indicizzato (RAG agent disponibile).
 * - isIndexed=true → "AI pronto" (verde, icon sparkles)
 * - isIndexed=false → "Solo manuale" (grigio, icon file)
 * - isLoading → nulla
 */
export function GameKbBadge({ isIndexed, isLoading = false }: GameKbBadgeProps): JSX.Element | null {
  if (isLoading) return null;

  if (isIndexed) {
    return (
      <span
        data-testid="game-kb-badge"
        data-indexed="true"
        className="inline-flex items-center gap-1 rounded-full bg-emerald-50 px-2 py-0.5 text-xs font-nunito font-medium text-emerald-700 border border-emerald-200"
      >
        <Sparkles className="h-3 w-3" aria-hidden="true" />
        AI pronto
      </span>
    );
  }

  return (
    <span
      data-testid="game-kb-badge"
      data-indexed="false"
      className="inline-flex items-center gap-1 rounded-full bg-gray-50 px-2 py-0.5 text-xs font-nunito font-medium text-gray-600 border border-gray-200"
    >
      <FileText className="h-3 w-3" aria-hidden="true" />
      Solo manuale
    </span>
  );
}
```

- [ ] **Step 1.2.4: Run test, verifica passi**

Run: `cd apps/web && pnpm test -- GameKbBadge`
Expected: PASS (3 test).

- [ ] **Step 1.2.5: Commit**

```bash
git add apps/web/src/components/game-night/GameKbBadge.tsx apps/web/src/components/game-night/__tests__/GameKbBadge.test.tsx
git commit -m "feat(game-night): add GameKbBadge component"
```

---

### Task 1.3 — Integrazione badge + warning nel `GameNightWizard`

**Files:**
- Modify: `apps/web/src/components/game-night/GameNightWizard.tsx`
- Modify: `apps/web/src/components/game-night/__tests__/GameNightWizard.test.tsx` (se esiste; altrimenti crearlo per il caso di soft-filter)

- [ ] **Step 1.3.1: Identifica il punto di integrazione**

Run: `grep -n "game.*Card\|gameOption\|onSelect.*game\|selectedGame" apps/web/src/components/game-night/GameNightWizard.tsx`

Expected: trovare il render dei game item nel selettore. Se la lista è renderizzata da un sub-componente in `apps/web/src/components/game-night/steps/`, modificare quel sub-componente invece del wizard root.

- [ ] **Step 1.3.2: Scrivi il test del sub-componente del game selector**

> **Strategia**: Testare il sub-componente `GameSelectorStep` (o equivalente) in isolamento, NON il wizard intero. Adatta il nome dell'import al risultato dello Step 1.3.1.

```tsx
// apps/web/src/components/game-night/steps/__tests__/GameSelectorStep.test.tsx
// (path effettivo dipende dallo Step 1.3.1)

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import type { ReactNode } from 'react';

import { GameSelectorStep } from '../GameSelectorStep'; // adatta al path reale

vi.mock('@/lib/domain-hooks/useGameKbStatus', () => ({
  useGameKbStatus: vi.fn((gameId: string | null) => ({
    isIndexed: gameId === 'catan-id',
    documentCount: gameId === 'catan-id' ? 3 : 0,
    coverageLevel: gameId === 'catan-id' ? 'Standard' : 'None',
    isLoading: false,
    error: null,
  })),
}));

const games = [
  { id: 'catan-id', name: 'Catan' },
  { id: 'obscure-id', name: 'ObscureGame' },
];

function wrap(ui: ReactNode): ReactNode {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{ui}</QueryClientProvider>;
}

describe('GameSelectorStep — soft filter', () => {
  it('shows AI pronto badge for indexed games and Solo manuale for others', () => {
    render(wrap(<GameSelectorStep games={games} selectedGameId={null} onSelect={() => {}} />));

    const catanCard = screen.getByText('Catan').closest('[data-testid="game-option"]') as HTMLElement;
    const obscureCard = screen.getByText('ObscureGame').closest('[data-testid="game-option"]') as HTMLElement;

    expect(within(catanCard).getByText('AI pronto')).toBeInTheDocument();
    expect(within(obscureCard).getByText('Solo manuale')).toBeInTheDocument();
  });

  it('shows inline warning when a non-indexed game is selected', async () => {
    const user = userEvent.setup();
    let selected: string | null = null;
    const { rerender } = render(
      wrap(
        <GameSelectorStep
          games={games}
          selectedGameId={selected}
          onSelect={id => {
            selected = id;
          }}
        />,
      ),
    );

    await user.click(screen.getByText('ObscureGame'));
    rerender(
      wrap(<GameSelectorStep games={games} selectedGameId="obscure-id" onSelect={() => {}} />),
    );

    expect(screen.getByTestId('kb-warning')).toBeInTheDocument();
    expect(
      screen.getByText(/Agente AI non disponibile.*PDF.*non.*indicizzato/i),
    ).toBeInTheDocument();
  });

  it('does not show warning for indexed games', () => {
    render(
      wrap(<GameSelectorStep games={games} selectedGameId="catan-id" onSelect={() => {}} />),
    );

    expect(screen.queryByTestId('kb-warning')).not.toBeInTheDocument();
  });
});
```

> **Nota**: il piano lascia all'engineer di adattare il nome del componente / props in base allo Step 1.3.1. Se il selector è inline nel `GameNightWizard.tsx`, l'engineer DEVE estrarlo prima in un sub-componente esportato per renderlo testabile.

- [ ] **Step 1.3.3: Run test, verifica fallisca**

Run: `cd apps/web && pnpm test -- GameNightWizard`
Expected: FAIL — badge e warning non implementati.

- [ ] **Step 1.3.4: Aggiungi badge nel render del game item**

Modifica il sub-componente che renderizza ogni opzione gioco. Esempio (adattare al codice reale):

```tsx
// All'interno di apps/web/src/components/game-night/steps/<GameSelectorStep>.tsx
import { GameKbBadge } from '../GameKbBadge';
import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';

function GameOption({ game, isSelected, onSelect }: GameOptionProps): JSX.Element {
  const kb = useGameKbStatus(game.id);

  return (
    <button
      type="button"
      data-testid="game-option"
      data-game-id={game.id}
      onClick={() => onSelect(game.id)}
      className={`flex items-center justify-between rounded-xl border p-3 transition ${
        isSelected ? 'border-amber-500 bg-amber-50' : 'border-gray-200 bg-white'
      }`}
    >
      <span className="font-nunito font-medium text-gray-900">{game.name}</span>
      <GameKbBadge isIndexed={kb.isIndexed} isLoading={kb.isLoading} />
    </button>
  );
}
```

- [ ] **Step 1.3.5: Aggiungi il warning inline come componente separato**

> **🔴 IMPORTANTE**: Non chiamare `useGameKbStatus` dentro un IIFE / callback / map nested — viola le Rules of Hooks. Estrai un componente dedicato.

Crea il sub-componente nello stesso file dello step (o in `GameKbBadge.tsx`):

```tsx
import { AlertTriangle } from 'lucide-react';
import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';

export function GameKbWarning({ gameId }: { gameId: string }): JSX.Element | null {
  const kb = useGameKbStatus(gameId);
  if (kb.isLoading || kb.isIndexed) return null;
  return (
    <div
      role="alert"
      data-testid="kb-warning"
      className="mt-3 flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3"
    >
      <AlertTriangle className="mt-0.5 h-4 w-4 flex-none text-amber-600" aria-hidden="true" />
      <p className="text-xs font-nunito text-amber-900">
        <strong>Agente AI non disponibile</strong> — il PDF di questo gioco non è ancora indicizzato.
        Puoi comunque iniziare la sessione e usare gli strumenti manuali.
      </p>
    </div>
  );
}
```

E nell'host (GameSelectorStep o equivalente):

```tsx
{selectedGameId && <GameKbWarning gameId={selectedGameId} />}
```

> **Nota performance**: ogni `useGameKbStatus(gameId)` con stesso `gameId` condivide la cache `react-query` (`staleTime: 60_000`), quindi non c'è doppio fetch tra il `GameOption` e il `GameKbWarning`.

- [ ] **Step 1.3.6: Run test, verifica passi**

Run: `cd apps/web && pnpm test -- GameNightWizard`
Expected: PASS (3 nuovi test + esistenti).

- [ ] **Step 1.3.7: Lint + typecheck**

Run: `cd apps/web && pnpm lint && pnpm typecheck`
Expected: PASS.

- [ ] **Step 1.3.8: Commit**

```bash
git add apps/web/src/components/game-night/
git commit -m "feat(game-night): integrate PDF-aware soft filter into wizard"
```

---

## PHASE 2 — F2: IndexedDB photo persistence

### Task 2.1 — Photo store module

**Files:**
- Create: `apps/web/src/lib/storage/photo-store.ts`
- Create: `apps/web/src/lib/storage/__tests__/photo-store.test.ts`

- [ ] **Step 2.1.1: Verifica `idb` in deps**

Run: `grep '"idb":' apps/web/package.json`
Expected: `"idb": "^8.0.3"` (già presente).

- [ ] **Step 2.1.2: Scrivi il test del store**

```ts
// apps/web/src/lib/storage/__tests__/photo-store.test.ts
import 'fake-indexeddb/auto';
import { describe, it, expect, beforeEach } from 'vitest';

import {
  addPhoto,
  listPhotos,
  deletePhoto,
  clearAllPhotos,
  type StoredPhoto,
} from '../photo-store';

const sessionId = 'sess-1';

function makeBlob(): Blob {
  return new Blob(['fake-png'], { type: 'image/png' });
}

describe('photo-store (IndexedDB)', () => {
  beforeEach(async () => {
    await clearAllPhotos();
  });

  it('addPhoto persists a photo and listPhotos returns it', async () => {
    const photo = await addPhoto(sessionId, makeBlob(), 'snap.png');

    expect(photo.id).toMatch(/^photo:sess-1:/);
    expect(photo.sessionId).toBe(sessionId);
    expect(photo.filename).toBe('snap.png');
    expect(photo.timestamp).toBeGreaterThan(0);

    const list = await listPhotos(sessionId);
    expect(list).toHaveLength(1);
    expect(list[0].id).toBe(photo.id);
  });

  it('listPhotos returns photos sorted by timestamp ascending', async () => {
    const a = await addPhoto(sessionId, makeBlob(), 'a.png');
    await new Promise(r => setTimeout(r, 5));
    const b = await addPhoto(sessionId, makeBlob(), 'b.png');

    const list = await listPhotos(sessionId);
    expect(list.map(p => p.id)).toEqual([a.id, b.id]);
  });

  it('listPhotos filters by sessionId', async () => {
    await addPhoto('sess-A', makeBlob(), 'a.png');
    await addPhoto('sess-B', makeBlob(), 'b.png');

    const listA = await listPhotos('sess-A');
    const listB = await listPhotos('sess-B');

    expect(listA).toHaveLength(1);
    expect(listB).toHaveLength(1);
    expect(listA[0].id).not.toBe(listB[0].id);
  });

  it('deletePhoto removes only the target', async () => {
    const a = await addPhoto(sessionId, makeBlob(), 'a.png');
    const b = await addPhoto(sessionId, makeBlob(), 'b.png');

    await deletePhoto(a.id);

    const list = await listPhotos(sessionId);
    expect(list).toHaveLength(1);
    expect(list[0].id).toBe(b.id);
  });

  it('returns photo blob via the dataUrl helper', async () => {
    const photo = await addPhoto(sessionId, makeBlob(), 'snap.png');
    expect(photo.blob).toBeInstanceOf(Blob);
    expect(photo.blob.type).toBe('image/png');
  });

  it('listPhotos returns empty array for unknown session', async () => {
    const list = await listPhotos('nonexistent');
    expect(list).toEqual([]);
  });
});
```

- [ ] **Step 2.1.3: Verifica `fake-indexeddb` in devDeps**

Run: `grep '"fake-indexeddb":' apps/web/package.json`
Expected: presente. **Se manca**:

```bash
cd apps/web && pnpm add -D fake-indexeddb
```

- [ ] **Step 2.1.4: Run test, verifica fallisca**

Run: `cd apps/web && pnpm test -- photo-store`
Expected: FAIL — modulo non esiste.

- [ ] **Step 2.1.5: Implementa il photo store**

```ts
// apps/web/src/lib/storage/photo-store.ts
import { openDB, type IDBPDatabase } from 'idb';

const DB_NAME = 'meepleai-photos';
const DB_VERSION = 1;
const STORE = 'photos';

export interface StoredPhoto {
  id: string;
  sessionId: string;
  filename: string;
  timestamp: number;
  blob: Blob;
}

interface PhotoSchema {
  photos: {
    key: string;
    value: StoredPhoto;
    indexes: {
      'by-session': string;
    };
  };
}

let dbPromise: Promise<IDBPDatabase<PhotoSchema>> | null = null;

function getDb(): Promise<IDBPDatabase<PhotoSchema>> {
  if (!dbPromise) {
    dbPromise = openDB<PhotoSchema>(DB_NAME, DB_VERSION, {
      upgrade(db) {
        const store = db.createObjectStore(STORE, { keyPath: 'id' });
        store.createIndex('by-session', 'sessionId', { unique: false });
      },
    });
  }
  return dbPromise;
}

function generateId(sessionId: string): string {
  const random = globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
  return `photo:${sessionId}:${random}`;
}

/**
 * Persists a captured photo for a given session.
 * Returns the StoredPhoto record (including the original Blob).
 */
export async function addPhoto(sessionId: string, blob: Blob, filename: string): Promise<StoredPhoto> {
  const db = await getDb();
  const photo: StoredPhoto = {
    id: generateId(sessionId),
    sessionId,
    filename,
    timestamp: Date.now(),
    blob,
  };
  await db.put(STORE, photo);
  return photo;
}

/**
 * Lists all photos belonging to the given session, sorted by timestamp asc.
 */
export async function listPhotos(sessionId: string): Promise<StoredPhoto[]> {
  const db = await getDb();
  const all = await db.getAllFromIndex(STORE, 'by-session', sessionId);
  return all.sort((a, b) => a.timestamp - b.timestamp);
}

/**
 * Deletes a single photo by id.
 */
export async function deletePhoto(id: string): Promise<void> {
  const db = await getDb();
  await db.delete(STORE, id);
}

/**
 * Clears every photo across all sessions. Intended for tests / reset.
 */
export async function clearAllPhotos(): Promise<void> {
  const db = await getDb();
  await db.clear(STORE);
}
```

- [ ] **Step 2.1.6: Run test, verifica passi**

Run: `cd apps/web && pnpm test -- photo-store`
Expected: PASS (6 test).

- [ ] **Step 2.1.7: Commit**

```bash
git add apps/web/src/lib/storage/ apps/web/package.json apps/web/pnpm-lock.yaml
git commit -m "feat(storage): add IndexedDB photo store with idb"
```

---

### Task 2.2 — Refactor `photos/page.tsx` per usare lo store

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/photos/page.tsx`
- Create: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/photos/__tests__/page.test.tsx`

- [ ] **Step 2.2.1: Scrivi il test della pagina**

```tsx
// apps/web/src/app/(authenticated)/sessions/live/[sessionId]/photos/__tests__/page.test.tsx
import 'fake-indexeddb/auto';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach } from 'vitest';

import PhotosPage from '../page';
import { clearAllPhotos, addPhoto } from '@/lib/storage/photo-store';

function makeFakeFile(name = 'snap.png'): File {
  return new File([new Blob(['fake'], { type: 'image/png' })], name, { type: 'image/png' });
}

describe('PhotosPage — IndexedDB persistence', () => {
  beforeEach(async () => {
    await clearAllPhotos();
  });

  it('loads existing photos from store on mount', async () => {
    await addPhoto('sess-x', new Blob(['a'], { type: 'image/png' }), 'preexisting.png');

    render(<PhotosPage params={Promise.resolve({ sessionId: 'sess-x' })} />);

    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());
  });

  it('persists captured photo and shows it in the grid', async () => {
    render(<PhotosPage params={Promise.resolve({ sessionId: 'sess-y' })} />);

    const input = await screen.findByTestId('photo-input');
    fireEvent.change(input, { target: { files: [makeFakeFile()] } });

    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());
  });

  it('survives a remount (data persisted in IndexedDB)', async () => {
    const { unmount } = render(<PhotosPage params={Promise.resolve({ sessionId: 'sess-z' })} />);

    const input = await screen.findByTestId('photo-input');
    fireEvent.change(input, { target: { files: [makeFakeFile()] } });
    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());

    unmount();

    render(<PhotosPage params={Promise.resolve({ sessionId: 'sess-z' })} />);
    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());
  });

  it('delete removes the photo from store', async () => {
    render(<PhotosPage params={Promise.resolve({ sessionId: 'sess-d' })} />);

    const input = await screen.findByTestId('photo-input');
    fireEvent.change(input, { target: { files: [makeFakeFile()] } });
    await waitFor(() => expect(screen.getByText(/1 foto/)).toBeInTheDocument());

    const deleteBtn = await screen.findByLabelText(/Elimina foto/i);
    fireEvent.click(deleteBtn);

    await waitFor(() => expect(screen.getByText(/Nessuna foto ancora/i)).toBeInTheDocument());
  });
});
```

- [ ] **Step 2.2.2: Run test, verifica fallisca**

Run: `cd apps/web && pnpm test -- "photos/__tests__/page"`
Expected: FAIL — la pagina usa ancora `useState` locale, non IndexedDB.

- [ ] **Step 2.2.3: Refactora `photos/page.tsx`**

```tsx
/**
 * Photos Child Card — /sessions/live/[sessionId]/photos
 *
 * Camera capture for board-state photos. Photos are persisted in IndexedDB
 * via lib/storage/photo-store. Local-only (no cross-device sync).
 */

'use client';

import { use, useRef, useState, useCallback, useEffect } from 'react';

import { Camera, Image as ImageIcon, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  addPhoto,
  listPhotos,
  deletePhoto,
  type StoredPhoto,
} from '@/lib/storage/photo-store';

// ─── Types ────────────────────────────────────────────────────────────────────

interface DisplayPhoto {
  id: string;
  objectUrl: string;
  timestamp: number;
}

// ─── Sub-components ───────────────────────────────────────────────────────────

interface PhotoCardProps {
  photo: DisplayPhoto;
  onDelete: (id: string) => void;
}

function PhotoCard({ photo, onDelete }: PhotoCardProps) {
  const date = new Date(photo.timestamp);
  const timeLabel = date.toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  return (
    <div className="group relative rounded-xl overflow-hidden bg-gray-100 aspect-square shadow-sm border border-white/60">
      {/* eslint-disable-next-line @next/next/no-img-element */}
      <img
        src={photo.objectUrl}
        alt={`Foto partita ${timeLabel}`}
        className="w-full h-full object-cover"
      />
      <div className="absolute inset-0 bg-black/0 group-hover:bg-black/30 transition-colors" />
      <div className="absolute bottom-0 inset-x-0 bg-gradient-to-t from-black/60 to-transparent p-2">
        <p className="text-white text-xs font-mono">{timeLabel}</p>
      </div>
      <button
        className="absolute top-1.5 right-1.5 h-7 w-7 rounded-full bg-black/40 hover:bg-red-500/80 flex items-center justify-center opacity-0 group-hover:opacity-100 transition-all"
        onClick={() => onDelete(photo.id)}
        aria-label={`Elimina foto ${timeLabel}`}
      >
        <Trash2 className="h-3.5 w-3.5 text-white" />
      </button>
    </div>
  );
}

// ─── Main Component ────────────────────────────────────────────────────────────

interface PhotosPageProps {
  params: Promise<{ sessionId: string }>;
}

function toDisplay(stored: StoredPhoto): DisplayPhoto {
  return {
    id: stored.id,
    objectUrl: URL.createObjectURL(stored.blob),
    timestamp: stored.timestamp,
  };
}

export default function PhotosPage({ params }: PhotosPageProps) {
  const { sessionId } = use(params);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [photos, setPhotos] = useState<DisplayPhoto[]>([]);

  // Load existing photos on mount
  useEffect(() => {
    let cancelled = false;
    listPhotos(sessionId).then(stored => {
      if (cancelled) return;
      setPhotos(stored.map(toDisplay));
    });
    return () => {
      cancelled = true;
    };
  }, [sessionId]);

  // Revoke object URLs on unmount
  useEffect(() => {
    return () => {
      photos.forEach(p => URL.revokeObjectURL(p.objectUrl));
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const handleCapture = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      const file = e.target.files?.[0];
      if (!file) return;
      e.target.value = '';

      const stored = await addPhoto(sessionId, file, file.name);
      setPhotos(prev => [...prev, toDisplay(stored)]);
    },
    [sessionId],
  );

  const handleDelete = useCallback(async (id: string) => {
    await deletePhoto(id);
    setPhotos(prev => {
      const target = prev.find(p => p.id === id);
      if (target) URL.revokeObjectURL(target.objectUrl);
      return prev.filter(p => p.id !== id);
    });
  }, []);

  return (
    <div className="space-y-4 p-4">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-xl font-quicksand font-bold text-gray-900">Foto partita</h2>
          <p className="text-xs text-gray-500 font-nunito mt-0.5">
            {photos.length === 0 ? 'Nessuna foto ancora' : `${photos.length} foto`}
          </p>
        </div>

        <Button
          size="sm"
          className="gap-2 bg-amber-500 hover:bg-amber-600 text-white font-nunito"
          onClick={() => fileInputRef.current?.click()}
          data-testid="capture-button"
        >
          <Camera className="h-4 w-4" />
          Scatta
        </Button>
      </div>

      <input
        ref={fileInputRef}
        type="file"
        accept="image/*"
        capture="environment"
        className="hidden"
        onChange={handleCapture}
        data-testid="photo-input"
      />

      {photos.length === 0 && (
        <div className="flex flex-col items-center gap-3 py-12 text-gray-400">
          <ImageIcon className="h-12 w-12 opacity-30" />
          <p className="text-sm font-nunito text-center">
            Scatta foto per documentare lo stato della partita
          </p>
          <Button
            variant="outline"
            className="gap-2 font-nunito"
            onClick={() => fileInputRef.current?.click()}
          >
            <Camera className="h-4 w-4" />
            Prima foto
          </Button>
        </div>
      )}

      {photos.length > 0 && (
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
          {photos.map(photo => (
            <PhotoCard key={photo.id} photo={photo} onDelete={handleDelete} />
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2.2.4: Run test, verifica passi**

Run: `cd apps/web && pnpm test -- "photos/__tests__/page"`
Expected: PASS (4 test).

- [ ] **Step 2.2.5: Lint + typecheck + tutta la suite F2**

Run: `cd apps/web && pnpm lint && pnpm typecheck && pnpm test -- storage photos`
Expected: PASS.

- [ ] **Step 2.2.6: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/photos/
git commit -m "feat(game-night): persist session photos in IndexedDB"
```

---

## PHASE 3 — F3: AutoSave health metric

### Task 3.1 — `IAutoSaveHealthTracker` + implementazione

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/IAutoSaveHealthTracker.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthTracker.cs`
- Create: `tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthTrackerTests.cs`

- [ ] **Step 3.1.1: Scrivi il test del tracker**

```csharp
// tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthTrackerTests.cs
using System;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.Internal;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class AutoSaveHealthTrackerTests
{
    [Fact]
    public void GetLastRunAgeSeconds_BeforeAnyRun_ReturnsNull()
    {
        var clock = new TestClock(DateTimeOffset.UtcNow);
        var tracker = new AutoSaveHealthTracker(clock);

        tracker.GetLastRunAgeSeconds().Should().BeNull();
    }

    [Fact]
    public void RecordRun_ThenGetAge_ReturnsZeroSeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new TestClock(now);
        var tracker = new AutoSaveHealthTracker(clock);

        tracker.RecordRun();

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    [Fact]
    public void GetLastRunAgeSeconds_AfterClockAdvances_ReturnsElapsedSeconds()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new TestClock(now);
        var tracker = new AutoSaveHealthTracker(clock);

        tracker.RecordRun();
        clock.Advance(TimeSpan.FromSeconds(45));

        tracker.GetLastRunAgeSeconds().Should().Be(45);
    }

    [Fact]
    public void RecordRun_IsThreadSafe_LastWriteWins()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new TestClock(now);
        var tracker = new AutoSaveHealthTracker(clock);

        tracker.RecordRun();
        clock.Advance(TimeSpan.FromSeconds(10));
        tracker.RecordRun();

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    private sealed class TestClock(DateTimeOffset start) : ISystemClock
    {
        private DateTimeOffset _now = start;
        public DateTimeOffset UtcNow => _now;
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
```

- [ ] **Step 3.1.2: Run test, verifica fallisca (compile error)**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutoSaveHealthTrackerTests"`
Expected: FAIL — `AutoSaveHealthTracker` non esiste.

- [ ] **Step 3.1.3: Implementa interface**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/IAutoSaveHealthTracker.cs
namespace Api.BoundedContexts.SessionTracking.Infrastructure.Health;

/// <summary>
/// Tracks the wall-clock time of the most recent successful AutoSaveSessionJob execution.
/// Used by Prometheus gauge to surface stalled background jobs.
/// </summary>
public interface IAutoSaveHealthTracker
{
    /// <summary>
    /// Records that AutoSaveSessionJob just executed (successfully or with handled error).
    /// </summary>
    void RecordRun();

    /// <summary>
    /// Returns the number of seconds elapsed since the last RecordRun call.
    /// Returns null if RecordRun has never been called (process startup or no active sessions).
    /// </summary>
    long? GetLastRunAgeSeconds();
}
```

- [ ] **Step 3.1.4: Implementa la classe concreta**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthTracker.cs
using System;
using System.Threading;
using Microsoft.Extensions.Internal;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Health;

/// <summary>
/// Thread-safe singleton tracker for AutoSaveSessionJob health.
/// Stores the last-run timestamp as Unix milliseconds in an Interlocked-protected field.
/// </summary>
internal sealed class AutoSaveHealthTracker(ISystemClock clock) : IAutoSaveHealthTracker
{
    private long _lastRunUnixMs;

    public void RecordRun()
    {
        var nowMs = clock.UtcNow.ToUnixTimeMilliseconds();
        Interlocked.Exchange(ref _lastRunUnixMs, nowMs);
    }

    public long? GetLastRunAgeSeconds()
    {
        var lastMs = Interlocked.Read(ref _lastRunUnixMs);
        if (lastMs == 0)
        {
            return null;
        }

        var nowMs = clock.UtcNow.ToUnixTimeMilliseconds();
        var deltaMs = nowMs - lastMs;
        return deltaMs / 1000;
    }
}
```

- [ ] **Step 3.1.5: Run test, verifica passi**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutoSaveHealthTrackerTests"`
Expected: PASS (4 test).

- [ ] **Step 3.1.6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/ tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthTrackerTests.cs
git commit -m "feat(autosave): add IAutoSaveHealthTracker for job health metric"
```

---

### Task 3.2 — Wire tracker in `AutoSaveSessionJob`

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/AutoSaveSessionJob.cs`
- Create: `tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveSessionJobHealthTests.cs`

- [ ] **Step 3.2.1: Scrivi il test di integrazione job → tracker**

```csharp
// tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveSessionJobHealthTests.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using Api.BoundedContexts.SessionTracking.Infrastructure.Scheduling;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Quartz;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class AutoSaveSessionJobHealthTests
{
    [Fact]
    public async Task Execute_OnSuccess_RecordsRunInTracker()
    {
        var tracker = new AutoSaveHealthTracker(new TestClock(DateTimeOffset.UtcNow));
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Unit.Value);

        var job = new AutoSaveSessionJob(mediator.Object, NullLogger<AutoSaveSessionJob>.Instance, tracker);
        var ctx = BuildContext(Guid.NewGuid());

        await job.Execute(ctx);

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    [Fact]
    public async Task Execute_OnHandledException_StillRecordsRun()
    {
        var tracker = new AutoSaveHealthTracker(new TestClock(DateTimeOffset.UtcNow));
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<AutoSaveSessionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var job = new AutoSaveSessionJob(mediator.Object, NullLogger<AutoSaveSessionJob>.Instance, tracker);
        var ctx = BuildContext(Guid.NewGuid());

        await job.Execute(ctx);

        tracker.GetLastRunAgeSeconds().Should().Be(0);
    }

    [Fact]
    public async Task Execute_OnInvalidSessionId_DoesNotRecordRun()
    {
        var tracker = new AutoSaveHealthTracker(new TestClock(DateTimeOffset.UtcNow));
        var mediator = new Mock<IMediator>();

        var job = new AutoSaveSessionJob(mediator.Object, NullLogger<AutoSaveSessionJob>.Instance, tracker);
        var ctx = BuildContext(sessionIdRaw: "not-a-guid");

        await job.Execute(ctx);

        tracker.GetLastRunAgeSeconds().Should().BeNull();
        mediator.Verify(
            m => m.Send(It.IsAny<AutoSaveSessionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static IJobExecutionContext BuildContext(Guid? sessionId = null, string? sessionIdRaw = null)
    {
        var dataMap = new JobDataMap();
        dataMap.Put(AutoSaveSessionJob.SessionIdKey, sessionIdRaw ?? sessionId!.Value.ToString());

        var ctx = new Mock<IJobExecutionContext>();
        ctx.SetupGet(c => c.MergedJobDataMap).Returns(dataMap);
        ctx.SetupGet(c => c.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private sealed class TestClock(DateTimeOffset start) : ISystemClock
    {
        public DateTimeOffset UtcNow { get; } = start;
    }
}
```

- [ ] **Step 3.2.2: Run test, verifica fallisca (compile error: constructor signature)**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutoSaveSessionJobHealthTests"`
Expected: FAIL — `AutoSaveSessionJob` non accetta `IAutoSaveHealthTracker`.

- [ ] **Step 3.2.3: Modifica `AutoSaveSessionJob` per iniettare il tracker**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/AutoSaveSessionJob.cs
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Scheduling;

[DisallowConcurrentExecution]
internal sealed class AutoSaveSessionJob(
    IMediator mediator,
    ILogger<AutoSaveSessionJob> logger,
    IAutoSaveHealthTracker healthTracker
) : IJob
{
    internal const string SessionIdKey = "SessionId";
    internal const string GroupName = "session-autosave";
    internal const int IntervalSeconds = 60;

    public async Task Execute(IJobExecutionContext context)
    {
        var sessionIdStr = context.MergedJobDataMap.GetString(SessionIdKey);
        if (!Guid.TryParse(sessionIdStr, out var sessionId))
        {
            logger.LogWarning("AutoSaveSessionJob: invalid SessionId in job data: {Raw}", sessionIdStr);
            return;
        }

        logger.LogDebug("AutoSaveSessionJob firing for session {SessionId}", sessionId);

        try
        {
            await mediator.Send(new AutoSaveSessionCommand(sessionId), context.CancellationToken)
                .ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Background job must not throw
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AutoSaveSessionJob failed for session {SessionId}", sessionId);
        }
#pragma warning restore CA1031
        finally
        {
            // Record liveness regardless of mediator outcome — a handled exception
            // is still a sign that the job runner is alive.
            healthTracker.RecordRun();
        }
    }

    internal static JobKey GetJobKey(Guid sessionId) =>
        new($"autosave-{sessionId}", GroupName);

    internal static TriggerKey GetTriggerKey(Guid sessionId) =>
        new($"autosave-trigger-{sessionId}", GroupName);
}
```

- [ ] **Step 3.2.4: Run test, verifica passi**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutoSaveSessionJobHealthTests"`
Expected: PASS (3 test).

- [ ] **Step 3.2.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Scheduling/AutoSaveSessionJob.cs tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveSessionJobHealthTests.cs
git commit -m "feat(autosave): record run liveness in AutoSaveSessionJob"
```

---

### Task 3.3 — ObservableGauge nella partial class metrics

**Files:**
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.SessionAutoSave.cs`

- [ ] **Step 3.3.1: Crea la partial class della metric**

```csharp
// apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.SessionAutoSave.cs
using System.Diagnostics.Metrics;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Observable gauge: seconds since the last AutoSaveSessionJob execution
    /// (across all active session schedules). Reads from the singleton
    /// IAutoSaveHealthTracker resolved at registration time.
    ///
    /// Reports -1 when no run has happened yet (process startup or no active sessions).
    /// Alert when value > 120 (more than 2 missed intervals at 60s each).
    /// </summary>
    public static void RegisterAutoSaveHealthGauge(IAutoSaveHealthTracker tracker)
    {
        Meter.CreateObservableGauge(
            name: "meepleai.session.autosave.last_run_age_seconds",
            observeValue: () => tracker.GetLastRunAgeSeconds() ?? -1,
            unit: "s",
            description: "Seconds elapsed since the last AutoSaveSessionJob execution. -1 when no run has occurred yet.");
    }
}
```

- [ ] **Step 3.3.2: Build per verificare compilazione**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED.

- [ ] **Step 3.3.3: Commit**

```bash
git add apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.SessionAutoSave.cs
git commit -m "feat(observability): add autosave last_run_age_seconds gauge"
```

---

### Task 3.3.bis — Background warning logger su threshold > 120s

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthLoggerService.cs`
- Create: `tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthLoggerServiceTests.cs`

> **Razionale**: la spec §scenario "AutoSave health metric" richiede `log warning "AutoSave job stale: last run X seconds ago"` quando age > 120s. La metric da sola non basta — serve un `IHostedService` che fa polling periodico e logga.

- [ ] **Step 3.3.bis.1: Scrivi il test**

```csharp
// tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthLoggerServiceTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Api.BoundedContexts.SessionTracking.Infrastructure.Health;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Unit;

public class AutoSaveHealthLoggerServiceTests
{
    [Fact]
    public void EvaluateAndLog_WhenAgeIsNull_DoesNotLog()
    {
        var tracker = new Mock<IAutoSaveHealthTracker>();
        tracker.Setup(t => t.GetLastRunAgeSeconds()).Returns((long?)null);
        var logger = new Mock<ILogger<AutoSaveHealthLoggerService>>();
        var svc = new AutoSaveHealthLoggerService(tracker.Object, logger.Object);

        svc.EvaluateAndLog();

        VerifyNoWarning(logger);
    }

    [Fact]
    public void EvaluateAndLog_WhenAgeBelowThreshold_DoesNotLog()
    {
        var tracker = new Mock<IAutoSaveHealthTracker>();
        tracker.Setup(t => t.GetLastRunAgeSeconds()).Returns(60L);
        var logger = new Mock<ILogger<AutoSaveHealthLoggerService>>();
        var svc = new AutoSaveHealthLoggerService(tracker.Object, logger.Object);

        svc.EvaluateAndLog();

        VerifyNoWarning(logger);
    }

    [Fact]
    public void EvaluateAndLog_WhenAgeAboveThreshold_LogsWarning()
    {
        var tracker = new Mock<IAutoSaveHealthTracker>();
        tracker.Setup(t => t.GetLastRunAgeSeconds()).Returns(150L);
        var logger = new Mock<ILogger<AutoSaveHealthLoggerService>>();
        var svc = new AutoSaveHealthLoggerService(tracker.Object, logger.Object);

        svc.EvaluateAndLog();

        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, _) => o.ToString()!.Contains("AutoSave job stale")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    private static void VerifyNoWarning(Mock<ILogger<AutoSaveHealthLoggerService>> logger)
    {
        logger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }
}
```

- [ ] **Step 3.3.bis.2: Run test, verifica fallisca**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutoSaveHealthLoggerServiceTests"`
Expected: FAIL — `AutoSaveHealthLoggerService` non esiste.

- [ ] **Step 3.3.bis.3: Implementa il service**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthLoggerService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Health;

/// <summary>
/// IHostedService that polls the AutoSave health tracker every 30 seconds and
/// emits a warning log when the last run is older than 120 seconds (2 missed
/// 60s intervals). Complements the Prometheus gauge with a textual signal.
/// </summary>
internal sealed class AutoSaveHealthLoggerService : BackgroundService
{
    private const int StaleThresholdSeconds = 120;
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IAutoSaveHealthTracker _tracker;
    private readonly ILogger<AutoSaveHealthLoggerService> _logger;

    public AutoSaveHealthLoggerService(
        IAutoSaveHealthTracker tracker,
        ILogger<AutoSaveHealthLoggerService> logger)
    {
        _tracker = tracker;
        _logger = logger;
    }

    /// <summary>
    /// Single evaluation pass. Public for unit testing.
    /// </summary>
    public void EvaluateAndLog()
    {
        var ageSeconds = _tracker.GetLastRunAgeSeconds();
        if (ageSeconds is null)
        {
            return;
        }

        if (ageSeconds.Value > StaleThresholdSeconds)
        {
            _logger.LogWarning(
                "AutoSave job stale: last run {AgeSeconds} seconds ago (threshold {Threshold}s)",
                ageSeconds.Value,
                StaleThresholdSeconds);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                EvaluateAndLog();
            }
#pragma warning disable CA1031 // Background loop must not throw
            catch (Exception ex)
            {
                _logger.LogError(ex, "AutoSaveHealthLoggerService evaluation failed");
            }
#pragma warning restore CA1031

            try
            {
                await Task.Delay(PollInterval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
```

- [ ] **Step 3.3.bis.4: Run test, verifica passi**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~AutoSaveHealthLoggerServiceTests"`
Expected: PASS (3 test).

- [ ] **Step 3.3.bis.5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Health/AutoSaveHealthLoggerService.cs tests/Api.Tests/BoundedContexts/SessionTracking/Unit/AutoSaveHealthLoggerServiceTests.cs
git commit -m "feat(observability): add AutoSaveHealthLoggerService for stale-job warnings"
```

---

### Task 3.4 — Registrazione DI + invocazione gauge

**Files:**
- Modify: l'extension class che registra i servizi SessionTracking (verifica path con Glob)

- [ ] **Step 3.4.1: Trova la classe di registrazione DI**

Run: `grep -rn "IAutoSaveSchedulerService\|QuartzAutoSaveSchedulerService" apps/api/src/Api/BoundedContexts/SessionTracking apps/api/src/Api/Extensions 2>/dev/null | grep -v "/bin/\|/obj/"`

Expected: trovare il file Extension che fa `services.AddSingleton<IAutoSaveSchedulerService, QuartzAutoSaveSchedulerService>()` o simile. Annotare il path.

- [ ] **Step 3.4.2: Aggiungi registrazione singleton + clock + hosted service**

Nel file individuato, vicino alla registrazione di `IAutoSaveSchedulerService`, aggiungi:

```csharp
// Health tracker for AutoSave job (singleton, observed by Prometheus gauge)
services.TryAddSingleton<Microsoft.Extensions.Internal.ISystemClock,
                        Microsoft.Extensions.Internal.SystemClock>();
services.AddSingleton<
    Api.BoundedContexts.SessionTracking.Infrastructure.Health.IAutoSaveHealthTracker,
    Api.BoundedContexts.SessionTracking.Infrastructure.Health.AutoSaveHealthTracker>();
services.AddHostedService<
    Api.BoundedContexts.SessionTracking.Infrastructure.Health.AutoSaveHealthLoggerService>();
```

> **Nota**: `TryAddSingleton` evita doppia registrazione di `ISystemClock` se già presente in Auth. Importa `Microsoft.Extensions.DependencyInjection.Extensions` se serve.

- [ ] **Step 3.4.3: Invoca `RegisterAutoSaveHealthGauge` allo startup**

Nel `Program.cs`, dopo `var app = builder.Build();` (prima dei middleware), aggiungi:

```csharp
// Register custom observable gauges that need DI
{
    using var scope = app.Services.CreateScope();
    var tracker = scope.ServiceProvider.GetRequiredService<
        Api.BoundedContexts.SessionTracking.Infrastructure.Health.IAutoSaveHealthTracker>();
    Api.Observability.MeepleAiMetrics.RegisterAutoSaveHealthGauge(tracker);
}
```

> **Pattern check**: se Program.cs già usa un metodo come `app.RegisterCustomMetrics()` in `WebApplicationExtensions.cs`, aggiungi l'invocazione lì invece per consistency.

- [ ] **Step 3.4.4: Build + run integration test smoke**

Run: `dotnet build apps/api/src/Api/Api.csproj`
Expected: BUILD SUCCEEDED.

Run (opzionale, smoke test integration sul `/metrics`):
```bash
cd apps/api/src/Api && dotnet run &
sleep 5
curl -s http://localhost:8080/metrics | grep meepleai_session_autosave_last_run_age_seconds
kill %1
```
Expected: la metrica appare con valore `-1` (nessun job ancora eseguito).

- [ ] **Step 3.4.5: Commit**

```bash
git add apps/api/src/Api/Extensions/ apps/api/src/Api/Program.cs
git commit -m "feat(observability): wire AutoSave health gauge in DI + startup"
```

---

## PHASE 4 — Validation finale

### Task 4.1 — Full test suite + lint

- [ ] **Step 4.1.1: Run backend full test**

Run: `dotnet test tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=SessionTracking|FullyQualifiedName~AutoSave"`
Expected: PASS (almeno i 7 nuovi test + tutti gli esistenti del BC).

- [ ] **Step 4.1.2: Run frontend full test**

Run: `cd apps/web && pnpm test`
Expected: PASS (suite intera, inclusi i nuovi GameKbBadge, useGameKbStatus, photo-store, photos page).

- [ ] **Step 4.1.3: Run frontend lint + typecheck**

Run: `cd apps/web && pnpm lint && pnpm typecheck`
Expected: PASS.

- [ ] **Step 4.1.4: Smoke test manuale mobile viewport**

In Chrome DevTools (375×667), avvia `pnpm dev`:
1. Login
2. Crea o apri un GameNight Published con 2 giochi (uno indicizzato, uno no)
3. Verifica che il selettore mostri 2 badge differenti
4. Seleziona il gioco non indicizzato → vedi warning inline
5. Procedi e crea la session
6. Naviga a `/sessions/live/<id>/photos`, scatta una foto (file picker)
7. Refresh della pagina → la foto è ancora presente
8. Apri `http://localhost:8080/metrics` → cerca `meepleai_session_autosave_last_run_age_seconds`

- [ ] **Step 4.1.5: Commit eventuali fix**

```bash
git add -p
git commit -m "fix: address smoke test findings"
```

---

### Task 4.2 — Apri PR

- [ ] **Step 4.2.1: Detect parent branch**

Run: `git config branch.$(git branch --show-current).parent || git merge-base --fork-point main-dev HEAD || echo "main-dev"`
Expected: `main-dev` (o il branch parent reale).

- [ ] **Step 4.2.2: Push branch**

```bash
git push -u origin $(git branch --show-current)
```

- [ ] **Step 4.2.3: Apri PR verso parent (NON main)**

```bash
gh pr create --base main-dev --title "feat(game-night): MVP hardening — PDF soft-filter + IndexedDB photos + autosave health" --body "$(cat <<'EOF'
## Summary
- F1: PDF-aware soft-filter nel GameNightWizard (badge "AI pronto" / "Solo manuale" + warning inline)
- F2: Persistenza foto via IndexedDB (`lib/storage/photo-store`), survive refresh
- F3: Metric `meepleai_session_autosave_last_run_age_seconds` esposta via /metrics

## Spec
docs/superpowers/specs/2026-04-08-game-night-mobile-flow-spec-panel.md

## Test plan
- [x] Unit: useGameKbStatus, GameKbBadge, photo-store, photos page, AutoSaveHealthTracker, AutoSaveSessionJob
- [x] Lint + typecheck FE
- [x] Build BE
- [x] Smoke test mobile viewport

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-Review Checklist (eseguito 2026-04-08)

### 1. Spec coverage
| Spec requirement | Plan task | OK |
|---|---|---|
| F1 badge AI pronto / Solo manuale | 1.1, 1.2, 1.3 | ✅ |
| F1 warning inline | 1.3 step 1.3.5 (`GameKbWarning`) | ✅ |
| F1 procedere comunque | 1.3.2 test + soft filter design | ✅ |
| F2 IndexedDB key `photo:<sessionId>:<uuid>` | 2.1 step 2.1.5 `generateId` | ✅ |
| F2 survive refresh | 2.2 step 2.2.1 test 3 | ✅ |
| F2 listing/delete | 2.1 + 2.2 | ✅ |
| F3 metric `meepleai_session_autosave_last_run_age_seconds` | 3.3 | ✅ |
| F3 log warning > 120s | 3.3.bis (aggiunto in self-review) | ✅ |
| F3 startup -1 quando null | 3.1 test 1 + 3.3 (`?? -1`) | ✅ |
| Smoke test mobile | 4.1.4 | ✅ |
| PR a parent branch | 4.2.1 | ✅ |

### 2. Placeholder scan
- ❌ Nessun TBD/TODO/"implement later"/"add appropriate"/"fill in details"
- ✅ Le note "verifica path con Glob/Grep" sono **investigation steps** legittimi (Step 1.1.1, 1.3.1, 3.4.1) — non placeholder
- ✅ Test setup di Step 1.3.2 è completo con QueryClient wrapper, mock vi.fn, render esplicito
- ✅ Riferimento "carica" placeholder in Architecture §F2 si riferisce al codice **esistente** da rimuovere, non al piano

### 3. Type consistency
- `useGameKbStatus(gameId: string | null)` → ritorna `UseGameKbStatusResult` con `isIndexed: boolean` — usato in `GameKbBadge`, `GameKbWarning`, `GameOption` con stessa firma ✅
- `StoredPhoto { id, sessionId, filename, timestamp, blob }` → consistente in `addPhoto`, `listPhotos`, `deletePhoto` ✅
- `DisplayPhoto { id, objectUrl, timestamp }` → derivato da StoredPhoto, conversione via `toDisplay()` ✅
- `IAutoSaveHealthTracker.GetLastRunAgeSeconds(): long?` → ritorno consistente in tracker, gauge (`?? -1`), logger (`is null` check) ✅
- `AutoSaveHealthTracker(ISystemClock clock)` → constructor signature consistente in test, DI registration ✅
- `AutoSaveSessionJob(IMediator, ILogger, IAutoSaveHealthTracker)` → signature consistente in test setup e implementazione ✅

### Issue trovati e fixati durante self-review
1. **🔴 Bug Rules of Hooks** (Step 1.3.5 originale): chiamata `useGameKbStatus` dentro IIFE. → Fixato estraendo `GameKbWarning` componente separato.
2. **Test setup placeholder** (Step 1.3.2 originale): commenti `/* same setup */`. → Fixato con setup completo e wrapper `QueryClientProvider`.
3. **Spec gap F3** (log warning > 120s): non coperto dal piano originale. → Aggiunto Task 3.3.bis con `AutoSaveHealthLoggerService` come `BackgroundService`.

### Verdict finale
✅ **Plan ready for execution**. Coverage 100%, no placeholders, type consistency verificata.
