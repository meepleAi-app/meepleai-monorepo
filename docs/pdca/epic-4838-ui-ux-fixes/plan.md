# Plan: Epic #4838 - UI/UX Fixes Orchestration

## Hypothesis

7 issue frontend che impattano usabilità su mobile/tablet/desktop. Implementazione sequenziale con `/implementa` per ogni issue, rispettando le dipendenze. Tutte le issue partono da `frontend-dev` come branch padre.

## Execution Sequence

```
Phase 1 (Foundation)
│
├─ /implementa #4839  ← Layout fix (prerequisito per Phase 2)
│   └─ PR → merge → cleanup
│
├─ /implementa #4844  ← Admin nav (indipendente, parallelo)
│   └─ PR → merge → cleanup
│
Phase 2 (MeepleCard - dopo merge #4839)
│
├─ /implementa #4840  ← Mobile tap fix
│   └─ PR → merge → cleanup
│
├─ /implementa #4841  ← Flip card touch/desktop
│   └─ PR → merge → cleanup
│
├─ /implementa #4842  ← Chat quick action
│   └─ PR → merge → cleanup
│
Phase 3 (Polish)
│
├─ /implementa #4843  ← iPad catalogo
│   └─ PR → merge → cleanup
│
├─ /implementa #4845  ← PDF delete feedback
│   └─ PR → merge → cleanup
│
└─ Epic #4838 chiusa
```

## Issue Detail Cards

### 1. /implementa #4839 - Layout fix (protected) → (authenticated)
- **Branch**: `feature/issue-4839-layout-fix` from `frontend-dev`
- **Scope**: Migrare pagine orfane da `(protected)` a `(authenticated)`
- **Key files**:
  - `apps/web/src/app/(protected)/agents/page.tsx` → move
  - `apps/web/src/app/(protected)/library/wishlist/page.tsx` → move
  - Audit tutte le pagine in `(protected)/`: notifications, profile, settings
- **Risk**: Conflitto route se `(authenticated)/agents/` esiste già (ha `[id]/`)
- **Validation**: Navigare a /agents, /library/wishlist → navbar presente
- **Commit**: `fix(layout): migrate (protected) routes to (authenticated) for unified navbar`

### 2. /implementa #4844 - Admin nav PDFs
- **Branch**: `feature/issue-4844-admin-nav-pdfs` from `frontend-dev`
- **Scope**: 1 file, aggiungere item alla nav config
- **Key files**: `apps/web/src/config/admin-dashboard-navigation.ts` (sezione Knowledge Base, ~linea 201)
- **Risk**: Basso, modifica isolata
- **Validation**: Admin → Knowledge Base sidebar → link "Documents" visibile e funzionante
- **Commit**: `fix(admin): add PDFs/Documents to Knowledge Base navigation`

### 3. /implementa #4840 - Mobile tap MeepleCard
- **Branch**: `feature/issue-4840-mobile-tap` from `frontend-dev`
- **Scope**: Debug e fix interazione touch
- **Key files**: `apps/web/src/components/ui/data-display/meeple-card.tsx` (linee 976-1019, 1444-1516)
- **Investigation**:
  - Verificare `window.matchMedia('(max-width: 768px)')` funziona su target device
  - Controllare se `mousedown` listener (linea 996) chiude bottom sheet prematuramente
  - Testare propagazione eventi con flip/drag/hover handlers
- **Risk**: Medio, componente complesso (1600 righe)
- **Validation**: Mobile viewport → tap card → bottom sheet appare con azioni
- **Commit**: `fix(meeple-card): fix mobile tap not showing action buttons`

### 4. /implementa #4841 - Flip card touch/desktop
- **Branch**: `feature/issue-4841-flip-card` from `frontend-dev`
- **Scope**: flipTrigger dinamico basato su device
- **Key files**:
  - `meeple-card.tsx` - passare `flipTrigger={isMobile ? 'button' : 'card'}`
  - `FlipCard.tsx` - già supporta entrambi i modi (linea 338)
- **Logic**:
  - Desktop: `flipTrigger='card'` → click ovunque = flip, nessun pulsante
  - Touch: `flipTrigger='button'` → primo tap = seleziona, pulsante "gira" appare
- **Risk**: Basso, meccanismo già implementato, solo wiring
- **Validation**: Desktop click=flip diretto | Mobile tap=seleziona poi pulsante gira
- **Commit**: `feat(meeple-card): dynamic flip trigger - touch button, desktop direct`

### 5. /implementa #4842 - Chat quick action
- **Branch**: `feature/issue-4842-chat-action` from `frontend-dev`
- **Scope**: Nuovo pulsante chat tra quick actions
- **Key files**:
  - `meeple-card-quick-actions.tsx` - aggiungere tipo 'chat'
  - `meeple-card.tsx` - passare azione chat
  - Consumer components (dove MeepleCard viene usato con giochi)
- **Design**: Icona MessageCircle, naviga a `/chat/new?gameId={id}`
- **Condizione**: Visibile solo se gioco ha knowledge base/agent
- **Risk**: Medio, tocca più componenti
- **Validation**: MeepleCard con KB → icona chat visibile → click → chat page
- **Commit**: `feat(meeple-card): add chat quick action button for games with KB`

### 6. /implementa #4843 - iPad catalogo
- **Branch**: `feature/issue-4843-ipad-catalog` from `frontend-dev`
- **Scope**: Fix responsive grid + button text overflow
- **Key files**:
  - `apps/web/src/app/(public)/games/catalog/page.tsx` - grid breakpoints
  - `MeepleGameCatalogCard.tsx` - card sizing
  - `meeple-card.tsx` - action button styling (linee 772-796)
- **Fix**:
  - Grid: `md:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5` (ridurre colonne su md)
  - Buttons: `truncate` o testo responsive
- **Risk**: Basso, CSS only
- **Validation**: iPad 768-1024px → catalogo completo, pulsanti leggibili
- **Commit**: `fix(catalog): responsive grid and button overflow for iPad viewport`

### 7. /implementa #4845 - PDF delete feedback
- **Branch**: `feature/issue-4845-pdf-delete` from `frontend-dev`
- **Scope**: Debug errore + fix toast feedback
- **Key files**:
  - Frontend: pagina admin PDFs (client component)
  - Backend: `BulkDeletePdfsCommandHandler.cs`, `DeletePdfCommandHandler.cs`
- **Investigation**:
  - Catturare response HTTP del bulk delete
  - Verificare se `FailedCount > 0` per errori Qdrant/blob best-effort
  - Fix: se DB delete ok → successo, warning per cleanup falliti
- **Risk**: Medio, potrebbe richiedere fix backend
- **Validation**: Delete PDF → toast "eliminato con successo", no errore falso
- **Commit**: `fix(pdf-delete): correct success/error feedback for PDF deletion`

## Checklist Pre-Implementazione

Per OGNI issue, prima di `/implementa`:

```
□ git checkout frontend-dev && git pull origin frontend-dev
□ Verificare che frontend-dev sia aggiornato
□ Nessun conflitto pendente
```

## Checklist Post-Implementazione

Per OGNI issue, dopo merge PR:

```
□ Issue chiusa su GitHub
□ Branch locale eliminato: git branch -D feature/issue-XXXX-*
□ git remote prune origin
□ frontend-dev aggiornato: git checkout frontend-dev && git pull
```

## Expected Outcomes

| Metric | Target |
|--------|--------|
| Issue chiuse | 7/7 |
| PR merged | 7/7 |
| Regressioni | 0 |
| pnpm typecheck | Pass per ogni PR |
| pnpm lint | Pass per ogni PR |

## Risks & Mitigation

| Risk | Probabilità | Impatto | Mitigation |
|------|-------------|---------|------------|
| Conflitto route agents | Media | Alto | Audit (authenticated)/agents/ prima di migrare |
| MeepleCard regression | Media | Alto | Test su grid, list, compact, featured variants |
| iPad non testabile | Alta | Medio | Chrome DevTools device emulation |
| PDF delete backend issue | Media | Medio | Analisi response prima di toccare backend |
