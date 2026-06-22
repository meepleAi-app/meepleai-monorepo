# SP5 Admin F2 — A2 Users re-skin (scope conservativo) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Re-skin del look-and-feel della lista `/admin/users` per allinearla al mockup A2 (`sp5-admin-users.html`), **mantenendo l'IA attuale a route singola** (lista + drill `[id]` su route separata). 2 task chirurgici: cell utente uniformato (avatar + name + email) e toolbar a filter-chips. Page esistente già ricca (search debounced, role filter, pagination, invitations pending al top, `InlineRoleSelect`); F2 NON la riscrive.

**Architecture:** Lavora sopra `apps/web/src/app/admin/(dashboard)/users/page.tsx`. Introduce un mini-componente `UserCell` riusabile (avatar gradient + name + email, come `.user-cell` del mockup) e converte il `<Select>` di ruolo in una riga di filter-chips (`.filter-chip` del mockup) — il `<Select>` resta come fallback sotto `md`. Niente cambio di routing, niente DataTable nuova, niente side-by-side detail panel (decisione UX rimandata).

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · Vitest. Niente nuove dipendenze.

**Depends on:** Fondamenta F0a/b/c + Pilota F1 (tutti in `main-dev`).

**Spec di riferimento:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §5 (A2 → `/admin/users` + `/admin/users/[id]`, "wiring delete/impersonate da verificare"), §9 (F2 = stress-test sicurezza).

**Mockup target:** `admin-mockups/design_handoff_admin/admin/sp5-admin-users.html` (focus su `.users-toolbar` + `.user-cell` + `.filter-chip`; il `.detail-card` sticky 460px = decisione UX in un plan dedicato).

**Non-goals di F2 (rimandati):**
- ❌ **Layout side-by-side list + detail panel 460px** — è un cambio UX significativo che merita un plan dedicato (decisione: tenere drill `/users/[id]` su route separata vs aggiungere `?selected=<id>` deep-link). NON in F2.
- ❌ Virtualization (>200 righe) — la pagination esistente è sufficiente per il volume attuale.
- ❌ Step-up UI modal per delete/impersonate — track ⊥ sicurezza (R5).
- ❌ Audit timeline esteso nel detail panel — richiede schema audit (R3) + side-by-side decision.
- ❌ Keyboard shortcuts (⌘K, g+u, j/k) — fuori scope pilota.

---

## File Structure

- **Create** `apps/web/src/components/admin/users/UserCell.tsx` — componente presentazionale `<UserCell user={{displayName,email,role}} />` (avatar gradient `--c-player`→`--c-chat`, name su una riga, email truncate, ottimizzato per cella tabella). + test.
- **Modify** `apps/web/src/app/admin/(dashboard)/users/page.tsx` — usa `UserCell` nelle righe utente; converte il `Select` ruolo in filter-chips (`.filter-chip` style); mantiene il `<Select>` originale come fallback per `<md`.
- (Optional) **Modify** `apps/web/src/app/admin/(dashboard)/users/__tests__/page.test.tsx` se esistono test che asseriscono la cell struttura attuale (allineare alla nuova).

---

## Task 1: `UserCell` componente presentazionale

**Files:**
- Create: `apps/web/src/components/admin/users/UserCell.tsx`
- Test: `apps/web/src/components/admin/users/__tests__/UserCell.test.tsx`

- [ ] **Step 1: Test (TDD)**

```tsx
// apps/web/src/components/admin/users/__tests__/UserCell.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { UserCell } from '../UserCell';

describe('UserCell', () => {
  it('renders displayName, email and an avatar initial', () => {
    render(
      <UserCell user={{ displayName: 'Aaron D.', email: 'aaron@example.com' }} />
    );
    expect(screen.getByText('Aaron D.')).toBeInTheDocument();
    expect(screen.getByText('aaron@example.com')).toBeInTheDocument();
    // Avatar shows the first letter of the display name
    expect(screen.getByTestId('user-cell-avatar')).toHaveTextContent('A');
  });

  it('falls back to email initial when displayName is null', () => {
    render(<UserCell user={{ displayName: null, email: 'maria@example.com' }} />);
    expect(screen.getByText('maria@example.com')).toBeInTheDocument();
    expect(screen.getByTestId('user-cell-avatar')).toHaveTextContent('M');
  });

  it('truncates long emails on small widths via CSS class', () => {
    render(
      <UserCell
        user={{ displayName: 'X', email: 'very.long.email.address@example.com' }}
      />
    );
    const email = screen.getByText('very.long.email.address@example.com');
    expect(email).toHaveClass('truncate');
  });
});
```

- [ ] **Step 2: Run, confirm FAIL** (module missing).

  Run: `cd apps/web && pnpm vitest run src/components/admin/users/__tests__/UserCell.test.tsx`

- [ ] **Step 3: Crea `UserCell.tsx`**

```tsx
// apps/web/src/components/admin/users/UserCell.tsx
/* eslint-disable local/no-hardcoded-color-utility -- text-white on the entity-tinted gradient avatar; mockup .user-avatar pattern. */
import React from 'react';

export interface UserCellUser {
  displayName: string | null;
  email: string;
  role?: string;
}

export interface UserCellProps {
  user: UserCellUser;
}

/**
 * Compact user cell for admin tables: gradient avatar (player→chat tokens) +
 * display name + truncated email. Mirrors the `.user-cell` pattern from the
 * SP5 A2 mockup. Presentational only — receives a plain user-shaped object.
 */
export function UserCell({ user }: UserCellProps) {
  const initial = (user.displayName?.charAt(0) ?? user.email.charAt(0) ?? '?').toUpperCase();

  return (
    <div className="flex items-center gap-2 min-w-0" data-testid="user-cell">
      <span
        data-testid="user-cell-avatar"
        className="shrink-0 grid place-items-center w-7 h-7 rounded-full text-white text-[11px] font-bold"
        style={{
          background:
            'linear-gradient(135deg, hsl(var(--c-player)), hsl(var(--c-chat)))',
        }}
        aria-hidden="true"
      >
        {initial}
      </span>
      <span className="flex flex-col min-w-0">
        <span className="font-quicksand text-[12.5px] font-bold leading-tight truncate">
          {user.displayName ?? user.email}
        </span>
        <span className="font-mono text-[10.5px] text-muted-foreground truncate">
          {user.email}
        </span>
      </span>
    </div>
  );
}

export default UserCell;
```

- [ ] **Step 4: Run, confirm PASS** (3 test).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/users/UserCell.tsx apps/web/src/components/admin/users/__tests__/UserCell.test.tsx
git commit -m "feat(admin/users): add UserCell presentational (mockup A2 .user-cell)"
```

---

## Task 2: Filter-chips per il ruolo + integrazione `UserCell` in tabella

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/users/page.tsx`

- [ ] **Step 1: Leggi la pagina** per individuare (a) la cella che renderizza la riga utente (probabilmente `<td>` con `displayName` + `email`); (b) il `<Select>` di ruolo controllato da `roleFilter` / `setRoleFilter`.

- [ ] **Step 2: Sostituisci la cella utente con `<UserCell user={...} />`**

  Importa `UserCell` in cima al file e sostituisci il blocco JSX esistente che renderizza nome+email con `<UserCell user={{ displayName: u.displayName, email: u.email, role: u.role }} />`. Le altre celle (ruolo, status, azioni) restano invariate.

- [ ] **Step 3: Aggiungi un filter-chips toolbar SOPRA il `<Select>` (non lo rimuovi: resta come fallback per piccoli schermi)**

  Sopra il blocco del Select esistente, inserisci:

  ```tsx
  <div className="hidden md:flex items-center gap-2 flex-wrap" data-testid="role-filter-chips">
    {ROLE_OPTIONS.map(role => {
      const active = roleFilter === role;
      const label = role === 'all' ? 'Tutti' : displayRole(role);
      return (
        <button
          key={role}
          type="button"
          onClick={() => setRoleFilter(role)}
          aria-pressed={active}
          className={`inline-flex items-center gap-1 px-2 py-1 rounded-full text-[11px] font-semibold font-quicksand border transition-colors ${
            active
              ? 'bg-entity-event/12 text-entity-event border-entity-event/30'
              : 'bg-muted text-muted-foreground border-border hover:bg-muted/80'
          }`}
        >
          {label}
        </button>
      );
    })}
  </div>
  ```

  Il `<Select>` esistente lo wrappi in `<div className="md:hidden">…</div>` così appare solo sotto `md` come fallback.

  NOTA: `bg-entity-event` / `text-entity-event` / `border-entity-event` sono utility canoniche (verifica in `apps/web/src/components/ui/entity-tokens.ts`); se la mappatura per "event" non esiste, ricadi su `bg-amber-500/12 text-amber-600 border-amber-500/30` con `eslint-disable-next-line local/no-hardcoded-color-utility` mirato (event tinge `--c-event` = ambra). Lo stesso `eslint-disable` esiste già a livello-file in altri componenti admin per pattern simili.

- [ ] **Step 4: Run dei test esistenti users (no regressione)**

  Run: `cd apps/web && pnpm vitest run "src/app/admin/(dashboard)/users/"`
  Expected: tutti i test esistenti restano verdi.

- [ ] **Step 5: Typecheck + lint**

  Run: `cd apps/web && pnpm typecheck && pnpm exec eslint src/components/admin/users "src/app/admin/(dashboard)/users/page.tsx"`
  Expected: nessun errore.

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/users/page.tsx"
git commit -m "feat(admin/users): role filter-chips + UserCell in rows (A2)"
```

---

## Self-Review

**1. Spec coverage:** F2 copre il re-skin presentazionale di A2 (mockup `.user-cell` + `.filter-chip`). Lo "stress-test sicurezza" più sostanziale (impersonate banner, delete typed-confirm, step-up UI, audit timeline) è esplicitamente non-goal in questo plan — i pattern esistono già nel codebase (`AdminConfirmationDialog`, `ImpersonateUserCommand`) e verranno collegati nel side-by-side detail panel in un plan separato, dopo la decisione UX.

**2. Placeholder scan:** ogni step ha codice/comandi concreti. L'unica "incognita" è la mappatura `entity-event` in Tailwind: lo step 3 fornisce un fallback esplicito se l'utility non esiste (eslint-disable mirato).

**3. Type consistency:** `UserCellUser` ha `displayName: string | null` (matcha la prop reale della pagina users); `role?` opzionale. Filter-chips usa `ROLE_OPTIONS` esistente; nessun nuovo tipo.

**Note di rischio:**
- Page `users/page.tsx` ha test esistenti in `__tests__/`. Il re-skin del cell potrebbe far fallire test che asseriscono la struttura DOM attuale (cell text raw). Se i test si rompono, allinearli alla nuova struttura `UserCell` (testid + truncate class).
- Side-by-side detail panel è esplicitamente non-goal: chi vuole quel pattern apre un plan dedicato (richiede deep-link `?selected=<id>` o route nested).

---

## Execution Handoff

Plan F2 salvato. Dopo l'esecuzione + review + merge, il pilota stress-test convalida che il design system (token + entity utility) regge anche su una pagina ricca. Da lì, si scala alle ondate F3-F6 e si decide il side-by-side detail in un plan dedicato.
