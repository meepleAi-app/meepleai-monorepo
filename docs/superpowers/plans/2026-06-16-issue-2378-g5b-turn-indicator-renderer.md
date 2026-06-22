# #2378 G5b TurnIndicatorRenderer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Ship a polymorphic `TurnIndicatorRenderer` switching on 7 `TurnOrderType` variants with an `Unknown` fallback, while preserving the existing `TurnIndicator` component as the RoundRobin branch body.

**Architecture:** 1 type module (`turn-state.ts` — discriminated union) + 1 dispatcher + 6 new branch components + 1 Unknown branch + 14 i18n keys + 9+ unit tests + 1 axe AA test. `SessionLiveView` swaps its `<TurnIndicator>` consumer for `<TurnIndicatorRenderer state={…}>` with a fixture-driven RoundRobin state.

**Tech Stack:** Next.js 16 App Router · React 19 · Vitest + Testing Library · jest-axe · react-intl (Gate A in parent).

**Spec:** `docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md`

**Branch:** `feature/issue-2378-g5b-turn-indicator-renderer` (parent `main-dev`)

**5 DEC user-locked:** 7 variant · keep TurnIndicator + new Renderer · 7° = FirstPlayerToken · discriminated union · Unknown → warning banner + console.warn.

---

## File Structure

**New (10):**
- `apps/web/src/lib/session-live/turn-state.ts` — `TurnOrderType` + `TurnState` discriminated union + helpers.
- `apps/web/src/components/features/session-live/turn-indicator-renderer/TurnIndicatorRenderer.tsx` — parent dispatcher (NEW folder).
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/RoundRobinBranch.tsx` — wraps existing `TurnIndicator`.
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/SequentialBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/SimultaneousBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/RealtimeBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/NoneBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/CustomBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/FirstPlayerTokenBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/UnknownBranch.tsx`
- `apps/web/src/components/features/session-live/turn-indicator-renderer/__tests__/TurnIndicatorRenderer.test.tsx`
- `apps/web/__tests__/session-live-turn-indicator-renderer-axe.test.tsx`

**Modified (4):**
- `apps/web/src/components/features/session-live/TurnIndicator.tsx` — REMOVE `data-slot="turn-indicator"` (moves to Renderer root). All other props/behavior unchanged.
- `apps/web/src/components/features/session-live/index.ts` — add barrel export for `TurnIndicatorRenderer` + types.
- `apps/web/src/locales/it.json` + `apps/web/src/locales/en.json` — add 14 keys under `pages.sessionLive.turnIndicator.*`.
- `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` — swap `<TurnIndicator>` consumer to `<TurnIndicatorRenderer state={…}>` with a `RoundRobin` fixture (real wiring deferred to #2389 follow-up).

---

## Task 1: `turn-state.ts` type contract

**Files:** Create `apps/web/src/lib/session-live/turn-state.ts`

- [ ] **Step 1.1: Write the file**

```ts
'use client';

/**
 * TurnState — Issue #2378 G5b.
 *
 * Discriminated union covering all 7 `TurnOrderType` variants plus shared
 * `PlayerInfo` shape. The renderer dispatcher narrows on `state.type`.
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md §4
 */

export type TurnOrderType =
  | 'RoundRobin'
  | 'Sequential'
  | 'Simultaneous'
  | 'Realtime'
  | 'None'
  | 'Custom'
  | 'FirstPlayerToken';

export interface PlayerInfo {
  readonly id: string;
  readonly name: string;
  readonly avatarUrl?: string;
}

export type TurnState =
  | {
      readonly type: 'RoundRobin';
      readonly round: number;
      readonly totalRounds: number;
      readonly activePlayerId: string;
      readonly playOrder: ReadonlyArray<string>;
    }
  | {
      readonly type: 'Sequential';
      readonly phases: ReadonlyArray<string>;
      readonly activePhaseIndex: number;
    }
  | {
      readonly type: 'Simultaneous';
      readonly phases?: ReadonlyArray<string>;
      readonly activePhaseIndex?: number;
    }
  | { readonly type: 'Realtime' }
  | { readonly type: 'None' }
  | {
      readonly type: 'Custom';
      readonly phases: ReadonlyArray<string>;
      readonly activePhaseIndex: number;
    }
  | {
      readonly type: 'FirstPlayerToken';
      readonly round: number;
      readonly totalRounds: number;
      readonly tokenHolderId: string;
      readonly playOrder: ReadonlyArray<string>;
    };
```

- [ ] **Step 1.2: Typecheck + commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/lib/session-live/turn-state.ts
git commit -m "feat(session-live): #2378 add TurnState discriminated union (7 variant)"
```

---

## Task 2: 7 branch components (TDD per branch)

**Files (8 new, 1 modified):**

- Create: 7 branch components + 1 Unknown branch under
  `apps/web/src/components/features/session-live/turn-indicator-renderer/branches/`
- Modify: `apps/web/src/components/features/session-live/TurnIndicator.tsx` —
  REMOVE `data-slot="turn-indicator"` attribute on the `<div>` root.

For each branch the contract is:

```ts
export interface BranchProps {
  readonly state: Extract<TurnState, { type: 'X' }>;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels; // single labels obj shared
}
```

Each branch renders a `<section data-slot="turn-branch-{kebabCase(type)}">`.

- [ ] **Step 2.1: RoundRobinBranch — wraps existing `TurnIndicator`**

Renders heading `{labels.roundRobinHeading}` + reuses
`<TurnIndicator current={state.round} total={state.totalRounds} … />` and
adds a play-order strip below (mockup `RoundRobinTurn` lines 337-388).

Code skeleton:

```tsx
import { TurnIndicator } from '../../TurnIndicator';
import type { PlayerInfo, TurnState } from '@/lib/session-live/turn-state';
import type { TurnIndicatorRendererLabels } from '../TurnIndicatorRenderer';

interface Props {
  state: Extract<TurnState, { type: 'RoundRobin' }>;
  players: ReadonlyArray<PlayerInfo>;
  viewerId: string;
  compact?: boolean;
  labels: TurnIndicatorRendererLabels;
}

export function RoundRobinBranch({ state, players, viewerId, compact, labels }: Props) {
  const activePlayer = players.find(p => p.id === state.activePlayerId);
  const activeName = activePlayer?.name ?? 'Sconosciuto';
  const isMyTurn = state.activePlayerId === viewerId;
  return (
    <section
      data-slot="turn-branch-round-robin"
      role="region"
      aria-label={labels.roundRobinHeading}
    >
      <h4>{labels.roundRobinHeading}</h4>
      <TurnIndicator
        current={state.round}
        total={state.totalRounds}
        activePlayerName={activeName}
        isMyTurn={isMyTurn}
        compact={compact}
        labels={{
          currentTurnAriaLabel: labels.roundCountTemplate,
          activePlayerLabel: '{playerName}',
          yourTurnLabel: labels.yourTurnLabel,
          waitingLabel: labels.waitingLabel,
        }}
      />
      <p>{labels.playOrderHeading}</p>
      <ol>
        {state.playOrder.map(id => {
          const p = players.find(q => q.id === id);
          return <li key={id}>{p?.name ?? id}</li>;
        })}
      </ol>
    </section>
  );
}
```

(Tailwind classes per mockup design tokens — semantic only, no raw HSL.)

- [ ] **Step 2.2 → 2.7: SequentialBranch, SimultaneousBranch, RealtimeBranch, NoneBranch, CustomBranch, FirstPlayerTokenBranch**

Each branch mirrors its mockup component (mockup lines 390-462 for the first
5, FirstPlayerToken added per DEC-3):

- **SequentialBranch**: PhaseStepper component (ordered list with active/past/future styling), heading + body text.
- **SimultaneousBranch**: player grid + optional day-phase strip.
- **RealtimeBranch**: warning banner + parallel marker (no individual turn).
- **NoneBranch**: free-form banner.
- **CustomBranch**: same PhaseStepper as Sequential but toolkit-driven heading.
- **FirstPlayerTokenBranch**: token holder avatar + rotation arrow + round counter (similar to RoundRobin but tokenHolderId instead of activePlayerId).

Each branch wraps active turn-affecting content in `aria-live="polite"` per
spec §6. Simultaneous/Realtime/None use `role="status"` for the banner.

The internal `PhaseStepper` helper is duplicated in Sequential and Custom
branches OR extracted to `apps/web/src/components/features/session-live/turn-indicator-renderer/internals/PhaseStepper.tsx`. Prefer **extraction** to avoid duplication.

Code skeletons follow the same pattern as Step 2.1. Use Tailwind semantic
tokens only (`bg-card`, `text-foreground`, `border-border`, `text-entity-player`,
`bg-entity-player/10`, etc.). NO raw HSL literals.

- [ ] **Step 2.8: UnknownBranch**

```tsx
interface Props {
  state: TurnState;
  labels: TurnIndicatorRendererLabels;
}

export function UnknownBranch({ state, labels }: Props) {
  return (
    <section
      data-slot="turn-branch-unknown"
      role="status"
      aria-live="polite"
    >
      <p className="font-bold">{labels.unknownTitle}</p>
      <p>{labels.unknownBody}</p>
      <code className="text-xs">turnOrderType: {(state as { type: string }).type}</code>
    </section>
  );
}
```

- [ ] **Step 2.9: Remove `data-slot="turn-indicator"` from `TurnIndicator.tsx`**

```tsx
// Before (line 52):
<div
  data-slot="turn-indicator"
  className={...}
>

// After:
<div className={...}>
```

This avoids double-selectors when `TurnIndicatorRenderer` (which becomes the
new owner of `data-slot="turn-indicator"`) wraps `TurnIndicator` via
`RoundRobinBranch`. Search the codebase for `data-slot="turn-indicator"`
references in tests:

```bash
grep -rn "turn-indicator" apps/web --include "*.test.tsx" --include "*.test.ts" --include "*.spec.ts"
```

If any test query targets `[data-slot="turn-indicator"]` and expects the
RoundRobin progress-bar markup, leave the test alone — the renderer mount
re-adds the same selector at a higher level, and TurnIndicator's children
still render inside.

- [ ] **Step 2.10: Commit per branch (8 commits total — TDD discipline)**

For EACH branch, the cycle is:
- Write failing unit test in `__tests__/<BranchName>.test.tsx`
- Run → expect FAIL (component not found).
- Write branch component.
- Run → expect PASS.
- Commit:

```bash
git add apps/web/src/components/features/session-live/turn-indicator-renderer/branches/<BranchName>.tsx \
        apps/web/src/components/features/session-live/turn-indicator-renderer/branches/__tests__/<BranchName>.test.tsx
git commit -m "feat(session-live): #2378 <BranchName> (<TurnOrderType> branch)"
```

(Group the `TurnIndicator.tsx` `data-slot` removal + the `PhaseStepper`
internal helper into the RoundRobinBranch / SequentialBranch commit as
needed.)

Each branch test verifies:
1. Renders without crashing.
2. Heading from `labels` is visible.
3. Branch-specific content (active player name for RoundRobin, phase name
   for Sequential/Custom, all-player grid for Simultaneous, etc.) renders.
4. `aria-live` (where applicable) is present.

---

## Task 3: `TurnIndicatorRenderer.tsx` dispatcher

**Files:** Create `apps/web/src/components/features/session-live/turn-indicator-renderer/TurnIndicatorRenderer.tsx`

- [ ] **Step 3.1: Write failing dispatcher test**

```tsx
// turn-indicator-renderer/__tests__/TurnIndicatorRenderer.test.tsx
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IntlProvider } from 'react-intl';

import { TurnIndicatorRenderer } from '../TurnIndicatorRenderer';
import type { TurnState, PlayerInfo } from '@/lib/session-live/turn-state';

const PLAYERS: PlayerInfo[] = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Sara' },
];

const LABELS = {
  roundRobinHeading: 'Round-robin',
  sequentialHeading: 'Fasi',
  simultaneousHeading: 'Simultaneo',
  realtimeHeading: 'Tempo reale',
  noneHeading: 'Libero',
  customHeading: 'Custom',
  firstPlayerTokenHeading: 'Token primo giocatore',
  unknownTitle: 'Tipo di turno non supportato',
  unknownBody: 'Aggiorna l\'app per supportare questa modalità.',
  yourTurnLabel: 'Tuo turno',
  waitingLabel: 'In attesa',
  roundCountTemplate: 'Round {current} di {total}',
  playOrderHeading: 'Ordine di gioco',
  firstPlayerTokenHolderTemplate: 'Token: {playerName}',
};

function renderRenderer(state: TurnState) {
  return render(
    <IntlProvider locale="it" messages={{}}>
      <TurnIndicatorRenderer
        state={state}
        players={PLAYERS}
        viewerId="p1"
        labels={LABELS}
      />
    </IntlProvider>
  );
}

describe('TurnIndicatorRenderer dispatch', () => {
  it.each([
    ['RoundRobin', { type: 'RoundRobin', round: 1, totalRounds: 4, activePlayerId: 'p1', playOrder: ['p1', 'p2'] }, 'turn-branch-round-robin'],
    ['Sequential', { type: 'Sequential', phases: ['Mattina', 'Notte'], activePhaseIndex: 0 }, 'turn-branch-sequential'],
    ['Simultaneous', { type: 'Simultaneous' }, 'turn-branch-simultaneous'],
    ['Realtime', { type: 'Realtime' }, 'turn-branch-realtime'],
    ['None', { type: 'None' }, 'turn-branch-none'],
    ['Custom', { type: 'Custom', phases: ['F1'], activePhaseIndex: 0 }, 'turn-branch-custom'],
    ['FirstPlayerToken', { type: 'FirstPlayerToken', round: 1, totalRounds: 4, tokenHolderId: 'p1', playOrder: ['p1', 'p2'] }, 'turn-branch-first-player-token'],
  ] as const)('renders %s branch', (_name, state, slot) => {
    const { container } = renderRenderer(state as TurnState);
    expect(container.querySelector(`[data-slot="${slot}"]`)).not.toBeNull();
  });

  it('renders Unknown branch when type is not registered + warns', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    // @ts-expect-error — deliberately invalid type for fallback test
    const { container } = renderRenderer({ type: 'BogusType' });
    expect(container.querySelector('[data-slot="turn-branch-unknown"]')).not.toBeNull();
    expect(warn).toHaveBeenCalledWith(
      '[TurnIndicatorRenderer] Unknown turnOrderType:',
      'BogusType'
    );
    warn.mockRestore();
  });

  it('renders Unknown gracefully when activePlayerId is not in players', () => {
    const { container } = renderRenderer({
      type: 'RoundRobin',
      round: 1,
      totalRounds: 4,
      activePlayerId: 'ghost',
      playOrder: ['ghost'],
    });
    expect(container.querySelector('[data-slot="turn-branch-round-robin"]')).not.toBeNull();
    expect(screen.getByText(/Sconosciuto/)).toBeInTheDocument();
  });

  it('renders empty phases gracefully (Sequential/Custom)', () => {
    const { container } = renderRenderer({
      type: 'Sequential',
      phases: [],
      activePhaseIndex: 0,
    });
    expect(container.querySelector('[data-slot="turn-branch-sequential"]')).not.toBeNull();
  });
});
```

- [ ] **Step 3.2: Run test (expect 10 FAIL)**

- [ ] **Step 3.3: Write `TurnIndicatorRenderer.tsx`**

```tsx
'use client';

/**
 * TurnIndicatorRenderer — Issue #2378 G5b polymorphic dispatcher.
 *
 * Switches on `state.type` (`TurnOrderType`) and renders one of 7 branch
 * components, or an `UnknownBranch` fallback for unregistered values.
 *
 * §5 contract (epic #2354 G5):
 *   - data-slot="turn-indicator" on the root <section>
 *   - children are sub-section data-slot="turn-branch-<kebab>"
 *   - No SSE state inside the dispatcher (parent owns the state)
 *
 * @see docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md §3
 */

import type { ReactElement } from 'react';

import type { TurnState, PlayerInfo, TurnOrderType } from '@/lib/session-live/turn-state';

import { RoundRobinBranch } from './branches/RoundRobinBranch';
import { SequentialBranch } from './branches/SequentialBranch';
import { SimultaneousBranch } from './branches/SimultaneousBranch';
import { RealtimeBranch } from './branches/RealtimeBranch';
import { NoneBranch } from './branches/NoneBranch';
import { CustomBranch } from './branches/CustomBranch';
import { FirstPlayerTokenBranch } from './branches/FirstPlayerTokenBranch';
import { UnknownBranch } from './branches/UnknownBranch';

export interface TurnIndicatorRendererLabels {
  readonly roundRobinHeading: string;
  readonly sequentialHeading: string;
  readonly simultaneousHeading: string;
  readonly realtimeHeading: string;
  readonly noneHeading: string;
  readonly customHeading: string;
  readonly firstPlayerTokenHeading: string;
  readonly unknownTitle: string;
  readonly unknownBody: string;
  readonly yourTurnLabel: string;
  readonly waitingLabel: string;
  readonly roundCountTemplate: string;
  readonly playOrderHeading: string;
  readonly firstPlayerTokenHolderTemplate: string;
}

export interface TurnIndicatorRendererProps {
  readonly state: TurnState;
  readonly players: ReadonlyArray<PlayerInfo>;
  readonly viewerId: string;
  readonly compact?: boolean;
  readonly labels: TurnIndicatorRendererLabels;
}

const KNOWN_TYPES: ReadonlySet<TurnOrderType> = new Set([
  'RoundRobin',
  'Sequential',
  'Simultaneous',
  'Realtime',
  'None',
  'Custom',
  'FirstPlayerToken',
]);

export function TurnIndicatorRenderer({
  state,
  players,
  viewerId,
  compact,
  labels,
}: TurnIndicatorRendererProps): ReactElement {
  const isKnown = KNOWN_TYPES.has(state.type as TurnOrderType);

  if (!isKnown) {
    // Defensive: surface unknown types in logs so operators see new BE enums.
    console.warn('[TurnIndicatorRenderer] Unknown turnOrderType:', (state as { type: string }).type);
    return (
      <section data-slot="turn-indicator" role="region" aria-label={labels.unknownTitle}>
        <UnknownBranch state={state} labels={labels} />
      </section>
    );
  }

  const heading = (() => {
    switch (state.type) {
      case 'RoundRobin': return labels.roundRobinHeading;
      case 'Sequential': return labels.sequentialHeading;
      case 'Simultaneous': return labels.simultaneousHeading;
      case 'Realtime': return labels.realtimeHeading;
      case 'None': return labels.noneHeading;
      case 'Custom': return labels.customHeading;
      case 'FirstPlayerToken': return labels.firstPlayerTokenHeading;
    }
  })();

  return (
    <section data-slot="turn-indicator" role="region" aria-label={heading}>
      {state.type === 'RoundRobin' && (
        <RoundRobinBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
      {state.type === 'Sequential' && (
        <SequentialBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
      {state.type === 'Simultaneous' && (
        <SimultaneousBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
      {state.type === 'Realtime' && (
        <RealtimeBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
      {state.type === 'None' && (
        <NoneBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
      {state.type === 'Custom' && (
        <CustomBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
      {state.type === 'FirstPlayerToken' && (
        <FirstPlayerTokenBranch state={state} players={players} viewerId={viewerId} compact={compact} labels={labels} />
      )}
    </section>
  );
}
```

- [ ] **Step 3.4: Run tests → expect 10 PASS**

- [ ] **Step 3.5: Add barrel export in `apps/web/src/components/features/session-live/index.ts`:**

```ts
export { TurnIndicatorRenderer } from '@/components/features/session-live/turn-indicator-renderer/TurnIndicatorRenderer';
export type {
  TurnIndicatorRendererLabels,
  TurnIndicatorRendererProps,
} from '@/components/features/session-live/turn-indicator-renderer/TurnIndicatorRenderer';
```

- [ ] **Step 3.6: Commit**

```bash
git add apps/web/src/components/features/session-live/turn-indicator-renderer/ \
        apps/web/src/components/features/session-live/index.ts
git commit -m "feat(session-live): #2378 TurnIndicatorRenderer dispatcher + 10 unit tests"
```

---

## Task 4: i18n keys (14 new)

**Files:** modify `apps/web/src/locales/it.json` + `apps/web/src/locales/en.json`

- [ ] **Step 4.1: Locate the existing `turnIndicator` block**

```bash
grep -n '"turnIndicator"' apps/web/src/locales/it.json
grep -n '"turnIndicator"' apps/web/src/locales/en.json
```

The existing block (under `pages.sessionLive.turnIndicator`) has 4 keys
(`currentTurnAriaLabel`, `activePlayerLabel`, `yourTurnLabel`, `waitingLabel`).
We ADD 10 new keys (the 4 existing stay) for a total of 14:

```json
"turnIndicator": {
  "currentTurnAriaLabel": "...",
  "activePlayerLabel": "...",
  "yourTurnLabel": "...",
  "waitingLabel": "...",
  "roundRobinHeading": "Turno attivo",
  "sequentialHeading": "Fasi",
  "simultaneousHeading": "Tutti giocano",
  "realtimeHeading": "Tempo reale",
  "noneHeading": "Gioco libero",
  "customHeading": "Sequenza custom",
  "firstPlayerTokenHeading": "Token primo giocatore",
  "unknownTitle": "Tipo di turno non supportato",
  "unknownBody": "Aggiorna l'app per supportare questa modalità.",
  "roundCountTemplate": "Round {current} di {total}",
  "playOrderHeading": "Ordine di gioco",
  "firstPlayerTokenHolderTemplate": "Token primo giocatore: {playerName}"
}
```

(Mirror in `en.json` with English copy.)

- [ ] **Step 4.2: Typecheck + commit**

```bash
cd apps/web && pnpm typecheck
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "feat(i18n): #2378 turnIndicator 10 new keys (G5b polymorphic renderer)"
```

---

## Task 5: SessionLiveView swap consumer (fixture-driven)

**Files:** modify `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`

- [ ] **Step 5.1: Build a `TurnIndicatorRendererLabels` memo + temporary fixture**

```tsx
// near the other label memos:
const turnRendererLabels = useMemo<TurnIndicatorRendererLabels>(
  (): TurnIndicatorRendererLabels => ({
    roundRobinHeading: t('pages.sessionLive.turnIndicator.roundRobinHeading'),
    sequentialHeading: t('pages.sessionLive.turnIndicator.sequentialHeading'),
    simultaneousHeading: t('pages.sessionLive.turnIndicator.simultaneousHeading'),
    realtimeHeading: t('pages.sessionLive.turnIndicator.realtimeHeading'),
    noneHeading: t('pages.sessionLive.turnIndicator.noneHeading'),
    customHeading: t('pages.sessionLive.turnIndicator.customHeading'),
    firstPlayerTokenHeading: t('pages.sessionLive.turnIndicator.firstPlayerTokenHeading'),
    unknownTitle: t('pages.sessionLive.turnIndicator.unknownTitle'),
    unknownBody: t('pages.sessionLive.turnIndicator.unknownBody'),
    yourTurnLabel: t('pages.sessionLive.turnIndicator.yourTurnLabel'),
    waitingLabel: t('pages.sessionLive.turnIndicator.waitingLabel'),
    roundCountTemplate:
      (intl.messages['pages.sessionLive.turnIndicator.roundCountTemplate'] as string) ??
      'Round {current} di {total}',
    playOrderHeading: t('pages.sessionLive.turnIndicator.playOrderHeading'),
    firstPlayerTokenHolderTemplate:
      (intl.messages['pages.sessionLive.turnIndicator.firstPlayerTokenHolderTemplate'] as string) ??
      'Token primo giocatore: {playerName}',
  }),
  [t, intl.messages]
);
```

Build a temporary RoundRobin fixture from `activeSession` (the real wiring
from `useLiveSessionStore.scoringType` etc. is deferred to **#2389** — same
gate as G5a):

```tsx
const turnRendererState = useMemo<TurnState>(
  (): TurnState => ({
    type: 'RoundRobin',
    round: activeSession.currentTurn,
    totalRounds: activeSession.totalTurns,
    activePlayerId: activeSession.activePlayerId ?? '',
    playOrder: activeSession.players.map(p => p.id),
  }),
  [activeSession]
);

const turnRendererPlayers = useMemo<ReadonlyArray<PlayerInfo>>(
  () => activeSession.players.map(p => ({ id: p.id, name: p.name })),
  [activeSession.players]
);
```

- [ ] **Step 5.2: Replace the desktop+mobile `<TurnIndicator>` usage**

Find the `'turn'` tab handler in both `desktopRightColumn` and
`mobileSheetContent` (~lines 1100 and 920). Currently:

```tsx
<TurnIndicator
  current={activeSession.currentTurn}
  total={activeSession.totalTurns}
  activePlayerName={activePlayerName}
  isMyTurn={isMyTurn}
  labels={turnIndicatorLabels}
/>
<PlayerRosterLive … />
```

Replace `<TurnIndicator … />` ONLY (keep `<PlayerRosterLive>` alongside) with:

```tsx
<TurnIndicatorRenderer
  state={turnRendererState}
  players={turnRendererPlayers}
  viewerId={activeSession.viewerId}
  labels={turnRendererLabels}
/>
```

For the mobile variant add `compact` prop.

- [ ] **Step 5.3: Run tests + typecheck**

```bash
cd apps/web && pnpm test src/app/\(authenticated\)/sessions/\[id\]/live --run
cd apps/web && pnpm typecheck
```

Expected: existing SessionLiveView tests still pass (the `[data-slot="turn-indicator"]` selector still resolves — now on the renderer root).

- [ ] **Step 5.4: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/\[id\]/live/_components/SessionLiveView.tsx
git commit -m "feat(session-live): #2378 wire SessionLiveView to TurnIndicatorRenderer"
```

---

## Task 6: axe AA component test (all 7 + Unknown)

**Files:** Create `apps/web/__tests__/session-live-turn-indicator-renderer-axe.test.tsx`

- [ ] **Step 6.1: Write the axe test**

```tsx
import { describe, it, expect, vi, beforeAll, afterAll } from 'vitest';
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { IntlProvider } from 'react-intl';

import { TurnIndicatorRenderer } from '@/components/features/session-live';
import type { TurnState, PlayerInfo } from '@/lib/session-live/turn-state';

const PLAYERS: PlayerInfo[] = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Sara' },
];

const LABELS = {
  // (same fixture as Task 3.1)
};

beforeAll(() => {
  vi.spyOn(console, 'warn').mockImplementation(() => {});
});

afterAll(() => {
  vi.restoreAllMocks();
});

function renderRenderer(state: TurnState) {
  return render(
    <IntlProvider locale="it" messages={{}}>
      <TurnIndicatorRenderer state={state} players={PLAYERS} viewerId="p1" labels={LABELS} />
    </IntlProvider>
  );
}

const STATES: ReadonlyArray<[string, TurnState]> = [
  ['RoundRobin', { type: 'RoundRobin', round: 1, totalRounds: 4, activePlayerId: 'p1', playOrder: ['p1', 'p2'] }],
  ['Sequential', { type: 'Sequential', phases: ['Mattina', 'Notte'], activePhaseIndex: 0 }],
  ['Simultaneous', { type: 'Simultaneous', phases: ['Mattina'], activePhaseIndex: 0 }],
  ['Realtime', { type: 'Realtime' }],
  ['None', { type: 'None' }],
  ['Custom', { type: 'Custom', phases: ['F1', 'F2'], activePhaseIndex: 0 }],
  ['FirstPlayerToken', { type: 'FirstPlayerToken', round: 1, totalRounds: 4, tokenHolderId: 'p1', playOrder: ['p1', 'p2'] }],
  // @ts-expect-error — Unknown fallback
  ['Unknown', { type: 'BogusType' }],
];

describe('TurnIndicatorRenderer axe AA', () => {
  for (const [name, state] of STATES) {
    it(`${name}: 0 axe AA violations`, async () => {
      const { container } = renderRenderer(state);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  }
});
```

- [ ] **Step 6.2: Run + commit**

```bash
cd apps/web && pnpm test __tests__/session-live-turn-indicator-renderer-axe --run
git add apps/web/__tests__/session-live-turn-indicator-renderer-axe.test.tsx
git commit -m "test(a11y): #2378 TurnIndicatorRenderer axe AA all 7 + Unknown (8 tests)"
```

---

## Task 7: Final verification + PR

- [ ] **Step 7.1: Full verification suite**

```bash
cd apps/web && pnpm typecheck
cd apps/web && pnpm test session-live --run
cd apps/web && pnpm test session-live-turn-indicator-renderer-axe --run
cd apps/web && pnpm lint
cd apps/web && pnpm lint:bgg 2>/dev/null
cd apps/web && pnpm lint:tokens 2>/dev/null
```

Expected: all pass. (Pre-existing warnings OK; no NEW errors.)

- [ ] **Step 7.2: Push branch**

```bash
git push -u origin feature/issue-2378-g5b-turn-indicator-renderer
```

- [ ] **Step 7.3: Create PR**

```bash
gh pr create --base main-dev --title "feat(session-live): #2378 G5b TurnIndicatorRenderer polymorphic dispatch (7 variant)" --body "$(cat <<'EOF'
## Summary
- New `TurnIndicatorRenderer` dispatcher switches on 7 `TurnOrderType` variants (RoundRobin, Sequential, Simultaneous, Realtime, None, Custom, FirstPlayerToken).
- `Unknown` fallback renders a warning banner + `console.warn` for unregistered values (Nygard failure-mode gap closed).
- Existing `TurnIndicator` is preserved as the body of the `RoundRobinBranch` (DEC-2). `data-slot="turn-indicator"` moves to the renderer root.
- 14 new i18n keys under `pages.sessionLive.turnIndicator.*`.
- SessionLiveView consumes the renderer with a RoundRobin fixture (real wiring deferred to #2389).
- 18 unit tests + 8 axe AA tests covering all branches.

## Refs
- Issue: #2378 (epic #2354 Session live shell)
- Spec: `docs/superpowers/specs/2026-06-16-issue-2378-g5b-turn-indicator-renderer-design.md`
- Plan: `docs/superpowers/plans/2026-06-16-issue-2378-g5b-turn-indicator-renderer.md`

## 5 DEC user-locked
- DEC-1 7 variant (mockup 6 + FirstPlayerToken)
- DEC-2 Keep TurnIndicator + new TurnIndicatorRenderer dispatcher
- DEC-3 7° variant = FirstPlayerToken
- DEC-4 Discriminated union TypeScript pattern
- DEC-5 Unknown → warning banner + console.warn

## Test plan
- [x] Unit: 10 dispatch tests + 8 branch tests + 4 graceful-degrade tests
- [x] Axe AA: 8 tests (7 variants + Unknown)
- [x] Typecheck + lint + tokens + BGG ban pass
- [x] SessionLiveView selector continuity (`data-slot="turn-indicator"`)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 7.4: Report PR URL.**

---

## Self-Review

**Spec coverage:**
- DEC-1 7 variants → Task 1 type union + Task 2 branches + Task 3 dispatcher.
- DEC-2 keep TurnIndicator → Task 2.1 RoundRobinBranch wraps it + Task 2.9 removes `data-slot`.
- DEC-3 FirstPlayerToken → Task 2.7.
- DEC-4 discriminated union → Task 1 turn-state.ts.
- DEC-5 Unknown banner + warn → Task 2.8 + Task 3 dispatcher.
- Spec §6 a11y → branches add `aria-live`/`role="status"` per branch.
- Spec §7 failure handling → graceful tests in Task 3 + branch fallbacks.
- Spec §8 testing → 9+ unit tests (Task 3) + 8 axe (Task 6) + 8 branch (Task 2.10) ≈ 25 tests.
- Spec §10 out-of-scope honored (no `bg-[hsl…]` touch in TurnIndicator).

**Placeholder scan:** No "TBD"/"TODO". Branch code skeletons in Task 2 are
abbreviated (each branch is a small component, ~30-50 LOC) and reference the
mockup source lines for fidelity.

**Type consistency:** `TurnIndicatorRendererLabels` field names match between
Task 3 spec and Task 4 i18n keys.

---

## Execution Handoff

Plan complete and saved to `docs/superpowers/plans/2026-06-16-issue-2378-g5b-turn-indicator-renderer.md`. Two execution options:

1. **Subagent-Driven** (recommended) — fresh subagent per task, two-stage review per task.
2. **Inline Execution** — current session, batch with checkpoints.
