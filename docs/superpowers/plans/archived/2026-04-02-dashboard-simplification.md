# Dashboard Simplification & Improvement Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Semplificare e migliorare la dashboard utente (`/dashboard`) rimuovendo duplicazioni, sostituendo contenuti fake, correggendo accessibilità e aggiungendo error handling visibile.

**Architecture:** Il `DashboardClient` (desktop) è un bento-grid a 12 colonne con sidebar embedded ridondante. Il piano rimuove la sidebar interna, sostituisce il `ChatPreviewWidget` fake con un widget di azioni rapide reale, corregge font sizes e colori con design tokens esistenti, e avvolge il grid in un `ErrorBoundary` per superfici gli errori già tracciati nello store.

**Tech Stack:** Next.js 15 App Router, React 19, Tailwind CSS 4, Zustand, `@/components/errors/ErrorBoundary`, `@/hooks/useAuthUser`, `next/image`

---

## File Map

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx` | Modify | Rimozione sidebar, fix font/colori/img, ErrorBoundary, widget greeting, QuickActionsWidget |
| `apps/web/src/lib/stores/dashboard-store.ts` | No changes | Store già ha error states — nessuna modifica necessaria |

> Tutti i cambiamenti sono nel solo file `dashboard-client.tsx`. Non servono nuovi file.

---

## Task 1: Rimuovere `BentoDashboardSidebar` (C1)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

La `BentoDashboardSidebar` (righe 563–629) duplica la navigazione del `UnifiedShell` già presente nel layout. Va rimossa completamente, insieme ai suoi dati (`SIDEBAR_NAV`, `SIDEBAR_MANAGE`).

- [ ] **Step 1: Aprire il file e identificare le sezioni da rimuovere**

  Le seguenti sezioni vanno eliminate:
  - Righe 565–572: costante `SIDEBAR_NAV`
  - Righe 574–577: costante `SIDEBAR_MANAGE`
  - Righe 579–629: funzione `BentoDashboardSidebar`
  - Riga 667: `<BentoDashboardSidebar />` nel JSX di `DashboardClient`

- [ ] **Step 2: Rimuovere SIDEBAR_NAV, SIDEBAR_MANAGE, BentoDashboardSidebar**

  Eliminare dal file le righe 563–629 (tutto il blocco `// ─── Bento Sidebar`).

- [ ] **Step 3: Rimuovere il render di BentoDashboardSidebar e rimuovere import `usePathname`**

  Nel body di `DashboardClient`, la riga:
  ```tsx
  <BentoDashboardSidebar />
  ```
  va rimossa.

  Rimuovere anche `usePathname` dall'import `next/navigation` (non più usato).

  Il contenitore `flex h-full` diventa:
  ```tsx
  return (
    <div className="flex-1 overflow-y-auto p-3.5">
      <div
        className="grid grid-cols-6 lg:grid-cols-12"
        style={{ gridAutoRows: '60px', gap: '8px' }}
      >
        {/* ... widgets ... */}
      </div>
    </div>
  );
  ```

- [ ] **Step 4: Build check**

  ```bash
  cd apps/web && pnpm typecheck 2>&1 | tail -20
  ```
  Expected: 0 errors.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "refactor(dashboard): remove redundant BentoDashboardSidebar (duplicates UnifiedShell nav)"
  ```

---

## Task 2: Fix font sizes (I1) + sostituire `<img>` con `<Image>` (I3) + aggiungere `data-testid` (I5)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

### 2a — Font sizes

Tutte le classi `text-[8px]` e `text-[9px]` vanno portate a `text-[11px]` minimo. Le `text-[10px]` diventano `text-[11px]`. Le `text-[11px]` restano.

- [ ] **Step 1: Sostituire font sizes sotto-soglia**

  Nel file, effettuare queste sostituzioni (replace-all per ciascuna):

  - `text-[8px]` → `text-[11px]` (colpisce: game title in TrendingWidget, font-mono in WidgetLabel reference)
  - `text-[9px]` → `text-[11px]` (colpisce: WidgetLabel, badge live session, rating star in LibraryWidget, leaderboard wins)
  - Le `text-[10px]` → `text-[11px]` (colpisce: "Vedi tutti N →", sub in KpiWidget)

  > Nota: `WidgetLabel` usa `text-[9px]` — dopo il replace diventa `text-[11px]`. L'aspetto cambierà leggermente ma sarà accessibile.

### 2b — `<img>` → `<Image>`

- [ ] **Step 2: Aggiungere import `Image` da `next/image`**

  Aggiungere in cima al file (dopo `import Link from 'next/link'`):
  ```tsx
  import Image from 'next/image';
  ```

- [ ] **Step 3: Sostituire `<img>` in `LiveSessionWidget`**

  Sostituire (righe ~210-215):
  ```tsx
  // eslint-disable-next-line @next/next/no-img-element
  <img
    src={session.gameImageUrl}
    alt={session.gameName}
    className="w-full h-full object-cover"
  />
  ```
  Con:
  ```tsx
  <Image
    src={session.gameImageUrl}
    alt={session.gameName}
    width={44}
    height={44}
    className="w-full h-full object-cover"
    unoptimized
  />
  ```

- [ ] **Step 4: Sostituire `<img>` in `LibraryWidget`**

  Sostituire (righe ~347-352):
  ```tsx
  // eslint-disable-next-line @next/next/no-img-element
  <img
    src={game.thumbnailUrl ?? game.imageUrl ?? ''}
    alt={game.title}
    className="w-7 h-7 rounded-md object-cover shrink-0"
  />
  ```
  Con:
  ```tsx
  <Image
    src={game.thumbnailUrl ?? game.imageUrl ?? ''}
    alt={game.title}
    width={28}
    height={28}
    className="w-7 h-7 rounded-md object-cover shrink-0"
    unoptimized
  />
  ```

- [ ] **Step 5: Sostituire `<img>` in `TrendingWidget`**

  Sostituire (righe ~540-545):
  ```tsx
  // eslint-disable-next-line @next/next/no-img-element
  <img
    src={game.thumbnailUrl}
    alt={game.title}
    className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all"
  />
  ```
  Con:
  ```tsx
  <Image
    src={game.thumbnailUrl}
    alt={game.title}
    width={36}
    height={48}
    className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all"
    unoptimized
  />
  ```

### 2c — data-testid

- [ ] **Step 6: Aggiungere `data-testid` ai widget principali nel JSX di `DashboardClient`**

  Modificare il JSX in `DashboardClient` per aggiungere prop `data-testid` ai componenti wrapper `BentoWidget` di ciascun widget. Dato che `BentoWidget` non accetta `data-testid`, aggiungerlo tramite un `div` wrapper dove necessario, OPPURE aggiungendo `data-testid` direttamente alla prop `className` `<div>` dentro i widget stessi.

  Il modo più semplice: aggiungere `data-testid` come prop a `BentoWidget` e propagarla al `<div>`:

  In `BentoWidgetProps` aggiungere:
  ```tsx
  'data-testid'?: string;
  ```

  In `BentoWidget`, aggiungere al `<div>`:
  ```tsx
  data-testid={props['data-testid']}
  ```

  Nei widget, aggiungere `data-testid`:
  - `LiveSessionWidget` → `data-testid="widget-live-session"`
  - `KpiWidget` (Partite) → `data-testid="widget-kpi-plays"`
  - `LibraryWidget` → `data-testid="widget-library"`
  - `ChatPreviewWidget` (o il suo sostituto) → `data-testid="widget-quick-actions"`
  - `LeaderboardWidget` → `data-testid="widget-leaderboard"`
  - `TrendingWidget` → `data-testid="widget-trending"`

- [ ] **Step 7: Build + typecheck**

  ```bash
  cd apps/web && pnpm typecheck 2>&1 | tail -20
  ```
  Expected: 0 errors.

- [ ] **Step 8: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "fix(dashboard): font sizes ≥11px, use next/image, add data-testid on widgets"
  ```

---

## Task 3: Migrare colori a CSS variables del design system (I2)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Il design system (`premium-gaming.css`) espone:
- `--gaming-accent-success: #22c55e` (verde, usato per live session)
- `--gaming-accent-info: #3b82f6` (blu, usato per chat)
- `--gaming-accent-ai: #8b5cf6` (viola, usato per player)
- `--gaming-bg-glass: rgba(255,255,255,0.05)`

Per i colori non presenti come variabili CSS (orange game, blue session, red event, amber agent), è corretto mantenere le costanti `C` ma dichiararle come rimando semantico. Sostituire dove i token esistono.

- [ ] **Step 1: Sostituire `C.success` con `var(--gaming-accent-success)`**

  La costante `C.success = 'hsl(142,70%,45%)'` corrisponde a `--gaming-accent-success`. Sostituire tutte le occorrenze di `C.success` con `'var(--gaming-accent-success)'` (come stringa).

  Sostituzioni nel file:
  - `accentColor={C.success}` → `accentColor="var(--gaming-accent-success)"`
  - `style={{ background: 'rgba(16,185,129,0.12)', color: C.success, border: '1px solid rgba(16,185,129,0.2)' }}`
    → `style={{ background: 'color-mix(in srgb, var(--gaming-accent-success) 12%, transparent)', color: 'var(--gaming-accent-success)', border: '1px solid color-mix(in srgb, var(--gaming-accent-success) 20%, transparent)' }}`
  - `style={{ background: 'rgba(16,185,129,0.04)' }}`
    → `style={{ background: 'color-mix(in srgb, var(--gaming-accent-success) 4%, transparent)' }}`
  - `style={{ background: C.success }}`
    → `style={{ background: 'var(--gaming-accent-success)' }}`

- [ ] **Step 2: Sostituire `C.chat` con `var(--gaming-accent-info)`**

  `C.chat = 'hsl(220,80%,55%)'` ≈ `#3b82f6` = `--gaming-accent-info`.

  Sostituire tutte le occorrenze `C.chat` → `'var(--gaming-accent-info)'` come stringa nelle style props.

- [ ] **Step 3: Rimuovere `C.success` e `C.chat` dalla costante `C`**

  Dopo le sostituzioni, rimuovere dall'oggetto `C`:
  ```ts
  // Rimuovere:
  success: 'hsl(142,70%,45%)',
  chat: 'hsl(220,80%,55%)',
  ```

- [ ] **Step 4: Build check**

  ```bash
  cd apps/web && pnpm typecheck 2>&1 | tail -20
  ```
  Expected: 0 errors.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "refactor(dashboard): use CSS design tokens for success/chat accent colors"
  ```

---

## Task 4: Sostituire `ChatPreviewWidget` fake con `QuickActionsWidget` (C2)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Il `ChatPreviewWidget` mostra una conversazione hardcoded non reale. Va sostituito con un widget di **azioni rapide** che mostri 3 CTA: Nuova Partita, Chat AI, Aggiungi Gioco. Mantiene le stesse dimensioni bento (6×4) e posizione nel grid.

- [ ] **Step 1: Rimuovere `ChatPreviewWidget` (righe ~383-453)**

  Eliminare l'intera funzione `ChatPreviewWidget`.

- [ ] **Step 2: Aggiungere `QuickActionsWidget`**

  Inserire la seguente funzione al suo posto:

  ```tsx
  // ─── Quick Actions Widget (6×4) ──────────────────────────────────────────────

  const QUICK_ACTIONS = [
    {
      icon: '🎲',
      label: 'Nuova Partita',
      sub: 'Registra una sessione',
      href: '/sessions/new',
      color: C.game,
    },
    {
      icon: '💬',
      label: 'Chat AI',
      sub: 'Fai domande sulle regole',
      href: '/chat',
      color: 'var(--gaming-accent-info)',
    },
    {
      icon: '📚',
      label: 'Aggiungi Gioco',
      sub: 'Cerca nel catalogo',
      href: '/library?tab=collection',
      color: C.kb,
    },
    {
      icon: '📊',
      label: 'Storico',
      sub: 'Vedi tutte le partite',
      href: '/sessions',
      color: C.session,
    },
  ] as const;

  function QuickActionsWidget() {
    const router = useRouter();
    return (
      <BentoWidget
        colSpan={6}
        rowSpan={4}
        className="flex flex-col"
        data-testid="widget-quick-actions"
      >
        <WidgetLabel>Azioni Rapide</WidgetLabel>
        <div className="flex-1 grid grid-cols-2 gap-2 mt-1">
          {QUICK_ACTIONS.map(action => (
            <button
              key={action.href}
              type="button"
              onClick={() => router.push(action.href)}
              className="flex flex-col items-start gap-1 rounded-xl p-3 border border-border/40 hover:border-border hover:bg-muted/20 transition-colors text-left"
              style={{ borderLeft: `3px solid ${action.color}` }}
            >
              <span className="text-xl">{action.icon}</span>
              <span
                className="font-quicksand font-bold text-[13px] leading-tight"
                style={{ color: action.color }}
              >
                {action.label}
              </span>
              <span className="text-[11px] text-muted-foreground leading-tight">
                {action.sub}
              </span>
            </button>
          ))}
        </div>
      </BentoWidget>
    );
  }
  ```

- [ ] **Step 3: Aggiornare il render in `DashboardClient`**

  Sostituire il render di `<ChatPreviewWidget />` con `<QuickActionsWidget />`.

- [ ] **Step 4: Build check**

  ```bash
  cd apps/web && pnpm typecheck 2>&1 | tail -20
  ```
  Expected: 0 errors.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "feat(dashboard): replace fake ChatPreviewWidget with real QuickActionsWidget"
  ```

---

## Task 5: Fix `LeaderboardWidget` — aggiungere etichetta periodo (I4)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Il widget mostra "Classifica Gruppo" ma calcola dai soli ultimi 8 sessions caricati. Va aggiunta un'etichetta che chiarisce il periodo ("ultime sessioni").

- [ ] **Step 1: Aggiornare `WidgetLabel` in `LeaderboardWidget`**

  In `LeaderboardWidget`, cambiare:
  ```tsx
  <WidgetLabel>Classifica Gruppo</WidgetLabel>
  ```
  Con:
  ```tsx
  <div className="flex items-center justify-between mb-1.5">
    <WidgetLabel>Classifica Gruppo</WidgetLabel>
    <span className="text-[10px] text-muted-foreground/60 font-mono">
      ultime partite
    </span>
  </div>
  ```

  > Rimuovere il `mb-1.5` da `WidgetLabel` perché ora è dentro il flex container.
  > In alternativa, rimuovere `className` override da `WidgetLabel` nel div wrapper tenendo il mb nella definizione originale.

  Versione semplificata senza modificare `WidgetLabel`:
  ```tsx
  <WidgetLabel>Classifica Gruppo</WidgetLabel>
  <p className="text-[10px] text-muted-foreground/60 font-mono -mt-1 mb-1.5">
    basata sulle ultime partite
  </p>
  ```

- [ ] **Step 2: Aggiornare `TrendingWidget` — chiarire fonte**

  In `TrendingWidget`, cambiare:
  ```tsx
  <WidgetLabel>Popolari questa settimana</WidgetLabel>
  ```
  Con:
  ```tsx
  <WidgetLabel>Popolari su MeepleAI · 7 giorni</WidgetLabel>
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "fix(dashboard): clarify leaderboard period and trending data source"
  ```

---

## Task 6: Aggiungere `ErrorBoundary` + visualizzare errori store (C3)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Lo store Zustand già traccia `statsError`, `sessionsError`, `gamesError`, `trendingError`. Questi non vengono mai mostrati all'utente. Aggiungere:
1. `ErrorBoundary` attorno al grid
2. Banner di errore inline nei widget che falliscono

- [ ] **Step 1: Aggiungere import `ErrorBoundary`**

  In cima al file, aggiungere:
  ```tsx
  import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
  ```

- [ ] **Step 2: Avvolgere il grid in `ErrorBoundary` in `DashboardClient`**

  Nel return di `DashboardClient`, avvolgere il contenuto principale:
  ```tsx
  return (
    <ErrorBoundary componentName="DashboardClient">
      <div className="flex-1 overflow-y-auto p-3.5">
        <div
          className="grid grid-cols-6 lg:grid-cols-12"
          style={{ gridAutoRows: '60px', gap: '8px' }}
        >
          {/* widgets */}
        </div>
      </div>
    </ErrorBoundary>
  );
  ```

- [ ] **Step 3: Mostrare errore in `LiveSessionWidget` quando `sessionsError`**

  Aggiungere `sessionsError` ai props estratti dallo store e passarlo a `LiveSessionWidget`:

  Nel `DashboardClient` destructuring aggiungere `sessionsError`.

  In `LiveSessionWidget` aggiungere prop `error?: string | null` e gestire:
  ```tsx
  if (error && !isLoading) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="flex items-center gap-3 border-dashed">
        <span className="text-lg">⚠️</span>
        <div>
          <p className="font-quicksand font-bold text-[13px]">Sessioni non disponibili</p>
          <p className="text-[11px] text-muted-foreground">Riprova tra poco</p>
        </div>
      </BentoWidget>
    );
  }
  ```

- [ ] **Step 4: Build check**

  ```bash
  cd apps/web && pnpm typecheck 2>&1 | tail -20
  ```
  Expected: 0 errors.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "feat(dashboard): add ErrorBoundary and surface sessionsError in LiveSessionWidget"
  ```

---

## Task 7: Aggiungere widget greeting personalizzato (N1)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/dashboard-client.tsx`

Aggiungere un widget di benvenuto personalizzato come prima riga del bento grid (span 12×1). Usa `useAuthUser` per il nome utente.

- [ ] **Step 1: Aggiungere import `useAuthUser`**

  ```tsx
  import { useAuthUser } from '@/hooks/useAuthUser';
  ```

- [ ] **Step 2: Aggiungere `GreetingWidget`**

  ```tsx
  // ─── Greeting Widget (12×1) ───────────────────────────────────────────────────

  function GreetingWidget({ displayName }: { displayName?: string | null }) {
    const hour = new Date().getHours();
    const greeting =
      hour < 12 ? 'Buongiorno' : hour < 18 ? 'Buon pomeriggio' : 'Buonasera';
    const name = displayName?.split(' ')[0] ?? 'Meepler';

    return (
      <BentoWidget
        colSpan={12}
        tabletColSpan={6}
        rowSpan={1}
        className="flex items-center justify-between"
        data-testid="widget-greeting"
      >
        <p className="font-quicksand font-bold text-[15px] text-foreground">
          {greeting}, <span style={{ color: C.game }}>{name}</span> 👋
        </p>
        <p className="text-[11px] text-muted-foreground hidden lg:block">
          {new Date().toLocaleDateString('it-IT', { weekday: 'long', day: 'numeric', month: 'long' })}
        </p>
      </BentoWidget>
    );
  }
  ```

- [ ] **Step 3: Usare `useAuthUser` in `DashboardClient`**

  Aggiungere nel body di `DashboardClient`:
  ```tsx
  const { user } = useAuthUser();
  ```

- [ ] **Step 4: Rendere `GreetingWidget` come prima riga del grid**

  Aggiungere come primo elemento nel grid:
  ```tsx
  {/* Row 0: Greeting (12×1) */}
  <GreetingWidget displayName={user?.displayName} />
  ```

- [ ] **Step 5: Build check**

  ```bash
  cd apps/web && pnpm typecheck 2>&1 | tail -20
  ```
  Expected: 0 errors.

- [ ] **Step 6: Commit**

  ```bash
  git add apps/web/src/app/\(authenticated\)/dashboard/dashboard-client.tsx
  git commit -m "feat(dashboard): add personalized greeting widget with user name and date"
  ```

---

## Self-Review

### Spec coverage

| Issue | Task | Coperto? |
|-------|------|----------|
| C1 - Rimuovere sidebar ridondante | Task 1 | ✅ |
| C2 - Sostituire ChatPreview fake | Task 4 | ✅ |
| C3 - Error handling + ErrorBoundary | Task 6 | ✅ |
| I1 - Font sizes ≥ 11px | Task 2a | ✅ |
| I2 - Design tokens per colori | Task 3 | ✅ (success + chat) |
| I3 - next/image invece di img | Task 2b | ✅ |
| I4 - Leaderboard periodo + trending fonte | Task 5 | ✅ |
| I5 - data-testid sui widget | Task 2c | ✅ |
| N1 - Greeting personalizzato | Task 7 | ✅ |
| N3 - KPI tooltip periodo | — | ❌ (out of scope, KPI label "Partite (mese)" già descrive il periodo) |
| N4 - Trending fonte badge | Task 5 | ✅ (aggiornato label) |

### Placeholder scan

- Nessun "TBD" o "TODO" nei task
- Ogni step contiene il codice esatto
- I nomi di funzioni sono consistenti (es. `QuickActionsWidget` usato sia in task 4 che nel render)

### Type consistency

- `BentoWidgetProps` esteso con `'data-testid'?: string` in Task 2c, usato da Task 4 e 7
- `LiveSessionWidget` riceve `error?: string | null` aggiunto in Task 6
- `GreetingWidget` riceve `displayName?: string | null` da `AuthUser.displayName` (tipo `string | null | undefined`)

---

**Piano completo.**
