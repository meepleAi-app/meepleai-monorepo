# SP5 Admin F1 — Pilota A1 Overview re-skin — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Validare il workflow di re-skin mock-→-componente applicandolo al **pilota A1 Overview** (`/admin/overview`) sopra la shell consolidata della fase Fondamenta (F0a/b/c già in `main-dev`). Allineare i 3 pattern-chiave del mockup A1 (`admin-mockups/design_handoff_admin/admin/sp5-admin-overview.html`): KPI row a 4 colonne, layout 2-col `[main | quick-actions sidebar 280px]`, topbar con refresh + crumbs. Pixel-perfect e charts row sono **fuori scope** del pilota (vengono nelle ondate F2-F6).

**Architecture:** F1 lavora *sopra* la pagina esistente (`apps/web/src/app/admin/(dashboard)/overview/page.tsx`) e i suoi sotto-componenti, senza riscrivere da zero. Il pilota valida che il workflow shell-F0+token-canonici-dark-scoped sia sufficiente per applicare il look-and-feel SP5 a una pagina esistente con modifiche mirate (layout grid + topbar + KPI count) — è esattamente questo che la spec §9 chiede al pilota.

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · Vitest + React Testing Library · Tailwind. Nessuna nuova dipendenza.

**Depends on:** F0a (nav config + filtro) · F0b (shell sidebar-responsive + AdminNavList) · F0c (dark scoped admin) — tutti in `main-dev`.

**Spec di riferimento:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §5 (A1 → `/admin/overview`, già `✅ full`), §9 (rollout F1 dopo F0).

**Mockup di riferimento:** `admin-mockups/design_handoff_admin/admin/sp5-admin-overview.html` (header "Overview sistema" + crumbs + refresh + theme toggle · alert-banner warning · `.overview-grid` 1fr+280px · `.kpi-row` 4-up · `.feed-row` 2-up · `.charts-row` 4-up · `.quick-actions` vertical list in sidebar).

**Non-goals di F1 (rimandati alle ondate):**
- ❌ Charts row 4-up (richiede dati metric/series non ancora disponibili in overview)
- ❌ Activity-feed + alerts-feed row 2-up (richiede SSE live events `events/live`, gap §5)
- ❌ Mobile fallback ("Console solo desktop") — fuori scope, vedi spec Non-goals §2
- ❌ Pixel-perfect parità col mockup (il pilota valida il workflow, non il pixel)

---

## File Structure

- **Modify** `apps/web/src/app/admin/(dashboard)/overview/page.tsx` — sostituisce il flow lineare con il 2-col grid `[main | quick-actions sidebar]`; aggiunge la topbar (h1 + crumbs + refresh button).
- **Modify** `apps/web/src/app/admin/(dashboard)/overview/KPIStatsRow.tsx` — passa da 3 a 4 colonne (aggiunge il 4° KPI dai dati esistenti, p.es. `recentSubmissions`).
- **Modify** `apps/web/src/app/admin/(dashboard)/overview/QuickActionsGrid.tsx` — variante "vertical sidebar" attiva via prop (rendering a lista verticale per la sidebar; il rendering esistente a griglia resta default per altre pagine).
- **Modify/Create** test in `apps/web/src/app/admin/(dashboard)/overview/__tests__/` — un test per task (vedi sotto), niente regressioni sui test esistenti.

---

## Task 1: KPI row da 3 a 4 colonne

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/overview/KPIStatsRow.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/KPIStatsRow.test.tsx` (create or extend)

- [ ] **Step 1: Leggi `KPIStatsRow.tsx` per individuare la grid utility attuale e i KPI esistenti.**

  Run: `cat apps/web/src/app/admin/(dashboard)/overview/KPIStatsRow.tsx`
  Expected: il componente accetta props `totalGames`, `totalUsers`, `activeUsers`, `pendingApprovals`, `recentSubmissions`. Identifica la classe che imposta `grid-cols-3` (o equivalente) sul wrapper grid.

- [ ] **Step 2: Scrivi un test che asserisce 4 KPI cards renderizzate quando tutti i 5 dati sono presenti**

  ```tsx
  // apps/web/src/app/admin/(dashboard)/overview/__tests__/KPIStatsRow.test.tsx
  import { render, screen } from '@testing-library/react';
  import { describe, it, expect } from 'vitest';

  import { KPIStatsRow } from '../KPIStatsRow';

  describe('KPIStatsRow', () => {
    it('renders 4 KPI cards when all data is provided', () => {
      render(
        <KPIStatsRow
          totalGames={42}
          totalUsers={100}
          activeUsers={30}
          pendingApprovals={5}
          recentSubmissions={7}
        />
      );
      // Each KPI exposes the value text — there should be 4 visible numbers
      expect(screen.getByText('42')).toBeInTheDocument();
      expect(screen.getByText('100')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.getByText('7')).toBeInTheDocument();
    });
  });
  ```

  Run: `cd apps/web && pnpm vitest run src/app/admin/\\(dashboard\\)/overview/__tests__/KPIStatsRow.test.tsx`
  Expected: FAIL if the current component only renders 3 KPIs (no `recentSubmissions` slot) or PASS if it already renders 4 but with the wrong grid count — adapt the assertion if needed.

- [ ] **Step 3: Modifica `KPIStatsRow.tsx` per esporre 4 colonne**

  Aggiorna il wrapper grid da `grid-cols-3` (o `md:grid-cols-3`) a `grid-cols-2 lg:grid-cols-4` (responsive: 2 su mobile, 4 su desktop). Se manca il 4° KPI nel rendering, aggiungilo dai dati già accettati come prop (`recentSubmissions` è il candidato più naturale e già passato dalla page.tsx). Mantieni il design dei card esistente (token semantici, no hardcoded color).

- [ ] **Step 4: Run del test, conferma PASS.**

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/admin/(dashboard)/overview/KPIStatsRow.tsx apps/web/src/app/admin/(dashboard)/overview/__tests__/KPIStatsRow.test.tsx
  git commit -m "feat(admin/overview): KPI row 4-up per mockup A1"
  ```

---

## Task 2: 2-col layout `[main | quick-actions sidebar 280px]`

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/overview/page.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/overview/QuickActionsGrid.tsx`
- Test: `apps/web/src/app/admin/(dashboard)/overview/__tests__/page.test.tsx` (create or extend)

- [ ] **Step 1: Leggi `QuickActionsGrid.tsx` per capire la struttura attuale (probabilmente render a griglia di action cards).**

- [ ] **Step 2: Aggiungi una variante "sidebar" a `QuickActionsGrid`**

  Estendi le props con `variant?: 'grid' | 'sidebar'` (default `'grid'`). Quando `'sidebar'`, rende la lista in colonna (es. `flex flex-col gap-px` con `border-bottom` tra le voci, come il mockup `.quick-actions`); quando `'grid'`, mantiene il rendering attuale (retrocompatibile con altre pagine).

- [ ] **Step 3: Modifica `page.tsx` per il layout 2-col al posto dello stack lineare**

  Wrappa la sezione principale in:

  ```tsx
  <div className="grid grid-cols-1 xl:grid-cols-[minmax(0,1fr)_280px] gap-4">
    <div className="flex flex-col gap-6">
      {/* contenuto principale: KPI, banner pending, queue, library+users */}
    </div>
    <aside className="flex flex-col gap-4">
      <h2 className="font-quicksand text-sm font-semibold uppercase tracking-wider text-muted-foreground px-3">
        Azioni rapide
      </h2>
      <QuickActionsGrid variant="sidebar" />
      <TechActionsBar />
    </aside>
  </div>
  ```

  Il breakpoint `xl:` (≥1280px) corrisponde al mockup (`@media (max-width: 1280px) { .overview-grid { grid-template-columns: 1fr; } }`). Sotto, lo stack è single-col (mobile-first preserva la usabilità).

  NB: la heading "Azioni Rapide" originale + il `<TechActionsBar />` migrano nell'`<aside>`.

- [ ] **Step 4: Test page.tsx — la sidebar quick-actions è presente in xl, l'h1 "Dashboard" resta**

  ```tsx
  // apps/web/src/app/admin/(dashboard)/overview/__tests__/page.test.tsx
  import { render, screen } from '@testing-library/react';
  import { describe, it, expect, vi } from 'vitest';
  import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

  // Mock the data layer
  vi.mock('@/lib/api', () => ({
    api: {
      admin: { getOverviewStats: () => Promise.resolve(null) },
      accessRequests: { getAccessRequests: () => Promise.resolve({ items: [], totalCount: 0, page: 1, pageSize: 5 }) },
      invitations: { getInvitations: () => Promise.resolve({ items: [], totalCount: 0 }) },
    },
  }));

  import OverviewPage from '../page';

  function renderPage() {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    return render(
      <QueryClientProvider client={qc}>
        <OverviewPage />
      </QueryClientProvider>
    );
  }

  describe('OverviewPage', () => {
    it('renders the Dashboard heading and the Azioni rapide sidebar', async () => {
      renderPage();
      expect(await screen.findByRole('heading', { name: /Dashboard/i })).toBeInTheDocument();
      expect(await screen.findByText(/Azioni rapide/i)).toBeInTheDocument();
    });
  });
  ```

  Run: `cd apps/web && pnpm vitest run src/app/admin/\\(dashboard\\)/overview/__tests__/page.test.tsx`
  Expected: PASS dopo lo step 3.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/admin/(dashboard)/overview/page.tsx apps/web/src/app/admin/(dashboard)/overview/QuickActionsGrid.tsx apps/web/src/app/admin/(dashboard)/overview/__tests__/page.test.tsx
  git commit -m "feat(admin/overview): 2-col layout with quick-actions sidebar (A1)"
  ```

---

## Task 3: Topbar Overview — h1 + crumbs + refresh button

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/overview/page.tsx`
- Test: estende `page.test.tsx` del Task 2

- [ ] **Step 1: Aggiungi al test esistente l'asserzione sul refresh button + crumbs**

  Aggiungi un `it` al `describe('OverviewPage', ...)`:

  ```tsx
  it('renders the topbar with a refresh button and breadcrumbs', async () => {
    renderPage();
    expect(await screen.findByRole('button', { name: /Refresh/i })).toBeInTheDocument();
    expect(screen.getByText(/Admin · Overview/i)).toBeInTheDocument();
  });
  ```

  Run il test, expected: FAIL (la pagina attuale non ha refresh button né crumbs).

- [ ] **Step 2: Modifica `page.tsx` — wrappa il blocco heading in una topbar con crumbs + bottone Refresh**

  Sostituisci il blocco heading attuale con:

  ```tsx
  <div className="flex items-start justify-between gap-4 pb-2 border-b border-border">
    <div>
      <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
        Overview sistema
      </h1>
      <p className="text-xs text-muted-foreground mt-1 font-mono">
        Admin · Overview{data?.stats ? ` · ultimo refresh ${new Date().toLocaleTimeString('it-IT')}` : ''}
      </p>
    </div>
    <Button variant="outline" size="sm" onClick={() => refetch()}>
      <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
      Refresh
    </Button>
  </div>
  ```

  `RefreshCwIcon` e `Button` sono già importati nel file (usati nell'errore banner).

  Il titolo passa da "Dashboard" a "Overview sistema" (coerente col mockup). Aggiorna l'asserzione del test del Task 2 di conseguenza se necessario (`/Overview sistema/i` invece di `/Dashboard/i`).

- [ ] **Step 3: Run del test, conferma PASS.**

- [ ] **Step 4: Run dell'intera suite overview per confermare nessuna regressione**

  Run: `cd apps/web && pnpm vitest run src/app/admin/\\(dashboard\\)/overview`
  Expected: tutti i test green.

- [ ] **Step 5: Commit**

  ```bash
  git add apps/web/src/app/admin/(dashboard)/overview/page.tsx apps/web/src/app/admin/(dashboard)/overview/__tests__/page.test.tsx
  git commit -m "feat(admin/overview): topbar with refresh + breadcrumbs (A1)"
  ```

---

## Self-Review

**1. Spec coverage:** F1 valida i pattern-chiave del re-skin A1 (KPI 4-up, 2-col layout, topbar). Charts/activity-feed/alerts-feed sono esplicitamente non-goal del pilota (vedi §Non-goals sopra) — andranno nelle ondate.

**2. Placeholder scan:** ogni Task ha codice/comandi concreti. I riferimenti "leggi il file" dove servono (Task 1 step 1, Task 2 step 1) sono per il context del file esistente — l'implementer ottiene il codice da modificare con un `cat`, non con un'invenzione di codice.

**3. Type consistency:** `KPIStatsRow` riceve le stesse props oggi e dopo (aggiunge solo `lg:grid-cols-4` + il 4° card); `QuickActionsGrid` estende le props con `variant`, default retrocompatibile; `page.test.tsx` usa il QueryClientProvider standard (no nuove dipendenze).

**Note di rischio:**
- Il test del Task 2 mocka `@/lib/api`: se la struttura `api.*` cambia di poco, i mock vanno aggiornati — è il pattern usato dagli altri test admin (vedi `agents/inspector/__tests__/`).
- Il breakpoint `xl:` (≥1280px) per il 2-col è coerente col mockup; su `lg` (1024px) il layout collassa a single-col. Se il design vuole 2-col già su `lg`, è un fix di una stringa (la decisione resta col designer).
- Il titolo "Overview sistema" sostituisce "Dashboard": è la dicitura del mockup, da confermare in review.

---

## Execution Handoff

Plan F1 salvato. Dopo l'esecuzione + review + merge, il pilota convalida il workflow di re-skin SP5 sopra la shell consolidata. Da lì si scala alle ondate **F2 A2 Users** (stress-test sicurezza: DataTable + impersonate + step-up + audit), **F3-F6** (cluster KB/AI, Ops, Tools, avanzati) e al track sicurezza ⊥ (audit schema, impersonate token, step-up strict).
