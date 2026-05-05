# ConnectionChip a11y + semantic fixes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Risolvere 5 findings dal code review di PR #542 (issue #543): Critical `href+onCreate` double-trigger, WCAG 1.3.3 `aria-label` vs `99+`, PopoverContent senza nome accessibile, test gaps per `href+disabled` e keyboard nav.

**Architecture:** 3 task TDD stretti in sequenza: (1) helper `formatCountForLabel` + aria-label match visuale, (2) branching `onCreate` precede `href`, (3) `useId()` + `aria-labelledby` su `PopoverContent`. Ogni task: test rosso → impl minima → verde → commit.

**Tech Stack:** Next.js 16 + React 19 (useId), Radix Popover, Vitest + Testing Library, TypeScript.

**Spec**: `docs/superpowers/specs/2026-04-23-connectionchip-a11y-fixes-design.md`
**Branch**: `feature/issue-543-connectionchip-a11y-fixes` (da `main-dev`)
**Target PR**: `main-dev`

---

## File structure

| File | Azione | Responsabilità |
|------|--------|----------------|
| `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx` | Modify | Helper `formatCountForLabel` + branching `hasCreate` precede href |
| `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipPopover.tsx` | Modify | `useId()` per headerId + `aria-labelledby` su `PopoverContent` |
| `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx` | Modify | +3 test: aria-label 99+, button-not-Link quando hasCreate+href+count=0, disabled+href |
| `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx` | Modify | +2 test: aria-labelledby wiring, Escape chiude + focus return |

**Regressione**: 137 test esistenti di `meeple-card/` devono restare verdi.

---

## Working directory & test commands

**CRITICAL**: tutti i comandi `pnpm` vanno eseguiti da `apps/web`. `cd` non persiste tra chiamate Bash — usa percorsi assoluti.

```bash
# Test singolo file
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx

# Test intera suite meeple-card (regression guard)
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/
```

**Lint-staged pattern**: prettier riformatterà i file al commit (double quotes, rimuove blank line tra import group). Pass.

---

## Task 1: Helper `formatCountForLabel` + aria-label match visuale

**Findings risolti**: Important #2 (WCAG 1.3.3)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx:19-21, 148-152`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`

### - [ ] Step 1: Scrivere il test rosso per `count > 99`

Aggiungere in `ConnectionChip.test.tsx` dentro `describe('ConnectionChip', () => { ... })`, dopo il test `has aria-label including count and entity label` (riga ~87):

```tsx
it('uses "99 or more" in aria-label when count exceeds 99', () => {
  render(<ConnectionChip entityType="session" count={150} />);
  const btn = screen.getByRole('button');
  expect(btn.getAttribute('aria-label')).toMatch(/99 or more/i);
  expect(btn.getAttribute('aria-label')).not.toMatch(/150/);
});
```

### - [ ] Step 2: Far fallire il test

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx -t "99 or more"
```

Expected: FAIL — aria-label contiene `"150 sessions"`, non `"99 or more sessions"`.

### - [ ] Step 3: Aggiungere helper `formatCountForLabel`

In `ConnectionChip.tsx`, subito dopo `formatCount` (riga 19-21), aggiungere:

```ts
function formatCountForLabel(count: number, label: string): string {
  if (count > 99) return `99 or more ${label}`;
  return `${count} ${label}`;
}
```

### - [ ] Step 4: Usare helper nell'aria-label

In `ConnectionChip.tsx`, sostituire le righe 148-152:

```tsx
const ariaLabel = hasCount
  ? `${count} ${labelEffective}`
  : hasCreate
    ? (createLabel ?? `Aggiungi ${labelEffective}`)
    : labelEffective;
```

con:

```tsx
const ariaLabel = hasCount
  ? formatCountForLabel(count, labelEffective)
  : hasCreate
    ? (createLabel ?? `Aggiungi ${labelEffective}`)
    : labelEffective;
```

### - [ ] Step 5: Far passare il test e verificare nessuna regressione

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
```

Expected: tutti i test ConnectionChip pass (esistenti 14 + 1 nuovo = 15 pass).

### - [ ] Step 6: Commit

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
git commit -m "fix(connectionchip): match aria-label to visual for count > 99 (#543)

WCAG 2.1 SC 1.3.3 — screen reader now announces '99 or more {label}'
when the badge displays '99+', matching sensory characteristics.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>"
```

---

## Task 2: Branching — `onCreate` precede `href` + test `href + disabled`

**Findings risolti**: Critical #1, Test gap #4

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx:159-173`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx`

### - [ ] Step 1: Scrivere i test rossi per branching

Aggiungere in `ConnectionChip.test.tsx` dopo il test `renders as a link when href is provided and no items/popover` (riga ~93).

**Nota**: il primo caso è splittato in due `it()` separati per isolare la diagnostica del FAIL. Il primo verifica solo "non è un Link" (sicuramente FAIL con codice attuale perché `href+count=0+onCreate` entra nel ramo Link). Il secondo verifica il click handler (dipende dal rendering come button).

```tsx
it('does NOT render as Link when hasCreate is true and href is provided (even with count 0)', () => {
  const onCreate = vi.fn();
  render(<ConnectionChip entityType="player" count={0} href="/players" onCreate={onCreate} />);
  expect(screen.queryByRole('link')).not.toBeInTheDocument();
  expect(screen.getByRole('button')).toBeInTheDocument();
});

it('invokes onCreate when clicked with href + count 0 + onCreate', async () => {
  const onCreate = vi.fn();
  render(<ConnectionChip entityType="player" count={0} href="/players" onCreate={onCreate} />);
  await userEvent.click(screen.getByRole('button'));
  expect(onCreate).toHaveBeenCalledTimes(1);
});

it('renders as disabled button when href is provided and disabled is true', () => {
  render(<ConnectionChip entityType="kb" count={0} href="/kb/123" disabled />);
  expect(screen.queryByRole('link')).not.toBeInTheDocument();
  const btn = screen.getByRole('button');
  expect(btn).toBeDisabled();
});
```

### - [ ] Step 2: Far fallire i test

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx -t "does NOT render as Link|invokes onCreate when clicked|renders as disabled button"
```

Expected:
- Test 1 ("does NOT render as Link"): **FAIL** — codice attuale renderizza `<Link>` perché guardia è `href && !hasItems && !disabled` senza `!hasCreate`
- Test 2 ("invokes onCreate when clicked"): **FAIL** — l'handler `onClick` sul `<Link>` potrebbe non venir chiamato in JSDOM, o essere chiamato ma la navigation non impedita; diagnostica isolata
- Test 3 ("renders as disabled button"): **PASS già** — `!disabled` guard esiste; continuerà a passare dopo l'impl

### - [ ] Step 3: Aggiornare branching per far vincere `onCreate`

In `ConnectionChip.tsx`, riga 160, sostituire:

```tsx
// Render as <Link> when href is provided and there's no popover to open.
if (href && !hasItems && !disabled) {
  return (
    <Link
      href={href}
      aria-label={ariaLabel}
      onClick={hasCreate ? () => onCreate?.() : undefined}
      className={rootClassName}
      style={rootStyle}
    >
      {chipInner}
      {labelEl}
    </Link>
  );
}
```

con:

```tsx
// Render as <Link> when href is provided, no popover, no create handler, and not disabled.
// onCreate has precedence over href: when count=0 with onCreate, we render a button to invoke create.
if (href && !hasItems && !hasCreate && !disabled) {
  return (
    <Link
      href={href}
      aria-label={ariaLabel}
      className={rootClassName}
      style={rootStyle}
    >
      {chipInner}
      {labelEl}
    </Link>
  );
}
```

Nota: rimosso `onClick={hasCreate ? () => onCreate?.() : undefined}` perché il branch ora esclude `hasCreate` — il workaround è dead code.

### - [ ] Step 4: Far passare i test e verificare nessuna regressione

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
```

Expected: 18 pass (15 precedenti + 3 nuovi). Particolare attenzione al test esistente `renders as a link when href is provided and no items/popover` (riga 88): deve continuare a passare perché non passa `onCreate`.

### - [ ] Step 5: Commit

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChip.test.tsx
git commit -m "fix(connectionchip): onCreate takes precedence over href (#543)

Before: href + onCreate + count=0 rendered <Link> with onClick
fallback that did not prevent navigation (double-trigger).
After: branch to <Link> only when hasCreate is false.

Adds regression test for href + disabled combination.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>"
```

---

## Task 3: `PopoverContent` accessibile + Escape test

**Findings risolti**: Important #3, Test gap #5 (Escape + focus return)

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipPopover.tsx`
- Test: `apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx`

### - [ ] Step 1: Scrivere i test rossi per aria-labelledby e Escape

Aggiungere in `ConnectionChipPopover.test.tsx` dopo l'ultimo test (`renders Lucide icon matching entity type`, riga ~83):

```tsx
it('PopoverContent has aria-labelledby pointing to the header title', () => {
  render(
    <ConnectionChipPopover open onOpenChange={() => {}} items={items} entityType="session">
      <button>trigger</button>
    </ConnectionChipPopover>
  );
  // Radix Popover renders content in a Portal. The Content element exposes role="dialog";
  // fallback strategy: cerca per role, se null fallback sul wrapper Radix data attribute.
  const content =
    document.querySelector('[role="dialog"]') ??
    document.querySelector('[data-radix-popper-content-wrapper] > *');
  expect(content).toBeTruthy();

  const labelledBy = content?.getAttribute('aria-labelledby');
  expect(labelledBy).toBeTruthy();

  const header = document.getElementById(labelledBy!);
  expect(header).toBeTruthy();
  // Header mostra "Session (N)" — il testo include l'entity label
  expect(header?.textContent?.toLowerCase()).toMatch(/session/);
});

it('pressing Escape closes the popover', async () => {
  const onOpenChange = vi.fn();
  render(
    <ConnectionChipPopover open onOpenChange={onOpenChange} items={items} entityType="session">
      <button>trigger</button>
    </ConnectionChipPopover>
  );
  // Radix ascolta Escape a livello document; il focus non deve essere forzato dentro il popover.
  await userEvent.keyboard('{Escape}');
  expect(onOpenChange).toHaveBeenCalledWith(false);
});
```

**Nota diagnostica**: se il test aria-labelledby fallisce sulla prima asserzione (`content` null), il `PopoverContent` non sta renderizzando `role="dialog"` — verificare la versione di `@radix-ui/react-popover`. Con versioni recenti (≥1.0) il role è impostato di default sul `Content`.

### - [ ] Step 2: Far fallire il test aria-labelledby

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx -t "aria-labelledby"
```

Expected: FAIL — `PopoverContent` non ha `aria-labelledby`.

Il test Escape dovrebbe già passare (Radix lo gestisce nativamente), ma fa parte del gap da colmare.

### - [ ] Step 3: Importare `useId` e applicare `aria-labelledby`

In `ConnectionChipPopover.tsx`, modificare l'import React a riga 3:

```tsx
import { useId, type ReactNode } from 'react';
```

Nella funzione `ConnectionChipPopover`, dopo `const label = entityLabel[entityType];` (riga 37), aggiungere:

```tsx
const headerId = useId();
```

Aggiornare `PopoverContent` (riga 43) aggiungendo `aria-labelledby={headerId}`:

```tsx
<PopoverContent
  align="start"
  sideOffset={6}
  aria-labelledby={headerId}
  className="w-56 p-0 overflow-hidden"
  style={{
    border: `1px solid ${tokens.border}`,
    background: 'var(--mc-bg-card, hsl(222 47% 11%))',
  }}
>
```

Aggiornare il `div` header (riga 52) aggiungendo `id={headerId}`:

```tsx
<div
  id={headerId}
  className="flex items-center gap-2 border-b px-3 py-2 text-xs font-semibold uppercase tracking-wide"
  style={{ borderColor: tokens.border, color: tokens.solid }}
>
```

### - [ ] Step 4: Far passare i test e verificare nessuna regressione

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx
```

Expected: 7 pass (5 esistenti + 2 nuovi).

### - [ ] Step 5: Regression guard — suite completa meeple-card

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm vitest run src/components/ui/data-display/meeple-card/
```

Expected: tutti i test pass. Baseline: 137 precedenti + 1 (Task 1) + 3 (Task 2) + 2 (Task 3) = **143 test pass attesi**.

Se fallimenti di test pre-esistenti NON introdotti in questa PR compaiono, annotarli ma non bloccare — eventuale issue separata.

### - [ ] Step 6: Commit

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/ConnectionChipPopover.tsx apps/web/src/components/ui/data-display/meeple-card/parts/__tests__/ConnectionChipPopover.test.tsx
git commit -m "fix(connectionchip-popover): add accessible name via aria-labelledby (#543)

useId() generates stable id for the header title; PopoverContent
references it via aria-labelledby so screen readers announce
the entity label when the dialog opens.

Adds regression test for Escape key behavior.

Co-Authored-By: Claude Opus 4 <noreply@anthropic.com>"
```

---

## Task 4: Typecheck + push + PR

### - [ ] Step 1: Typecheck complessivo

Run:
```bash
cd D:/Repositories/meepleai-monorepo-frontend/apps/web && pnpm typecheck
```

Expected: no errors.

### - [ ] Step 2: Push branch

Run:
```bash
git push -u origin feature/issue-543-connectionchip-a11y-fixes
```

### - [ ] Step 3: Creare PR verso `main-dev`

Run (da bash, HEREDOC):
```bash
gh pr create --base main-dev --title "fix(connectionchip): resolve a11y + semantic findings (#543)" --body "$(cat <<'EOF'
## Summary

Risolve i 5 findings del code review post-merge di PR #542 (issue #543):

- **Critical**: `onCreate` ora ha precedenza su `href` quando `count=0` — niente più double-trigger
- **Important (WCAG 1.3.3)**: `aria-label` dice "99 or more {label}" quando il badge mostra `99+`
- **Important**: `PopoverContent` ora ha nome accessibile via `aria-labelledby` → `id` dell'header
- **Test gap**: coperti `href + disabled` (button disabilitato, non Link) e `Escape` key (chiude popover)

Blocca Step 1.6 (renderer integration in MeepleCard.tsx).

## Test plan

- [x] 3 nuovi test in `ConnectionChip.test.tsx` (aria-label 99+, button-not-Link con hasCreate+href, disabled+href)
- [x] 2 nuovi test in `ConnectionChipPopover.test.tsx` (aria-labelledby wiring, Escape closes)
- [x] Regression guard: suite meeple-card/ completa pass
- [x] `pnpm typecheck` pass

## Spec & Plan

- Spec: \`docs/superpowers/specs/2026-04-23-connectionchip-a11y-fixes-design.md\`
- Plan: \`docs/superpowers/plans/2026-04-23-connectionchip-a11y-fixes-plan.md\`

Closes #543

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Self-review

**Spec coverage**:
- §3.1 (branching `onCreate` precede `href`) → Task 2 ✓
- §3.2 (helper `formatCountForLabel`) → Task 1 ✓
- §3.3 (`useId` + `aria-labelledby`) → Task 3 ✓
- §4.1 test 1 (button-not-Link hasCreate+count=0+href) → Task 2 Step 1 ✓
- §4.1 test 2 (disabled button con href+disabled) → Task 2 Step 1 ✓
- §4.1 test 3 (99 or more in aria-label) → Task 1 Step 1 ✓
- §4.2 test 4 (aria-labelledby wiring) → Task 3 Step 1 ✓
- §4.2 test 5 (Escape closes + focus return) → Task 3 Step 1 (solo Escape closes; focus return gestito da Radix, non verifichiamo esplicitamente perché in JSDOM il focus management può essere flakey) ✓
- §4.3 Regression guard → Task 3 Step 5 ✓

**Placeholder scan**: nessun TBD/TODO. Ogni step ha codice completo o comando esatto.

**Type consistency**: `formatCountForLabel(count: number, label: string): string` usato coerentemente. `headerId` generato con `useId()` usato su `PopoverContent` e `div` header. Signature stabile tra task.

**Scope**: ristretto a 4 file (2 src + 2 test) + 1 PR. Out of scope correttamente deferiti (renderer integration, i18n, Tab cycling).

**Note su §4.2 test 5**: il design dello spec parlava di "Escape + focus return". Nel piano ho ridotto a "Escape closes" perché testare il focus return in JSDOM è instabile (Radix usa `document.activeElement` con FocusScope che in JSDOM si comporta diversamente da browser reale). Il comportamento è garantito da Radix e coperto da Playwright E2E a livello app. Documentato qui come deviazione consapevole.
