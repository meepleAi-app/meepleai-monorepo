# Epic #4838 - UI/UX Fixes Implementation Plan

## Overview

7 bug/miglioramenti UI: layout, MeepleCard interactions, admin navigation.

## Issues & Execution Order

### Phase 1: Foundation (prerequisiti)

#### Issue #4839 - Route group (protected) → (authenticated) migration
- **Branch**: `feature/issue-4839-layout-fix` from `frontend-dev`
- **Scope**: Migrare pagine da `(protected)` a `(authenticated)`
- **Files**:
  - `apps/web/src/app/(protected)/agents/page.tsx` → `(authenticated)/`
  - `apps/web/src/app/(protected)/library/wishlist/page.tsx` → `(authenticated)/`
  - Verificare: notifications, profile, settings, admin in `(protected)`
- **Validation**: Tutte le pagine mostrano navbar
- **Command**: `/implementa #4839`

#### Issue #4844 - Admin nav PDFs (indipendente)
- **Branch**: `feature/issue-4844-admin-nav-pdfs` from `frontend-dev`
- **Scope**: Aggiungere "Documents" alla sezione Knowledge Base
- **Files**: `apps/web/src/config/admin-dashboard-navigation.ts`
- **Validation**: Link visibile e attivo in admin sidebar
- **Command**: `/implementa #4844`

### Phase 2: MeepleCard fixes (dipende da #4839)

#### Issue #4840 - Mobile tap MeepleCard
- **Branch**: `feature/issue-4840-mobile-tap` from `frontend-dev`
- **Scope**: Fix handleMobileClick e bottom sheet
- **Files**: `apps/web/src/components/ui/data-display/meeple-card.tsx`
- **Validation**: Tap → bottom sheet visibile su mobile
- **Command**: `/implementa #4840`

#### Issue #4841 - Flip card touch/desktop (parallelo con #4842)
- **Branch**: `feature/issue-4841-flip-card` from `frontend-dev`
- **Scope**: flipTrigger dinamico basato su isMobile
- **Files**:
  - `meeple-card.tsx` - passare flipTrigger dinamico
  - `FlipCard.tsx` - già supporta entrambi i modi
- **Validation**: Desktop click=flip, touch tap=select+button
- **Command**: `/implementa #4841`

#### Issue #4842 - Chat quick action (parallelo con #4841)
- **Branch**: `feature/issue-4842-chat-action` from `frontend-dev`
- **Scope**: Aggiungere pulsante chat tra quick actions
- **Files**:
  - `meeple-card-quick-actions.tsx`
  - `meeple-card.tsx`
  - Consumer components
- **Validation**: Pulsante chat visibile, click naviga a chat
- **Command**: `/implementa #4842`

### Phase 3: Polish (indipendenti)

#### Issue #4843 - iPad catalogo
- **Branch**: `feature/issue-4843-ipad-catalog` from `frontend-dev`
- **Scope**: Fix responsive grid e button overflow
- **Files**:
  - `apps/web/src/app/(public)/games/catalog/page.tsx`
  - `MeepleGameCatalogCard.tsx`
- **Validation**: Catalogo completo su iPad 768-1024px
- **Command**: `/implementa #4843`

#### Issue #4845 - PDF delete feedback
- **Branch**: `feature/issue-4845-pdf-delete` from `frontend-dev`
- **Scope**: Debug errore + fix feedback
- **Files**:
  - `(authenticated)/admin/pdfs/client.tsx` o `(dashboard)/knowledge-base/documents/page.tsx`
  - Possibile: `BulkDeletePdfsCommandHandler.cs`
- **Validation**: Delete mostra toast successo, no errore falso
- **Command**: `/implementa #4845`
- **Note**: Richiede debug browser per identificare causa esatta

## Execution Flow

```
/implementa #4839 → PR → merge → branch cleanup
    ↓ (parallelo)
/implementa #4844 → PR → merge → branch cleanup
    ↓
/implementa #4840 → PR → merge → branch cleanup
    ↓ (parallelo)
/implementa #4841 → PR → merge → branch cleanup
/implementa #4842 → PR → merge → branch cleanup
    ↓ (parallelo)
/implementa #4843 → PR → merge → branch cleanup
/implementa #4845 → PR → merge → branch cleanup
    ↓
Epic #4838 chiusa
```

## Per ogni /implementa

Workflow standard:
1. `git checkout frontend-dev && git pull`
2. `git checkout -b feature/issue-XXXX-desc`
3. `git config branch.feature/issue-XXXX-desc.parent frontend-dev`
4. Implementazione
5. Test: `pnpm typecheck && pnpm lint`
6. Commit con conventional commits
7. PR to `frontend-dev`
8. Code review
9. Merge
10. Update issue status (local + GitHub)
11. `git checkout frontend-dev && git pull && git branch -D feature/issue-XXXX-desc`
