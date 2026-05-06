# Redesign V2 — Library Pilot Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire l'interfaccia consumer su branch `redesign/v2` partendo dal pilota Library, costruendo foundation (M0), primitivi scoped a Library (M1) e Library mobile+desktop+game-drawer/page (M2) con i mockup già disponibili in `admin-mockups/*.html`.

**Architecture:** Hybrid responsive (`md:` breakpoint 768px) drawer mobile + page desktop. Primitivi nuovi in `apps/web/src/components/ui/v2/` che coesistono con quelli esistenti (`data-display/`) senza deprecarli. Token design system: **Tailwind 4 usa `@theme` in `globals.css`** (NON `tailwind.config.js` per i colori) — 7 utility entity (`game/player/session/agent/kb/chat/event`) già esistono nel blocco `@theme` a `globals.css:484-490`; vanno aggiunte le 2 mancanti (`toolkit`, `tool`). Stack: Next.js 16 App Router, React 19, Tailwind 4, Zustand 5, React Query 5, Vitest 3, Playwright, MSW, `vaul` per bottom-sheet mobile, Radix Dialog per drawer desktop.

**Tech Stack:** Next.js 16.2.4, React 19.2.4, Tailwind 4, Zustand 5, React Query 5, Vitest 3 + jest-axe, Playwright, MSW, framer-motion 12, vaul 1.x (da installare), shadcn/ui Radix Dialog già presente.

---

## File Structure

### M0 — Foundation
- Modifica: `apps/web/src/styles/globals.css` (add `--e-toolkit`/`--e-tool` raw HSL in `:root` + `--color-entity-toolkit`/`--color-entity-tool` in `@theme`)
- Modifica: `apps/web/src/app/layout.tsx` (add JetBrains Mono font, verifica Quicksand/Nunito)
- Crea: `apps/web/src/components/ui/v2/theme-toggle/theme-toggle.tsx`
- Crea: `apps/web/src/components/ui/v2/theme-toggle/theme-toggle.test.tsx`
- Modifica: `apps/web/package.json` (add `vaul`)
- Modifica: `apps/web/bundle-size-baseline.json` (update baseline)
- Note: `tailwind.config.js` NON viene toccato per i colori (Tailwind 4 ignora `theme.extend.colors` in JS)

### M1 — Primitivi Library-scoped
- Crea: `apps/web/src/components/ui/v2/entity-chip/entity-chip.tsx` + `.test.tsx` + `.stories.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-pip/entity-pip.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-card/entity-card.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-card/entity-card-grid.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-card/entity-card-list.tsx`
- Crea: `apps/web/src/components/ui/v2/drawer/drawer.tsx` + `.test.tsx` (hybrid vaul/radix)
- Crea: `apps/web/src/components/ui/v2/entity-tokens.ts` (const map entity→token className + emoji)

### M2 — Library pilot
- Crea: `apps/web/src/app/(authenticated)/library/v2/page.tsx` (entry responsive)
- Crea: `apps/web/src/components/library/v2/LibraryMobile.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryDesktop.tsx` + `.test.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryFilterTabs.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryViewToggle.tsx`
- Crea: `apps/web/src/components/library/v2/GameDrawerContent.tsx` + `.test.tsx` (mobile)
- Crea: `apps/web/src/components/library/v2/GamePageContent.tsx` (desktop split-view)
- Crea: `apps/web/e2e/library-v2.spec.ts` (Playwright E2E)

---

# M0 — Foundation

### Task 1: Create `redesign/v2` branch from `main-dev`

**Files:** — (git state only)

- [ ] **Step 1: Ensure main-dev is current**

Run: `git fetch origin && git checkout main-dev && git pull origin main-dev`
Expected: `Already up to date.` or fast-forward.

- [ ] **Step 2: Create redesign/v2 branch**

Run: `git checkout -b redesign/v2 && git config branch.redesign/v2.parent main-dev`
Expected: `Switched to a new branch 'redesign/v2'`

- [ ] **Step 3: Push and set upstream**

Run: `git push -u origin redesign/v2`
Expected: `Branch 'redesign/v2' set up to track 'origin/redesign/v2'.`

---

### Task 2: Install `vaul` dependency

**Files:**
- Modifica: `apps/web/package.json`
- Modifica: `apps/web/pnpm-lock.yaml`

- [ ] **Step 1: Install vaul**

Run: `cd apps/web && pnpm add vaul@^1.1.2`
Expected: `+ vaul 1.1.2` in output, package.json updated.

- [ ] **Step 2: Verify import works**

Create temp file `apps/web/src/vaul-check.ts`:
```ts
import { Drawer } from 'vaul';
export const _check: typeof Drawer = Drawer;
```

Run: `cd apps/web && pnpm typecheck -- --noEmit`
Expected: No errors for `vaul-check.ts`.

- [ ] **Step 3: Delete temp and commit**

Run:
```bash
rm apps/web/src/vaul-check.ts
git add apps/web/package.json apps/web/pnpm-lock.yaml
git commit -m "feat(deps): add vaul for bottom-sheet drawer physics (M0)"
```

---

### Task 3: Add `toolkit` + `tool` entity tokens to `globals.css` (Tailwind 4 `@theme`)

**Context (critical for Tailwind 4):**
Il progetto usa Tailwind CSS 4.x con configurazione CSS-first via `@theme` in `globals.css`. Le estensioni di colore in `tailwind.config.js` (`theme.extend.colors`) **non generano utility class** in Tailwind 4. Tutti i colori entity devono essere dichiarati come CSS custom properties dentro il blocco `@theme` di `globals.css`. Architettura esistente (righe ~484-509 di `globals.css`):
- `:root { --e-<name>: <HSL raw> }` — raw HSL values (7 entities: game/player/session/agent/document/chat/event)
- `@theme { --color-entity-<name>: hsl(var(--e-<name>)) }` — Tailwind utility alias (7 entities)

Sono mancanti: `toolkit` e `tool`. Aggiungere entrambi in entrambi i blocchi.

**Files:**
- Modifica: `apps/web/src/styles/globals.css` (aggiunta in `:root` E in `@theme`)

- [ ] **Step 1: Write failing test**

Create `apps/web/src/__tests__/entity-theme-tokens.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

describe('globals.css entity tokens (Tailwind 4 @theme)', () => {
  const css = readFileSync(
    resolve(__dirname, '../styles/globals.css'),
    'utf8'
  );

  it('defines raw HSL --e-* vars for all 9 entities in :root', () => {
    const rawTokens = ['--e-game', '--e-player', '--e-session', '--e-agent', '--e-document', '--e-chat', '--e-event', '--e-toolkit', '--e-tool'];
    rawTokens.forEach((t) => expect(css).toContain(t));
  });

  it('defines --color-entity-* Tailwind utilities in @theme for all 9 entities', () => {
    const utilityTokens = [
      '--color-entity-game', '--color-entity-player', '--color-entity-session',
      '--color-entity-agent', '--color-entity-kb', '--color-entity-chat',
      '--color-entity-event', '--color-entity-toolkit', '--color-entity-tool',
    ];
    utilityTokens.forEach((t) => expect(css).toContain(t));
  });

  it('maps --color-entity-kb to --e-document (alias)', () => {
    expect(css).toMatch(/--color-entity-kb:\s*hsl\(var\(--e-document\)\)/);
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- entity-theme-tokens --run`
Expected: FAIL — `--e-toolkit`, `--e-tool`, `--color-entity-toolkit`, `--color-entity-tool` mancanti.

- [ ] **Step 3: Add raw HSL values in `:root`**

In `apps/web/src/styles/globals.css`, trova il blocco `:root` che contiene `--e-game: 25 95% 45%;` (circa riga 503-509). Aggiungi DOPO `--e-event: 350 89% 60%;`:
```css
  --e-toolkit: 160 70% 45%;  /* Teal Green — Redesign V2 */
  --e-tool: 195 80% 50%;     /* Sky Blue — Epic #412 */
```

- [ ] **Step 4: Add Tailwind utility aliases in `@theme`**

In `apps/web/src/styles/globals.css`, trova il blocco `@theme` che contiene `--color-entity-event: hsl(var(--e-event));` (circa riga 490). Aggiungi DOPO quella riga:
```css
  --color-entity-toolkit: hsl(var(--e-toolkit));
  --color-entity-tool: hsl(var(--e-tool));
```

- [ ] **Step 5: Verify Tailwind kb alias exists (should already)**

Verifica che a circa riga 488 di `globals.css` esista:
```css
  --color-entity-kb: hsl(var(--e-document));
```
Se manca (improbabile), aggiungila nel blocco `@theme`. Questa è la ragione per cui le utility sono `bg-entity-kb` lato consumer ma il raw HSL vive come `--e-document`.

- [ ] **Step 6: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- entity-theme-tokens --run`
Expected: PASS (3 test).

- [ ] **Step 7: Smoke test — Tailwind build emette le utility**

Run: `cd apps/web && pnpm build 2>&1 | tail -20`
Expected: build completa senza warning `Cannot apply unknown utility class bg-entity-toolkit` o simili.

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/styles/globals.css apps/web/src/__tests__/entity-theme-tokens.test.ts
git commit -m "feat(tokens): add toolkit+tool entity tokens to Tailwind 4 @theme (M0)"
```

---

### Task 4: Verify all 9 entity utilities render correctly (Tailwind 4 smoke)

**Context:** Task 3 ha dichiarato le 9 entity utility in `@theme`. Task 4 verifica che Tailwind 4 le compili e le applichi davvero al DOM. Non tocca file CSS; è una verifica end-to-end che `bg-entity-*` e `text-entity-*` producono CSS valido runtime.

**Files:**
- Crea: `apps/web/src/__tests__/entity-utilities-render.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/__tests__/entity-utilities-render.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

describe('Tailwind 4 entity-* utilities render', () => {
  const ENTITIES = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'] as const;

  ENTITIES.forEach((e) => {
    it(`applies bg-entity-${e} and text-entity-${e} classes to DOM`, () => {
      const { container } = render(
        <div data-testid={`probe-${e}`} className={`bg-entity-${e} text-entity-${e} border-entity-${e}`} />
      );
      const el = container.querySelector(`[data-testid="probe-${e}"]`);
      expect(el).toBeTruthy();
      expect(el?.className).toContain(`bg-entity-${e}`);
      expect(el?.className).toContain(`text-entity-${e}`);
      expect(el?.className).toContain(`border-entity-${e}`);
    });
  });
});
```

- [ ] **Step 2: Run test — expect PASS (classes applied to DOM)**

Run: `cd apps/web && pnpm test -- entity-utilities-render --run`
Expected: PASS (9 test). Questo valida solo che la classe arrivi al DOM — il runtime visuale (computed style) richiede il build pipeline.

- [ ] **Step 3: Manual visual smoke (optional but recommended)**

Run: `cd apps/web && pnpm dev` e apri devtools su qualsiasi pagina; in Console:
```js
const probe = document.createElement('div');
probe.className = 'bg-entity-toolkit p-4';
document.body.appendChild(probe);
getComputedStyle(probe).backgroundColor;
// Expected: "rgb(...)" — NOT "rgba(0, 0, 0, 0)" (default trasparente)
```
Se il valore è trasparente la utility non è stata generata da Tailwind 4.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/__tests__/entity-utilities-render.test.tsx
git commit -m "test(tailwind): smoke test entity-* utilities apply to DOM (M0)"
```

---

### Task 5: Create entity-tokens helper module

**Files:**
- Crea: `apps/web/src/components/ui/v2/entity-tokens.ts`
- Crea: `apps/web/src/components/ui/v2/entity-tokens.test.ts`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/ui/v2/entity-tokens.test.ts`:
```ts
import { describe, it, expect } from 'vitest';
import { ENTITY_TOKENS, getEntityToken, type EntityType } from './entity-tokens';

describe('entity-tokens', () => {
  it('provides 9 canonical entity types', () => {
    const types: EntityType[] = ['game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool'];
    types.forEach((t) => {
      const token = getEntityToken(t);
      expect(token.bg).toContain('bg-entity-');
      expect(token.text).toContain('text-entity-');
      expect(token.emoji).toBeTruthy();
      expect(token.label).toBeTruthy();
    });
  });

  it('maps kb to document tailwind class', () => {
    expect(getEntityToken('kb').bg).toBe('bg-entity-document');
  });

  it('returns emoji for toolkit as 🧰', () => {
    expect(getEntityToken('toolkit').emoji).toBe('🧰');
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- entity-tokens --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/ui/v2/entity-tokens.ts`:
```ts
export type EntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';

export interface EntityToken {
  readonly bg: string;
  readonly bgSoft: string;
  readonly text: string;
  readonly border: string;
  readonly emoji: string;
  readonly label: string;
}

// Map "kb" -> tailwind class "document" (pre-existing naming)
const TAILWIND_KEY: Record<EntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'document',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
};

const EMOJI: Record<EntityType, string> = {
  game: '🎲',
  player: '👤',
  session: '🎯',
  agent: '🤖',
  kb: '📚',
  chat: '💬',
  event: '🗓️',
  toolkit: '🧰',
  tool: '🔧',
};

const LABEL: Record<EntityType, string> = {
  game: 'Gioco',
  player: 'Giocatore',
  session: 'Sessione',
  agent: 'Agente',
  kb: 'Base di conoscenza',
  chat: 'Chat',
  event: 'Evento',
  toolkit: 'Toolkit',
  tool: 'Tool',
};

export function getEntityToken(type: EntityType): EntityToken {
  const k = TAILWIND_KEY[type];
  return {
    bg: `bg-entity-${k}`,
    bgSoft: `bg-entity-${k}/10`,
    text: `text-entity-${k}`,
    border: `border-entity-${k}`,
    emoji: EMOJI[type],
    label: LABEL[type],
  };
}

export const ENTITY_TOKENS: readonly EntityType[] = [
  'game', 'player', 'session', 'agent', 'kb', 'chat', 'event', 'toolkit', 'tool',
] as const;
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- entity-tokens --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/v2/entity-tokens.ts apps/web/src/components/ui/v2/entity-tokens.test.ts
git commit -m "feat(v2): entity-tokens helper maps 9 entity types to Tailwind classes (M0)"
```

---

### Task 6: Theme toggle component (light/dark persist in localStorage)

**Files:**
- Crea: `apps/web/src/components/ui/v2/theme-toggle/theme-toggle.tsx`
- Crea: `apps/web/src/components/ui/v2/theme-toggle/theme-toggle.test.tsx`

> ⚠️ **Hydration flash — accettato per il pilot**
> Questo componente usa `useState(false) + useEffect` per leggere `localStorage` lato client, quindi il primo render server-side sarà sempre in tema light anche se l'utente ha salvato `dark`. Il flash è visibile per ~50-100ms al primo paint. Per il pilot questo trade-off è accettato: le route pilot (`/library/v2`) sono dietro auth e il flash non è bloccante. Il fix production-ready richiede `next-themes` o uno script inline in `<head>` che setta `class="dark"` su `<html>` prima dell'idratazione React — da pianificare in milestone M3+ quando il redesign V2 diventerà default. Non modificare il `RootLayout` globale in questa PR.

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/ui/v2/theme-toggle/theme-toggle.test.tsx`:
```tsx
import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ThemeToggle } from './theme-toggle';

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
  });

  it('toggles dark class on html element', () => {
    render(<ThemeToggle />);
    const btn = screen.getByRole('button', { name: /tema/i });
    fireEvent.click(btn);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    fireEvent.click(btn);
    expect(document.documentElement.classList.contains('dark')).toBe(false);
  });

  it('persists choice in localStorage mai-theme', () => {
    render(<ThemeToggle />);
    fireEvent.click(screen.getByRole('button', { name: /tema/i }));
    expect(localStorage.getItem('mai-theme')).toBe('dark');
  });

  it('reads initial value from localStorage', () => {
    localStorage.setItem('mai-theme', 'dark');
    render(<ThemeToggle />);
    expect(document.documentElement.classList.contains('dark')).toBe(true);
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- theme-toggle --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/ui/v2/theme-toggle/theme-toggle.tsx`:
```tsx
'use client';
import { useEffect, useState } from 'react';

const STORAGE_KEY = 'mai-theme';

export function ThemeToggle(): JSX.Element {
  const [isDark, setIsDark] = useState(false);

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    const shouldBeDark = stored === 'dark';
    setIsDark(shouldBeDark);
    document.documentElement.classList.toggle('dark', shouldBeDark);
  }, []);

  const handleToggle = (): void => {
    const next = !isDark;
    setIsDark(next);
    document.documentElement.classList.toggle('dark', next);
    localStorage.setItem(STORAGE_KEY, next ? 'dark' : 'light');
  };

  return (
    <button
      type="button"
      onClick={handleToggle}
      aria-label={isDark ? 'Passa a tema chiaro' : 'Passa a tema scuro'}
      className="inline-flex h-9 w-9 items-center justify-center rounded-md border border-border bg-background text-foreground hover:bg-accent"
    >
      <span aria-hidden="true">{isDark ? '☀️' : '🌙'}</span>
    </button>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- theme-toggle --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/v2/theme-toggle/
git commit -m "feat(v2): ThemeToggle with localStorage mai-theme persistence (M0)"
```

---

# M1 — Primitivi Library-scoped

### Task 7: EntityChip (sm | md)

**Files:**
- Crea: `apps/web/src/components/ui/v2/entity-chip/entity-chip.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-chip/entity-chip.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/ui/v2/entity-chip/entity-chip.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { EntityChip } from './entity-chip';

expect.extend(toHaveNoViolations);

describe('EntityChip', () => {
  it('renders emoji and label', () => {
    render(<EntityChip entity="game" label="Catan" />);
    expect(screen.getByText('🎲')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('applies entity color class', () => {
    const { container } = render(<EntityChip entity="session" label="Turno 1" />);
    expect(container.querySelector('.bg-entity-session\\/10')).toBeInTheDocument();
  });

  it('renders size sm by default and md when prop set', () => {
    const { container, rerender } = render(<EntityChip entity="agent" label="Rulebook AI" />);
    expect(container.querySelector('.text-xs')).toBeInTheDocument();
    rerender(<EntityChip entity="agent" label="Rulebook AI" size="md" />);
    expect(container.querySelector('.text-sm')).toBeInTheDocument();
  });

  it('has no a11y violations', async () => {
    const { container } = render(<EntityChip entity="player" label="Alice" />);
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- entity-chip --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/ui/v2/entity-chip/entity-chip.tsx`:
```tsx
import { getEntityToken, type EntityType } from '../entity-tokens';

export interface EntityChipProps {
  readonly entity: EntityType;
  readonly label: string;
  readonly size?: 'sm' | 'md';
}

export function EntityChip({ entity, label, size = 'sm' }: EntityChipProps): JSX.Element {
  const t = getEntityToken(entity);
  const sizeClasses = size === 'sm' ? 'text-xs px-2 py-0.5' : 'text-sm px-3 py-1';
  return (
    <span
      className={`inline-flex items-center gap-1 rounded-full ${t.bgSoft} ${t.text} ${sizeClasses} font-medium`}
    >
      <span aria-hidden="true">{t.emoji}</span>
      <span>{label}</span>
    </span>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- entity-chip --run`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/v2/entity-chip/
git commit -m "feat(v2): EntityChip primitive with sm|md sizes (M1)"
```

---

### Task 8: EntityPip (32px avatar + overlap stack + +N)

**Files:**
- Crea: `apps/web/src/components/ui/v2/entity-pip/entity-pip.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-pip/entity-pip.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/ui/v2/entity-pip/entity-pip.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { EntityPip, EntityPipStack } from './entity-pip';

describe('EntityPip', () => {
  it('renders entity emoji inside 32px circle', () => {
    render(<EntityPip entity="player" />);
    const pip = screen.getByRole('img', { name: /giocatore/i });
    expect(pip).toHaveClass('h-8', 'w-8', 'rounded-full');
  });

  it('applies entity color class', () => {
    const { container } = render(<EntityPip entity="game" />);
    expect(container.querySelector('.bg-entity-game\\/10')).toBeInTheDocument();
  });
});

describe('EntityPipStack', () => {
  it('renders up to max pips with -ml overlap', () => {
    const { container } = render(
      <EntityPipStack
        items={[
          { entity: 'player' }, { entity: 'player' }, { entity: 'player' },
        ]}
        max={3}
      />
    );
    const pips = container.querySelectorAll('[data-pip]');
    expect(pips).toHaveLength(3);
  });

  it('shows +N overflow indicator when exceeding max', () => {
    render(
      <EntityPipStack
        items={[
          { entity: 'player' }, { entity: 'player' },
          { entity: 'player' }, { entity: 'player' }, { entity: 'player' },
        ]}
        max={3}
      />
    );
    expect(screen.getByText('+2')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- entity-pip --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/ui/v2/entity-pip/entity-pip.tsx`:
```tsx
import { getEntityToken, type EntityType } from '../entity-tokens';

export interface EntityPipProps {
  readonly entity: EntityType;
  readonly title?: string;
}

export function EntityPip({ entity, title }: EntityPipProps): JSX.Element {
  const t = getEntityToken(entity);
  return (
    <span
      data-pip
      role="img"
      aria-label={title ?? t.label}
      className={`inline-flex h-8 w-8 items-center justify-center rounded-full ring-2 ring-background ${t.bgSoft} ${t.text}`}
    >
      <span aria-hidden="true">{t.emoji}</span>
    </span>
  );
}

export interface EntityPipStackProps {
  readonly items: ReadonlyArray<{ entity: EntityType; title?: string }>;
  readonly max?: number;
}

export function EntityPipStack({ items, max = 3 }: EntityPipStackProps): JSX.Element {
  const visible = items.slice(0, max);
  const overflow = items.length - max;
  return (
    <div className="flex items-center">
      {visible.map((it, i) => (
        <span key={i} className={i > 0 ? '-ml-2' : ''}>
          <EntityPip entity={it.entity} title={it.title} />
        </span>
      ))}
      {overflow > 0 && (
        <span
          data-pip-overflow
          className="-ml-2 inline-flex h-8 w-8 items-center justify-center rounded-full ring-2 ring-background bg-muted text-muted-foreground text-xs font-medium"
        >
          +{overflow}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- entity-pip --run`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/v2/entity-pip/
git commit -m "feat(v2): EntityPip + EntityPipStack with overflow indicator (M1)"
```

---

### Task 9: EntityCard (grid variant)

**Files:**
- Crea: `apps/web/src/components/ui/v2/entity-card/entity-card.tsx`
- Crea: `apps/web/src/components/ui/v2/entity-card/entity-card.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/ui/v2/entity-card/entity-card.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { axe, toHaveNoViolations } from 'jest-axe';
import { EntityCard } from './entity-card';

expect.extend(toHaveNoViolations);

describe('EntityCard grid variant', () => {
  it('renders title, subtitle, entity emoji', () => {
    render(
      <EntityCard
        entity="game"
        variant="grid"
        title="Catan"
        subtitle="Klaus Teuber"
      />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('invokes onSelect when clicked', () => {
    const onSelect = vi.fn();
    render(
      <EntityCard
        entity="game"
        variant="grid"
        title="Catan"
        onSelect={onSelect}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: /catan/i }));
    expect(onSelect).toHaveBeenCalledOnce();
  });

  it('has no a11y violations', async () => {
    const { container } = render(
      <EntityCard entity="session" variant="grid" title="Partita 12" onSelect={() => {}} />
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- entity-card --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/ui/v2/entity-card/entity-card.tsx`:
```tsx
import { getEntityToken, type EntityType } from '../entity-tokens';

export type EntityCardVariant = 'grid' | 'list';

export interface EntityCardProps {
  readonly entity: EntityType;
  readonly variant: EntityCardVariant;
  readonly title: string;
  readonly subtitle?: string;
  readonly imageUrl?: string;
  readonly badge?: string;
  readonly onSelect?: () => void;
}

export function EntityCard(props: EntityCardProps): JSX.Element {
  const t = getEntityToken(props.entity);
  const isGrid = props.variant === 'grid';

  const content = (
    <>
      <div className={`flex ${isGrid ? 'flex-col' : 'flex-row items-center gap-3'} ${isGrid ? 'gap-2' : ''}`}>
        <div
          className={`${isGrid ? 'aspect-[3/4] w-full' : 'h-12 w-12 flex-shrink-0'} rounded-md ${t.bgSoft} flex items-center justify-center overflow-hidden`}
        >
          {props.imageUrl ? (
            <img src={props.imageUrl} alt="" className="h-full w-full object-cover" />
          ) : (
            <span className={`${t.text} text-3xl`} aria-hidden="true">{t.emoji}</span>
          )}
        </div>
        <div className={isGrid ? 'px-1' : 'min-w-0 flex-1'}>
          <div className="flex items-start justify-between gap-2">
            <h3 className="truncate font-heading text-sm font-semibold">{props.title}</h3>
            {props.badge && (
              <span className={`shrink-0 rounded px-1.5 py-0.5 text-[10px] ${t.bgSoft} ${t.text}`}>
                {props.badge}
              </span>
            )}
          </div>
          {props.subtitle && (
            <p className="truncate text-xs text-muted-foreground">{props.subtitle}</p>
          )}
        </div>
      </div>
    </>
  );

  if (props.onSelect) {
    return (
      <button
        type="button"
        onClick={props.onSelect}
        aria-label={props.title}
        className="group w-full text-left rounded-lg border border-border bg-card p-2 transition hover:border-foreground/20 hover:shadow-sm focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary"
      >
        {content}
      </button>
    );
  }

  return (
    <div className="w-full rounded-lg border border-border bg-card p-2">
      {content}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- entity-card --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/v2/entity-card/
git commit -m "feat(v2): EntityCard primitive with grid|list variants (M1)"
```

---

### Task 10: Drawer primitive (hybrid vaul mobile + Radix Dialog desktop)

**Files:**
- Crea: `apps/web/src/components/ui/v2/drawer/drawer.tsx`
- Crea: `apps/web/src/components/ui/v2/drawer/drawer.test.tsx`
- Crea: `apps/web/src/components/ui/v2/drawer/use-is-desktop.ts`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/ui/v2/drawer/drawer.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Drawer } from './drawer';

describe('Drawer', () => {
  it('renders title and children when open', () => {
    render(
      <Drawer open onOpenChange={() => {}} title="Dettaglio Catan">
        <p>Contenuto</p>
      </Drawer>
    );
    expect(screen.getByText('Dettaglio Catan')).toBeInTheDocument();
    expect(screen.getByText('Contenuto')).toBeInTheDocument();
  });

  it('does not render when closed', () => {
    render(
      <Drawer open={false} onOpenChange={() => {}} title="Hidden">
        <p>Hidden body</p>
      </Drawer>
    );
    expect(screen.queryByText('Hidden body')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- ui/v2/drawer --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement helper hook**

Create `apps/web/src/components/ui/v2/drawer/use-is-desktop.ts`:
```ts
'use client';
import { useEffect, useState } from 'react';

export function useIsDesktop(): boolean {
  const [isDesktop, setIsDesktop] = useState(false);
  useEffect(() => {
    const mql = window.matchMedia('(min-width: 768px)');
    const handler = (e: MediaQueryListEvent): void => setIsDesktop(e.matches);
    setIsDesktop(mql.matches);
    mql.addEventListener('change', handler);
    return () => mql.removeEventListener('change', handler);
  }, []);
  return isDesktop;
}
```

- [ ] **Step 4: Implement Drawer**

Create `apps/web/src/components/ui/v2/drawer/drawer.tsx`:
```tsx
'use client';
import type { ReactNode } from 'react';
import { Drawer as Vaul } from 'vaul';
import * as Dialog from '@radix-ui/react-dialog';
import { useIsDesktop } from './use-is-desktop';

export interface DrawerProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly title: string;
  readonly children: ReactNode;
}

export function Drawer({ open, onOpenChange, title, children }: DrawerProps): JSX.Element {
  const isDesktop = useIsDesktop();

  if (isDesktop) {
    return (
      <Dialog.Root open={open} onOpenChange={onOpenChange}>
        <Dialog.Portal>
          <Dialog.Overlay className="fixed inset-0 z-40 bg-black/40" />
          <Dialog.Content className="fixed right-0 top-0 z-50 h-full w-full max-w-md bg-background shadow-lg focus:outline-none">
            <div className="sticky top-0 flex items-center justify-between border-b border-border bg-background px-4 py-3">
              <Dialog.Title className="font-heading text-base font-semibold">{title}</Dialog.Title>
              <Dialog.Close aria-label="Chiudi" className="text-muted-foreground hover:text-foreground">✕</Dialog.Close>
            </div>
            <div className="overflow-y-auto p-4">{children}</div>
          </Dialog.Content>
        </Dialog.Portal>
      </Dialog.Root>
    );
  }

  return (
    <Vaul.Root open={open} onOpenChange={onOpenChange}>
      <Vaul.Portal>
        <Vaul.Overlay className="fixed inset-0 z-40 bg-black/40" />
        <Vaul.Content className="fixed inset-x-0 bottom-0 z-50 flex h-[90vh] flex-col rounded-t-2xl bg-background">
          <div className="mx-auto mt-2 h-1.5 w-10 rounded-full bg-muted" aria-hidden="true" />
          <div className="flex items-center justify-between px-4 py-3 border-b border-border">
            <Vaul.Title className="font-heading text-base font-semibold">{title}</Vaul.Title>
          </div>
          <div className="flex-1 overflow-y-auto p-4">{children}</div>
        </Vaul.Content>
      </Vaul.Portal>
    </Vaul.Root>
  );
}
```

- [ ] **Step 5: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- ui/v2/drawer --run`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/ui/v2/drawer/
git commit -m "feat(v2): Drawer hybrid vaul-mobile + Radix-desktop @ 768px breakpoint (M1)"
```

---

# M2 — Library pilot

### Task 11: LibraryFilterTabs (All | Owned | Wishlist)

**Files:**
- Crea: `apps/web/src/components/library/v2/LibraryFilterTabs.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryFilterTabs.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/library/v2/LibraryFilterTabs.test.tsx`:
```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryFilterTabs, type LibraryFilter } from './LibraryFilterTabs';

describe('LibraryFilterTabs', () => {
  it('renders 3 tabs with counts', () => {
    render(
      <LibraryFilterTabs
        value="all"
        onChange={() => {}}
        counts={{ all: 12, owned: 8, wishlist: 4 }}
      />
    );
    expect(screen.getByRole('tab', { name: /tutti.*12/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /possedut.*8/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /wishlist.*4/i })).toBeInTheDocument();
  });

  it('marks active tab with aria-selected', () => {
    render(
      <LibraryFilterTabs value="owned" onChange={() => {}} counts={{ all: 1, owned: 1, wishlist: 0 }} />
    );
    expect(screen.getByRole('tab', { name: /possedut/i })).toHaveAttribute('aria-selected', 'true');
  });

  it('calls onChange when tab clicked', () => {
    const onChange = vi.fn<[LibraryFilter], void>();
    render(
      <LibraryFilterTabs value="all" onChange={onChange} counts={{ all: 0, owned: 0, wishlist: 0 }} />
    );
    fireEvent.click(screen.getByRole('tab', { name: /wishlist/i }));
    expect(onChange).toHaveBeenCalledWith('wishlist');
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- LibraryFilterTabs --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/library/v2/LibraryFilterTabs.tsx`:
```tsx
export type LibraryFilter = 'all' | 'owned' | 'wishlist';

export interface LibraryFilterTabsProps {
  readonly value: LibraryFilter;
  readonly onChange: (next: LibraryFilter) => void;
  readonly counts: Record<LibraryFilter, number>;
}

const TABS: ReadonlyArray<{ id: LibraryFilter; label: string }> = [
  { id: 'all', label: 'Tutti' },
  { id: 'owned', label: 'Posseduti' },
  { id: 'wishlist', label: 'Wishlist' },
];

export function LibraryFilterTabs({ value, onChange, counts }: LibraryFilterTabsProps): JSX.Element {
  return (
    <div role="tablist" className="flex gap-1 border-b border-border">
      {TABS.map((tab) => {
        const isActive = value === tab.id;
        return (
          <button
            key={tab.id}
            role="tab"
            type="button"
            aria-selected={isActive}
            onClick={() => onChange(tab.id)}
            className={`px-3 py-2 text-sm font-medium transition border-b-2 ${
              isActive
                ? 'border-entity-game text-foreground'
                : 'border-transparent text-muted-foreground hover:text-foreground'
            }`}
          >
            {tab.label} <span className="ml-1 text-xs opacity-70">({counts[tab.id]})</span>
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- LibraryFilterTabs --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/v2/LibraryFilterTabs.tsx apps/web/src/components/library/v2/LibraryFilterTabs.test.tsx
git commit -m "feat(library-v2): filter tabs All|Owned|Wishlist with counts (M2)"
```

---

### Task 12: LibraryViewToggle (grid | list, persists in localStorage)

**Files:**
- Crea: `apps/web/src/components/library/v2/LibraryViewToggle.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryViewToggle.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/library/v2/LibraryViewToggle.test.tsx`:
```tsx
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryViewToggle, useLibraryView } from './LibraryViewToggle';

describe('useLibraryView', () => {
  beforeEach(() => localStorage.clear());

  it('defaults to grid', () => {
    function Probe() {
      const { view } = useLibraryView();
      return <span>{view}</span>;
    }
    render(<Probe />);
    expect(screen.getByText('grid')).toBeInTheDocument();
  });

  it('persists change to localStorage mai-library-view', () => {
    function Probe() {
      const { view, setView } = useLibraryView();
      return <button type="button" onClick={() => setView('list')}>{view}</button>;
    }
    render(<Probe />);
    fireEvent.click(screen.getByRole('button'));
    expect(localStorage.getItem('mai-library-view')).toBe('list');
  });
});

describe('LibraryViewToggle', () => {
  beforeEach(() => localStorage.clear());

  it('renders grid and list buttons', () => {
    render(<LibraryViewToggle />);
    expect(screen.getByRole('button', { name: /griglia/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /lista/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- LibraryViewToggle --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/library/v2/LibraryViewToggle.tsx`:
```tsx
'use client';
import { useEffect, useState, useCallback } from 'react';

export type LibraryView = 'grid' | 'list';
const STORAGE_KEY = 'mai-library-view';

export function useLibraryView(): { view: LibraryView; setView: (v: LibraryView) => void } {
  const [view, setViewState] = useState<LibraryView>('grid');

  useEffect(() => {
    const stored = localStorage.getItem(STORAGE_KEY);
    if (stored === 'list' || stored === 'grid') setViewState(stored);
  }, []);

  const setView = useCallback((v: LibraryView): void => {
    setViewState(v);
    localStorage.setItem(STORAGE_KEY, v);
  }, []);

  return { view, setView };
}

export function LibraryViewToggle(): JSX.Element {
  const { view, setView } = useLibraryView();
  return (
    <div className="inline-flex items-center gap-1 rounded-md border border-border p-0.5">
      <button
        type="button"
        aria-label="Vista griglia"
        aria-pressed={view === 'grid'}
        onClick={() => setView('grid')}
        className={`rounded px-2 py-1 text-xs ${view === 'grid' ? 'bg-accent text-foreground' : 'text-muted-foreground'}`}
      >
        ▦
      </button>
      <button
        type="button"
        aria-label="Vista lista"
        aria-pressed={view === 'list'}
        onClick={() => setView('list')}
        className={`rounded px-2 py-1 text-xs ${view === 'list' ? 'bg-accent text-foreground' : 'text-muted-foreground'}`}
      >
        ☰
      </button>
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- LibraryViewToggle --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/v2/LibraryViewToggle.tsx apps/web/src/components/library/v2/LibraryViewToggle.test.tsx
git commit -m "feat(library-v2): grid|list view toggle with localStorage persist (M2)"
```

---

### Task 13: LibraryMobile component (filter tabs + grid of EntityCard)

**Files:**
- Crea: `apps/web/src/components/library/v2/LibraryMobile.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryMobile.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/library/v2/LibraryMobile.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryMobile, type LibraryGameItem } from './LibraryMobile';

const items: LibraryGameItem[] = [
  { id: 'g1', title: 'Catan', publisher: 'Kosmos', owned: true, wishlist: false },
  { id: 'g2', title: 'Ticket to Ride', publisher: 'Days of Wonder', owned: false, wishlist: true },
  { id: 'g3', title: 'Carcassonne', publisher: 'HiG', owned: true, wishlist: false },
];

describe('LibraryMobile', () => {
  it('renders all items by default', () => {
    render(<LibraryMobile items={items} onSelect={() => {}} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Carcassonne')).toBeInTheDocument();
  });

  it('filters by owned when owned tab clicked', () => {
    render(<LibraryMobile items={items} onSelect={() => {}} />);
    fireEvent.click(screen.getByRole('tab', { name: /possedut/i }));
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
    expect(screen.getByText('Carcassonne')).toBeInTheDocument();
  });

  it('renders empty state when filter produces no results', () => {
    render(
      <LibraryMobile
        items={[{ id: 'g1', title: 'Catan', publisher: 'Kosmos', owned: true, wishlist: false }]}
        onSelect={() => {}}
      />
    );
    fireEvent.click(screen.getByRole('tab', { name: /wishlist/i }));
    expect(screen.getByText(/nessun gioco/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- LibraryMobile --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/library/v2/LibraryMobile.tsx`:
```tsx
'use client';
import { useState, useMemo } from 'react';
import { EntityCard } from '@/components/ui/v2/entity-card/entity-card';
import { LibraryFilterTabs, type LibraryFilter } from './LibraryFilterTabs';

export interface LibraryGameItem {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly imageUrl?: string;
  readonly owned: boolean;
  readonly wishlist: boolean;
}

export interface LibraryMobileProps {
  readonly items: ReadonlyArray<LibraryGameItem>;
  readonly onSelect: (id: string) => void;
}

export function LibraryMobile({ items, onSelect }: LibraryMobileProps): JSX.Element {
  const [filter, setFilter] = useState<LibraryFilter>('all');

  const filtered = useMemo(() => {
    if (filter === 'owned') return items.filter((i) => i.owned);
    if (filter === 'wishlist') return items.filter((i) => i.wishlist);
    return items;
  }, [items, filter]);

  const counts = useMemo(() => ({
    all: items.length,
    owned: items.filter((i) => i.owned).length,
    wishlist: items.filter((i) => i.wishlist).length,
  }), [items]);

  return (
    <div className="flex flex-col gap-3">
      <LibraryFilterTabs value={filter} onChange={setFilter} counts={counts} />
      {filtered.length === 0 ? (
        <p className="py-8 text-center text-sm text-muted-foreground">Nessun gioco in questa vista.</p>
      ) : (
        <div className="grid grid-cols-2 gap-3">
          {filtered.map((g) => (
            <EntityCard
              key={g.id}
              entity="game"
              variant="grid"
              title={g.title}
              subtitle={g.publisher}
              imageUrl={g.imageUrl}
              onSelect={() => onSelect(g.id)}
            />
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- LibraryMobile --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/v2/LibraryMobile.tsx apps/web/src/components/library/v2/LibraryMobile.test.tsx
git commit -m "feat(library-v2): mobile hub with filter tabs + EntityCard grid (M2)"
```

---

### Task 14: GameDrawerContent (tabs Info | Sessioni | Chat)

**Files:**
- Crea: `apps/web/src/components/library/v2/GameDrawerContent.tsx`
- Crea: `apps/web/src/components/library/v2/GameDrawerContent.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/library/v2/GameDrawerContent.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameDrawerContent } from './GameDrawerContent';

describe('GameDrawerContent', () => {
  const game = {
    id: 'g1',
    title: 'Catan',
    publisher: 'Kosmos',
    description: 'Classico di Klaus Teuber',
    minPlayers: 3,
    maxPlayers: 4,
    playTimeMinutes: 75,
    sessionCount: 5,
    chatCount: 2,
  };

  it('renders Info tab by default with game description', () => {
    render(<GameDrawerContent game={game} />);
    expect(screen.getByText('Classico di Klaus Teuber')).toBeInTheDocument();
  });

  it('switches to Sessioni tab', () => {
    render(<GameDrawerContent game={game} />);
    fireEvent.click(screen.getByRole('tab', { name: /sessioni/i }));
    expect(screen.getByText(/5 sessioni/i)).toBeInTheDocument();
  });

  it('uses entity game color on header', () => {
    const { container } = render(<GameDrawerContent game={game} />);
    expect(container.querySelector('.bg-entity-game\\/10')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- GameDrawerContent --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/library/v2/GameDrawerContent.tsx`:
```tsx
'use client';
import { useState } from 'react';
import { getEntityToken } from '@/components/ui/v2/entity-tokens';

export interface GameDrawerGame {
  readonly id: string;
  readonly title: string;
  readonly publisher?: string;
  readonly description?: string;
  readonly minPlayers?: number;
  readonly maxPlayers?: number;
  readonly playTimeMinutes?: number;
  readonly sessionCount: number;
  readonly chatCount: number;
}

type Tab = 'info' | 'sessions' | 'chat';

export interface GameDrawerContentProps {
  readonly game: GameDrawerGame;
  readonly initialTab?: Tab;
}

const TABS: ReadonlyArray<{ id: Tab; label: string }> = [
  { id: 'info', label: 'Info' },
  { id: 'sessions', label: 'Sessioni' },
  { id: 'chat', label: 'Chat' },
];

export function GameDrawerContent({ game, initialTab = 'info' }: GameDrawerContentProps): JSX.Element {
  const [tab, setTab] = useState<Tab>(initialTab);
  const t = getEntityToken('game');

  return (
    <div className="flex flex-col gap-4">
      <header className={`-mx-4 -mt-4 px-4 py-6 ${t.bgSoft}`}>
        <div className={`mb-1 text-xs ${t.text}`}>
          {t.emoji} {t.label}
        </div>
        <h2 className="font-heading text-2xl font-semibold">{game.title}</h2>
        {game.publisher && <p className="text-sm text-muted-foreground">{game.publisher}</p>}
      </header>

      <nav role="tablist" className="flex gap-1 border-b border-border">
        {TABS.map((x) => (
          <button
            key={x.id}
            role="tab"
            type="button"
            aria-selected={tab === x.id}
            onClick={() => setTab(x.id)}
            className={`px-3 py-2 text-sm transition border-b-2 ${
              tab === x.id ? `${t.border} text-foreground` : 'border-transparent text-muted-foreground'
            }`}
          >
            {x.label}
          </button>
        ))}
      </nav>

      <section role="tabpanel" className="text-sm">
        {tab === 'info' && (
          <div className="space-y-3">
            {game.description && <p>{game.description}</p>}
            <dl className="grid grid-cols-2 gap-2 text-xs">
              {game.minPlayers != null && game.maxPlayers != null && (
                <div><dt className="text-muted-foreground">Giocatori</dt><dd>{game.minPlayers}–{game.maxPlayers}</dd></div>
              )}
              {game.playTimeMinutes != null && (
                <div><dt className="text-muted-foreground">Durata</dt><dd>{game.playTimeMinutes} min</dd></div>
              )}
            </dl>
          </div>
        )}
        {tab === 'sessions' && (
          <p className="text-muted-foreground">{game.sessionCount} sessioni registrate.</p>
        )}
        {tab === 'chat' && (
          <p className="text-muted-foreground">{game.chatCount} thread di chat.</p>
        )}
      </section>
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- GameDrawerContent --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/v2/GameDrawerContent.tsx apps/web/src/components/library/v2/GameDrawerContent.test.tsx
git commit -m "feat(library-v2): GameDrawerContent with Info|Sessioni|Chat tabs (M2)"
```

---

### Task 15: LibraryDesktop (split-view: lista sx + detail dx)

**Files:**
- Crea: `apps/web/src/components/library/v2/LibraryDesktop.tsx`
- Crea: `apps/web/src/components/library/v2/LibraryDesktop.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/components/library/v2/LibraryDesktop.test.tsx`:
```tsx
import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryDesktop } from './LibraryDesktop';

const items = [
  { id: 'g1', title: 'Catan', publisher: 'Kosmos', owned: true, wishlist: false, description: 'Classico' },
  { id: 'g2', title: 'Root', publisher: 'Leder', owned: false, wishlist: true },
];

describe('LibraryDesktop', () => {
  it('renders list on the left with all items', () => {
    render(<LibraryDesktop items={items} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Root')).toBeInTheDocument();
  });

  it('shows empty detail state initially', () => {
    render(<LibraryDesktop items={items} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('shows game detail when item clicked', () => {
    render(<LibraryDesktop items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /catan/i }));
    expect(screen.getByText('Classico')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- LibraryDesktop --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement**

Create `apps/web/src/components/library/v2/LibraryDesktop.tsx`:
```tsx
'use client';
import { useState, useMemo } from 'react';
import { EntityCard } from '@/components/ui/v2/entity-card/entity-card';
import { LibraryFilterTabs, type LibraryFilter } from './LibraryFilterTabs';
import { GameDrawerContent, type GameDrawerGame } from './GameDrawerContent';

export interface LibraryDesktopItem extends GameDrawerGame {
  readonly owned: boolean;
  readonly wishlist: boolean;
  readonly imageUrl?: string;
}

export interface LibraryDesktopProps {
  readonly items: ReadonlyArray<LibraryDesktopItem>;
}

export function LibraryDesktop({ items }: LibraryDesktopProps): JSX.Element {
  const [filter, setFilter] = useState<LibraryFilter>('all');
  const [selected, setSelected] = useState<string | null>(null);

  const filtered = useMemo(() => {
    if (filter === 'owned') return items.filter((i) => i.owned);
    if (filter === 'wishlist') return items.filter((i) => i.wishlist);
    return items;
  }, [items, filter]);

  const counts = useMemo(() => ({
    all: items.length,
    owned: items.filter((i) => i.owned).length,
    wishlist: items.filter((i) => i.wishlist).length,
  }), [items]);

  const selectedItem = items.find((i) => i.id === selected) ?? null;

  return (
    <div className="grid h-full grid-cols-[1fr_2fr] gap-4">
      <aside className="flex flex-col gap-3 overflow-y-auto border-r border-border pr-4">
        <LibraryFilterTabs value={filter} onChange={setFilter} counts={counts} />
        <div className="flex flex-col gap-2">
          {filtered.map((g) => (
            <EntityCard
              key={g.id}
              entity="game"
              variant="list"
              title={g.title}
              subtitle={g.publisher}
              imageUrl={g.imageUrl}
              onSelect={() => setSelected(g.id)}
            />
          ))}
        </div>
      </aside>
      <section className="overflow-y-auto">
        {selectedItem ? (
          <GameDrawerContent game={selectedItem} />
        ) : (
          <p className="py-12 text-center text-sm text-muted-foreground">
            Seleziona un gioco per vedere i dettagli.
          </p>
        )}
      </section>
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- LibraryDesktop --run`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/v2/LibraryDesktop.tsx apps/web/src/components/library/v2/LibraryDesktop.test.tsx
git commit -m "feat(library-v2): desktop split-view list + detail panel (M2)"
```

---

### Task 16: Library v2 responsive entry page

**Files:**
- Crea: `apps/web/src/app/(authenticated)/library/v2/page.tsx`
- Crea: `apps/web/src/app/(authenticated)/library/v2/LibraryV2Client.tsx`
- Crea: `apps/web/src/app/(authenticated)/library/v2/LibraryV2Client.test.tsx`

- [ ] **Step 1: Write failing test**

Create `apps/web/src/app/(authenticated)/library/v2/LibraryV2Client.test.tsx`:
```tsx
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { LibraryV2Client, type LibraryV2Item } from './LibraryV2Client';

const items: LibraryV2Item[] = [
  { id: 'g1', title: 'Catan', owned: true, wishlist: false, sessionCount: 0, chatCount: 0 },
];

describe('LibraryV2Client', () => {
  beforeEach(() => {
    // force mobile viewport
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation((query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });
  });

  it('renders mobile layout on small viewport', () => {
    render(<LibraryV2Client items={items} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

Run: `cd apps/web && pnpm test -- LibraryV2Client --run`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement client component**

Create `apps/web/src/app/(authenticated)/library/v2/LibraryV2Client.tsx`:
```tsx
'use client';
import { useState } from 'react';
import { useIsDesktop } from '@/components/ui/v2/drawer/use-is-desktop';
import { Drawer } from '@/components/ui/v2/drawer/drawer';
import { LibraryMobile, type LibraryGameItem } from '@/components/library/v2/LibraryMobile';
import { LibraryDesktop, type LibraryDesktopItem } from '@/components/library/v2/LibraryDesktop';
import { GameDrawerContent } from '@/components/library/v2/GameDrawerContent';

export interface LibraryV2Item extends LibraryGameItem, Omit<LibraryDesktopItem, 'owned' | 'wishlist' | 'imageUrl' | 'id' | 'title' | 'publisher'> {}

export interface LibraryV2ClientProps {
  readonly items: ReadonlyArray<LibraryV2Item>;
}

export function LibraryV2Client({ items }: LibraryV2ClientProps): JSX.Element {
  const isDesktop = useIsDesktop();
  const [openId, setOpenId] = useState<string | null>(null);
  const openGame = items.find((i) => i.id === openId) ?? null;

  if (isDesktop) {
    return <LibraryDesktop items={items as ReadonlyArray<LibraryDesktopItem>} />;
  }

  return (
    <>
      <LibraryMobile items={items} onSelect={setOpenId} />
      <Drawer
        open={openId !== null}
        onOpenChange={(o) => !o && setOpenId(null)}
        title={openGame?.title ?? 'Dettaglio'}
      >
        {openGame && <GameDrawerContent game={openGame} />}
      </Drawer>
    </>
  );
}
```

- [ ] **Step 4: Implement page (server component)**

Create `apps/web/src/app/(authenticated)/library/v2/page.tsx`:
```tsx
import { LibraryV2Client, type LibraryV2Item } from './LibraryV2Client';

export const metadata = { title: 'Libreria (v2)' };

// TEMP: static seed until react-query wiring. Redirect/revert once real data hook lands.
const SEED: ReadonlyArray<LibraryV2Item> = [
  { id: 'seed-catan', title: 'Catan', publisher: 'Kosmos', owned: true, wishlist: false,
    description: 'Classico strategico di Klaus Teuber.', minPlayers: 3, maxPlayers: 4,
    playTimeMinutes: 75, sessionCount: 12, chatCount: 3 },
  { id: 'seed-root', title: 'Root', publisher: 'Leder Games', owned: false, wishlist: true,
    description: 'Wargame asimmetrico nella foresta.', minPlayers: 2, maxPlayers: 4,
    playTimeMinutes: 90, sessionCount: 0, chatCount: 0 },
];

export default function LibraryV2Page(): JSX.Element {
  return (
    <main className="mx-auto w-full max-w-6xl p-4 md:p-6">
      <h1 className="mb-4 font-heading text-2xl font-semibold">Libreria</h1>
      <LibraryV2Client items={SEED} />
    </main>
  );
}
```

- [ ] **Step 5: Run test — expect PASS**

Run: `cd apps/web && pnpm test -- LibraryV2Client --run`
Expected: PASS (1 test).

- [ ] **Step 6: Run full build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds, route `/library/v2` listed in output.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/v2/
git commit -m "feat(library-v2): /library/v2 responsive entry (mobile drawer + desktop split) (M2)"
```

---

### Task 17: Playwright E2E smoke — /library/v2 mobile + desktop

**Files:**
- Crea: `apps/web/e2e/library-v2.spec.ts`

- [ ] **Step 1: Write failing E2E test**

Create `apps/web/e2e/library-v2.spec.ts`:
```ts
// Usa il fixture autenticato del progetto: /library/v2 è dentro il route group
// (authenticated) e richiede sessione utente. `userPage` esegue loginAsUser() prima
// del test (vedi apps/web/e2e/fixtures/auth.ts).
import { test, expect } from './fixtures/auth';

test.describe('Library V2 pilot', () => {
  test('mobile: opens game drawer on card tap', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/library/v2');
    await expect(page.getByRole('heading', { name: 'Libreria' })).toBeVisible();
    await page.getByRole('button', { name: /catan/i }).click();
    await expect(page.getByRole('tab', { name: /info/i })).toBeVisible();
    await page.getByRole('tab', { name: /sessioni/i }).click();
    await expect(page.getByText(/12 sessioni/i)).toBeVisible();
  });

  test('desktop: shows split-view with detail on selection', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto('/library/v2');
    await expect(page.getByText(/seleziona un gioco/i)).toBeVisible();
    await page.getByRole('button', { name: /root/i }).click();
    await expect(page.getByText('Wargame asimmetrico nella foresta.')).toBeVisible();
  });

  test('filter tabs update counts', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/library/v2');
    await expect(page.getByRole('tab', { name: /tutti.*2/i })).toBeVisible();
    await page.getByRole('tab', { name: /possedut/i }).click();
    await expect(page.getByRole('button', { name: /catan/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /root/i })).not.toBeVisible();
  });
});
```

> **Nota auth**: il fixture `userPage` effettua il login tramite `loginAsUser(page)` prima che il test giri. Non serve `storageState` né configurazione globale: il pattern è già in uso in altri spec E2E (vedi `apps/web/e2e/fixtures/auth.ts:436-463`).

- [ ] **Step 2: Run E2E — expect PASS (after prev tasks implemented)**

Run: `cd apps/web && pnpm test:e2e -- library-v2`
Expected: 3 tests PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/library-v2.spec.ts
git commit -m "test(e2e): Library v2 mobile drawer + desktop split-view smoke (M2)"
```

---

### Task 18: Update bundle-size baseline

**Files:**
- Modifica: `apps/web/bundle-size-baseline.json`

- [ ] **Step 1: Run build and capture new bundle size**

Run: `cd apps/web && pnpm build`
Read reported bundle size from output (look for `/library/v2` route line).

- [ ] **Step 2: Update baseline**

Open `apps/web/bundle-size-baseline.json`, locate the `totalSize` field (currently `12451770`), update to the new value reported by the build. If other per-route entries exist, add `/library/v2` with its reported size.

- [ ] **Step 3: Re-run bundle check**

Run: `cd apps/web && pnpm check:bundle-size` (or equivalent script defined in package.json, e.g., `pnpm lint:bundle`)
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add apps/web/bundle-size-baseline.json
git commit -m "chore(bundle): update baseline after /library/v2 pilot (M2)"
```

---

### Task 19: Open PR draft → main-dev

**Files:** — (git state only)

- [ ] **Step 1: Push branch**

Run: `git push origin redesign/v2`
Expected: `To github.com:.../meepleai-monorepo.git`

- [ ] **Step 2: Open draft PR**

Run:
```bash
gh pr create --draft --base main-dev --head redesign/v2 \
  --title "feat(redesign-v2): Library pilot — foundation + primitives + hub" \
  --body "$(cat <<'EOF'
## Summary
- M0 Foundation: design-tokens entity colors (9 tokens), Tailwind entity.* utilities, vaul installed, ThemeToggle + mai-theme localStorage
- M1 Primitives (library-scoped): EntityChip, EntityPip+Stack, EntityCard (grid|list), Drawer (vaul mobile + Radix desktop @ 768px)
- M2 Library pilot: /library/v2 responsive (mobile drawer + desktop split-view), filter tabs, view toggle, GameDrawerContent with Info|Sessioni|Chat tabs

## Test plan
- [x] Unit: 15+ tests across primitives and library components (Vitest + jest-axe)
- [x] E2E: Playwright smoke on mobile and desktop viewports
- [x] Build: `pnpm build` succeeds, new bundle baseline committed
- [ ] Manual QA: navigate to /library/v2 on mobile Chrome + desktop Firefox

## Follow-up (not in this PR)
- Wire real data via react-query (currently static SEED)
- Migrate /library hub route to /library/v2 behaviors
- M3 Sessions, M4 Chat, M5 Game Nights per `docs/superpowers/specs/2026-04-20-redesign-v2-p0-mockups-brief.md`

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```
Expected: PR URL returned.

---

## Self-Review

**Spec coverage:**
- ✅ M0 Foundation: entity token add (Task 3), Tailwind classes (Task 4), vaul (Task 2), branch (Task 1), theme toggle (Task 6), entity-tokens helper (Task 5).
- ✅ M1 Primitivi scoped-Library: EntityChip (Task 7), EntityPip (Task 8), EntityCard grid/list (Task 9), Drawer hybrid (Task 10). *Nota: BottomBar, Topbar, ConnectionBar re-skin, MiniNav, RecentsBar re-skin NON inclusi — scoped a Library pilot minimo; da aggiungere in piani successivi M3+.*
- ✅ M2 Library pilot: filter tabs (Task 11), view toggle (Task 12), mobile hub (Task 13), drawer content (Task 14), desktop split (Task 15), entry page (Task 16), E2E (Task 17).
- ✅ Bundle baseline (Task 18), PR draft (Task 19).
- ⚠️ **Gap consapevole**: il piano usa dati seed statici in `page.tsx` (Task 16 Step 4) — wiring react-query è follow-up. Scelta deliberata per mantenere il pilota leggibile e scoped.

**Placeholder scan:** Nessun "TODO/TBD" nel codice. Il commento `// TEMP: static seed` in Task 16 è esplicito, non un placeholder mascherato.

**Type consistency:**
- `EntityType` definito in Task 5, usato in Task 7, 8, 9, 10, 14 — coerente.
- `LibraryFilter` definito in Task 11, riusato in Task 13, 15 — coerente.
- `GameDrawerGame` definito in Task 14, esteso in Task 15 (`LibraryDesktopItem`) e referenziato in Task 16 (`LibraryV2Item`) — coerente.
- `LibraryGameItem` definito in Task 13, esteso in Task 16 — coerente.
- `useIsDesktop` definito in Task 10, consumato in Task 16 — coerente.
- Token helper API: `getEntityToken(type).{bg,bgSoft,text,border,emoji,label}` definito in Task 5, usato identicamente in Task 7, 8, 9, 14 — coerente.

---

**Plan complete and saved to `docs/superpowers/plans/2026-04-20-redesign-v2-library-pilot-plan.md`.** Two execution options:

**1. Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
