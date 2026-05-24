# SP5 Admin F0c — Dark Theme Scoped to /admin/* — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Applicare il tema **dark come default sotto `/admin/*`**, mantenendo il default light nel resto del prodotto, senza FOUC e senza combattere `next-themes`.

**Architecture:** Approccio **container-scoped** (non si tocca `<html>`). Si estende il selettore dei token dark perché valga anche quando `data-theme="dark"` è su un contenitore (non solo su `:root`); poi si applica `data-theme="dark"` al `<div>` root di `AdminShell`. I token CSS dark vengono così ridefiniti per il solo subtree admin via ereditarietà, e le utility `dark:` funzionano già (il custom-variant copre `[data-theme="dark"] *`). Nessuna manipolazione runtime di `<html>`, nessun conflitto con `next-themes`/localStorage, nessun cleanup all'uscita (lo scope muore con `AdminShell`).

**Tech Stack:** Next.js 16 (App Router) · React 19 · TypeScript · CSS custom properties · Vitest · Playwright (E2E). Niente nuove dipendenze.

**Depends on:** **F0b** (modifica lo stesso `AdminShell.tsx`). Esegui F0a → F0b → F0c in ordine.

**Spec di riferimento:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §4.3 (dark default scoped `/admin/*`), R9.

**Perché NON l'approccio `<html>`-based:** forzare `data-theme="dark"` su `<html>` via effect richiede di salvare/ripristinare il tema all'uscita (App Router naviga client-side, `next-themes` non rimonta), rischia FOUC al primo load e può confliggere con le riscritture di `next-themes`. L'approccio container-scoped evita tutto ciò spostando lo scope su un nodo che è già SSR-rendered dentro il segmento admin.

**Decisione di scope:** F0c forza dark su admin (default). Un eventuale **toggle light/dark dentro l'admin** è un follow-up (non incluso): R9 chiede "dark default", il toggle globale resta per il resto del prodotto.

---

## File Structure

- **Modify** `apps/web/src/styles/design-tokens-canonical.css:165` — estendi il selettore `:root[data-theme="dark"]` a `:root[data-theme="dark"], [data-theme="dark"]` così i token dark valgono anche scoped a un contenitore.
- **Modify** `apps/web/src/components/layout/AdminShell/AdminShell.tsx` — aggiungi `data-theme="dark"` (+ `data-admin-shell` per i test) al `<div>` root.
- **Modify** `apps/web/src/components/layout/AdminShell/__tests__/AdminShell.test.tsx` — asserisci l'attributo `data-theme="dark"`.
- **Create** `apps/web/e2e/admin/admin-dark-scope.spec.ts` — E2E: `/admin/*` dark, route non-admin light.

---

## Task 1: Estendi il selettore dei token dark al container scope

**Files:**
- Modify: `apps/web/src/styles/design-tokens-canonical.css:165`

> NOTA TDD: una modifica al selettore CSS non è verificabile in unit test (jsdom non risolve le custom properties dei file CSS reali). La verifica avviene nell'E2E del Task 3. Questo task è una modifica chirurgica di una riga + razionale.

- [ ] **Step 1: Apri il file e individua il blocco dark**

Run: `cd apps/web && sed -n '163,170p' src/styles/design-tokens-canonical.css`
Expected: la riga 165 è `:root[data-theme="dark"] {` (apertura del blocco token dark).

- [ ] **Step 2: Estendi il selettore**

Sostituisci esattamente:

```css
:root[data-theme="dark"] {
```

con:

```css
/* DS: dark tokens apply both on :root (global theme) AND on any container
   carrying data-theme="dark" (e.g. AdminShell), enabling per-section dark scope
   without touching <html>. See SP5 F0c. */
:root[data-theme="dark"],
[data-theme="dark"] {
```

Non cambiare il corpo del blocco (i valori dei token dark restano invariati).

- [ ] **Step 3: Verifica che il light theme globale non regredisca (build CSS)**

Run: `cd apps/web && pnpm exec stylelint src/styles/design-tokens-canonical.css` (se stylelint è configurato; altrimenti salta)
Expected: nessun errore di sintassi sul selettore. In assenza di stylelint, verifica manuale che il blocco apra con i due selettori e chiuda regolarmente con `}`.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/styles/design-tokens-canonical.css
git commit -m "feat(tokens): allow dark tokens to scope to a container (not only :root)"
```

---

## Task 2: `AdminShell` applica `data-theme="dark"`

**Files:**
- Modify: `apps/web/src/components/layout/AdminShell/AdminShell.tsx`
- Modify: `apps/web/src/components/layout/AdminShell/__tests__/AdminShell.test.tsx`

- [ ] **Step 1: Aggiungi l'asserzione al test esistente di `AdminShell`**

Aggiungi questo test al `describe('AdminShell', ...)` creato in F0b:

```tsx
  it('scopes the dark theme to the admin shell root', () => {
    mockUseCurrentUser.mockReturnValue({ data: { id: 'u', email: 'a@b.c', role: 'admin' } });
    const { container } = render(<AdminShell><p>page body</p></AdminShell>);
    const root = container.querySelector('[data-admin-shell]');
    expect(root).not.toBeNull();
    expect(root).toHaveAttribute('data-theme', 'dark');
  });
```

- [ ] **Step 2: Esegui il test e verifica che fallisca**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminShell/__tests__/AdminShell.test.tsx`
Expected: FAIL — nessun elemento `[data-admin-shell]` / nessun `data-theme="dark"`.

- [ ] **Step 3: Aggiungi gli attributi al `<div>` root di `AdminShell`**

Modifica la riga di apertura del `<div>` root (da F0b):

```tsx
    <div data-admin-shell data-theme="dark" className="flex min-h-dvh flex-col bg-[var(--bg)]">
```

(invariato tutto il resto del componente). `bg-[var(--bg)]` ora risolve al `--bg` dark perché il `data-theme="dark"` sullo stesso elemento ridefinisce i token per sé e i discendenti (grazie al Task 1).

- [ ] **Step 4: Esegui i test e verifica che passino**

Run: `cd apps/web && pnpm vitest run src/components/layout/AdminShell/__tests__/AdminShell.test.tsx`
Expected: PASS (i test di F0b + il nuovo test dark).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/layout/AdminShell/AdminShell.tsx apps/web/src/components/layout/AdminShell/__tests__/AdminShell.test.tsx
git commit -m "feat(admin): scope dark theme to AdminShell root (/admin/*)"
```

---

## Task 3: E2E — dark su /admin, light fuori

**Files:**
- Create: `apps/web/e2e/admin/admin-dark-scope.spec.ts`

- [ ] **Step 1: Scrivi il test E2E**

```typescript
// apps/web/e2e/admin/admin-dark-scope.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Admin dark theme scope', () => {
  test.beforeEach(() => {
    process.env.PLAYWRIGHT_AUTH_BYPASS = 'true';
  });

  test('admin shell is scoped to dark', async ({ page }) => {
    await page.goto('/admin/overview');
    const shell = page.locator('[data-admin-shell]');
    await expect(shell).toHaveAttribute('data-theme', 'dark');
    // The shell background resolves to the dark token, not the light cream.
    const bg = await shell.evaluate(el => getComputedStyle(el).backgroundColor);
    expect(bg).not.toBe('rgb(247, 243, 238)'); // #f7f3ee light cream
  });

  test('a non-admin route stays light at the document root', async ({ page }) => {
    await page.goto('/dashboard');
    await expect(page.locator('html')).toHaveAttribute('data-theme', 'light');
    await expect(page.locator('[data-admin-shell]')).toHaveCount(0);
  });
});
```

- [ ] **Step 2: Esegui l'E2E**

Run: `cd apps/web && pnpm test:e2e -- admin/admin-dark-scope.spec.ts`
Expected: PASS — `/admin/overview` ha `data-admin-shell[data-theme="dark"]` con bg non-cream; `/dashboard` resta light e non ha l'admin shell.

> Se il primo run fallisce per setup auth/server, allinea il pattern di bypass agli altri test in `apps/web/e2e/admin/` (es. `admin-dashboard-navigation.spec.ts`) — usano `process.env.PLAYWRIGHT_AUTH_BYPASS = 'true'` nel `beforeEach`.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/admin/admin-dark-scope.spec.ts
git commit -m "test(admin): e2e for /admin dark scope vs light elsewhere"
```

---

## Self-Review

**1. Spec coverage:** F0c copre §4.3 "dark default scoped `/admin/*`" e R9. Density alta (§4.3) NON è in F0c — è un work-stream separato (la spec la marca come decisione D-2, da affrontare a parte).

**2. Placeholder scan:** nessun TODO. La modifica CSS è mostrata esatta (selettore prima/dopo). Gli attributi su `AdminShell` sono mostrati. L'E2E è completo.

**3. Type consistency:** nessun nuovo tipo. `data-admin-shell` è l'aggancio condiviso tra il test unit (Task 2) e l'E2E (Task 3) — coerente.

**Note di rischio:**
- **Specificità CSS:** `[data-theme="dark"]` (0,1,0) ha la stessa specificità di `[data-theme="light"]`; il light vive su `:root[...="light"]` (0,1,1) che è più specifico ma su un altro elemento. Per il subtree admin vince il `data-theme="dark"` del contenitore (più vicino nell'albero per le custom properties ereditate). Verifica visiva ≥ Task 3.
- **`dark:` utilities:** funzionano nel subtree perché `@custom-variant dark` include `[data-theme="dark"] *` (verificato in globals.css durante l'esplorazione). Nessun componente admin dovrebbe dipendere da `.dark` su `<html>`; se emerge un caso, va spostato su token semantici.
- **Toggle in-admin assente** (vedi Decisione di scope). Se in review si vuole un toggle dentro admin, è un follow-up dedicato (richiede stato + override del `data-theme` del contenitore).

---

## Execution Handoff

Plan F0c salvato. Con F0a + F0b + F0c la **fase Fondamenta** è interamente pianificata. Sotto-plan successivi: pilota **F1 A1 Overview** (re-skin sopra la nuova shell), poi ondate F2-F6.
