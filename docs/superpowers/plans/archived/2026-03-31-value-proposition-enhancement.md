# MeepleAI — Value Proposition Enhancement

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Trasformare MeepleAI da "utility AI per boardgame" a "compagno che salva ogni serata di gioco" — con pitch riformulato, gateway job evidente, storia di gioco prominente, identità del giocatore e misurazione del flywheel.

**Architecture:** Cinque stream indipendenti, tutti deployabili separatamente. A e B sono P0 (copia/UX landing), C è P1 (profilo stats), D è P2 (activity feed), E è P2 (analytics flywheel). Ogni stream tocca solo frontend salvo Stream E che aggiunge events al backend.

**Tech Stack:** Next.js 16 · React 19 · Tailwind 4 · shadcn/ui · Vitest (unit) · Playwright (E2E) · TypeScript

> ⚠️ Questi stream sono indipendenti: si può iniziare da qualunque stream senza dipendenze cross-stream. Si raccomanda di fare A e B prima per impatto utente immediato.

---

## File Map

### Stream A — Landing "Jobs" Reframe
| Operazione | File |
|------------|------|
| Modify | `apps/web/src/components/landing/HowItWorksSteps.tsx` |
| Modify | `apps/web/src/components/landing/WelcomeHero.tsx` |
| Modify test | `apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx` |
| Modify test | `apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx` |

### Stream B — Gateway CTA "Chiedi le regole"
| Operazione | File |
|------------|------|
| Create | `apps/web/src/components/landing/RulesQuickDemo.tsx` |
| Create test | `apps/web/src/components/landing/__tests__/RulesQuickDemo.test.tsx` |
| Modify | `apps/web/src/app/(public)/page.tsx` |
| Modify | `apps/web/src/app/(chat)/chat/new/page.tsx` (aggiunge prompt suggestions) |

### Stream C — Profile Overview Stats Expansion
| Operazione | File |
|------------|------|
| Modify | `apps/web/src/app/(authenticated)/profile/page.tsx` (OverviewTab) |
| Create | `apps/web/src/hooks/useRecentSessions.ts` |
| Create test | `apps/web/src/hooks/__tests__/useRecentSessions.test.ts` |

### Stream D — Activity Feed
| Operazione | File |
|------------|------|
| Create | `apps/web/src/components/profile/ActivityFeed.tsx` |
| Create | `apps/web/src/hooks/useActivityFeed.ts` |
| Create test | `apps/web/src/hooks/__tests__/useActivityFeed.test.ts` |
| Create test | `apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx` |
| Modify | `apps/web/src/app/(authenticated)/profile/page.tsx` (ActivityTab) |

### Stream E — Flywheel Analytics
| Operazione | File |
|------------|------|
| Create | `apps/web/src/lib/analytics/flywheel-events.ts` |
| Create test | `apps/web/src/lib/analytics/__tests__/flywheel-events.test.ts` |
| Modify | `apps/web/src/lib/analytics/track-event.ts` |
| Modify | `apps/web/src/app/(auth)/register/page.tsx` (evento sign_up) |
| Modify | vari componenti per wiring eventi (dettagliato in Stream E) |

---

## STREAM A — Landing Page: dai "passi di setup" ai "problemi risolti"

**Problema attuale:** `HowItWorksSteps` mostra 4 passi operativi (Trova → Carica → Gioca → Salva) che descrivono il setup, non il valore. Un utente che arriva non vede il suo problema rispecchiato.

**Target:** Mostrare 4 pain point risolti. L'utente si riconosce, non legge un tutorial.

### Task A1: Aggiorna i test per il nuovo copy di HowItWorksSteps

**Files:**
- Modify: `apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx`

- [ ] **Step 1: Leggi il test esistente**

  ```bash
  cat apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx
  ```

- [ ] **Step 2: Aggiorna il test con il nuovo copy**

  Sostituisci i test che verificano il vecchio testo con quelli per il nuovo:

  ```tsx
  import { render, screen } from '@testing-library/react';
  import { HowItWorksSteps } from '../HowItWorksSteps';

  describe('HowItWorksSteps', () => {
    it('mostra 4 sezioni di valore', () => {
      render(<HowItWorksSteps />);
      expect(screen.getAllByRole('listitem').length).toBe(4);
    });

    it('mostra il pain point delle regole', () => {
      render(<HowItWorksSteps />);
      expect(screen.getByText(/regola subita/i)).toBeInTheDocument();
    });

    it('mostra il pain point delle dispute', () => {
      render(<HowItWorksSteps />);
      expect(screen.getByText(/niente più dispute/i)).toBeInTheDocument();
    });

    it('mostra il pain point della serata', () => {
      render(<HowItWorksSteps />);
      expect(screen.getByText(/serata salvata/i)).toBeInTheDocument();
    });

    it('mostra la storia di gioco', () => {
      render(<HowItWorksSteps />);
      expect(screen.getByText(/ricorda tutto/i)).toBeInTheDocument();
    });

    it('ha landmark section con id come-funziona', () => {
      render(<HowItWorksSteps />);
      expect(document.getElementById('come-funziona')).toBeInTheDocument();
    });
  });
  ```

- [ ] **Step 3: Esegui i test — devono FALLIRE**

  ```bash
  cd apps/web && pnpm test src/components/landing/__tests__/HowItWorksSteps.test.tsx
  ```

  Atteso: FAIL — i testi cercano testo non ancora presente.

### Task A2: Riscrivi HowItWorksSteps con focus sui problemi

**Files:**
- Modify: `apps/web/src/components/landing/HowItWorksSteps.tsx`

- [ ] **Step 1: Sostituisci `steps` con pain-point-focused content**

  ```tsx
  const steps = [
    {
      num: '1',
      icon: '⚡',
      title: 'Regola subita',
      desc: 'Chiedi in italiano, ricevi la risposta dalla pagina esatta del manuale. In 10 secondi.',
    },
    {
      num: '2',
      icon: '🤝',
      title: 'Niente più dispute',
      desc: "L'AI cita la pagina. La discussione finisce lì. Il gioco continua.",
    },
    {
      num: '3',
      icon: '🎲',
      title: 'Serata salvata',
      desc: 'Punteggi, timer, setup guidato — tutto al tavolo senza interrompere la partita.',
    },
    {
      num: '4',
      icon: '📖',
      title: 'Ricorda tutto',
      desc: 'La tua storia di gioco, le partite, lo stile. Il tuo profilo da gamer cresce con te.',
    },
  ];
  ```

  Il JSX rimane identico (nessun cambio strutturale, solo il `steps` array). Wrap the items in a `<ul>` con `<li>` per il test `getAllByRole('listitem')`:

  ```tsx
  export function HowItWorksSteps() {
    return (
      <section
        id="come-funziona"
        aria-labelledby="how-it-works-heading"
        className="bg-muted/30 px-4 py-20"
      >
        <div className="mx-auto max-w-5xl">
          <h2
            id="how-it-works-heading"
            className="mb-12 text-center text-3xl font-bold text-foreground"
          >
            Come funziona
          </h2>
          <ul className="grid list-none grid-cols-1 gap-8 p-0 sm:grid-cols-2 lg:grid-cols-4">
            {steps.map(step => (
              <li key={step.num} className="flex flex-col items-center text-center">
                <div className="mb-3 flex h-10 w-10 items-center justify-center rounded-full bg-primary text-sm font-bold text-primary-foreground">
                  {step.num}
                </div>
                <span aria-hidden="true" className="mb-2 text-2xl">
                  {step.icon}
                </span>
                <h3 className="mb-1 text-lg font-semibold text-foreground">{step.title}</h3>
                <p className="text-sm text-muted-foreground">{step.desc}</p>
              </li>
            ))}
          </ul>
        </div>
      </section>
    );
  }
  ```

- [ ] **Step 2: Esegui i test — devono PASSARE**

  ```bash
  cd apps/web && pnpm test src/components/landing/__tests__/HowItWorksSteps.test.tsx
  ```

  Atteso: PASS tutti.

- [ ] **Step 3: Commit**

  ```bash
  git add apps/web/src/components/landing/HowItWorksSteps.tsx \
           apps/web/src/components/landing/__tests__/HowItWorksSteps.test.tsx
  git commit -m "feat(landing): reframe how-it-works from setup steps to pain points solved"
  ```

### Task A3: Aggiorna WelcomeHero — subtext più empatico

**Files:**
- Modify: `apps/web/src/components/landing/WelcomeHero.tsx`
- Modify test: `apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx`

- [ ] **Step 1: Aggiorna il test per il nuovo subtext**

  Leggi prima il test esistente: `cat apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx`

  Aggiungi (senza rimuovere i test esistenti che ancora passano):

  ```tsx
  it('mostra un subtext che nomina la dispute/regole', () => {
    render(<WelcomeHero />);
    // Il subtext deve menzionare esplicitamente dispute o regole
    const subtext = screen.getByText(/dispute|regol/i);
    expect(subtext).toBeInTheDocument();
  });
  ```

- [ ] **Step 2: Verifica che il test esistente passi ancora (non rompere nulla)**

  ```bash
  cd apps/web && pnpm test src/components/landing/__tests__/WelcomeHero.test.tsx
  ```

  Se il test che hai aggiunto fallisce, continua al prossimo step.

- [ ] **Step 3: Il subtext attuale menziona già "dispute" — verifica**

  Il testo attuale è: `"Setup, regole, punteggi, dispute — un agente AI che conosce il tuo gioco e vi aiuta al tavolo."`

  Questo già contiene "regole" e "dispute". Il test dovrebbe già passare. Se così è, nessuna modifica al componente è necessaria. Salta al commit.

  Se vuoi comunque rafforzare, sostituisci il subtext con:

  ```tsx
  <p className="mb-10 max-w-lg text-lg text-muted-foreground">
    Dispute sulle regole alle 23? Setup complicato? Punteggi da gestire?
    Un agente AI che conosce il tuo gioco risolve tutto — al tavolo, in tempo reale.
  </p>
  ```

- [ ] **Step 4: Esegui tutti i test landing**

  ```bash
  cd apps/web && pnpm test src/components/landing/
  ```

  Atteso: tutti PASS.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/components/landing/WelcomeHero.tsx \
           apps/web/src/components/landing/__tests__/WelcomeHero.test.tsx
  git commit -m "feat(landing): strengthen hero subtext with explicit pain points"
  ```

---

## STREAM B — Gateway CTA: "Chiedi le regole subito"

**Problema attuale:** Il valore principale (risoluzione regole) è nascosto nel passo 3 del flusso setup. Sulla landing non esiste un punto di ingresso diretto per il gateway job. Gli utenti non autenticati non vedono mai come funziona la cosa.

**Target:** Aggiungere sulla landing un blocco visivo che mostra l'esperienza chiave (chiedere una regola) — anche senza registrazione.

### Task B1: Crea componente RulesQuickDemo

**Files:**
- Create: `apps/web/src/components/landing/RulesQuickDemo.tsx`
- Create test: `apps/web/src/components/landing/__tests__/RulesQuickDemo.test.tsx`

- [ ] **Step 1: Scrivi il test prima del componente**

  Crea `apps/web/src/components/landing/__tests__/RulesQuickDemo.test.tsx`:

  ```tsx
  import { render, screen } from '@testing-library/react';
  import { RulesQuickDemo } from '../RulesQuickDemo';

  describe('RulesQuickDemo', () => {
    it('renderizza il titolo del blocco', () => {
      render(<RulesQuickDemo />);
      expect(screen.getByRole('heading', { name: /chiedi una regola/i })).toBeInTheDocument();
    });

    it('mostra esempi di domande cliccabili', () => {
      render(<RulesQuickDemo />);
      const examples = screen.getAllByRole('button');
      expect(examples.length).toBeGreaterThanOrEqual(3);
    });

    it('ha un link a /register o /chat/new', () => {
      render(<RulesQuickDemo />);
      const cta = screen.getByRole('link', { name: /prova gratis|inizia|chiedi ora/i });
      expect(cta).toBeInTheDocument();
    });
  });
  ```

- [ ] **Step 2: Esegui il test — deve FALLIRE**

  ```bash
  cd apps/web && pnpm test src/components/landing/__tests__/RulesQuickDemo.test.tsx
  ```

  Atteso: FAIL — componente non esiste.

- [ ] **Step 3: Crea il componente**

  Crea `apps/web/src/components/landing/RulesQuickDemo.tsx`:

  ```tsx
  import Link from 'next/link';

  import { Button } from '@/components/ui/primitives/button';

  const EXAMPLE_QUESTIONS = [
    '🎲 "Posso usare una carta azione già nella mano del turno precedente?"',
    '🃏 "Cosa succede se il mazzo si esaurisce durante la pescata?"',
    '🏰 "Un attacco con 2 guerrieri contro 1 — chi vince a parità di forza?"',
    '⚔️ "Si possono giocare più carte evento nello stesso turno?"',
  ];

  export function RulesQuickDemo() {
    return (
      <section
        aria-labelledby="rules-demo-heading"
        className="px-4 py-16 bg-background"
      >
        <div className="mx-auto max-w-3xl text-center">
          <h2
            id="rules-demo-heading"
            className="mb-3 text-2xl font-bold text-foreground"
          >
            Chiedi una regola
          </h2>
          <p className="mb-8 text-muted-foreground">
            Come se avessi un esperto del gioco sempre al tavolo con te.
          </p>

          <div className="mb-8 grid gap-3 text-left">
            {EXAMPLE_QUESTIONS.map((q, i) => (
              <button
                key={i}
                type="button"
                disabled
                className="rounded-xl border border-border/60 bg-muted/30 px-4 py-3 text-sm text-foreground/80 text-left cursor-default"
                aria-label={`Esempio di domanda: ${q}`}
              >
                {q}
              </button>
            ))}
          </div>

          <Button asChild size="lg">
            <Link href="/register">Prova gratis — risposta in 10 secondi</Link>
          </Button>
        </div>
      </section>
    );
  }
  ```

- [ ] **Step 4: Esegui il test — deve PASSARE**

  ```bash
  cd apps/web && pnpm test src/components/landing/__tests__/RulesQuickDemo.test.tsx
  ```

  Atteso: PASS.

### Task B2: Integra RulesQuickDemo nella landing

**Files:**
- Modify: `apps/web/src/app/(public)/page.tsx`

- [ ] **Step 1: Leggi la landing page attuale**

  ```bash
  cat apps/web/src/app/\(public\)/page.tsx
  ```

- [ ] **Step 2: Aggiungi `RulesQuickDemo` dopo `HowItWorksSteps`**

  ```tsx
  import { RulesQuickDemo } from '@/components/landing/RulesQuickDemo';
  // ... altri import esistenti

  export default function LandingPage() {
    return (
      <>
        <AuthRedirect />
        <WelcomeHero />
        <HowItWorksSteps />
        <RulesQuickDemo />   {/* ← AGGIUNTO */}
        <SocialProofBar />
        <WelcomeCTA />
      </>
    );
  }
  ```

  (Preserva l'ordine esatto degli altri componenti.)

- [ ] **Step 3: Verifica visiva in dev**

  ```bash
  cd apps/web && pnpm dev
  ```

  Apri http://localhost:3000 — verifica che il blocco appaia dopo "Come funziona".

- [ ] **Step 4: Commit**

  ```bash
  git add apps/web/src/components/landing/RulesQuickDemo.tsx \
           apps/web/src/components/landing/__tests__/RulesQuickDemo.test.tsx \
           apps/web/src/app/\(public\)/page.tsx
  git commit -m "feat(landing): add rules quick demo section as gateway CTA"
  ```

### Task B3: Aggiungi prompt suggestions alla nuova chat

**Files:**
- Modify: `apps/web/src/app/(chat)/chat/new/page.tsx`

- [ ] **Step 1: Leggi la pagina chat/new esistente**

  ```bash
  cat apps/web/src/app/\(chat\)/chat/new/page.tsx
  ```

- [ ] **Step 2: Identifica dove inserire i suggerimenti**

  Cerca il componente o il form dove l'utente digita il primo messaggio. Se c'è già un campo input, aggiungi sotto di esso un elenco di chip cliccabili con domande suggerite.

  Pattern da seguire (adatta al markup esistente):

  ```tsx
  const RULE_SUGGESTIONS = [
    'Ho una domanda sulle regole di un gioco',
    'Come funziona il setup di questa partita?',
    'C\'è una disputa al tavolo — aiutami a risolverla',
  ];

  // Nel JSX, dopo l'input del messaggio:
  <div className="mt-3 flex flex-wrap gap-2">
    {RULE_SUGGESTIONS.map((s) => (
      <button
        key={s}
        type="button"
        onClick={() => setMessage(s)} // o il setter del tuo form
        className="rounded-full border border-border px-3 py-1 text-xs text-muted-foreground hover:bg-muted transition-colors"
      >
        {s}
      </button>
    ))}
  </div>
  ```

  **Nota**: adatta `setMessage` al pattern di state management già presente nella pagina. Non introdurre un nuovo sistema di state.

- [ ] **Step 3: Verifica che la build non si rompa**

  ```bash
  cd apps/web && pnpm build
  ```

  Atteso: build completata senza errori TS.

- [ ] **Step 4: Commit**

  ```bash
  git add apps/web/src/app/\(chat\)/chat/new/page.tsx
  git commit -m "feat(chat): add rules-focused prompt suggestions to new chat page"
  ```

---

## STREAM C — Profile Overview: più stats, meno vuoto

**Problema attuale:** `OverviewTab` mostra solo `totalGames` e `favoriteGames`. `UserLibraryStats` ha molti più dati già disponibili (`wishlistCount`, `ownedCount`, `privatePdfs`). Non c'è nessun dato sulle sessioni.

**Target:** Mostrare 6 statistiche significative nella overview + collegamento rapido alla storia di gioco.

### Task C1: Crea hook useRecentSessions

**Files:**
- Create: `apps/web/src/hooks/useRecentSessions.ts`
- Create test: `apps/web/src/hooks/__tests__/useRecentSessions.test.ts`

- [ ] **Step 1: Scopri l'endpoint delle sessioni**

  ```bash
  grep -r "GetSessionHistory\|getSessions\|session.*history" apps/web/src/lib/api/ --include="*.ts" -l
  ```

  Identifica lo schema della risposta sessioni (cerca il file trovato e nota i campi).

- [ ] **Step 2: Scrivi il test dell'hook**

  Crea `apps/web/src/hooks/__tests__/useRecentSessions.test.ts`:

  ```ts
  import { renderHook, waitFor } from '@testing-library/react';
  import { vi, describe, it, expect, beforeEach } from 'vitest';

  // Mock dell'api module — adatta il path se diverso
  vi.mock('@/lib/api', () => ({
    api: {
      sessions: {
        list: vi.fn(),
      },
    },
  }));

  import { api } from '@/lib/api';
  import { useRecentSessions } from '../useRecentSessions';

  const mockSessions = [
    { id: '1', gameTitle: 'Wingspan', playedAt: '2026-03-30T20:00:00Z', playerCount: 3 },
    { id: '2', gameTitle: 'Catan', playedAt: '2026-03-28T19:00:00Z', playerCount: 4 },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('useRecentSessions', () => {
    it('ritorna isLoading=true inizialmente', () => {
      vi.mocked(api.sessions.list).mockResolvedValue({ items: [], totalCount: 0 });
      const { result } = renderHook(() => useRecentSessions(3));
      expect(result.current.isLoading).toBe(true);
    });

    it('ritorna le sessioni dopo il fetch', async () => {
      vi.mocked(api.sessions.list).mockResolvedValue({ items: mockSessions, totalCount: 2 });
      const { result } = renderHook(() => useRecentSessions(3));
      await waitFor(() => expect(result.current.isLoading).toBe(false));
      expect(result.current.sessions).toHaveLength(2);
    });

    it('rispetta il limite passato come parametro', async () => {
      vi.mocked(api.sessions.list).mockResolvedValue({ items: mockSessions, totalCount: 2 });
      renderHook(() => useRecentSessions(3));
      await waitFor(() => {});
      expect(api.sessions.list).toHaveBeenCalledWith(expect.objectContaining({ pageSize: 3 }));
    });

    it('espone error se il fetch fallisce', async () => {
      vi.mocked(api.sessions.list).mockRejectedValue(new Error('Network error'));
      const { result } = renderHook(() => useRecentSessions(3));
      await waitFor(() => expect(result.current.isLoading).toBe(false));
      expect(result.current.error).toBeTruthy();
    });
  });
  ```

- [ ] **Step 3: Esegui il test — deve FALLIRE**

  ```bash
  cd apps/web && pnpm test src/hooks/__tests__/useRecentSessions.test.ts
  ```

- [ ] **Step 4: Implementa l'hook**

  Prima verifica come `api.sessions.list` è definito nel codebase:

  ```bash
  grep -r "sessions" apps/web/src/lib/api/index.ts 2>/dev/null || \
  grep -r "sessions.*list\|listSessions" apps/web/src/lib/api/ --include="*.ts" -l
  ```

  Crea `apps/web/src/hooks/useRecentSessions.ts` adattando il path dell'API:

  ```ts
  import { useEffect, useState } from 'react';

  import { api } from '@/lib/api';

  export interface RecentSession {
    id: string;
    gameTitle: string;
    playedAt: string;
    playerCount: number;
  }

  export interface UseRecentSessionsResult {
    sessions: RecentSession[];
    isLoading: boolean;
    error: string | null;
  }

  export function useRecentSessions(limit: number): UseRecentSessionsResult {
    const [sessions, setSessions] = useState<RecentSession[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
      api.sessions
        .list({ pageSize: limit, page: 1 })
        .then(data => setSessions(data.items ?? []))
        .catch(err => setError(err instanceof Error ? err.message : 'Errore caricamento sessioni'))
        .finally(() => setIsLoading(false));
    }, [limit]);

    return { sessions, isLoading, error };
  }
  ```

  **Nota**: adatta `api.sessions.list` al metodo effettivo trovato nel passo precedente.

- [ ] **Step 5: Esegui il test — deve PASSARE**

  ```bash
  cd apps/web && pnpm test src/hooks/__tests__/useRecentSessions.test.ts
  ```

### Task C2: Espandi OverviewTab con più statistiche

**Files:**
- Modify: `apps/web/src/app/(authenticated)/profile/page.tsx`

- [ ] **Step 1: Sostituisci OverviewTab con versione espansa**

  Nel file `apps/web/src/app/(authenticated)/profile/page.tsx`, sostituisci l'intera funzione `OverviewTab`:

  ```tsx
  import { useRecentSessions } from '@/hooks/useRecentSessions';
  // aggiungi questo import in cima al file

  function OverviewTab() {
    const [stats, setStats] = useState<UserLibraryStats | null>(null);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);
    const { sessions, isLoading: sessionsLoading } = useRecentSessions(3);

    useEffect(() => {
      api.library
        .getStats()
        .then(data => setStats(data))
        .catch(err => setError(err instanceof Error ? err.message : 'Failed to load stats'))
        .finally(() => setIsLoading(false));
    }, []);

    return (
      <div className="space-y-6">
        {/* Library Stats — espanso */}
        <Card className="border-l-4 border-l-[hsl(262,83%,58%)] shadow-sm">
          <CardHeader className="pb-2">
            <CardTitle className="font-quicksand text-lg">La tua libreria</CardTitle>
          </CardHeader>
          <CardContent>
            {isLoading && (
              <div className="grid grid-cols-3 gap-3">
                {[...Array(6)].map((_, i) => (
                  <Skeleton key={i} className="h-14 w-full rounded-lg" />
                ))}
              </div>
            )}
            {error && (
              <Alert>
                <AlertDescription className="font-nunito text-sm">{error}</AlertDescription>
              </Alert>
            )}
            {!isLoading && !error && stats && (
              <div className="grid grid-cols-3 gap-3">
                <StatTile icon={Gamepad2} label="Giochi" value={stats.totalGames} color="text-primary" />
                <StatTile icon={Heart} label="Preferiti" value={stats.favoriteGames} color="text-red-400" />
                <StatTile icon={BookOpen} label="Posseduti" value={stats.ownedCount ?? 0} color="text-green-500" />
                <StatTile icon={Trophy} label="Wishlist" value={stats.wishlistCount ?? 0} color="text-amber-500" />
                <StatTile icon={FileText} label="PDF caricati" value={stats.privatePdfs ?? 0} color="text-blue-500" />
                <StatTile
                  icon={Package}
                  label="In prestito"
                  value={stats.inPrestitoCount ?? 0}
                  color="text-orange-400"
                />
              </div>
            )}
          </CardContent>
        </Card>

        {/* Ultime partite */}
        <Card className="border-l-4 border-l-green-400 shadow-sm">
          <CardHeader className="pb-2 flex flex-row items-center justify-between">
            <CardTitle className="font-quicksand text-lg">Ultime partite</CardTitle>
            <Button asChild variant="ghost" size="sm" className="font-nunito gap-1">
              <Link href="/play-records">
                Tutte <ChevronRight className="h-3 w-3" />
              </Link>
            </Button>
          </CardHeader>
          <CardContent>
            {sessionsLoading && (
              <div className="space-y-2">
                {[...Array(3)].map((_, i) => <Skeleton key={i} className="h-10 w-full rounded-lg" />)}
              </div>
            )}
            {!sessionsLoading && sessions.length === 0 && (
              <p className="text-sm text-muted-foreground font-nunito">
                Nessuna partita ancora.{' '}
                <Link href="/sessions" className="underline">Inizia una sessione</Link>
              </p>
            )}
            {!sessionsLoading && sessions.map(s => (
              <div key={s.id} className="flex items-center justify-between py-2 border-b border-border/30 last:border-0">
                <span className="font-nunito text-sm font-medium">{s.gameTitle}</span>
                <span className="text-xs text-muted-foreground font-nunito">
                  {new Date(s.playedAt).toLocaleDateString('it-IT', { day: '2-digit', month: 'short' })}
                </span>
              </div>
            ))}
          </CardContent>
        </Card>

        {/* Quick Actions — invariato */}
        <Card className="border-l-4 border-l-amber-400 shadow-sm">
          <CardHeader className="pb-2">
            <CardTitle className="font-quicksand text-lg">Azioni rapide</CardTitle>
          </CardHeader>
          <CardContent>
            <div className="space-y-2">
              <QuickActionLink href="/profile/achievements" icon={Trophy} label="Achievements" description="Badge e milestone guadagnati" />
              <QuickActionLink href="/library" icon={BookOpen} label="La mia libreria" description="Gestisci la tua collezione" />
              <QuickActionLink href="/play-records" icon={Gamepad2} label="Storia di gioco" description="Tutte le partite giocate" />
              <QuickActionLink href="/sessions" icon={Activity} label="Sessioni attive" description="Partite in corso" />
            </div>
          </CardContent>
        </Card>
      </div>
    );
  }
  ```

  Aggiungi anche il sotto-componente `StatTile` (prima di `OverviewTab`):

  ```tsx
  import { FileText, Package } from 'lucide-react';
  // aggiungili agli import lucide-react esistenti

  function StatTile({
    icon: Icon,
    label,
    value,
    color,
  }: {
    icon: React.ElementType;
    label: string;
    value: number;
    color: string;
  }) {
    return (
      <div className="flex items-center gap-2 p-3 rounded-lg bg-muted/30">
        <Icon className={`h-4 w-4 shrink-0 ${color}`} />
        <div>
          <p className="text-xs text-muted-foreground font-nunito">{label}</p>
          <p className="text-lg font-bold font-quicksand">{value}</p>
        </div>
      </div>
    );
  }
  ```

- [ ] **Step 2: Verifica typecheck**

  ```bash
  cd apps/web && pnpm typecheck
  ```

  Risolvi eventuali errori TypeScript prima di procedere.

- [ ] **Step 3: Commit**

  ```bash
  git add apps/web/src/hooks/useRecentSessions.ts \
           apps/web/src/hooks/__tests__/useRecentSessions.test.ts \
           apps/web/src/app/\(authenticated\)/profile/page.tsx
  git commit -m "feat(profile): expand overview tab with 6 library stats and recent sessions"
  ```

---

## STREAM D — Activity Feed: implementa il "coming soon"

**Problema attuale:** Il tab "Activity" è un placeholder. È il luogo dove il profilo gamer prende vita — le sessioni giocate, gli achievement ottenuti, i giochi aggiunti.

**Target:** Mostrare un feed cronologico delle attività recenti dell'utente.

### Task D1: Crea hook useActivityFeed

**Files:**
- Create: `apps/web/src/hooks/useActivityFeed.ts`
- Create test: `apps/web/src/hooks/__tests__/useActivityFeed.test.ts`

- [ ] **Step 1: Scrivi il test**

  Crea `apps/web/src/hooks/__tests__/useActivityFeed.test.ts`:

  ```ts
  import { renderHook, waitFor } from '@testing-library/react';
  import { vi, describe, it, expect, beforeEach } from 'vitest';

  vi.mock('@/lib/api', () => ({
    api: {
      sessions: { list: vi.fn() },
      gamification: { getRecentAchievements: vi.fn() },
    },
  }));

  import { api } from '@/lib/api';
  import { useActivityFeed } from '../useActivityFeed';

  const mockSessions = [
    { id: 's1', gameTitle: 'Wingspan', playedAt: '2026-03-30T20:00:00Z', playerCount: 3 },
  ];
  const mockAchievements = [
    { id: 'a1', name: 'Prime Ali', earnedAt: '2026-03-29T15:00:00Z', iconUrl: null },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.sessions.list).mockResolvedValue({ items: mockSessions, totalCount: 1 });
    vi.mocked(api.gamification.getRecentAchievements).mockResolvedValue(mockAchievements);
  });

  describe('useActivityFeed', () => {
    it('parte con isLoading true', () => {
      const { result } = renderHook(() => useActivityFeed());
      expect(result.current.isLoading).toBe(true);
    });

    it('ritorna items ordinati per data desc dopo il fetch', async () => {
      const { result } = renderHook(() => useActivityFeed());
      await waitFor(() => expect(result.current.isLoading).toBe(false));
      expect(result.current.items.length).toBeGreaterThan(0);
      // il più recente deve essere primo
      const dates = result.current.items.map(i => new Date(i.timestamp).getTime());
      for (let i = 0; i < dates.length - 1; i++) {
        expect(dates[i]).toBeGreaterThanOrEqual(dates[i + 1]);
      }
    });

    it('assegna type corretto agli item', async () => {
      const { result } = renderHook(() => useActivityFeed());
      await waitFor(() => expect(result.current.isLoading).toBe(false));
      const types = result.current.items.map(i => i.type);
      expect(types).toContain('session');
      expect(types).toContain('achievement');
    });
  });
  ```

- [ ] **Step 2: Esegui — deve FALLIRE**

  ```bash
  cd apps/web && pnpm test src/hooks/__tests__/useActivityFeed.test.ts
  ```

- [ ] **Step 3: Implementa l'hook**

  Prima verifica il metodo per gli achievement recenti:

  ```bash
  grep -r "getRecentAchievements\|RecentAchievement" apps/web/src/lib/api/ --include="*.ts" -l
  ```

  Crea `apps/web/src/hooks/useActivityFeed.ts`:

  ```ts
  import { useEffect, useState } from 'react';

  import { api } from '@/lib/api';

  export type ActivityItemType = 'session' | 'achievement';

  export interface ActivityItem {
    id: string;
    type: ActivityItemType;
    title: string;
    subtitle?: string;
    timestamp: string;
    iconEmoji: string;
  }

  export interface UseActivityFeedResult {
    items: ActivityItem[];
    isLoading: boolean;
    error: string | null;
  }

  export function useActivityFeed(limit = 10): UseActivityFeedResult {
    const [items, setItems] = useState<ActivityItem[]>([]);
    const [isLoading, setIsLoading] = useState(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
      Promise.all([
        api.sessions.list({ pageSize: limit, page: 1 }).catch(() => ({ items: [] })),
        api.gamification.getRecentAchievements().catch(() => []),
      ])
        .then(([sessionsData, achievements]) => {
          const sessionItems: ActivityItem[] = (sessionsData.items ?? []).map(s => ({
            id: `session-${s.id}`,
            type: 'session' as const,
            title: s.gameTitle ?? 'Partita',
            subtitle: `${s.playerCount ?? 0} giocatori`,
            timestamp: s.playedAt,
            iconEmoji: '🎲',
          }));

          const achievementItems: ActivityItem[] = (achievements as Array<{
            id: string;
            name: string;
            earnedAt: string;
          }>).map(a => ({
            id: `achievement-${a.id}`,
            type: 'achievement' as const,
            title: a.name,
            subtitle: 'Achievement sbloccato',
            timestamp: a.earnedAt,
            iconEmoji: '🏆',
          }));

          const all = [...sessionItems, ...achievementItems].sort(
            (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
          );

          setItems(all.slice(0, limit));
        })
        .catch(err => setError(err instanceof Error ? err.message : 'Errore'))
        .finally(() => setIsLoading(false));
    }, [limit]);

    return { items, isLoading, error };
  }
  ```

  **Nota**: adatta i path API (`api.sessions.list`, `api.gamification.getRecentAchievements`) ai metodi effettivi trovati nel codebase.

- [ ] **Step 4: Esegui — deve PASSARE**

  ```bash
  cd apps/web && pnpm test src/hooks/__tests__/useActivityFeed.test.ts
  ```

### Task D2: Crea componente ActivityFeed

**Files:**
- Create: `apps/web/src/components/profile/ActivityFeed.tsx`
- Create test: `apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx`

- [ ] **Step 1: Scrivi il test**

  Crea `apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx`:

  ```tsx
  import { render, screen } from '@testing-library/react';
  import { vi } from 'vitest';

  vi.mock('@/hooks/useActivityFeed', () => ({
    useActivityFeed: vi.fn(),
  }));

  import { useActivityFeed } from '@/hooks/useActivityFeed';
  import { ActivityFeed } from '../ActivityFeed';

  describe('ActivityFeed', () => {
    it('mostra skeleton durante il caricamento', () => {
      vi.mocked(useActivityFeed).mockReturnValue({ items: [], isLoading: true, error: null });
      render(<ActivityFeed />);
      expect(document.querySelectorAll('[data-testid="activity-skeleton"]').length).toBeGreaterThan(0);
    });

    it('mostra gli item quando caricati', () => {
      vi.mocked(useActivityFeed).mockReturnValue({
        items: [
          { id: '1', type: 'session', title: 'Wingspan', subtitle: '3 giocatori', timestamp: '2026-03-30T20:00:00Z', iconEmoji: '🎲' },
          { id: '2', type: 'achievement', title: 'Prime Ali', subtitle: 'Achievement sbloccato', timestamp: '2026-03-29T15:00:00Z', iconEmoji: '🏆' },
        ],
        isLoading: false,
        error: null,
      });
      render(<ActivityFeed />);
      expect(screen.getByText('Wingspan')).toBeInTheDocument();
      expect(screen.getByText('Prime Ali')).toBeInTheDocument();
    });

    it('mostra stato vuoto se non ci sono attività', () => {
      vi.mocked(useActivityFeed).mockReturnValue({ items: [], isLoading: false, error: null });
      render(<ActivityFeed />);
      expect(screen.getByText(/nessuna attività/i)).toBeInTheDocument();
    });
  });
  ```

- [ ] **Step 2: Esegui — deve FALLIRE**

  ```bash
  cd apps/web && pnpm test src/components/profile/__tests__/ActivityFeed.test.tsx
  ```

- [ ] **Step 3: Implementa il componente**

  Crea `apps/web/src/components/profile/ActivityFeed.tsx`:

  ```tsx
  import { Skeleton } from '@/components/ui/feedback/skeleton';
  import { useActivityFeed } from '@/hooks/useActivityFeed';

  function formatRelativeDate(dateStr: string): string {
    const date = new Date(dateStr);
    const now = new Date();
    const diffDays = Math.floor((now.getTime() - date.getTime()) / (1000 * 60 * 60 * 24));
    if (diffDays === 0) return 'Oggi';
    if (diffDays === 1) return 'Ieri';
    if (diffDays < 7) return `${diffDays} giorni fa`;
    return date.toLocaleDateString('it-IT', { day: '2-digit', month: 'short' });
  }

  export function ActivityFeed() {
    const { items, isLoading, error } = useActivityFeed(15);

    if (isLoading) {
      return (
        <div className="space-y-3">
          {[...Array(5)].map((_, i) => (
            <Skeleton key={i} className="h-14 w-full rounded-xl" data-testid="activity-skeleton" />
          ))}
        </div>
      );
    }

    if (error) {
      return (
        <p className="text-sm text-destructive font-nunito">
          Errore nel caricamento delle attività.
        </p>
      );
    }

    if (items.length === 0) {
      return (
        <div className="py-12 text-center">
          <p className="text-2xl mb-2">🎲</p>
          <p className="font-medium font-quicksand mb-1">Nessuna attività ancora</p>
          <p className="text-sm text-muted-foreground font-nunito">
            Le tue partite e gli achievement guadagnati appariranno qui.
          </p>
        </div>
      );
    }

    return (
      <div className="space-y-2">
        {items.map(item => (
          <div
            key={item.id}
            className="flex items-center gap-3 p-3 rounded-xl border border-border/40 hover:bg-muted/30 transition-colors"
          >
            <span className="text-xl shrink-0" aria-hidden="true">
              {item.iconEmoji}
            </span>
            <div className="flex-1 min-w-0">
              <p className="font-medium text-sm font-nunito truncate">{item.title}</p>
              {item.subtitle && (
                <p className="text-xs text-muted-foreground font-nunito">{item.subtitle}</p>
              )}
            </div>
            <span className="text-xs text-muted-foreground font-nunito shrink-0">
              {formatRelativeDate(item.timestamp)}
            </span>
          </div>
        ))}
      </div>
    );
  }
  ```

- [ ] **Step 4: Esegui — deve PASSARE**

  ```bash
  cd apps/web && pnpm test src/components/profile/__tests__/ActivityFeed.test.tsx
  ```

### Task D3: Sostituisci "coming soon" con ActivityFeed nel profilo

**Files:**
- Modify: `apps/web/src/app/(authenticated)/profile/page.tsx`

- [ ] **Step 1: Sostituisci `ActivityTab`**

  Nel file `profile/page.tsx`, sostituisci l'intera funzione `ActivityTab`:

  ```tsx
  import { ActivityFeed } from '@/components/profile/ActivityFeed';
  // aggiungi in cima al file

  function ActivityTab() {
    return (
      <div className="space-y-4">
        <div className="flex items-center justify-between">
          <p className="text-sm text-muted-foreground font-nunito">
            Le tue ultime partite, achievement e aggiornamenti alla libreria
          </p>
        </div>
        <ActivityFeed />
      </div>
    );
  }
  ```

- [ ] **Step 2: Verifica typecheck e test**

  ```bash
  cd apps/web && pnpm typecheck && pnpm test
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add apps/web/src/hooks/useActivityFeed.ts \
           apps/web/src/hooks/__tests__/useActivityFeed.test.ts \
           apps/web/src/components/profile/ActivityFeed.tsx \
           apps/web/src/components/profile/__tests__/ActivityFeed.test.tsx \
           apps/web/src/app/\(authenticated\)/profile/page.tsx
  git commit -m "feat(profile): implement activity feed replacing coming-soon placeholder"
  ```

---

## STREAM E — Flywheel Analytics: strumentazione eventi chiave

**Problema attuale:** `trackEvent()` esiste ma è un no-op (console.log in dev, nulla in prod). Non sappiamo dove gli utenti si fermano nel flywheel.

**Target:** Definire i 6 eventi chiave del flywheel, instrumentare il frontend, e aggiungere una produzione-ready analytics call (struttura predisposta per qualunque provider).

**Il flywheel MeepleAI:**
```
1. sign_up          — utente si registra
2. game_added       — utente aggiunge il primo gioco
3. pdf_uploaded     — utente carica il primo rulebook
4. rules_chat_started — utente apre una chat di regole
5. session_created  — utente crea la prima sessione
6. session_shared   — utente condivide/invita un amico
```

### Task E1: Definisci e testa i flywheel events

**Files:**
- Create: `apps/web/src/lib/analytics/flywheel-events.ts`
- Create test: `apps/web/src/lib/analytics/__tests__/flywheel-events.test.ts`

- [ ] **Step 1: Scrivi il test**

  Crea `apps/web/src/lib/analytics/__tests__/flywheel-events.test.ts`:

  ```ts
  import { vi, describe, it, expect, beforeEach } from 'vitest';

  vi.mock('@/lib/analytics/track-event', () => ({
    trackEvent: vi.fn(),
  }));

  import { trackEvent } from '@/lib/analytics/track-event';
  import {
    trackSignUp,
    trackGameAdded,
    trackPdfUploaded,
    trackRulesChatStarted,
    trackSessionCreated,
    trackSessionShared,
  } from '../flywheel-events';

  beforeEach(() => vi.clearAllMocks());

  describe('flywheel events', () => {
    it('trackSignUp chiama trackEvent con flywheel_sign_up', () => {
      trackSignUp({ method: 'email' });
      expect(trackEvent).toHaveBeenCalledWith('flywheel_sign_up', { method: 'email' });
    });

    it('trackGameAdded chiama trackEvent con flywheel_game_added', () => {
      trackGameAdded({ gameId: 'g1', source: 'catalog' });
      expect(trackEvent).toHaveBeenCalledWith('flywheel_game_added', { gameId: 'g1', source: 'catalog' });
    });

    it('trackPdfUploaded chiama trackEvent con flywheel_pdf_uploaded', () => {
      trackPdfUploaded({ gameId: 'g1', fileSizeKb: 1200 });
      expect(trackEvent).toHaveBeenCalledWith('flywheel_pdf_uploaded', { gameId: 'g1', fileSizeKb: 1200 });
    });

    it('trackRulesChatStarted chiama trackEvent con flywheel_rules_chat_started', () => {
      trackRulesChatStarted({ gameId: 'g1', promptType: 'rule_dispute' });
      expect(trackEvent).toHaveBeenCalledWith('flywheel_rules_chat_started', { gameId: 'g1', promptType: 'rule_dispute' });
    });

    it('trackSessionCreated chiama trackEvent con flywheel_session_created', () => {
      trackSessionCreated({ gameId: 'g1', playerCount: 4 });
      expect(trackEvent).toHaveBeenCalledWith('flywheel_session_created', { gameId: 'g1', playerCount: 4 });
    });

    it('trackSessionShared chiama trackEvent con flywheel_session_shared', () => {
      trackSessionShared({ sessionId: 's1', method: 'invite_code' });
      expect(trackEvent).toHaveBeenCalledWith('flywheel_session_shared', { sessionId: 's1', method: 'invite_code' });
    });
  });
  ```

- [ ] **Step 2: Esegui — deve FALLIRE**

  ```bash
  cd apps/web && pnpm test src/lib/analytics/__tests__/flywheel-events.test.ts
  ```

- [ ] **Step 3: Implementa flywheel-events.ts**

  Crea `apps/web/src/lib/analytics/flywheel-events.ts`:

  ```ts
  import { trackEvent } from './track-event';

  export function trackSignUp(props: { method: 'email' | 'google' | 'github' }): void {
    trackEvent('flywheel_sign_up', props);
  }

  export function trackGameAdded(props: { gameId: string; source: 'catalog' | 'private' | 'bgg' }): void {
    trackEvent('flywheel_game_added', props);
  }

  export function trackPdfUploaded(props: { gameId: string; fileSizeKb?: number }): void {
    trackEvent('flywheel_pdf_uploaded', props);
  }

  export function trackRulesChatStarted(props: {
    gameId?: string;
    promptType: 'rule_dispute' | 'setup' | 'general' | 'suggestion';
  }): void {
    trackEvent('flywheel_rules_chat_started', props);
  }

  export function trackSessionCreated(props: { gameId: string; playerCount: number }): void {
    trackEvent('flywheel_session_created', props);
  }

  export function trackSessionShared(props: { sessionId: string; method: 'invite_code' | 'link' }): void {
    trackEvent('flywheel_session_shared', props);
  }
  ```

- [ ] **Step 4: Esegui — deve PASSARE**

  ```bash
  cd apps/web && pnpm test src/lib/analytics/__tests__/flywheel-events.test.ts
  ```

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/lib/analytics/flywheel-events.ts \
           apps/web/src/lib/analytics/__tests__/flywheel-events.test.ts
  git commit -m "feat(analytics): define flywheel event tracking functions"
  ```

### Task E2: Wira gli eventi nei punti chiave del frontend

**Files:** (da individuare nei passi seguenti)

> Per ogni evento, il pattern è: trova dove avviene l'azione → aggiungi la chiamata dopo il successo (non prima).

- [ ] **Step 1: Wira `trackSignUp` nella registrazione**

  ```bash
  # Trova il componente di registrazione
  grep -r "register\|Register" apps/web/src/app/\(auth\)/ --include="*.tsx" -l
  ```

  Nel componente trovato, dopo il successo della chiamata API di registrazione:

  ```tsx
  import { trackSignUp } from '@/lib/analytics/flywheel-events';

  // Dopo await api.auth.register(...)
  trackSignUp({ method: 'email' }); // o 'google' se OAuth
  ```

- [ ] **Step 2: Wira `trackGameAdded`**

  ```bash
  # Trova dove viene chiamato AddGameToLibrary
  grep -r "addToLibrary\|AddGameToLibrary" apps/web/src/ --include="*.tsx" -l | head -5
  ```

  Nel componente trovato, dopo il successo:

  ```tsx
  import { trackGameAdded } from '@/lib/analytics/flywheel-events';

  // Dopo il successo di addToLibrary
  trackGameAdded({ gameId: game.id, source: 'catalog' });
  ```

- [ ] **Step 3: Wira `trackPdfUploaded`**

  ```bash
  # Trova dove viene invocato l'upload PDF
  grep -r "uploadPdf\|UploadPdf\|upload.*pdf" apps/web/src/ --include="*.tsx" -l | head -5
  ```

  Nel componente trovato, dopo il successo:

  ```tsx
  import { trackPdfUploaded } from '@/lib/analytics/flywheel-events';

  trackPdfUploaded({ gameId: currentGameId });
  ```

- [ ] **Step 4: Wira `trackRulesChatStarted` nella nuova chat**

  Nel file `apps/web/src/app/(chat)/chat/new/page.tsx`, dopo che la chat è stata creata con successo:

  ```tsx
  import { trackRulesChatStarted } from '@/lib/analytics/flywheel-events';

  // Dopo la creazione del thread
  trackRulesChatStarted({ gameId: selectedGameId ?? undefined, promptType: 'general' });
  ```

- [ ] **Step 5: Wira `trackSessionCreated`**

  ```bash
  grep -r "createSession\|CreateSession" apps/web/src/ --include="*.tsx" -l | head -5
  ```

  Nel componente trovato:

  ```tsx
  import { trackSessionCreated } from '@/lib/analytics/flywheel-events';

  trackSessionCreated({ gameId: session.gameId, playerCount: session.playerCount });
  ```

- [ ] **Step 6: Verifica build**

  ```bash
  cd apps/web && pnpm build
  ```

  Atteso: build senza errori TS.

- [ ] **Step 7: Commit finale**

  ```bash
  git add -p  # aggiungi solo i file con wiring eventi
  git commit -m "feat(analytics): wire flywheel events across registration, library, pdf upload, chat and sessions"
  ```

---

## Checklist finale

Prima di considerare ogni stream completo:

- [ ] `pnpm test` — tutti i test passano
- [ ] `pnpm typecheck` — nessun errore TypeScript
- [ ] `pnpm lint` — nessun warning critico
- [ ] Verifica visiva in dev (`pnpm dev`) per ogni stream UI

## Ordine consigliato

| Ordine | Stream | Impatto | Effort |
|--------|--------|---------|--------|
| 1 | A — Landing reframe | Alto (pitch utente) | Basso |
| 2 | B — Gateway CTA | Alto (conversione) | Medio |
| 3 | C — Profile stats | Medio (retention) | Medio |
| 4 | D — Activity feed | Medio (identità) | Medio-Alto |
| 5 | E — Flywheel events | Strategico (misura) | Medio |
