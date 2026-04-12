# Dashboard Mockup Alignment — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allineare completamente il dashboard utente alla specifica visuale definita in `admin-mockups/dashboard-new-user-mockup.html`, rimuovere codice orfano e aggiungere copertura test mancante.

**Architecture:** Il mockup è un phone design (390px) con 4 hub block (Giochi/Sessioni/Agenti/Toolkit). `DashboardClient` implementa già questa struttura con classi responsive. Lo split `DashboardMobile` / `DashboardClient` è stato eliminato: il `DesktopShell` fornisce già TopBar + MiniNavSlot per tutte le dimensioni. `dashboard-mobile.tsx` è ora dead code.

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, Vitest, `@testing-library/react`, hook: `useLibrary`, `useGames`, `useActiveSessions`, `useAgents`, `useBatchGameStatus`, `useAddGameToLibrary`

---

## Stato pre-piano (già implementato in PR #397)

Le seguenti modifiche sono **già committate** su `feature/user-pages-hub-redesign`:
- ✅ `DashboardClient`: 4 hub block (Giochi/Sessioni/Agenti/Toolkit) con HubLayout
- ✅ `NewUserGamesBlock`: catalogo con KB badge + CTA Aggiungi
- ✅ Sort KB-first → rating tie-break (annotazione 1 mockup)
- ✅ Rimossi `showViewToggle` da tutti i blocchi
- ✅ `EmptyCTA`: Sessioni (1 CTA) + Agenti (2 CTA primary/outline)
- ✅ `ToolkitCarousel`: 4 tool statici (dado/clessidra/scoreboard/token)
- ✅ `page.tsx`: unificato — solo `<DashboardClient />`, rimosso split mobile/desktop
- ✅ `fix(proxy)`: MSW service worker escluso dal route guard redirect

## Lavoro rimanente

---

### Task 1: Rimuovere `dashboard-mobile.tsx` (dead code)

**Files:**
- Delete: `apps/web/src/app/(authenticated)/dashboard/dashboard-mobile.tsx`

- [ ] **Step 1: Verificare che non sia importato da nessuna parte**

```bash
grep -r "DashboardMobile\|dashboard-mobile" apps/web/src --include="*.ts" --include="*.tsx"
```

Output atteso: nessun match (solo la definizione nel file stesso).

- [ ] **Step 2: Eliminare il file**

```bash
git rm apps/web/src/app/\(authenticated\)/dashboard/dashboard-mobile.tsx
```

- [ ] **Step 3: Verificare che il build non sia rotto**

```bash
cd apps/web && pnpm typecheck
```

Output atteso: nessun errore.

- [ ] **Step 4: Eseguire i test della dashboard**

```bash
cd apps/web && npx vitest run src/app/\(authenticated\)/dashboard/__tests__/DashboardClient.test.tsx
```

Output atteso: 6/6 passed.

- [ ] **Step 5: Commit**

```bash
git commit -m "chore(dashboard): delete orphaned DashboardMobile component

dashboard-mobile.tsx is no longer imported since page.tsx was unified
to use DashboardClient for all screen sizes (DesktopShell provides
TopBar + MiniNavSlot on all viewports)."
```

---

### Task 2: Aggiungere test per sort KB-first e stato returning user

**Files:**
- Modify: `apps/web/src/app/(authenticated)/dashboard/__tests__/DashboardClient.test.tsx`

**Contesto:** I test esistenti coprono solo lo stato new-user (libreria vuota). Mancano:
1. Test che verifica l'ordinamento KB-first nel catalogo
2. Test per lo stato returning-user (libreria con giochi → mostra MeepleCard, no hint banner)

- [ ] **Step 1: Aggiungere mock dati per returning user**

In `DashboardClient.test.tsx`, aggiungere describe block `'returning user'`:

```typescript
describe('returning user (library has games)', () => {
  beforeEach(() => {
    vi.mocked(useLibrary).mockReturnValue({
      data: {
        items: [
          {
            id: 'lib-1',
            gameId: 'g1',
            gameTitle: 'Puerto Rico',
            gamePublisher: 'Alea',
            gameImageUrl: null,
            averageRating: 8.5,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 12,
      },
      isLoading: false,
    } as ReturnType<typeof useLibrary>);
  });

  it('does NOT show catalog hint banner for returning user', () => {
    render(<DashboardClient />);
    expect(screen.queryByText(/Libreria vuota/i)).not.toBeInTheDocument();
  });

  it('shows library game title for returning user', () => {
    render(<DashboardClient />);
    expect(screen.getByText('Puerto Rico')).toBeInTheDocument();
  });
});
```

Aggiungere import in cima al file (dopo gli import esistenti):

```typescript
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useGames } from '@/hooks/queries/useGames';
```

- [ ] **Step 2: Aggiungere test KB-sort order**

Aggiungere describe block `'new user catalog sort'`:

```typescript
describe('new user catalog sort — KB-first', () => {
  beforeEach(() => {
    vi.mocked(useGames).mockReturnValue({
      data: {
        games: [
          // rating più alta ma senza KB
          { id: 'g1', title: 'Game NoKB High Rating', publisher: 'Pub', createdAt: '2024-01-01T00:00:00Z', averageRating: 9.5, hasKnowledgeBase: false },
          // rating più bassa ma con KB
          { id: 'g2', title: 'Game WithKB Low Rating', publisher: 'Pub', createdAt: '2024-01-01T00:00:00Z', averageRating: 7.0, hasKnowledgeBase: true },
        ],
      },
      isLoading: false,
    } as ReturnType<typeof useGames>);
  });

  it('renders KB game before non-KB game regardless of rating', () => {
    render(<DashboardClient />);
    const gameNames = screen.getAllByText(/Game (WithKB|NoKB)/);
    // First match should be the KB game
    expect(gameNames[0]).toHaveTextContent('Game WithKB Low Rating');
    expect(gameNames[1]).toHaveTextContent('Game NoKB High Rating');
  });
});
```

- [ ] **Step 3: Eseguire i nuovi test per verificare che passino**

```bash
cd apps/web && npx vitest run src/app/\(authenticated\)/dashboard/__tests__/DashboardClient.test.tsx
```

Output atteso: tutti i test (inclusi i nuovi) passano.

Se il test KB-sort fallisce, significa che la logica di sort non funziona come atteso — debuggare in `NewUserGamesBlock` il `useMemo` sort.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/dashboard/__tests__/DashboardClient.test.tsx
git commit -m "test(dashboard): add returning-user state and KB-sort order coverage

- returning user: no catalog hint banner, shows library game titles
- KB-sort: games with hasKnowledgeBase=true appear before KB=false
  regardless of averageRating (spec annotation 1 of mockup)"
```

---

### Task 3: Chiusura PR e aggiornamento issue

**Files:**
- None (git/GitHub operations)

- [ ] **Step 1: Verificare stato finale della branch**

```bash
git log --oneline origin/main-dev..HEAD
```

Output atteso: lista dei commit della feature branch.

- [ ] **Step 2: Eseguire test suite completa**

```bash
cd apps/web && pnpm test
```

Output atteso: tutti i test passano.

- [ ] **Step 3: Aggiornare il body della PR #397**

Usare `gh pr edit 397` per aggiornare la descrizione con il changelog completo:

```bash
gh pr edit 397 --body "$(cat <<'EOF'
## Summary

- feat: DashboardClient rewrite con 4 hub block (Giochi/Sessioni/Agenti/Toolkit)
- feat: new-user empty state — catalogo top giochi con KB badge + CTA Aggiungi
- fix: sort giochi KB-first → rating tie-break (spec mockup annotazione 1)
- fix: rimossi view-mode toggle da tutti i blocchi HubLayout
- fix: MSW service worker escluso dal alpha route guard redirect
- refactor: unificato mobile/desktop — DashboardClient per tutte le dimensioni
- chore: eliminato DashboardMobile (dead code)
- test: copertura returning-user + KB-sort order

**Reference mockup:** `admin-mockups/dashboard-new-user-mockup.html`

## Test Plan
- [x] `pnpm typecheck` — nessun errore TS
- [x] `pnpm test` — tutti i test passano
- [x] Vitest: DashboardClient (new user, returning user, KB-sort, EmptyCTA, Toolkit)
- [x] Build produzione (`pnpm build`) — 133/133 pagine generate

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 4: Merge della PR (quando approvata)**

```bash
gh pr merge 397 --squash --delete-branch
```

Dopo il merge:
```bash
git checkout main-dev && git pull
git branch -D feature/user-pages-hub-redesign
```

---

## Self-Review del piano

**Spec coverage:**
- ✅ Annotazione 1 (KB-sort) → Task 2 test
- ✅ Annotazione 2 (KB badge verde/grigio) → già implementato, test non necessario (UI-only)
- ✅ Annotazione 3 (hint banner) → già implementato + test esistente
- ✅ Annotazione 4 (CTA Aggiungi) → già implementato + test esistente
- ✅ Annotazione 5 (sessions empty CTA) → già implementato + test esistente
- ✅ Annotazione 6 (agents empty 2 CTA) → già implementato + test esistente
- ✅ Annotazione 7 (HubLayout toolbar sempre visibile) → già implementato (struttura)
- ✅ Annotazione 8-11 (Toolkit carousel) → già implementato + test esistente
- ✅ Dead code removal → Task 1
- ✅ Mobile unification → già implementato, nessun test aggiuntivo necessario

**Placeholder scan:** Nessun TODO/TBD presente.

**Type consistency:** I tipi `ReturnType<typeof useLibrary>` e `ReturnType<typeof useGames>` sono coerenti con i mock già usati nel file test.
