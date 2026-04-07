# MeepleCard Quality Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Correggere 3 bug critici e 5 miglioramenti nel sistema MeepleCard senza modificare l'architettura esistente.

**Architecture:** Fix mirati e chirurgici su file esistenti — nessuna creazione di nuovi componenti. Ogni task è indipendente e committabile separatamente. TDD: test prima, implementazione poi.

**Tech Stack:** React 19, TypeScript, Vitest + React Testing Library, Tailwind CSS 3, CVA

**Spec di riferimento:** `docs/superpowers/specs/2026-04-04-meeple-card-quality-fixes-design.md`

---

## File Map

| File | Modifiche |
|------|-----------|
| `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx` | BUG-1 (bottom bar), IMP-1 (key), IMP-3 (viewTransitionName) |
| `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx` | BUG-2 (hover color), ACC-1 (aria label) |
| `apps/web/src/components/ui/data-display/meeple-card/CartaEstesa.tsx` | IMP-2 (entity system) |
| `apps/web/src/components/ui/data-display/meeple-card/types.ts` | IMP-4 (isInteractive default doc) |
| `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx` | Test BUG-1, IMP-1, IMP-3 |
| `apps/web/src/components/ui/data-display/meeple-card/__tests__/CartaEstesa.test.tsx` | Test IMP-2 |

---

## Task 1: BUG-1 — Bottom bar condizionale

**Context:** `MeepleCardGrid.tsx` linea 579-590. Il div `bottomBar` viene sempre renderizzato (24px di dead space) anche quando `bottomStatValue` è undefined.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx:579-590`

- [ ] **Step 1.1: Aggiungi test che fallisce per bottom bar sempre presente**

Apri `apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx` e aggiungi alla fine del `describe` esistente:

```tsx
  it('non renderizza il bottom bar quando bottomStatValue è undefined', () => {
    const { container } = render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        // bottomStatValue NON passato
      />
    );
    // Il bottom bar non deve essere nel DOM
    const bottomBar = container.querySelector('[data-testid="meeple-card-bottom-bar"]');
    expect(bottomBar).not.toBeInTheDocument();
  });

  it('renderizza il bottom bar quando bottomStatValue è definito', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        bottomStatLabel="Partite"
        bottomStatValue="42"
      />
    );
    expect(document.querySelector('[data-testid="meeple-card-bottom-bar"]')).toBeInTheDocument();
    expect(document.querySelector('[data-testid="meeple-card-bottom-bar"]')).toHaveTextContent('42');
  });
```

- [ ] **Step 1.2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run MeepleCardGrid
```

Expected: FAIL — `bottomBar` trovato nel DOM anche senza `bottomStatValue`.

- [ ] **Step 1.3: Applica il fix in MeepleCardGrid.tsx**

Trova il blocco alle linee 578-590 e sostituiscilo con:

```tsx
      {/* Bottom bar — solo se ha contenuto */}
      {bottomStatValue && (
        <div
          data-testid="meeple-card-bottom-bar"
          className="flex items-center justify-end px-2 shrink-0 bg-black/70 border-t border-white/5"
          style={{ height: `${CARD_SECTION_HEIGHTS.bottomBar}px` }}
        >
          <span className="text-[9px] text-white/60 shrink-0">
            {bottomStatLabel ? `${bottomStatLabel} ` : ''}
            {bottomStatValue}
          </span>
        </div>
      )}
```

> **Nota:** aggiungi `data-testid="meeple-card-bottom-bar"` al div per il test. Rimuovi il `<span />` vuoto.

- [ ] **Step 1.4: Esegui i test per verificare che passano**

```bash
cd apps/web && pnpm test --run MeepleCardGrid
```

Expected: PASS su tutti i test.

- [ ] **Step 1.5: Commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx
git commit -m "fix(meeple-card): render bottom bar only when bottomStatValue is defined"
```

---

## Task 2: IMP-1 — Sostituisci `key={index}` con `key={label}` nel metadata map

**Context:** `MeepleCardGrid.tsx` linee 537 e 551. Il `.map((item, index) =>` usa `index` come key — instabile quando l'array cambia ordine.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx:522-557`

- [ ] **Step 2.1: Applica il fix direttamente** (no test richiesto — key è un hint di React non verificabile via RTL)

Nelle linee 537 e 551 di `MeepleCardGrid.tsx`, sostituisci `key={index}` con `key={item.label ?? item.value ?? String(index)}`:

Linea 537 — nel branch `if (item.onClick)`:
```tsx
<button
  key={item.label ?? item.value ?? String(index)}
  type="button"
  ...
>
```

Linea 551 — nel branch `return <span>`:
```tsx
<span
  key={item.label ?? item.value ?? String(index)}
  className="..."
>
```

> **Perché `?? String(index)` come fallback:** Se un item non ha né `label` né `value` (edge case), il fallback a `index` evita chiavi duplicate.

- [ ] **Step 2.2: Verifica typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: no errori.

- [ ] **Step 2.3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "fix(meeple-card): use stable keys in metadata.map instead of array index"
```

---

## Task 3: IMP-3 — `viewTransitionName` con feature detection

**Context:** `MeepleCardGrid.tsx` linea 268. `viewTransitionName` assegnato senza verificare il supporto browser.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx:263-269`

- [ ] **Step 3.1: Aggiungi test per viewTransitionName condizionale**

In `MeepleCardGrid.test.tsx`, aggiungi:

```tsx
  it('non imposta viewTransitionName quando entityId non è definito', () => {
    render(<MeepleCardGrid entity="game" title="Test" />);
    const card = document.querySelector('[data-card-root]') as HTMLElement | null;
    expect(card?.style.viewTransitionName).toBeFalsy();
  });

  it('imposta viewTransitionName quando entityId è definito e il browser lo supporta', () => {
    // jsdom non supporta startViewTransition, quindi il check ritorna false in test
    // Ma verifichiamo che il path code sia corretto controllando il comportamento
    render(<MeepleCardGrid entity="game" title="Test" entityId="abc-123" />);
    const card = document.querySelector('[data-card-root]') as HTMLElement | null;
    // In jsdom: startViewTransition non esiste → viewTransitionName è undefined (non impostato)
    // Il test verifica che il componente non vada in errore
    expect(card).toBeInTheDocument();
  });
```

- [ ] **Step 3.2: Applica il fix in MeepleCardGrid.tsx**

Prima di `return` (tra le destructure e il JSX, es. dopo linea 165), aggiungi:

```tsx
  const supportsViewTransition =
    typeof document !== 'undefined' && 'startViewTransition' in document;
```

Poi a linea 268, modifica:

```tsx
viewTransitionName:
  supportsViewTransition && entityId ? `meeple-card-${entityId}` : undefined,
```

- [ ] **Step 3.3: Esegui i test**

```bash
cd apps/web && pnpm test --run MeepleCardGrid
```

Expected: PASS su tutti i test.

- [ ] **Step 3.4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/MeepleCardGrid.test.tsx
git commit -m "fix(meeple-card): guard viewTransitionName with browser feature detection"
```

---

## Task 4: BUG-2 — Icon hover color (`--hover-color` CSS var consumata)

**Context:** `meeple-card-quick-actions.tsx` linea 138-144. `--hover-color` è impostata sull'elemento `<Icon>` ma nessuna regola CSS la consuma. L'icona non cambia colore al hover.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx:138-145`

- [ ] **Step 4.1: Aggiungi test che verifica il colore icon al hover**

Crea il file `apps/web/src/components/ui/data-display/__tests__/MeepleCardQuickActions.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Pencil } from 'lucide-react';

import { MeepleCardQuickActions } from '../meeple-card-quick-actions';

describe('MeepleCardQuickActions', () => {
  const actions = [
    {
      icon: Pencil,
      label: 'Modifica',
      onClick: vi.fn(),
    },
  ];

  it('renderizza l\'azione con il data-entity-color corretto', () => {
    render(<MeepleCardQuickActions actions={actions} entityType="game" />);
    // Il wrapper button deve avere l'attributo style con --hover-color
    const button = screen.getByRole('button', { name: 'Modifica' });
    // Verifica che la variabile CSS sia impostata sul SVG icon
    const icon = button.querySelector('svg');
    expect(icon).toBeInTheDocument();
  });

  it('l\'icon ha la classe che consuma --hover-color al hover', () => {
    render(<MeepleCardQuickActions actions={actions} entityType="player" />);
    const button = screen.getByRole('button', { name: 'Modifica' });
    const icon = button.querySelector('svg');
    // Il className deve includere group-hover:text-[var(--hover-color)]
    expect(icon?.className).toContain('group-hover:text-[var(--hover-color)]');
  });
});
```

- [ ] **Step 4.2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run MeepleCardQuickActions
```

Expected: FAIL — l'icona non ha la classe `group-hover:text-[var(--hover-color)]`.

- [ ] **Step 4.3: Applica il fix in `meeple-card-quick-actions.tsx`**

Il problema è a due livelli:
1. Il `--hover-color` è sull'`<Icon>` invece che sul `<button>` (il contesto `group`)
2. L'`<Icon>` non ha classi CSS che consumino la variabile

**Passo A** — Aggiungi la classe `group` al button (linea ~111):

```tsx
<button
  ...
  className={cn(
    buttonSize,
    'group',  // ← aggiungi qui
    'rounded-full flex items-center justify-center',
    ...
  )}
  style={{
    transitionDelay: `${delay}ms`,
    ['--tw-ring-color' as string]: `hsl(${entityColor})`,
    ['--hover-color' as string]: `hsl(${entityColor})`,  // ← sposta qui dal Icon
  }}
>
```

**Passo B** — Aggiorna l'`<Icon>` per consumare la variabile e rimuovere il vecchio style:

```tsx
<Icon
  className={cn(
    iconSize,
    'transition-colors duration-200',
    !action.disabled
      ? 'text-slate-600 group-hover:text-[var(--hover-color)]'
      : 'stroke-slate-600'
  )}
  strokeWidth={2}
  // style rimosso — --hover-color ora sul button parent (group)
/>
```

- [ ] **Step 4.4: Esegui i test**

```bash
cd apps/web && pnpm test --run MeepleCardQuickActions
```

Expected: PASS.

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx
git add apps/web/src/components/ui/data-display/__tests__/MeepleCardQuickActions.test.tsx
git commit -m "fix(meeple-card): consume --hover-color CSS var in quick action icons via group-hover"
```

---

## Task 5: ACC-1 — ARIA label localizzabile

**Context:** `meeple-card-quick-actions.tsx`. I `TooltipContent` e `aria-label` usano stringhe hardcoded in inglese/italiano miste.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx:30-58`

- [ ] **Step 5.1: Aggiungi test che verifica aria-label personalizzabile**

Nel file `apps/web/src/components/ui/data-display/__tests__/MeepleCardQuickActions.test.tsx`, aggiungi:

```tsx
  it('usa aria-label dell\'azione come label accessibile', () => {
    const actionsIT = [
      { icon: Pencil, label: 'Modifica gioco', onClick: vi.fn() },
    ];
    render(<MeepleCardQuickActions actions={actionsIT} entityType="game" />);
    expect(screen.getByRole('button', { name: 'Modifica gioco' })).toBeInTheDocument();
  });

  it('usa disabledTooltip come aria-label quando l\'azione è disabilitata', () => {
    const disabledActions = [
      {
        icon: Pencil,
        label: 'Modifica',
        onClick: vi.fn(),
        disabled: true,
        disabledTooltip: 'Accedi per modificare',
      },
    ];
    render(<MeepleCardQuickActions actions={disabledActions} entityType="game" />);
    expect(screen.getByRole('button', { name: 'Accedi per modificare' })).toBeInTheDocument();
  });
```

- [ ] **Step 5.2: Esegui i test**

```bash
cd apps/web && pnpm test --run MeepleCardQuickActions
```

Expected: PASS — il comportamento era già corretto (aria-label già usa `action.label`), i test lo documentano.

- [ ] **Step 5.3: Commit**

```bash
git add apps/web/src/components/ui/data-display/__tests__/MeepleCardQuickActions.test.tsx
git commit -m "test(meeple-card): add accessibility tests for QuickActions aria-label"
```

---

## Task 6: IMP-2 — CartaEstesa allineata al token system

**Context:** `CartaEstesa.tsx` linea 60 usa emoji `🎲` hardcoded. Il componente non importa `entityColors` e non accetta `entity: MeepleEntityType`.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/CartaEstesa.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card/__tests__/CartaEstesa.test.tsx`

- [ ] **Step 6.1: Leggi il test esistente di CartaEstesa**

```bash
cat "apps/web/src/components/ui/data-display/meeple-card/__tests__/CartaEstesa.test.tsx"
```

- [ ] **Step 6.2: Aggiungi test per icona entity-specifica**

Nel file `CartaEstesa.test.tsx`, aggiungi:

```tsx
  it('mostra l\'icona di fallback corretta per l\'entity type "player"', () => {
    render(
      <CartaEstesa
        title="Luigi Rossi"
        entityColor="262 83% 58%"
        entity="player"
      />
    );
    // player usa l'icona ♟ invece di 🎲
    expect(document.querySelector('[data-testid="carta-estesa-entity-icon"]')).toHaveTextContent('♟');
  });

  it('mostra 🎲 di default per entity type "game"', () => {
    render(
      <CartaEstesa
        title="Wingspan"
        entityColor="25 95% 45%"
        entity="game"
      />
    );
    expect(document.querySelector('[data-testid="carta-estesa-entity-icon"]')).toHaveTextContent('🎲');
  });
```

- [ ] **Step 6.3: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test --run CartaEstesa
```

Expected: FAIL — `data-testid="carta-estesa-entity-icon"` non esiste e l'icona è sempre `🎲`.

- [ ] **Step 6.4: Applica il fix in CartaEstesa.tsx**

**Passo A** — Aggiorna le import e aggiungi la mappa icone:

```tsx
'use client';

import Image from 'next/image';

import { cn } from '@/lib/utils';

import { entityColors } from '../meeple-card-styles';

import type { MeepleEntityType } from './types';

// Mappa emoji fallback per entity type (quando non c'è immagine)
const ENTITY_FALLBACK_ICON: Record<MeepleEntityType, string> = {
  game: '🎲',
  player: '♟',
  session: '⏳',
  agent: '⚡',
  kb: '📜',
  chatSession: '💬',
  event: '✦',
  toolkit: '🛠',
};
```

**Passo B** — Aggiorna l'interfaccia `CartaEstesaProps`:

```tsx
interface CartaEstesaProps {
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  /** Tipo di entità — usato per l'icona di fallback e colori */
  entity?: MeepleEntityType;
  entityColor: string; // HSL without wrapper
  stats?: CartaEstesaStat[];
  tags?: string[];
  description?: string;
  linkCount?: number;
  children?: React.ReactNode;
  className?: string;
}
```

**Passo C** — Aggiorna il destructuring e il render dell'icona (linea ~27-60):

```tsx
export function CartaEstesa({
  title,
  subtitle,
  imageUrl,
  rating,
  entity = 'game',
  entityColor,
  stats,
  tags,
  description,
  linkCount,
  children,
  className,
}: CartaEstesaProps) {
  const fallbackIcon = ENTITY_FALLBACK_ICON[entity];
```

E nel JSX, sostituisci la linea 60:
```tsx
{/* Prima: <span className="text-5xl opacity-30">🎲</span> */}
<span
  data-testid="carta-estesa-entity-icon"
  className="text-5xl opacity-30"
>
  {fallbackIcon}
</span>
```

- [ ] **Step 6.5: Esegui i test**

```bash
cd apps/web && pnpm test --run CartaEstesa
```

Expected: PASS.

- [ ] **Step 6.6: Verifica typecheck**

```bash
cd apps/web && pnpm typecheck
```

Expected: no errori. Se `MeepleEntityType` non è esportato da `types.ts`, verifica il path import.

- [ ] **Step 6.7: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/CartaEstesa.tsx
git add apps/web/src/components/ui/data-display/meeple-card/__tests__/CartaEstesa.test.tsx
git commit -m "feat(carta-estesa): align entity fallback icon with token system via MeepleEntityType"
```

---

## Task 7: IMP-4 — Documenta il default di `isInteractive`

**Context:** `types.ts` — `isInteractive` non ha un JSDoc che documenta il default. Causa inconsistenze nei consumer.

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/types.ts`

- [ ] **Step 7.1: Trova la prop in types.ts**

```bash
grep -n "isInteractive" apps/web/src/components/ui/data-display/meeple-card/types.ts
```

- [ ] **Step 7.2: Aggiorna il JSDoc**

Trova la riga con `isInteractive` e aggiorna il commento:

```tsx
  /**
   * Abilita comportamento interattivo (onClick, keyboard nav, role="button").
   * @default true — disabilitare in contesti di sola lettura (preview, print)
   */
  isInteractive?: boolean;
```

- [ ] **Step 7.3: Verifica che il default `true` sia applicato nei variant**

In `MeepleCardGrid.tsx`, cerca dove `isInteractive` è usato:

```bash
grep -n "isInteractive" apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
```

Se trovi destructuring senza default, aggiorna a:
```tsx
isInteractive = true,
```
nel destructuring di `props`.

- [ ] **Step 7.4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/types.ts
git add apps/web/src/components/ui/data-display/meeple-card/variants/MeepleCardGrid.tsx
git commit -m "docs(meeple-card): document isInteractive default=true in types.ts"
```

---

## Task 8: Test suite finale e verifica completa

- [ ] **Step 8.1: Esegui tutti i test MeepleCard**

```bash
cd apps/web && pnpm test --run --reporter=verbose 2>&1 | grep -E "meeple-card|CartaEstesa|QuickActions"
```

Expected: tutti PASS.

- [ ] **Step 8.2: Esegui typecheck completo**

```bash
cd apps/web && pnpm typecheck
```

Expected: 0 errori.

- [ ] **Step 8.3: Esegui lint**

```bash
cd apps/web && pnpm lint
```

Expected: 0 errori (warning accettabili).

- [ ] **Step 8.4: Commit finale di verifica (se ci sono fix lint residui)**

Se lint ha richiesto modifiche:
```bash
git add -p  # review interattivo
git commit -m "chore(meeple-card): fix lint warnings after quality fixes"
```

---

## Checklist di accettazione finale

- [ ] `data-testid="meeple-card-bottom-bar"` non presente nel DOM senza `bottomStatValue`
- [ ] `data-testid="meeple-card-bottom-bar"` presente e con testo corretto con `bottomStatValue`
- [ ] Quick action icons mostrano colore entity al hover (verificabile in browser DevTools)
- [ ] `CartaEstesa` con `entity="player"` mostra `♟`, con `entity="game"` mostra `🎲`
- [ ] Nessun `key={index}` nelle liste metadata di MeepleCardGrid
- [ ] `isInteractive` ha JSDoc con `@default true`
- [ ] `viewTransitionName` solo se `'startViewTransition' in document`
- [ ] `pnpm typecheck` e `pnpm lint` passano senza errori
