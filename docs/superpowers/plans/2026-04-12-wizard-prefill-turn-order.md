# Session Wizard — Pre-fill & Turn Order (S1 + S2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiunge game pre-fill (skip step 1 se lanciato dalla game card) e definizione ordine turni (nuovo step 3) al wizard di creazione sessione mobile.

**Architecture:** `page.tsx` legge `?gameId=&gameName=` via `useSearchParams` e li passa come props a `SessionWizardMobile`. Il wizard espande da 4 a 5 step inserendo "Ordine Turni" come step 3. `handleStart` raccoglie i `playerIds` restituiti da `addPlayer` e chiama l'API esistente `updateTurnOrder`. `SessionDrawerSheet` allinea il param da `?game=` a `?gameId=&gameName=`.

**Tech Stack:** Next.js 16 App Router · React 19 · Vitest + React Testing Library · `@/lib/api` (liveSessionsClient) · lucide-react

---

## File Map

| File | Tipo | Modifica |
|------|------|---------|
| `apps/web/src/app/(authenticated)/sessions/new/page.tsx` | Modify | Aggiunge `useSearchParams`, passa prefill props al wizard |
| `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx` | Modify | Nuove props, 5 step, step 3 UI, `handleStart` aggiornato |
| `apps/web/src/components/library/SessionDrawerSheet.tsx` | Modify | Allinea URL param: `?game=` → `?gameId=&gameName=` |
| `apps/web/src/app/(authenticated)/sessions/new/__tests__/session-wizard-mobile.test.tsx` | Create | Test unitari per S1 e S2 |

---

## Task 1: Crea branch e test file (TDD — failing)

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/new/__tests__/session-wizard-mobile.test.tsx`

- [ ] **1.1 — Crea branch**

```bash
cd D:/Repositories/meepleai-monorepo-frontend
git checkout main-dev && git pull
git checkout -b feature/s1-s2-wizard-prefill-turn-order
git config branch.feature/s1-s2-wizard-prefill-turn-order.parent main-dev
```

- [ ] **1.2 — Crea test file**

Crea `apps/web/src/app/(authenticated)/sessions/new/__tests__/session-wizard-mobile.test.tsx`:

```tsx
/**
 * SessionWizardMobile — S1 prefill + S2 turn order tests
 */
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SessionWizardMobile } from '../session-wizard-mobile';

// ── Mocks ──────────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: () => ({ data: { items: [] }, isLoading: false }),
}));

const mockApi = {
  games: { getPhaseTemplates: vi.fn().mockResolvedValue([]) },
  liveSessions: {
    createSession: vi.fn().mockResolvedValue('session-123'),
    addPlayer: vi
      .fn()
      .mockResolvedValueOnce('player-uuid-1')
      .mockResolvedValueOnce('player-uuid-2')
      .mockResolvedValueOnce('player-uuid-3'),
    updateTurnOrder: vi.fn().mockResolvedValue(undefined),
    configurePhases: vi.fn().mockResolvedValue(undefined),
  },
};

vi.mock('@/lib/api', () => ({ api: mockApi }));

beforeEach(() => {
  vi.clearAllMocks();
  mockApi.liveSessions.addPlayer
    .mockResolvedValueOnce('player-uuid-1')
    .mockResolvedValueOnce('player-uuid-2')
    .mockResolvedValueOnce('player-uuid-3');
});

// ── S1: Pre-fill ───────────────────────────────────────────────────────────────

describe('SessionWizardMobile — S1: pre-fill da game card', () => {
  it('mostra step 1 (scegli gioco) quando nessun prefilledGameId', () => {
    render(<SessionWizardMobile />);
    expect(screen.getByText('Scegli un gioco')).toBeInTheDocument();
  });

  it('salta a step 2 quando prefilledGameId è fornito', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    expect(screen.queryByText('Scegli un gioco')).not.toBeInTheDocument();
    expect(screen.getByText('Aggiungi giocatori')).toBeInTheDocument();
  });

  it('mostra context pill del gioco pre-selezionato nello step 2', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('mostra 5 dot con il primo marcato come done (pre-filled)', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    const dots = screen.getAllByRole('button', { name: /Passo/i });
    expect(dots).toHaveLength(5);
  });
});

// ── S2: Turn Order ─────────────────────────────────────────────────────────────

describe('SessionWizardMobile — S2: step ordine turni', () => {
  it('mostra lo step "Ordine turni" dopo step giocatori', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    // step 2 → Avanti → step 3
    fireEvent.click(screen.getByText('Avanti'));
    expect(screen.getByText('Ordine turni')).toBeInTheDocument();
  });

  it('mostra badge posizione "1°" per il primo giocatore', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    expect(screen.getByText('1°')).toBeInTheDocument();
  });

  it('sposta giocatore in basso cliccando ↓', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    // Aggiungi secondo giocatore in step 2
    const nameInput = screen.getByLabelText('Nome nuovo giocatore');
    fireEvent.change(nameInput, { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));

    // → step 3 (ordine turni)
    fireEvent.click(screen.getByText('Avanti'));

    // "Giocatore 1" è in posizione 1 — clicca ↓ per spostarlo
    const downButtons = screen.getAllByRole('button', { name: /Sposta in basso/i });
    fireEvent.click(downButtons[0]);

    // Ora Bob dovrebbe avere badge "1°"
    const firstBadge = screen.getByText('1°');
    const firstRow = firstBadge.closest('[role="listitem"]');
    expect(firstRow).toHaveTextContent('Bob');
  });

  it('disabilita freccia ▲ per il primo giocatore', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    const upButtons = screen.getAllByRole('button', { name: /Sposta in alto/i });
    expect(upButtons[0]).toBeDisabled();
  });

  it('la summary mostra la sezione ordine turni con badge numerati', () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);
    // step 2 → Avanti (step 3)
    fireEvent.click(screen.getByText('Avanti'));
    // step 3 → Avanti (step 4 — fasi)
    fireEvent.click(screen.getByText('Avanti'));
    // step 4 → Salta (step 5 — summary)
    fireEvent.click(screen.getByText('Salta'));
    // Summary dovrebbe mostrare "Ordine turni"
    expect(screen.getByText('Ordine turni')).toBeInTheDocument();
  });
});

// ── S2: handleStart chiama updateTurnOrder ─────────────────────────────────────

describe('SessionWizardMobile — S2: handleStart chiama updateTurnOrder', () => {
  it('chiama updateTurnOrder con i playerIds in ordine dopo addPlayer', async () => {
    render(<SessionWizardMobile prefilledGameId="game-abc" prefilledGameName="Catan" />);

    // Aggiungi secondo giocatore
    const nameInput = screen.getByLabelText('Nome nuovo giocatore');
    fireEvent.change(nameInput, { target: { value: 'Bob' } });
    fireEvent.click(screen.getByText('Aggiungi'));

    // step 2 → 3 → 4 → 5
    fireEvent.click(screen.getByText('Avanti')); // → step 3
    fireEvent.click(screen.getByText('Avanti')); // → step 4
    fireEvent.click(screen.getByText('Salta'));  // → step 5

    // Avvia partita
    fireEvent.click(screen.getByText('Inizia a Giocare'));

    await waitFor(() => {
      expect(mockApi.liveSessions.updateTurnOrder).toHaveBeenCalledWith('session-123', {
        playerIds: ['player-uuid-1', 'player-uuid-2'],
      });
    });
  });
});
```

- [ ] **1.3 — Verifica che i test falliscano (expected)**

```bash
cd apps/web && pnpm test -- --reporter=verbose session-wizard-mobile 2>&1 | tail -20
```

Expected: FAIL — `SessionWizardMobile` non ha props `prefilledGameId`, step 3 non esiste.

---

## Task 2: Aggiorna `page.tsx` — legge searchParams

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/page.tsx`

- [ ] **2.1 — Aggiungi `useSearchParams` e passa prefill props**

Sostituisci il contenuto di `page.tsx`:

```tsx
'use client';

/**
 * New Session Page
 *
 * Issue #5041 — Sessions Redesign Phase 1
 * Issue #123 — Game Night Quick Start Wizard entry point
 * S1: reads ?gameId=&gameName= to pre-fill the wizard (skip step 1)
 */

import { useCallback, useState } from 'react';

import { PartyPopper } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { GameNightWizard } from '@/components/game-night/GameNightWizard';
import { SessionCreationWizard } from '@/components/session/SessionCreationWizard';
import { Button } from '@/components/ui/primitives/button';

import { SessionWizardMobile } from './session-wizard-mobile';

export default function NewSessionPage() {
  const [showWizard, setShowWizard] = useState(false);
  const router = useRouter();
  const searchParams = useSearchParams();

  const prefilledGameId = searchParams.get('gameId') ?? undefined;
  const prefilledGameName = searchParams.get('gameName') ?? undefined;

  const handleWizardComplete = useCallback(
    (sessionId: string) => {
      router.push(`/sessions/${sessionId}/play`);
    },
    [router]
  );

  return (
    <>
      {/* Mobile: simplified wizard — pre-filled if launched from game card */}
      <div className="lg:hidden container mx-auto px-4 py-6">
        <SessionWizardMobile
          prefilledGameId={prefilledGameId}
          prefilledGameName={prefilledGameName}
        />
      </div>

      {/* Desktop: full wizard */}
      <div className="hidden lg:block container mx-auto px-4 py-6 space-y-6">
        {!showWizard ? (
          <div
            className="rounded-xl border-2 border-amber-500/30 bg-amber-500/5 p-6"
            data-testid="game-night-entry"
          >
            <h2 className="font-quicksand font-bold text-xl mb-2">Serata di Gioco</h2>
            <p className="text-sm text-muted-foreground mb-4">
              Trova un gioco, carica il regolamento e inizia subito — l&apos;agente AI ti assiste.
            </p>
            <Button
              onClick={() => setShowWizard(true)}
              className="bg-amber-600 hover:bg-amber-700 text-white"
              data-testid="start-game-night-button"
            >
              <PartyPopper className="h-4 w-4 mr-2" aria-hidden="true" />
              Inizia Serata di Gioco
            </Button>
          </div>
        ) : (
          <div className="rounded-xl border border-slate-200 dark:border-slate-700 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-quicksand font-bold text-xl">Serata di Gioco</h2>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setShowWizard(false)}
                data-testid="close-wizard-button"
              >
                Chiudi
              </Button>
            </div>
            <GameNightWizard onComplete={handleWizardComplete} />
          </div>
        )}
        {!showWizard && <SessionCreationWizard />}
      </div>
    </>
  );
}
```

- [ ] **2.2 — Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/new/page.tsx
git commit -m "feat(wizard): read gameId/gameName from searchParams and pass to mobile wizard"
```

---

## Task 3: Allinea `SessionDrawerSheet` — param URL

**Files:**
- Modify: `apps/web/src/components/library/SessionDrawerSheet.tsx`

- [ ] **3.1 — Aggiorna `handleNewSession`**

In `SessionDrawerSheet.tsx`, sostituisci la funzione `handleNewSession` (riga ~50):

```tsx
// Prima
const handleNewSession = () => {
  onOpenChange(false);
  window.location.href = `/sessions/new?game=${gameId}`;
};

// Dopo
const handleNewSession = () => {
  onOpenChange(false);
  window.location.href = `/sessions/new?gameId=${encodeURIComponent(gameId)}&gameName=${encodeURIComponent(gameTitle)}`;
};
```

(`gameTitle` è già disponibile come prop di `SessionDrawerSheet`.)

- [ ] **3.2 — Commit**

```bash
git add apps/web/src/components/library/SessionDrawerSheet.tsx
git commit -m "fix(wizard): align session start URL param to ?gameId=&gameName= convention"
```

---

## Task 4: Refactoring `session-wizard-mobile.tsx` — props + tipo 5 step + dot aggiornati

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx`

- [ ] **4.1 — Aggiorna import lucide**

Sostituisci la riga import lucide esistente:

```tsx
// Prima
import { Search, Check, Plus, Trash2, Gamepad2, Users, Rocket, Layers, X } from 'lucide-react';

// Dopo
import { Search, Check, Plus, Trash2, Gamepad2, Users, Rocket, Layers, X, ArrowUpDown, ChevronUp, ChevronDown } from 'lucide-react';
```

- [ ] **4.2 — Aggiorna Types, Props e Constants**

Sostituisci il blocco `// ========== Types ==========` ... `// ========== Component ==========` con:

```tsx
// ========== Types ==========

interface WizardPlayer {
  id: string;
  displayName: string;
  color: PlayerColor;
}

interface WizardPhase {
  localId: string;
  phaseName: string;
}

type WizardStep = 1 | 2 | 3 | 4 | 5;

// ========== Props ==========

interface SessionWizardMobileProps {
  prefilledGameId?: string;
  prefilledGameName?: string;
}

// ========== Constants ==========

const COLOR_PALETTE: { value: PlayerColor; label: string; hex: string; className: string }[] = [
  { value: 'Red', label: 'Rosso', hex: '#ef4444', className: 'bg-red-500' },
  { value: 'Blue', label: 'Blu', hex: '#3b82f6', className: 'bg-blue-500' },
  { value: 'Green', label: 'Verde', hex: '#22c55e', className: 'bg-green-500' },
  { value: 'Yellow', label: 'Giallo', hex: '#eab308', className: 'bg-yellow-400' },
  { value: 'Purple', label: 'Viola', hex: '#a855f7', className: 'bg-purple-500' },
  { value: 'Orange', label: 'Arancione', hex: '#f97316', className: 'bg-orange-500' },
];

const STEP_ICONS: Record<WizardStep, React.ElementType> = {
  1: Gamepad2,
  2: Users,
  3: ArrowUpDown,
  4: Layers,
  5: Rocket,
};
```

- [ ] **4.3 — Aggiorna firma funzione e state iniziale**

Sostituisci la riga `export function SessionWizardMobile()` e i primi `useState`:

```tsx
export function SessionWizardMobile({
  prefilledGameId,
  prefilledGameName,
}: SessionWizardMobileProps) {
  const router = useRouter();
  const [step, setStep] = useState<WizardStep>(prefilledGameId ? 2 : 1);
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGameId, setSelectedGameId] = useState<string | null>(prefilledGameId ?? null);
  const [selectedGameName, setSelectedGameName] = useState(prefilledGameName ?? '');
```

(Lascia invariati tutti gli altri `useState` sotto.)

- [ ] **4.4 — Aggiorna Progress Dots da 4 a 5**

Sostituisci il blocco `{/* Progress dots */}`:

```tsx
{/* Progress dots */}
<div className="flex items-center justify-center gap-3 py-4">
  {([1, 2, 3, 4, 5] as WizardStep[]).map(s => {
    const Icon = STEP_ICONS[s];
    const isActive = s === step;
    const isDone = s < step || (s === 1 && !!prefilledGameId);
    return (
      <button
        key={s}
        onClick={() => {
          if (s === 1 && !!prefilledGameId) return; // step 1 non cliccabile se pre-filled
          if (s < step) setStep(s);
        }}
        disabled={s > step}
        className={cn(
          'flex items-center justify-center h-10 w-10 rounded-full transition-all',
          isActive && 'bg-amber-500 text-white shadow-md scale-110',
          isDone && !isActive && 'bg-amber-500/20 text-amber-600',
          !isActive && !isDone && 'bg-slate-200 dark:bg-slate-700 text-slate-400',
          s === 1 && !!prefilledGameId && 'opacity-50'
        )}
        aria-label={`Passo ${s}`}
      >
        {isDone && !isActive ? <Check className="h-4 w-4" /> : <Icon className="h-4 w-4" />}
      </button>
    );
  })}
</div>
```

- [ ] **4.5 — Rinomera step 3 (fasi) → 4 e step 4 (ready) → 5 nel JSX**

Nel blocco "Step content":
- Cambia `{step === 3 && (` → `{step === 4 && (` (blocco fasi)
- Cambia `{step === 4 && (` → `{step === 5 && (` (blocco ready)

Nei bottoni di navigazione del blocco fasi (ora step 4):
- `Indietro` → `setStep(3)` (torna a ordine turni)
- `Avanti/Salta` → `setStep(5)`

Nel bottone `Indietro` del blocco ready (ora step 5):
- `setStep(3)` → `setStep(4)` (torna a fasi)

Nel bottone `Avanti` del blocco giocatori (step 2):
- `setStep(3)` → `setStep(3)` ← invariato (va al nuovo step 3 ordine turni) ✓

- [ ] **4.6 — Aggiunge context pill del gioco in step 2**

Nel blocco step 2, subito dopo `<div>` con titolo "Aggiungi giocatori", aggiungi la pill solo se pre-filled:

```tsx
{/* Context pill — gioco pre-selezionato */}
{prefilledGameId && selectedGameName && (
  <div className="flex items-center gap-2 rounded-xl bg-amber-500/10 border border-amber-500/30 px-3 py-2">
    <Gamepad2 className="h-4 w-4 text-amber-500 shrink-0" />
    <div>
      <p className="text-[10px] text-muted-foreground uppercase tracking-wide">Gioco selezionato</p>
      <p className="text-sm font-semibold text-amber-400">{selectedGameName}</p>
    </div>
  </div>
)}
```

- [ ] **4.7 — Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/new/session-wizard-mobile.tsx
git commit -m "refactor(wizard): 5 steps, prefill props, ArrowUpDown icon, step renumbering"
```

---

## Task 5: Aggiunge Step 3 — Ordine Turni (UI + handlers)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx`

- [ ] **5.1 — Aggiungi handlers `movePlayerUp` e `movePlayerDown`**

Subito dopo il blocco `const changePlayerColor = useCallback(...)`, aggiungi:

```tsx
const movePlayerUp = useCallback((id: string) => {
  setPlayers(prev => {
    const idx = prev.findIndex(p => p.id === id);
    if (idx <= 0) return prev;
    const next = [...prev];
    [next[idx - 1], next[idx]] = [next[idx], next[idx - 1]];
    return next;
  });
}, []);

const movePlayerDown = useCallback((id: string) => {
  setPlayers(prev => {
    const idx = prev.findIndex(p => p.id === id);
    if (idx >= prev.length - 1) return prev;
    const next = [...prev];
    [next[idx], next[idx + 1]] = [next[idx + 1], next[idx]];
    return next;
  });
}, []);
```

- [ ] **5.2 — Aggiungi blocco JSX step 3**

Nel blocco "Step content", inserisci **dopo** il blocco step 2 e **prima** del blocco step 4 (fasi):

```tsx
{/* ——— Step 3: Ordine Turni ——— */}
{step === 3 && (
  <div className="space-y-4">
    <div>
      <h2 className="text-lg font-bold font-quicksand">Ordine turni</h2>
      <p className="text-sm text-muted-foreground">
        Chi gioca per primo? Usa le frecce per riordinare.
      </p>
    </div>

    <div className="space-y-2" role="list" aria-label="Ordine dei turni">
      {players.map((player, index) => {
        const colorInfo = COLOR_PALETTE.find(c => c.value === player.color);
        const isFirst = index === 0;
        const isLast = index === players.length - 1;
        return (
          <div
            key={player.id}
            role="listitem"
            className={cn(
              'flex items-center gap-3 rounded-xl border p-3 transition-colors',
              isFirst
                ? 'border-amber-500/40 bg-amber-500/5'
                : 'border-border bg-card'
            )}
          >
            {/* Position badge */}
            <span className="text-xs font-bold text-amber-500 w-5 shrink-0 text-center">
              {index + 1}°
            </span>

            {/* Color dot */}
            <div
              className={cn('h-7 w-7 rounded-full shrink-0', colorInfo?.className ?? 'bg-gray-400')}
            />

            {/* Name */}
            <span className="flex-1 font-medium text-sm truncate">{player.displayName}</span>

            {/* Reorder buttons */}
            <div className="flex flex-col gap-0.5 shrink-0">
              <button
                type="button"
                disabled={isFirst}
                onClick={() => movePlayerUp(player.id)}
                aria-label={`Sposta in alto ${player.displayName}`}
                className={cn(
                  'p-1 rounded transition-colors',
                  isFirst
                    ? 'opacity-20 cursor-not-allowed text-muted-foreground'
                    : 'text-muted-foreground hover:bg-white/10 hover:text-white'
                )}
              >
                <ChevronUp className="h-4 w-4" />
              </button>
              <button
                type="button"
                disabled={isLast}
                onClick={() => movePlayerDown(player.id)}
                aria-label={`Sposta in basso ${player.displayName}`}
                className={cn(
                  'p-1 rounded transition-colors',
                  isLast
                    ? 'opacity-20 cursor-not-allowed text-muted-foreground'
                    : 'text-muted-foreground hover:bg-white/10 hover:text-white'
                )}
              >
                <ChevronDown className="h-4 w-4" />
              </button>
            </div>
          </div>
        );
      })}
    </div>

    {players.length === 1 && (
      <p className="text-xs text-muted-foreground text-center py-2">
        Un solo giocatore — ordine non applicabile.
      </p>
    )}

    {/* Navigation */}
    <div className="flex gap-3">
      <Button variant="outline" className="flex-1" onClick={() => setStep(2)}>
        Indietro
      </Button>
      <GradientButton fullWidth size="lg" onClick={() => setStep(4)} className="flex-1">
        Avanti
      </GradientButton>
    </div>
  </div>
)}
```

- [ ] **5.3 — Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/new/session-wizard-mobile.tsx
git commit -m "feat(wizard): add step 3 — turn order with up/down reorder and position badges"
```

---

## Task 6: Aggiorna `handleStart` — `updateTurnOrder` + ordine in summary

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/new/session-wizard-mobile.tsx`

- [ ] **6.1 — Sostituisci `handleStart`**

Sostituisci l'intera funzione `handleStart`:

```tsx
const handleStart = useCallback(async () => {
  if (!selectedGameName || players.length === 0) return;

  setIsCreating(true);
  setError(null);

  try {
    // 1. Create session
    const sessionId = await api.liveSessions.createSession({
      gameName: selectedGameName,
      gameId: selectedGameId ?? undefined,
    });

    // 2. Add all players in turn order — collect returned playerIds
    const addedPlayerIds: string[] = [];
    for (const player of players) {
      try {
        const playerId = await api.liveSessions.addPlayer(sessionId, {
          displayName: player.displayName,
          color: player.color,
        });
        addedPlayerIds.push(playerId);
      } catch (playerErr) {
        const failedName = player.displayName;
        const remaining = players.length - addedPlayerIds.length;
        const msg = playerErr instanceof Error ? playerErr.message : 'Errore aggiunta giocatore';
        setError(
          `Errore aggiungendo "${failedName}" (${addedPlayerIds.length}/${players.length} aggiunti). ${msg}. ` +
            (remaining > 1 ? `${remaining - 1} giocatori restanti non aggiunti.` : '')
        );
        router.push(`/sessions/live/${sessionId}`);
        return;
      }
    }

    // 3. Set turn order — order of addedPlayerIds matches user-defined order from step 3
    if (addedPlayerIds.length > 1) {
      try {
        await api.liveSessions.updateTurnOrder(sessionId, { playerIds: addedPlayerIds });
      } catch {
        // Non-blocking — session proceeds with default order
      }
    }

    // 4. Configure phases if any defined
    const validPhases = phases.filter(p => p.phaseName.trim().length > 0);
    if (validPhases.length > 0) {
      try {
        await api.liveSessions.configurePhases(sessionId, {
          phaseNames: validPhases.map(p => p.phaseName.trim()),
        });
      } catch {
        // Non-blocking
      }
    }

    // 5. Navigate to live session
    router.push(`/sessions/live/${sessionId}`);
  } catch (err) {
    const msg = err instanceof Error ? err.message : 'Errore nella creazione della sessione';
    setError(msg);
  } finally {
    setIsCreating(false);
  }
}, [selectedGameName, selectedGameId, players, phases, router]);
```

- [ ] **6.2 — Aggiungi sezione ordine turni nella summary (step 5)**

Nel blocco step 5, subito **dopo** il blocco "Players" e **prima** del blocco "Phases", aggiungi:

```tsx
{/* Turn order summary */}
{players.length > 1 && (
  <div>
    <p className="text-xs text-muted-foreground mb-2">Ordine turni</p>
    <div className="flex flex-wrap gap-2">
      {players.map((player, index) => {
        const colorInfo = COLOR_PALETTE.find(c => c.value === player.color);
        return (
          <div
            key={player.id}
            className="flex items-center gap-1.5 rounded-full bg-slate-100 dark:bg-slate-800 px-2.5 py-1"
          >
            <span className="text-[10px] font-bold text-amber-500">{index + 1}°</span>
            <div className={cn('h-2.5 w-2.5 rounded-full', colorInfo?.className ?? 'bg-gray-400')} />
            <span className="text-xs font-medium">{player.displayName}</span>
          </div>
        );
      })}
    </div>
  </div>
)}
```

- [ ] **6.3 — Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/new/session-wizard-mobile.tsx
git commit -m "feat(wizard): call updateTurnOrder after addPlayer, show turn order in summary"
```

---

## Task 7: Esegui test, typecheck, lint — poi crea PR

**Files:**
- Test: `apps/web/src/app/(authenticated)/sessions/new/__tests__/session-wizard-mobile.test.tsx`

- [ ] **7.1 — Esegui test**

```bash
cd apps/web && pnpm test -- --reporter=verbose session-wizard-mobile 2>&1 | tail -30
```

Expected: tutti i test PASS.

Se fallisce il test `sposta giocatore in basso`: verifica che `aria-label` nei bottoni ChevronDown corrisponda esattamente a `Sposta in basso ${player.displayName}`.

- [ ] **7.2 — Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | grep -E "error|session-wizard|new/page" | head -20
```

Expected: nessun errore TS.

- [ ] **7.3 — Lint**

```bash
cd apps/web && pnpm lint 2>&1 | grep -E "session-wizard|new/page|SessionDrawerSheet" | head -20
```

Expected: nessun warning/errore sui file modificati.

- [ ] **7.4 — Commit test**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/new/__tests__/
git commit -m "test(wizard): add tests for S1 pre-fill and S2 turn order"
```

- [ ] **7.5 — Push e crea PR**

```bash
git push -u origin feature/s1-s2-wizard-prefill-turn-order
gh pr create \
  --base main-dev \
  --title "feat(wizard): game card pre-fill + turn order step (S1+S2)" \
  --body "$(cat <<'EOF'
## Summary
- **S1**: wizard apre direttamente step 2 quando lanciato con \`?gameId=&gameName=\` dalla game card (skip step 1)
- **S2**: nuovo step 3 "Ordine turni" con frecce ↑↓; \`updateTurnOrder\` chiamato dopo i \`addPlayer\`
- \`SessionDrawerSheet\`: allineato da \`?game=\` a \`?gameId=&gameName=\`

## Files modified
- \`sessions/new/page.tsx\` — useSearchParams, passa prefill props
- \`session-wizard-mobile.tsx\` — 5 step, step 3 nuovo, handleStart aggiornato
- \`SessionDrawerSheet.tsx\` — param URL allineato

## Test plan
- [ ] Unit: pre-fill salta step 1, step 3 ordine turni, reorder funziona, updateTurnOrder chiamato
- [ ] Manual mobile: tap "Gioca" su game card → wizard apre a step 2 con game pill
- [ ] Manual: riordina giocatori → sessione live parte con il primo giocatore corretto

🤖 Generated with [Claude Code](https://claude.ai/claude-code)
EOF
)"
```

---

## Self-Review

### Spec coverage
| Requisito | Task |
|---|---|
| S1: wizard pre-fill via `?gameId=` | Task 2, 4 |
| S1: skip step 1 quando pre-filled | Task 4.3, 4.4 |
| S1: context pill del gioco in step 2 | Task 4.6 |
| S1: `SessionDrawerSheet` param allineato | Task 3 |
| S2: step 3 ordine turni | Task 5 |
| S2: badge posizione 1°/2°/3° | Task 5.2 |
| S2: frecce ↑↓ per riordino | Task 5.2 |
| S2: freccia disabilitata a inizio/fine lista | Task 5.2 |
| S2: `updateTurnOrder` API call in handleStart | Task 6.1 |
| S2: ordine turni visibile nella summary | Task 6.2 |
| Test TDD: tutti i casi coperti | Task 1.2 |

### Placeholder scan
Nessun TBD, TODO, o "similar to Task N".

### Type consistency
- `WizardStep = 1|2|3|4|5` — definito in Task 4.2, usato in Tasks 4.3, 4.4, 5.2 ✓
- `movePlayerUp(id: string)` / `movePlayerDown(id: string)` — definiti in Task 5.1, chiamati in Task 5.2 ✓
- `addedPlayerIds: string[]` — corrisponde al return `Promise<string>` di `addPlayer` ✓
- `prefilledGameId?: string` / `prefilledGameName?: string` — definiti in Task 4.2, usati in Tasks 4.3, 4.4, 4.6 ✓
- `updateTurnOrder(sessionId, { playerIds: string[] })` — schema esistente `UpdateTurnOrderRequestSchema` ✓
