# Onboarding Product Tour Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `/onboarding` single-step profile form with 5-step Claude Design product-tour (Welcome → Games → Agents → FirstSession → Complete), calling `completeOnboarding(false)` only at the end.

**Architecture:** Client-side wizard with local state + `localStorage` step resume. No backend persistence for game/agent selections. Reuses existing M6 v2 primitives (`Btn`, `StepProgress`, `ToggleSwitch`) + inline step-specific components (GameTile, ActionCard, Confetti).

**Tech Stack:** Next.js 16 App Router, React 19.2, Vitest 3 + RTL, Playwright, Tailwind 4 with entity tokens.

**Spec:** `docs/superpowers/specs/2026-04-20-onboarding-product-tour-design.md`

**Branch:** `refactor/redesign-v2-m6` (PR #484 already open to `main-dev`)

---

## File Structure

**New files:**
```
apps/web/src/components/onboarding/tour/
  data.ts
  Confetti.tsx
  WelcomeStep.tsx
  GameSelectStep.tsx
  AgentToggleStep.tsx
  FirstSessionStep.tsx
  CompleteStep.tsx
  index.ts
  __tests__/data.test.ts
  __tests__/Confetti.test.tsx
  __tests__/WelcomeStep.test.tsx
  __tests__/GameSelectStep.test.tsx
  __tests__/AgentToggleStep.test.tsx
  __tests__/FirstSessionStep.test.tsx
  __tests__/CompleteStep.test.tsx

apps/web/src/app/(authenticated)/onboarding/
  OnboardingTourClient.tsx
  __tests__/OnboardingTourClient.test.tsx

apps/web/e2e/
  onboarding-tour.spec.ts
```

**Modified files:**
```
apps/web/src/app/(authenticated)/onboarding/page.tsx   (full rewrite)
apps/web/bundle-size-baseline.json                     (bump after build)
```

**Removed files:**
```
apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx
```

---

### Task 1: data.ts constants

**Files:**
- Create: `apps/web/src/components/onboarding/tour/data.ts`
- Test: `apps/web/src/components/onboarding/tour/__tests__/data.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/data.test.ts`:

```ts
import { describe, expect, it } from 'vitest';

import { ACTIONS, AGENTS, GAMES, MIN_SELECTED, STEP_ENTITIES, STEP_LABELS } from '../data';

describe('onboarding tour data', () => {
  it('GAMES has 8 entries with required fields', () => {
    expect(GAMES).toHaveLength(8);
    for (const g of GAMES) {
      expect(g.id).toMatch(/^[a-z0-9-]+$/);
      expect(g.title).toBeTruthy();
      expect(typeof g.year).toBe('number');
      expect(g.players).toMatch(/\d/);
      expect(g.emoji).toBeTruthy();
      expect(g.gradient).toHaveLength(2);
    }
  });

  it('AGENTS has 4 entries, 3 defaultOn', () => {
    expect(AGENTS).toHaveLength(4);
    expect(AGENTS.filter(a => a.defaultOn)).toHaveLength(3);
  });

  it('ACTIONS map to existing authenticated routes', () => {
    const hrefs = ACTIONS.map(a => a.href);
    expect(hrefs).toEqual(['/game-nights', '/library', '/agents']);
  });

  it('MIN_SELECTED is 3', () => {
    expect(MIN_SELECTED).toBe(3);
  });

  it('STEP_LABELS and STEP_ENTITIES align', () => {
    expect(STEP_LABELS).toEqual(['Giochi', 'Agenti', 'Sessione']);
    expect(STEP_ENTITIES).toEqual(['game', 'agent', 'session']);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/data.test.ts`
Expected: FAIL — module `../data` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/data.ts`:

```ts
export interface TourGame {
  readonly id: string;
  readonly title: string;
  readonly year: number;
  readonly players: string;
  readonly emoji: string;
  /** HSL gradient stops used for the tile cover (["h s% l%", "h s% l%"]). */
  readonly gradient: readonly [string, string];
}

export interface TourAgent {
  readonly id: string;
  readonly emoji: string;
  readonly name: string;
  readonly desc: string;
  readonly defaultOn: boolean;
}

export type TourActionEntity = 'event' | 'game' | 'agent';

export interface TourAction {
  readonly id: string;
  readonly emoji: string;
  readonly title: string;
  readonly desc: string;
  readonly entity: TourActionEntity;
  readonly href: string;
}

export const GAMES: readonly TourGame[] = [
  { id: 'catan', title: 'Catan', year: 1995, players: '3–4', emoji: '🌾', gradient: ['25 88% 50%', '15 80% 38%'] },
  { id: 'carcassonne', title: 'Carcassonne', year: 2000, players: '2–5', emoji: '🏰', gradient: ['142 52% 38%', '158 50% 28%'] },
  { id: 'ticket', title: 'Ticket to Ride', year: 2004, players: '2–5', emoji: '🚂', gradient: ['220 68% 50%', '235 62% 40%'] },
  { id: 'wingspan', title: 'Wingspan', year: 2019, players: '1–5', emoji: '🦜', gradient: ['262 65% 55%', '278 60% 42%'] },
  { id: '7wonders', title: '7 Wonders', year: 2010, players: '2–7', emoji: '🏛️', gradient: ['38 85% 50%', '22 78% 40%'] },
  { id: 'terraforming', title: 'Terraforming Mars', year: 2016, players: '1–5', emoji: '🚀', gradient: ['350 72% 52%', '8 65% 40%'] },
  { id: 'azul', title: 'Azul', year: 2017, players: '2–4', emoji: '🔷', gradient: ['195 78% 48%', '210 68% 38%'] },
  { id: 'splendor', title: 'Splendor', year: 2014, players: '2–4', emoji: '💎', gradient: ['174 62% 38%', '188 56% 28%'] },
] as const;

export const AGENTS: readonly TourAgent[] = [
  { id: 'rules', emoji: '🎲', name: 'Agente Regole', desc: 'Risposte precise con citazione pagina PDF', defaultOn: true },
  { id: 'strategy', emoji: '🎯', name: 'Agente Strategia', desc: 'Consigli tattici durante la partita', defaultOn: true },
  { id: 'setup', emoji: '🔧', name: 'Agente Setup', desc: 'Ti guida nel preparare il tavolo', defaultOn: true },
  { id: 'narrator', emoji: '📚', name: 'Agente Cronista', desc: 'Narra la partita in tempo reale', defaultOn: false },
] as const;

export const ACTIONS: readonly TourAction[] = [
  { id: 'event', emoji: '🎉', title: 'Crea la prima serata', desc: 'Pianifica con amici', entity: 'event', href: '/game-nights' },
  { id: 'library', emoji: '🎲', title: 'Esplora la library', desc: 'Vedi i tuoi giochi', entity: 'game', href: '/library' },
  { id: 'chat', emoji: '💬', title: 'Chatta con un agente', desc: 'Prova una domanda', entity: 'agent', href: '/agents' },
] as const;

export const MIN_SELECTED = 3;
export const STEP_LABELS: readonly ['Giochi', 'Agenti', 'Sessione'] = ['Giochi', 'Agenti', 'Sessione'];
export const STEP_ENTITIES: readonly ['game', 'agent', 'session'] = ['game', 'agent', 'session'];
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/data.test.ts`
Expected: PASS (5/5)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/data.ts \
        apps/web/src/components/onboarding/tour/__tests__/data.test.ts
git commit -m "feat(onboarding-tour): tour data constants (games/agents/actions)"
```

---

### Task 2: Confetti component

**Files:**
- Create: `apps/web/src/components/onboarding/tour/Confetti.tsx`
- Test: `apps/web/src/components/onboarding/tour/__tests__/Confetti.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/Confetti.test.tsx`:

```tsx
import { render } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Confetti } from '../Confetti';

describe('Confetti', () => {
  it('renders 40 aria-hidden pieces', () => {
    const { container } = render(<Confetti />);
    const root = container.querySelector('[data-testid="onboarding-confetti"]');
    expect(root).not.toBeNull();
    expect(root?.getAttribute('aria-hidden')).toBe('true');
    expect(root?.children).toHaveLength(40);
  });

  it('accepts a custom count', () => {
    const { container } = render(<Confetti count={5} />);
    const root = container.querySelector('[data-testid="onboarding-confetti"]');
    expect(root?.children).toHaveLength(5);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/Confetti.test.tsx`
Expected: FAIL — module `../Confetti` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/Confetti.tsx`:

```tsx
import type { CSSProperties, JSX } from 'react';
import { useMemo } from 'react';

const COLORS = [
  'hsl(25 95% 58%)',
  'hsl(38 92% 62%)',
  'hsl(240 62% 68%)',
  'hsl(262 68% 68%)',
  'hsl(350 85% 68%)',
  'hsl(142 60% 55%)',
  'hsl(195 76% 58%)',
  'hsl(174 62% 52%)',
];

export interface ConfettiProps {
  readonly count?: number;
}

interface Piece {
  readonly id: number;
  readonly left: number;
  readonly color: string;
  readonly delay: number;
  readonly dur: number;
  readonly size: number;
  readonly rot: number;
  readonly wide: boolean;
}

export function Confetti({ count = 40 }: ConfettiProps): JSX.Element {
  const pieces = useMemo<Piece[]>(
    () =>
      Array.from({ length: count }, (_, i) => ({
        id: i,
        left: 5 + Math.random() * 90,
        color: COLORS[i % COLORS.length],
        delay: Math.random() * 0.9,
        dur: 2.2 + Math.random() * 1.6,
        size: Math.round(6 + Math.random() * 9),
        rot: Math.round(Math.random() * 360),
        wide: Math.random() > 0.5,
      })),
    [count]
  );

  return (
    <div
      data-testid="onboarding-confetti"
      aria-hidden="true"
      className="pointer-events-none absolute inset-0 overflow-hidden motion-reduce:hidden"
    >
      {pieces.map(p => {
        const style: CSSProperties = {
          position: 'absolute',
          top: '-10%',
          left: `${p.left}%`,
          width: p.wide ? `${p.size * 1.6}px` : `${p.size}px`,
          height: p.wide ? `${p.size * 0.55}px` : `${p.size}px`,
          borderRadius: p.wide ? '2px' : '50%',
          background: p.color,
          transform: `rotate(${p.rot}deg)`,
          animation: `onboarding-confetti-fall ${p.dur}s ${p.delay}s linear forwards`,
        };
        return <span key={p.id} style={style} />;
      })}
      <style>{`
        @keyframes onboarding-confetti-fall {
          0% { transform: translateY(0) rotate(0deg); opacity: 1; }
          100% { transform: translateY(110vh) rotate(720deg); opacity: 0; }
        }
      `}</style>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/Confetti.test.tsx`
Expected: PASS (2/2)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/Confetti.tsx \
        apps/web/src/components/onboarding/tour/__tests__/Confetti.test.tsx
git commit -m "feat(onboarding-tour): Confetti component (40 animated pieces, motion-safe)"
```

---

### Task 3: WelcomeStep

**Files:**
- Create: `apps/web/src/components/onboarding/tour/WelcomeStep.tsx`
- Test: `apps/web/src/components/onboarding/tour/__tests__/WelcomeStep.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/WelcomeStep.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { WelcomeStep } from '../WelcomeStep';

describe('WelcomeStep', () => {
  it('greets by userName when provided', () => {
    render(<WelcomeStep userName="Luca" onStart={vi.fn()} onSkip={vi.fn()} />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/Benvenuto in MeepleAI, Luca/);
  });

  it('renders greeting without userName', () => {
    render(<WelcomeStep userName={null} onStart={vi.fn()} onSkip={vi.fn()} />);
    const heading = screen.getByRole('heading', { level: 1 });
    expect(heading).toHaveTextContent(/Benvenuto in MeepleAI!/);
    expect(heading.textContent).not.toMatch(/,/);
  });

  it('fires onStart on CTA click', async () => {
    const onStart = vi.fn();
    const user = userEvent.setup();
    render(<WelcomeStep userName={null} onStart={onStart} onSkip={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    expect(onStart).toHaveBeenCalledTimes(1);
  });

  it('fires onSkip on skip link', async () => {
    const onSkip = vi.fn();
    const user = userEvent.setup();
    render(<WelcomeStep userName={null} onStart={vi.fn()} onSkip={onSkip} />);
    await user.click(screen.getByRole('button', { name: /salta/i }));
    expect(onSkip).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/WelcomeStep.test.tsx`
Expected: FAIL — module `../WelcomeStep` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/WelcomeStep.tsx`:

```tsx
import type { JSX } from 'react';

import { Btn } from '@/components/ui/v2/btn';

export interface WelcomeStepProps {
  readonly userName: string | null;
  readonly onStart: () => void;
  readonly onSkip: () => void;
}

export function WelcomeStep({ userName, onStart, onSkip }: WelcomeStepProps): JSX.Element {
  return (
    <div className="flex flex-col items-center gap-8 px-6 py-10 text-center">
      <div className="flex flex-col items-center gap-4" aria-hidden="true">
        <div className="flex h-20 w-20 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-400 to-orange-500 text-3xl font-bold text-white">
          M
        </div>
        <div className="flex gap-2 text-3xl">
          <span>♟️</span>
          <span>🎲</span>
          <span>🃏</span>
        </div>
      </div>
      <div className="flex flex-col items-center gap-3">
        <h1 className="font-quicksand text-2xl font-bold sm:text-3xl">
          {userName ? (
            <>
              Benvenuto in{' '}
              <span className="bg-gradient-to-r from-amber-500 to-orange-500 bg-clip-text text-transparent">
                MeepleAI
              </span>
              , {userName}!
            </>
          ) : (
            <>
              Benvenuto in{' '}
              <span className="bg-gradient-to-r from-amber-500 to-orange-500 bg-clip-text text-transparent">
                MeepleAI
              </span>
              !
            </>
          )}
        </h1>
        <p className="font-nunito text-sm text-muted-foreground">
          Configuriamo l&apos;app in 3 minuti — giochi, agenti e prima sessione.
        </p>
      </div>
      <div className="flex w-full max-w-xs flex-col gap-2">
        <Btn variant="primary" size="lg" fullWidth onClick={onStart}>
          Inizia il tour →
        </Btn>
        <Btn variant="ghost" size="md" fullWidth onClick={onSkip}>
          Salta, esploro da solo
        </Btn>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/WelcomeStep.test.tsx`
Expected: PASS (4/4)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/WelcomeStep.tsx \
        apps/web/src/components/onboarding/tour/__tests__/WelcomeStep.test.tsx
git commit -m "feat(onboarding-tour): WelcomeStep (greeting + start/skip CTAs)"
```

---

### Task 4: GameSelectStep

**Files:**
- Create: `apps/web/src/components/onboarding/tour/GameSelectStep.tsx`
- Test: `apps/web/src/components/onboarding/tour/__tests__/GameSelectStep.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/GameSelectStep.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { GameSelectStep } from '../GameSelectStep';

describe('GameSelectStep', () => {
  it('renders 8 games as aria-pressed buttons', () => {
    render(<GameSelectStep selected={[]} onToggle={vi.fn()} />);
    const buttons = screen.getAllByRole('button');
    // 8 game tiles
    expect(buttons).toHaveLength(8);
    for (const btn of buttons) expect(btn).toHaveAttribute('aria-pressed');
  });

  it('reflects selected state on aria-pressed', () => {
    render(<GameSelectStep selected={['catan', 'azul']} onToggle={vi.fn()} />);
    expect(screen.getByRole('button', { name: /catan/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /azul/i })).toHaveAttribute('aria-pressed', 'true');
    expect(screen.getByRole('button', { name: /wingspan/i })).toHaveAttribute('aria-pressed', 'false');
  });

  it('calls onToggle with game id on click', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(<GameSelectStep selected={[]} onToggle={onToggle} />);
    await user.click(screen.getByRole('button', { name: /catan/i }));
    expect(onToggle).toHaveBeenCalledWith('catan');
  });

  it('counter shows remaining needed when under MIN_SELECTED', () => {
    render(<GameSelectStep selected={['catan']} onToggle={vi.fn()} />);
    const counter = screen.getByText(/1 di 3 selezionati/i);
    expect(counter).toBeInTheDocument();
    expect(counter).toHaveAttribute('aria-live', 'polite');
  });

  it('counter shows ready state at MIN_SELECTED', () => {
    render(<GameSelectStep selected={['catan', 'azul', 'wingspan']} onToggle={vi.fn()} />);
    expect(screen.getByText(/3 giochi selezionati/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/GameSelectStep.test.tsx`
Expected: FAIL — module `../GameSelectStep` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/GameSelectStep.tsx`:

```tsx
import type { CSSProperties, JSX } from 'react';

import clsx from 'clsx';

import { GAMES, MIN_SELECTED, type TourGame } from './data';

export interface GameSelectStepProps {
  readonly selected: readonly string[];
  readonly onToggle: (id: string) => void;
}

function GameTile({
  game,
  selected,
  onToggle,
}: {
  readonly game: TourGame;
  readonly selected: boolean;
  readonly onToggle: (id: string) => void;
}): JSX.Element {
  const bg: CSSProperties = {
    background: `linear-gradient(160deg, hsl(${game.gradient[0]}), hsl(${game.gradient[1]}))`,
  };
  return (
    <button
      type="button"
      aria-pressed={selected}
      aria-label={`${game.title}, ${selected ? 'selezionato' : 'seleziona'}`}
      onClick={() => onToggle(game.id)}
      className={clsx(
        'group relative flex flex-col overflow-hidden rounded-xl border-2 text-left transition-transform',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2',
        selected
          ? 'border-[hsl(var(--e-game))] scale-[0.97]'
          : 'border-transparent hover:scale-[1.02]'
      )}
    >
      <div className="relative flex h-24 items-center justify-center text-4xl" style={bg}>
        <span aria-hidden="true">{game.emoji}</span>
        {selected && (
          <span
            aria-hidden="true"
            className="absolute right-1.5 top-1.5 flex h-6 w-6 items-center justify-center rounded-full bg-white text-sm font-bold text-[hsl(var(--e-game))] shadow"
          >
            ✓
          </span>
        )}
      </div>
      <div className="flex flex-col gap-0.5 bg-card px-2 py-2">
        <span className="font-quicksand text-sm font-semibold">{game.title}</span>
        <span className="font-nunito text-xs text-muted-foreground">
          {game.year} · {game.players}p
        </span>
      </div>
    </button>
  );
}

export function GameSelectStep({ selected, onToggle }: GameSelectStepProps): JSX.Element {
  const count = selected.length;
  const ready = count >= MIN_SELECTED;
  return (
    <div className="flex flex-col gap-5 px-4 py-6">
      <div className="text-center">
        <h2 className="font-quicksand text-xl font-bold">
          Quali giochi hai nella tua{' '}
          <span className="text-[hsl(var(--e-game))]">ludoteca</span>?
        </h2>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Seleziona almeno {MIN_SELECTED}, li useremo per personalizzare l&apos;esperienza.
        </p>
      </div>
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4">
        {GAMES.map(g => (
          <GameTile
            key={g.id}
            game={g}
            selected={selected.includes(g.id)}
            onToggle={onToggle}
          />
        ))}
      </div>
      <p
        aria-live="polite"
        className={clsx(
          'text-center font-nunito text-sm',
          ready ? 'text-[hsl(var(--e-game))] font-semibold' : 'text-muted-foreground'
        )}
      >
        {ready
          ? `✓ ${count} giochi selezionati`
          : `${count} di ${MIN_SELECTED} selezionati — ancora ${MIN_SELECTED - count}`}
      </p>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/GameSelectStep.test.tsx`
Expected: PASS (5/5)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/GameSelectStep.tsx \
        apps/web/src/components/onboarding/tour/__tests__/GameSelectStep.test.tsx
git commit -m "feat(onboarding-tour): GameSelectStep (8 tiles, min-3 gating counter)"
```

---

### Task 5: AgentToggleStep

**Files:**
- Create: `apps/web/src/components/onboarding/tour/AgentToggleStep.tsx`
- Test: `apps/web/src/components/onboarding/tour/__tests__/AgentToggleStep.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/AgentToggleStep.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { AgentToggleStep } from '../AgentToggleStep';

function defaultStates() {
  return { rules: true, strategy: true, setup: true, narrator: false };
}

describe('AgentToggleStep', () => {
  it('renders 4 agents as switches', () => {
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={vi.fn()} />);
    expect(screen.getAllByRole('switch')).toHaveLength(4);
  });

  it('reflects aria-checked per state', () => {
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={vi.fn()} />);
    expect(screen.getByRole('switch', { name: /agente regole/i })).toHaveAttribute('aria-checked', 'true');
    expect(screen.getByRole('switch', { name: /agente cronista/i })).toHaveAttribute('aria-checked', 'false');
  });

  it('calls onToggle with agent id on click', async () => {
    const onToggle = vi.fn();
    const user = userEvent.setup();
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={onToggle} />);
    await user.click(screen.getByRole('switch', { name: /agente cronista/i }));
    expect(onToggle).toHaveBeenCalledWith('narrator');
  });

  it('active-count is aria-live and matches enabled agents', () => {
    render(<AgentToggleStep agentStates={defaultStates()} onToggle={vi.fn()} />);
    const live = screen.getByText(/3 agenti attivi/i);
    expect(live).toHaveAttribute('aria-live', 'polite');
  });

  it('uses singular form when one agent active', () => {
    render(
      <AgentToggleStep
        agentStates={{ rules: true, strategy: false, setup: false, narrator: false }}
        onToggle={vi.fn()}
      />
    );
    expect(screen.getByText(/1 agente attivo/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/AgentToggleStep.test.tsx`
Expected: FAIL — module `../AgentToggleStep` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/AgentToggleStep.tsx`:

```tsx
import type { JSX } from 'react';

import { ToggleSwitch } from '@/components/ui/v2/toggle-switch';

import { AGENTS } from './data';

export type AgentStates = Record<string, boolean>;

export interface AgentToggleStepProps {
  readonly agentStates: AgentStates;
  readonly onToggle: (id: string) => void;
}

export function AgentToggleStep({ agentStates, onToggle }: AgentToggleStepProps): JSX.Element {
  const activeCount = AGENTS.filter(a => agentStates[a.id]).length;
  return (
    <div className="flex flex-col gap-5 px-4 py-6">
      <div className="text-center">
        <h2 className="font-quicksand text-xl font-bold">
          Attiva i tuoi <span className="text-[hsl(var(--e-agent))]">assistenti</span>
        </h2>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Ogni agente è specializzato in un tipo di domanda. Puoi modificarli in qualsiasi momento.
        </p>
      </div>
      <ul className="flex flex-col gap-2">
        {AGENTS.map(a => (
          <li
            key={a.id}
            className="flex items-center gap-3 rounded-xl border border-border bg-card px-3 py-3"
          >
            <span className="text-2xl" aria-hidden="true">
              {a.emoji}
            </span>
            <div className="flex flex-1 flex-col">
              <span className="font-quicksand text-sm font-semibold">{a.name}</span>
              <span className="font-nunito text-xs text-muted-foreground">{a.desc}</span>
            </div>
            <ToggleSwitch
              entity="agent"
              checked={agentStates[a.id] ?? false}
              onCheckedChange={() => onToggle(a.id)}
              ariaLabel={a.name}
            />
          </li>
        ))}
      </ul>
      <p
        aria-live="polite"
        className="text-center font-nunito text-sm text-muted-foreground"
      >
        {activeCount === 1 ? '1 agente attivo' : `${activeCount} agenti attivi`}
      </p>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/AgentToggleStep.test.tsx`
Expected: PASS (5/5)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/AgentToggleStep.tsx \
        apps/web/src/components/onboarding/tour/__tests__/AgentToggleStep.test.tsx
git commit -m "feat(onboarding-tour): AgentToggleStep (4 agents with ToggleSwitch v2)"
```

---

### Task 6: FirstSessionStep

**Files:**
- Create: `apps/web/src/components/onboarding/tour/FirstSessionStep.tsx`
- Test: `apps/web/src/components/onboarding/tour/__tests__/FirstSessionStep.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/FirstSessionStep.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { FirstSessionStep } from '../FirstSessionStep';

describe('FirstSessionStep', () => {
  it('renders 3 action cards with entity data attribute', () => {
    render(<FirstSessionStep onChoose={vi.fn()} />);
    const cards = screen.getAllByRole('button');
    expect(cards).toHaveLength(3);
    const entities = cards.map(c => c.getAttribute('data-entity'));
    expect(entities).toEqual(['event', 'game', 'agent']);
  });

  it('fires onChoose with action id and href', async () => {
    const onChoose = vi.fn();
    const user = userEvent.setup();
    render(<FirstSessionStep onChoose={onChoose} />);
    await user.click(screen.getByRole('button', { name: /esplora la library/i }));
    expect(onChoose).toHaveBeenCalledWith({ id: 'library', href: '/library' });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/FirstSessionStep.test.tsx`
Expected: FAIL — module `../FirstSessionStep` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/FirstSessionStep.tsx`:

```tsx
import type { JSX } from 'react';

import { ACTIONS, type TourAction } from './data';

export interface FirstSessionChoice {
  readonly id: string;
  readonly href: string;
}

export interface FirstSessionStepProps {
  readonly onChoose: (choice: FirstSessionChoice) => void;
}

function ActionCard({
  action,
  onChoose,
}: {
  readonly action: TourAction;
  readonly onChoose: (choice: FirstSessionChoice) => void;
}): JSX.Element {
  return (
    <button
      type="button"
      data-entity={action.entity}
      aria-label={action.title}
      onClick={() => onChoose({ id: action.id, href: action.href })}
      className="group flex w-full items-center gap-3 rounded-xl border border-border bg-card px-4 py-4 text-left transition-colors hover:border-[hsl(var(--e-session))] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-offset-2"
    >
      <span className="text-2xl" aria-hidden="true">
        {action.emoji}
      </span>
      <div className="flex flex-1 flex-col">
        <span className="font-quicksand text-sm font-semibold">{action.title}</span>
        <span className="font-nunito text-xs text-muted-foreground">{action.desc}</span>
      </div>
      <span
        aria-hidden="true"
        className="text-lg text-muted-foreground transition-transform group-hover:translate-x-1"
      >
        →
      </span>
    </button>
  );
}

export function FirstSessionStep({ onChoose }: FirstSessionStepProps): JSX.Element {
  return (
    <div className="flex flex-col gap-5 px-4 py-6">
      <div className="text-center">
        <h2 className="font-quicksand text-xl font-bold">
          Tutto pronto! Cosa vuoi fare{' '}
          <span className="text-[hsl(var(--e-session))]">ora</span>?
        </h2>
        <p className="mt-1 font-nunito text-sm text-muted-foreground">
          Scegli da dove partire — puoi sempre tornare.
        </p>
      </div>
      <div className="flex flex-col gap-2">
        {ACTIONS.map(a => (
          <ActionCard key={a.id} action={a} onChoose={onChoose} />
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/FirstSessionStep.test.tsx`
Expected: PASS (2/2)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/FirstSessionStep.tsx \
        apps/web/src/components/onboarding/tour/__tests__/FirstSessionStep.test.tsx
git commit -m "feat(onboarding-tour): FirstSessionStep (3 action cards routing to real paths)"
```

---

### Task 7: CompleteStep

**Files:**
- Create: `apps/web/src/components/onboarding/tour/CompleteStep.tsx`
- Test: `apps/web/src/components/onboarding/tour/__tests__/CompleteStep.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/onboarding/tour/__tests__/CompleteStep.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { CompleteStep } from '../CompleteStep';

describe('CompleteStep', () => {
  it('renders completion heading and CTA', () => {
    render(<CompleteStep isSubmitting={false} error={null} onHome={vi.fn()} />);
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent(/benvenuto/i);
    expect(screen.getByRole('button', { name: /vai alla home/i })).toBeInTheDocument();
  });

  it('calls onHome on CTA click', async () => {
    const onHome = vi.fn();
    const user = userEvent.setup();
    render(<CompleteStep isSubmitting={false} error={null} onHome={onHome} />);
    await user.click(screen.getByRole('button', { name: /vai alla home/i }));
    expect(onHome).toHaveBeenCalledTimes(1);
  });

  it('disables CTA while submitting', () => {
    render(<CompleteStep isSubmitting={true} error={null} onHome={vi.fn()} />);
    expect(screen.getByRole('button', { name: /vai alla home/i })).toBeDisabled();
  });

  it('shows error with role=alert and a retry affordance', () => {
    render(<CompleteStep isSubmitting={false} error="Rete non disponibile" onHome={vi.fn()} />);
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent(/rete non disponibile/i);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/CompleteStep.test.tsx`
Expected: FAIL — module `../CompleteStep` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/components/onboarding/tour/CompleteStep.tsx`:

```tsx
import type { JSX } from 'react';

import { Btn } from '@/components/ui/v2/btn';

import { Confetti } from './Confetti';

export interface CompleteStepProps {
  readonly isSubmitting: boolean;
  readonly error: string | null;
  readonly onHome: () => void;
}

export function CompleteStep({ isSubmitting, error, onHome }: CompleteStepProps): JSX.Element {
  return (
    <div className="relative flex min-h-[360px] flex-col items-center justify-center gap-6 px-6 py-10 text-center">
      <Confetti />
      <div className="flex h-24 w-24 items-center justify-center rounded-2xl bg-gradient-to-br from-amber-400 to-orange-500 text-4xl font-bold text-white" aria-hidden="true">
        M
      </div>
      <h1 className="font-quicksand text-2xl font-bold sm:text-3xl">
        <span className="bg-gradient-to-r from-amber-500 to-orange-500 bg-clip-text text-transparent">
          Benvenuto!
        </span>
      </h1>
      <p className="font-nunito text-sm text-muted-foreground">
        MeepleAI è pronto.
        <br />
        Buon gioco!
      </p>
      {error && (
        <p role="alert" className="font-nunito text-sm text-destructive">
          {error}
        </p>
      )}
      <Btn
        variant="primary"
        size="lg"
        onClick={onHome}
        disabled={isSubmitting}
        loading={isSubmitting}
      >
        Vai alla home →
      </Btn>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/onboarding/tour/__tests__/CompleteStep.test.tsx`
Expected: PASS (4/4)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/onboarding/tour/CompleteStep.tsx \
        apps/web/src/components/onboarding/tour/__tests__/CompleteStep.test.tsx
git commit -m "feat(onboarding-tour): CompleteStep (confetti + submit/error/home CTA)"
```

---

### Task 8: Barrel export

**Files:**
- Create: `apps/web/src/components/onboarding/tour/index.ts`

- [ ] **Step 1: Write the barrel**

Create `apps/web/src/components/onboarding/tour/index.ts`:

```ts
export { Confetti } from './Confetti';
export { WelcomeStep } from './WelcomeStep';
export { GameSelectStep } from './GameSelectStep';
export { AgentToggleStep, type AgentStates } from './AgentToggleStep';
export { FirstSessionStep, type FirstSessionChoice } from './FirstSessionStep';
export { CompleteStep } from './CompleteStep';
export {
  GAMES,
  AGENTS,
  ACTIONS,
  MIN_SELECTED,
  STEP_LABELS,
  STEP_ENTITIES,
  type TourGame,
  type TourAgent,
  type TourAction,
  type TourActionEntity,
} from './data';
```

- [ ] **Step 2: Verify typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS (no errors in `components/onboarding/tour/**`)

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/onboarding/tour/index.ts
git commit -m "chore(onboarding-tour): barrel export for tour components"
```

---

### Task 9: OnboardingTourClient orchestrator

**Files:**
- Create: `apps/web/src/app/(authenticated)/onboarding/OnboardingTourClient.tsx`
- Test: `apps/web/src/app/(authenticated)/onboarding/__tests__/OnboardingTourClient.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/app/(authenticated)/onboarding/__tests__/OnboardingTourClient.test.tsx`:

```tsx
import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { OnboardingTourClient } from '../OnboardingTourClient';

const routerPush = vi.fn();
const routerReplace = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: routerPush, replace: routerReplace }),
}));

const completeOnboarding = vi.fn();
vi.mock('@/lib/api', () => ({
  createApiClient: () => ({
    auth: {
      completeOnboarding: (...args: unknown[]) => completeOnboarding(...args),
    },
  }),
}));

beforeEach(() => {
  routerPush.mockReset();
  routerReplace.mockReset();
  completeOnboarding.mockReset();
  window.localStorage.clear();
});

afterEach(() => {
  window.localStorage.clear();
});

describe('OnboardingTourClient', () => {
  it('starts on Welcome step and moves to Games on start', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName="Luca" />);
    expect(screen.getByRole('heading', { level: 1, name: /benvenuto/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    expect(screen.getByRole('heading', { level: 2, name: /ludoteca/i })).toBeInTheDocument();
  });

  it('Avanti is disabled until 3 games selected', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName={null} />);
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));

    const advance = screen.getByRole('button', { name: /avanti/i });
    expect(advance).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /catan/i }));
    await user.click(screen.getByRole('button', { name: /azul/i }));
    expect(advance).toBeDisabled();

    await user.click(screen.getByRole('button', { name: /wingspan/i }));
    expect(advance).toBeEnabled();
  });

  it('progresses Welcome→Games→Agents→FirstSession→Complete', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true });
    render(<OnboardingTourClient userName={null} />);

    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    await user.click(screen.getByRole('button', { name: /catan/i }));
    await user.click(screen.getByRole('button', { name: /azul/i }));
    await user.click(screen.getByRole('button', { name: /wingspan/i }));
    await user.click(screen.getByRole('button', { name: /avanti/i }));

    expect(screen.getByRole('heading', { level: 2, name: /assistenti/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /avanti/i }));

    expect(screen.getByRole('heading', { level: 2, name: /cosa vuoi fare/i })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /^salta$/i }));

    expect(screen.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeInTheDocument();
  });

  it('skip from Welcome goes directly to Complete', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName={null} />);
    await user.click(screen.getByRole('button', { name: /salta, esploro da solo/i }));
    expect(screen.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeInTheDocument();
  });

  it('FirstSession action completes onboarding and routes to href', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true });
    render(<OnboardingTourClient userName={null} initialStep={3} />);

    await user.click(screen.getByRole('button', { name: /esplora la library/i }));

    await waitFor(() => expect(completeOnboarding).toHaveBeenCalledWith(false));
    expect(routerPush).toHaveBeenCalledWith('/library');
  });

  it('Complete "Vai alla home" completes onboarding and routes to /library', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockResolvedValue({ ok: true });
    render(<OnboardingTourClient userName={null} initialStep={4} />);

    await user.click(screen.getByRole('button', { name: /vai alla home/i }));
    await waitFor(() => expect(completeOnboarding).toHaveBeenCalledWith(false));
    expect(routerPush).toHaveBeenCalledWith('/library');
  });

  it('shows error and keeps user on Complete when completeOnboarding fails', async () => {
    const user = userEvent.setup();
    completeOnboarding.mockRejectedValue(new Error('Rete non disponibile'));
    render(<OnboardingTourClient userName={null} initialStep={4} />);

    await user.click(screen.getByRole('button', { name: /vai alla home/i }));
    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent(/rete non disponibile/i);
    });
    expect(routerPush).not.toHaveBeenCalled();
  });

  it('persists step to localStorage', async () => {
    const user = userEvent.setup();
    render(<OnboardingTourClient userName={null} />);
    await user.click(screen.getByRole('button', { name: /inizia il tour/i }));
    await waitFor(() =>
      expect(window.localStorage.getItem('mai-onboarding-step')).toBe('1')
    );
  });

  it('resumes from localStorage on mount', () => {
    window.localStorage.setItem('mai-onboarding-step', '2');
    render(<OnboardingTourClient userName={null} />);
    expect(screen.getByRole('heading', { level: 2, name: /assistenti/i })).toBeInTheDocument();
  });

  it('ignores invalid localStorage values', () => {
    window.localStorage.setItem('mai-onboarding-step', '99');
    render(<OnboardingTourClient userName={null} />);
    expect(screen.getByRole('heading', { level: 1, name: /benvenuto in/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run 'src/app/(authenticated)/onboarding/__tests__/OnboardingTourClient.test.tsx'`
Expected: FAIL — module `../OnboardingTourClient` not found

- [ ] **Step 3: Write minimal implementation**

Create `apps/web/src/app/(authenticated)/onboarding/OnboardingTourClient.tsx`:

```tsx
'use client';

import type { JSX } from 'react';
import { useCallback, useEffect, useMemo, useState } from 'react';

import { useRouter } from 'next/navigation';

import {
  AGENTS,
  AgentToggleStep,
  CompleteStep,
  FirstSessionStep,
  GameSelectStep,
  MIN_SELECTED,
  STEP_ENTITIES,
  STEP_LABELS,
  WelcomeStep,
  type AgentStates,
  type FirstSessionChoice,
} from '@/components/onboarding/tour';
import { Btn } from '@/components/ui/v2/btn';
import { StepProgress, type StepEntityKey } from '@/components/ui/v2/step-progress';
import { createApiClient } from '@/lib/api';

const STORAGE_KEY = 'mai-onboarding-step';
const VALID_STEPS = [0, 1, 2, 3, 4] as const;
type TourStep = (typeof VALID_STEPS)[number];

function readStoredStep(): TourStep | null {
  if (typeof window === 'undefined') return null;
  const raw = window.localStorage.getItem(STORAGE_KEY);
  if (raw === null) return null;
  const parsed = Number.parseInt(raw, 10);
  if (!Number.isFinite(parsed)) return null;
  return (VALID_STEPS as readonly number[]).includes(parsed) ? (parsed as TourStep) : null;
}

function defaultAgentStates(): AgentStates {
  return Object.fromEntries(AGENTS.map(a => [a.id, a.defaultOn]));
}

export interface OnboardingTourClientProps {
  readonly userName: string | null;
  /** Override starting step (used in tests). */
  readonly initialStep?: TourStep;
}

export function OnboardingTourClient({
  userName,
  initialStep,
}: OnboardingTourClientProps): JSX.Element {
  const router = useRouter();
  const api = useMemo(() => createApiClient(), []);

  const [step, setStep] = useState<TourStep>(() => {
    if (initialStep !== undefined) return initialStep;
    return readStoredStep() ?? 0;
  });
  const [selectedGames, setSelectedGames] = useState<string[]>([]);
  const [agentStates, setAgentStates] = useState<AgentStates>(defaultAgentStates);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    try {
      window.localStorage.setItem(STORAGE_KEY, String(step));
    } catch {
      // Ignore quota errors — resume becomes no-op.
    }
  }, [step]);

  const go = useCallback((next: TourStep) => {
    setStep(next);
  }, []);

  const complete = useCallback(
    async (redirectTo: string) => {
      setIsSubmitting(true);
      setError(null);
      try {
        await api.auth.completeOnboarding(false);
        try {
          window.localStorage.removeItem(STORAGE_KEY);
        } catch {
          // ignore
        }
        router.push(redirectTo);
      } catch (e) {
        setError(e instanceof Error ? e.message : 'Si è verificato un errore. Riprova.');
      } finally {
        setIsSubmitting(false);
      }
    },
    [api, router]
  );

  const toggleGame = useCallback((id: string) => {
    setSelectedGames(prev =>
      prev.includes(id) ? prev.filter(g => g !== id) : [...prev, id]
    );
  }, []);

  const toggleAgent = useCallback((id: string) => {
    setAgentStates(prev => ({ ...prev, [id]: !prev[id] }));
  }, []);

  const handleChoose = useCallback(
    (choice: FirstSessionChoice) => {
      void complete(choice.href);
    },
    [complete]
  );

  const handleHome = useCallback(() => {
    void complete('/library');
  }, [complete]);

  const stepEntity: StepEntityKey =
    step >= 1 && step <= 3 ? (STEP_ENTITIES[step - 1] as StepEntityKey) : 'game';

  const progressSteps = useMemo(() => STEP_LABELS.map(label => ({ label })), []);

  return (
    <div className="mx-auto flex min-h-screen w-full max-w-md flex-col">
      {step >= 1 && step <= 3 && (
        <div className="flex items-center gap-3 px-4 pt-6">
          <StepProgress
            steps={progressSteps}
            currentIndex={step - 1}
            entity={stepEntity}
            ariaLabel="Avanzamento onboarding"
            className="flex-1"
          />
          <Btn variant="ghost" size="sm" onClick={() => go(4)}>
            Salta
          </Btn>
        </div>
      )}
      <main className="flex-1">
        {step === 0 && (
          <WelcomeStep
            userName={userName}
            onStart={() => go(1)}
            onSkip={() => go(4)}
          />
        )}
        {step === 1 && <GameSelectStep selected={selectedGames} onToggle={toggleGame} />}
        {step === 2 && <AgentToggleStep agentStates={agentStates} onToggle={toggleAgent} />}
        {step === 3 && <FirstSessionStep onChoose={handleChoose} />}
        {step === 4 && (
          <CompleteStep isSubmitting={isSubmitting} error={error} onHome={handleHome} />
        )}
      </main>
      {(step === 1 || step === 2 || step === 3) && (
        <footer className="sticky bottom-0 flex items-center justify-between gap-2 border-t border-border bg-background px-4 py-3">
          {step === 1 ? (
            <span />
          ) : (
            <Btn variant="ghost" onClick={() => go((step - 1) as TourStep)}>
              ← Indietro
            </Btn>
          )}
          {step === 1 && (
            <Btn
              variant="primary"
              entity="game"
              disabled={selectedGames.length < MIN_SELECTED}
              onClick={() => go(2)}
            >
              Avanti → ({selectedGames.length}/{MIN_SELECTED})
            </Btn>
          )}
          {step === 2 && (
            <Btn variant="primary" entity="agent" onClick={() => go(3)}>
              Avanti →
            </Btn>
          )}
          {step === 3 && (
            <Btn variant="ghost" onClick={() => go(4)}>
              Salta
            </Btn>
          )}
        </footer>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run 'src/app/(authenticated)/onboarding/__tests__/OnboardingTourClient.test.tsx'`
Expected: PASS (9/9)

- [ ] **Step 5: Commit**

```bash
git add 'apps/web/src/app/(authenticated)/onboarding/OnboardingTourClient.tsx' \
        'apps/web/src/app/(authenticated)/onboarding/__tests__/OnboardingTourClient.test.tsx'
git commit -m "feat(onboarding-tour): OnboardingTourClient orchestrator (state, navigation, API)"
```

---

### Task 10: Replace page.tsx + remove old test

**Files:**
- Modify: `apps/web/src/app/(authenticated)/onboarding/page.tsx` (full rewrite)
- Delete: `apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx`

- [ ] **Step 1: Delete old page test**

Run:
```bash
rm "apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx"
```

- [ ] **Step 2: Rewrite page.tsx**

Replace entire contents of `apps/web/src/app/(authenticated)/onboarding/page.tsx`:

```tsx
'use client';

import type { JSX } from 'react';
import { useEffect } from 'react';

import { Loader2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';

import { OnboardingTourClient } from './OnboardingTourClient';

export default function OnboardingPage(): JSX.Element | null {
  const router = useRouter();
  const { user, loading, refreshUser } = useAuth();

  useEffect(() => {
    void refreshUser();
  }, [refreshUser]);

  useEffect(() => {
    if (!loading && user?.onboardingCompleted) {
      router.replace('/library');
    }
  }, [loading, user, router]);

  if (loading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-amber-500" aria-label="Caricamento" />
      </div>
    );
  }

  if (!user || user.onboardingCompleted) {
    return null;
  }

  const userName = user.displayName?.trim() || null;
  return <OnboardingTourClient userName={userName} />;
}
```

- [ ] **Step 3: Run affected tests**

Run: `cd apps/web && pnpm vitest run 'src/app/(authenticated)/onboarding'`
Expected: PASS (all remaining onboarding tests green)

- [ ] **Step 4: Run typecheck + lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`
Expected: PASS with no errors

- [ ] **Step 5: Commit**

```bash
git add 'apps/web/src/app/(authenticated)/onboarding/page.tsx'
git rm 'apps/web/src/app/(authenticated)/onboarding/__tests__/page.test.tsx'
git commit -m "refactor(onboarding): /onboarding uses OnboardingTourClient (drop profile form)"
```

---

### Task 11: E2E happy path + skip

**Files:**
- Create: `apps/web/e2e/onboarding-tour.spec.ts`

- [ ] **Step 1: Write the E2E spec**

Create `apps/web/e2e/onboarding-tour.spec.ts`:

```ts
// /onboarding is inside the (authenticated) route group and requires a session
// with `onboardingCompleted: false`. `userPage` provides such a user via MSW scenarios.
import { expect, test } from './fixtures/auth';

test.describe('Onboarding product tour', () => {
  test('mobile: full happy path lands on /library', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/onboarding');

    await expect(page.getByRole('heading', { level: 1, name: /benvenuto in/i })).toBeVisible();
    await page.getByRole('button', { name: /inizia il tour/i }).click();

    // Games
    await expect(page.getByRole('heading', { level: 2, name: /ludoteca/i })).toBeVisible();
    await page.getByRole('button', { name: /catan/i }).click();
    await page.getByRole('button', { name: /azul/i }).click();
    await page.getByRole('button', { name: /wingspan/i }).click();
    await page.getByRole('button', { name: /avanti/i }).click();

    // Agents
    await expect(page.getByRole('heading', { level: 2, name: /assistenti/i })).toBeVisible();
    await page.getByRole('button', { name: /avanti/i }).click();

    // FirstSession → skip to Complete
    await expect(page.getByRole('heading', { level: 2, name: /cosa vuoi fare/i })).toBeVisible();
    await page.getByRole('button', { name: /^salta$/i }).click();

    // Complete → home
    await expect(page.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeVisible();
    await page.getByRole('button', { name: /vai alla home/i }).click();

    await page.waitForURL('**/library');
  });

  test('skip from Welcome goes to Complete then /library', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/onboarding');

    await page.getByRole('button', { name: /salta, esploro da solo/i }).click();
    await expect(page.getByRole('heading', { level: 1, name: /benvenuto!/i })).toBeVisible();
    await page.getByRole('button', { name: /vai alla home/i }).click();

    await page.waitForURL('**/library');
  });

  test('FirstSession CTA routes to chosen href', async ({ userPage: page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto('/onboarding');

    await page.getByRole('button', { name: /inizia il tour/i }).click();
    await page.getByRole('button', { name: /catan/i }).click();
    await page.getByRole('button', { name: /azul/i }).click();
    await page.getByRole('button', { name: /wingspan/i }).click();
    await page.getByRole('button', { name: /avanti/i }).click();
    await page.getByRole('button', { name: /avanti/i }).click();

    await page.getByRole('button', { name: /esplora la library/i }).click();
    await page.waitForURL('**/library');
  });
});
```

- [ ] **Step 2: Verify it lints**

Run: `cd apps/web && pnpm lint e2e/onboarding-tour.spec.ts`
Expected: PASS

*Full e2e execution requires integration env; do not run here. The spec is verified by existing CI workflow on PR.*

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/onboarding-tour.spec.ts
git commit -m "test(e2e): onboarding tour happy path + skip + first-session CTA"
```

---

### Task 12: Update bundle baseline + push

**Files:**
- Modify: `apps/web/bundle-size-baseline.json`

- [ ] **Step 1: Run production build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds. Note bundle size output.

- [ ] **Step 2: Measure actual total**

Run:
```bash
cd apps/web && node -e "const fs=require('fs'),p=require('path'),d=p.join('.next','static','chunks');let t=0;for(const f of fs.readdirSync(d,{recursive:true})){const fp=p.join(d,f);if(fs.statSync(fp).isFile())t+=fs.statSync(fp).size}console.log(t)"
```

Note the number printed (call it `NEW_TOTAL`).

- [ ] **Step 3: Update baseline**

Read current `apps/web/bundle-size-baseline.json`. Replace `totalBytes` with `NEW_TOTAL` from Step 2 and `updatedAt` with today's date. Keep `toleranceBytes: 10240`.

Example (values will differ — use actual measurement):

```json
{
  "description": "Baseline JS bundle size for prod build (no mock). Update manually in dedicated PRs.",
  "updatedAt": "2026-04-21",
  "totalBytes": 13050000,
  "toleranceBytes": 10240
}
```

- [ ] **Step 4: Verify bundle-size test passes**

Run: `cd apps/web && pnpm vitest run __tests__/bundle-size.test.ts`
Expected: PASS

- [ ] **Step 5: Commit + push**

```bash
git add apps/web/bundle-size-baseline.json
git commit -m "chore(bundle): update baseline after /onboarding tour migration"
git push origin refactor/redesign-v2-m6
```

- [ ] **Step 6: Add PR #484 comment with M2.5 scope**

Run:
```bash
gh pr comment 484 --body "$(cat <<'EOF'
## Fase 2.5 Onboarding product-tour — added to this PR

Extended PR #484 scope with the M2.5 Onboarding product-tour (Tasks 1-12 of the 2026-04-21 plan).

### New components (\`src/components/onboarding/tour/\`)
- \`data.ts\` — GAMES (8), AGENTS (4), ACTIONS (3), MIN_SELECTED, STEP_LABELS, STEP_ENTITIES
- \`Confetti\` — 40 animated pieces, motion-safe
- \`WelcomeStep\`, \`GameSelectStep\`, \`AgentToggleStep\`, \`FirstSessionStep\`, \`CompleteStep\`

### New orchestrator
- \`/onboarding\` rewritten from single-step profile form to 5-step tour
- \`OnboardingTourClient\` handles step state, localStorage resume, \`completeOnboarding(false)\` at the end
- \`displayName\`/avatar deferred to \`/settings/profile\`

### Tests
- 7 unit test files (data + 5 steps + orchestrator): ~30 assertions
- 3 Playwright E2E smoke tests (happy path, skip, direct CTA)

### Bundle baseline
- Bumped after \`pnpm build\` — see diff in \`bundle-size-baseline.json\`

### Spec + plan
- \`docs/superpowers/specs/2026-04-20-onboarding-product-tour-design.md\`
- \`docs/superpowers/plans/2026-04-21-onboarding-product-tour.md\`
EOF
)"
```

---

## Self-Review

**Spec coverage:** Each spec section maps to tasks:
- Route `/onboarding` rewrite → Task 10
- 5-step flow → Tasks 3-7 + Task 9 (orchestrator)
- `data.ts` + ACTIONS real hrefs → Task 1
- Progress bar entity-accented → Task 9 (uses existing StepProgress)
- State shape + localStorage resume → Task 9 tests
- `completeOnboarding(false)` only call → Task 9 (and tested)
- A11y (aria-pressed, role=switch, aria-live, progressbar, prefers-reduced-motion) → covered in each step (Tasks 4, 5, 7 + Confetti `motion-reduce:hidden`)
- Error handling → Task 7 + Task 9 (error test)
- Out of scope (WelcomeChecklist, backend persist, OnboardingWizard) → untouched
- Bundle baseline update → Task 12
- E2E tests → Task 11
- Test matrix → Tasks 1-9

**Placeholder scan:** No "TBD", "TODO", "similar to Task N", or placeholder error-handling. Bundle baseline Step 3 uses example JSON with note to substitute actual measurement — explicit.

**Type consistency:**
- `AgentStates` defined in `AgentToggleStep.tsx` (Task 5), re-exported from barrel (Task 8), consumed by orchestrator (Task 9) ✓
- `FirstSessionChoice` defined in `FirstSessionStep.tsx` (Task 6), re-exported, consumed ✓
- `TourStep` type internal to orchestrator ✓
- `StepEntityKey` imported from `step-progress` primitive (matches actual export) ✓
- `Btn` imported from `@/components/ui/v2/btn` with `variant`, `size`, `entity`, `fullWidth`, `loading`, `disabled`, `onClick` props (all verified against actual `btn.tsx`) ✓
- `ToggleSwitch` imported with `entity`, `checked`, `onCheckedChange`, `ariaLabel` (all verified against actual file) ✓
- `STORAGE_KEY = 'mai-onboarding-step'` matches mockup reference ✓

**Gaps found & fixed:** None.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-04-21-onboarding-product-tour.md`. Two execution options:

**1. Subagent-Driven (recommended)** — I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** — Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**
