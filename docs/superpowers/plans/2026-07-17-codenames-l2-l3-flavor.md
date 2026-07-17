# Codenames L2+L3 Flavor Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a Codenames live flavor — a 5×5 word grid + team trackers + active clue, driven by a small per-game `gameState` on the L1 layer, reusing the generalized flavor plumbing.

**Architecture:** FE-only, reuses the game-agnostic pattern (`flavors/<game>/`; `FlavorRenderer` already dispatches on `FLAVOR_MAP` with `FlavorProps`; each flavor self-builds i18n labels). The board (25 cells) + current team + active clue live in `gameState`; assassin/team-counts/winner are derived; a preset generates a valid key layout. VP stays in the existing scoring system. Discrete edits PUT immediately (no debounce).

**Tech Stack:** Next.js 16 · React 19 · TypeScript · Zod · Zustand (`useLiveSessionStore`) · TanStack Query (`useUpdateLiveGameState`) · react-intl (`useIntl`) + `@/hooks/useTranslation` · Vitest + Testing Library + jest-axe · Tailwind semantic tokens (piece/key colours inline `hsl()` via a palette module).

## Global Constraints

- **Issue:** #2790 (G6d, epic #3025). Spec: `docs/superpowers/specs/2026-07-17-codenames-l2-l3-flavor-design.md`.
- **Zero backend changes.** Scores use the existing scoring editor; `gameState` is the opaque L1 blob.
- **State schema:** `v: 1`, `game: 'codenames'`. `parseCodenamesGameState` returns `null` (never throws) on wrong game/version/shape.
- **`gameState` shape:** `{ v, game, board: 25×{word,key,revealed}, currentTeam: 'red'|'blue', clue: {word,number}|null }`. `board` length EXACTLY 25. Never scores.
- **Derived (never stored):** `isAssassinRevealed`, per-team `{total,found}`, and the winner — all from `board`(+`currentTeam`).
- **Key distribution:** 25 = 9 starting-team · 8 other · 7 neutral · 1 assassin. Words are 25 DISTINCT entries from a static bank.
- **`currentTeam` is STORED** (not derived from phases) for robustness.
- **Discrete edits → IMMEDIATE PUT (no debounce).** The editor commits optimistically + `mutate(next)` right away. (Catan/Wingspan's 500 ms debounce was for continuous `+/-`; Codenames edits are discrete must-not-be-lost events.)
- **Host-edit only** (`viewerRole === 'Host'`). **Scoring (leaderboard) renders ungated**; only the board/tracker/clue gate on `gameState` (host CTA when null). **Perspective toggle (operative/spymaster) is LOCAL component state**, host-only, default operative.
- **Flavors self-build i18n labels** via `useIntl` + `useTranslation`; templates (`{n}`/`{word}`) via `intl.messages[id] as string ?? fallback`, static via `t(id)`.
- **Colours:** semantic Tailwind tokens EXCEPT the 4 Codenames key colours (red/blue/neutral/assassin) → inline `hsl()` via a small palette module (token-lint safe, like `catan-palette`).
- **Tests:** Vitest, TDD, output pristine. Query via `data-slot`/roles, not `getByTestId`. Files under `apps/web/src/components/features/session-live/flavors/codenames/`. Run from `apps/web`.
- **Windows:** pre-commit runs `pnpm typecheck` (~2 min) — allow ≥5 min for commits; if TS2307 on stale `.next/types`, `rm -rf .next/types` first (never `--no-verify`).

## File Structure

Create under `flavors/codenames/`: `codenames-state.ts` (schema + derivations), `codenames-board-preset.ts` (word bank + generator), `codenames-palette.ts` (key colours), `use-codenames-state-editor.ts` (immediate-PUT host mutators), `CodenamesWordGrid.tsx`, `CodenamesTeamTracker.tsx`, `CodenamesCurrentClueStrip.tsx`, `CodenamesLiveFlavor.tsx`, `__tests__/*`.
Modify: `session-live/FlavorRenderer.tsx` (one `FLAVOR_MAP` entry), `src/locales/it.json` + `en.json` (`flavor.codenames.*`).

---

## Task 1: L2 state schema + derivations

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/codenames-state.ts`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/codenames-state.test.ts`

**Interfaces:**
- Produces: `CodenamesGameState`, `CodenamesCell`, `CodenamesClue`, `CodenamesTeam`, `CodenamesKey` types; `parseCodenamesGameState(raw): CodenamesGameState | null`; `isAssassinRevealed(board): boolean`; `teamCounts(board, team): {total; found}`; `codenamesWinner(s): CodenamesTeam | null`; `oppositeTeam(t): CodenamesTeam`; `CODENAMES_STATE_VERSION = 1`; `CODENAMES_BOARD_SIZE = 25`; `CODENAMES_KEY_COUNTS = { starting: 9, other: 8, neutral: 7, assassin: 1 }`. (`isAssassinRevealed`/`teamCounts` take the `board` array; only `codenamesWinner` needs the full state for `currentTeam`.)

- [ ] **Step 1: Write the failing test**

```ts
// codenames-state.test.ts
import { describe, expect, it } from 'vitest';

import {
  CODENAMES_KEY_COUNTS,
  codenamesWinner,
  isAssassinRevealed,
  oppositeTeam,
  parseCodenamesGameState,
  teamCounts,
  type CodenamesCell,
} from '../codenames-state';

function cells(spec: Array<[CodenamesCell['key'], boolean]>): CodenamesCell[] {
  return spec.map(([key, revealed], i) => ({ word: `W${i}`, key, revealed }));
}

// 25 cells: 9 red, 8 blue, 7 neutral, 1 assassin
function board25(): CodenamesCell[] {
  const spec: Array<[CodenamesCell['key'], boolean]> = [
    ...Array(9).fill(['red', false]),
    ...Array(8).fill(['blue', false]),
    ...Array(7).fill(['neutral', false]),
    ['assassin', false],
  ];
  return cells(spec);
}

const VALID = { v: 1, game: 'codenames', board: board25(), currentTeam: 'red', clue: null };

describe('parseCodenamesGameState', () => {
  it('parses a well-formed state', () => {
    expect(parseCodenamesGameState(VALID)?.currentTeam).toBe('red');
  });
  it('returns null for a different game', () => {
    expect(parseCodenamesGameState({ ...VALID, game: 'catan' })).toBeNull();
  });
  it('returns null for a future version', () => {
    expect(parseCodenamesGameState({ ...VALID, v: 2 })).toBeNull();
  });
  it('returns null when the board is not exactly 25 cells', () => {
    expect(parseCodenamesGameState({ ...VALID, board: board25().slice(0, 24) })).toBeNull();
  });
  it('returns null for malformed / non-object', () => {
    expect(parseCodenamesGameState(null)).toBeNull();
    expect(parseCodenamesGameState('x')).toBeNull();
  });
  it('accepts a non-null clue', () => {
    const parsed = parseCodenamesGameState({ ...VALID, clue: { word: 'MARE', number: 2 } });
    expect(parsed?.clue).toEqual({ word: 'MARE', number: 2 });
  });
});

describe('derivations', () => {
  it('oppositeTeam flips', () => {
    expect(oppositeTeam('red')).toBe('blue');
    expect(oppositeTeam('blue')).toBe('red');
  });
  it('teamCounts derives total + found from the board', () => {
    const b = board25();
    b[0].revealed = true; // one red revealed
    expect(teamCounts(b, 'red')).toEqual({ total: 9, found: 1 });
    expect(teamCounts(b, 'blue')).toEqual({ total: 8, found: 0 });
  });
  it('isAssassinRevealed is true only when the assassin cell is revealed', () => {
    expect(isAssassinRevealed(VALID.board)).toBe(false);
    const b = board25();
    b[24].revealed = true; // the assassin
    expect(isAssassinRevealed(b)).toBe(true);
  });
  it('winner: assassin revealed → the OTHER team (currentTeam loses)', () => {
    const b = board25();
    b[24].revealed = true;
    expect(codenamesWinner({ ...VALID, board: b, currentTeam: 'red' })).toBe('blue');
  });
  it('winner: all of a team revealed → that team', () => {
    const b = board25();
    for (let i = 0; i < 8; i++) b[9 + i].revealed = true; // all 8 blue
    expect(codenamesWinner({ ...VALID, board: b })).toBe('blue');
  });
  it('winner: null when the game is ongoing', () => {
    expect(codenamesWinner(VALID)).toBeNull();
  });
  it('exposes the standard key counts', () => {
    expect(CODENAMES_KEY_COUNTS).toEqual({ starting: 9, other: 8, neutral: 7, assassin: 1 });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/codenames-state.test.ts`
Expected: FAIL — `Cannot find module '../codenames-state'`.

- [ ] **Step 3: Write the implementation**

```ts
// codenames-state.ts
import { z } from 'zod';

export const CODENAMES_STATE_VERSION = 1;
export const CODENAMES_BOARD_SIZE = 25;
export const CODENAMES_KEY_COUNTS = { starting: 9, other: 8, neutral: 7, assassin: 1 } as const;

export const CodenamesTeamSchema = z.enum(['red', 'blue']);
export type CodenamesTeam = z.infer<typeof CodenamesTeamSchema>;

export const CodenamesKeySchema = z.enum(['red', 'blue', 'neutral', 'assassin']);
export type CodenamesKey = z.infer<typeof CodenamesKeySchema>;

export const CodenamesCellSchema = z.object({
  word: z.string(),
  key: CodenamesKeySchema,
  revealed: z.boolean(),
});
export type CodenamesCell = z.infer<typeof CodenamesCellSchema>;

export const CodenamesClueSchema = z.object({ word: z.string(), number: z.number().int().min(0) });
export type CodenamesClue = z.infer<typeof CodenamesClueSchema>;

export const CodenamesGameStateSchema = z.object({
  v: z.literal(CODENAMES_STATE_VERSION),
  game: z.literal('codenames'),
  board: z.array(CodenamesCellSchema).length(CODENAMES_BOARD_SIZE),
  currentTeam: CodenamesTeamSchema,
  clue: CodenamesClueSchema.nullable(),
});
export type CodenamesGameState = z.infer<typeof CodenamesGameStateSchema>;

export function parseCodenamesGameState(raw: unknown): CodenamesGameState | null {
  const result = CodenamesGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function oppositeTeam(team: CodenamesTeam): CodenamesTeam {
  return team === 'red' ? 'blue' : 'red';
}

export function isAssassinRevealed(board: CodenamesCell[]): boolean {
  return board.some(c => c.key === 'assassin' && c.revealed);
}

export function teamCounts(
  board: CodenamesCell[],
  team: CodenamesTeam
): { total: number; found: number } {
  let total = 0;
  let found = 0;
  for (const c of board) {
    if (c.key !== team) continue;
    total++;
    if (c.revealed) found++;
  }
  return { total, found };
}

/** assassin revealed → the on-turn team loses (other wins); all of a team revealed → that team; else null. */
export function codenamesWinner(s: CodenamesGameState): CodenamesTeam | null {
  if (isAssassinRevealed(s.board)) return oppositeTeam(s.currentTeam);
  for (const team of ['red', 'blue'] as const) {
    const { total, found } = teamCounts(s.board, team);
    if (total > 0 && found === total) return team;
  }
  return null;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/codenames-state.test.ts`
Expected: PASS (15 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/codenames-state.ts" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/codenames-state.test.ts"
git commit -m "feat(session-live): #2790 Codenames L2 state schema + derivations"
```

---

## Task 2: Board preset generator + word bank

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/codenames-board-preset.ts`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/codenames-board-preset.test.ts`

**Interfaces:**
- Consumes: `CodenamesCell`, `CodenamesTeam`, `CODENAMES_BOARD_SIZE` from `./codenames-state`.
- Produces: `generateCodenamesBoard(startingTeam?: CodenamesTeam): { board: CodenamesCell[]; startingTeam: CodenamesTeam }`; `CODENAMES_WORD_BANK: ReadonlyArray<string>`.

- [ ] **Step 1: Write the failing test**

```ts
// codenames-board-preset.test.ts
import { describe, expect, it } from 'vitest';

import { CODENAMES_WORD_BANK, generateCodenamesBoard } from '../codenames-board-preset';
import type { CodenamesKey } from '../codenames-state';

function keyCounts(board: { key: CodenamesKey }[]): Record<string, number> {
  return board.reduce<Record<string, number>>((acc, c) => {
    acc[c.key] = (acc[c.key] ?? 0) + 1;
    return acc;
  }, {});
}

describe('generateCodenamesBoard', () => {
  it('produces exactly 25 cells with 25 distinct words', () => {
    const { board } = generateCodenamesBoard();
    expect(board).toHaveLength(25);
    expect(new Set(board.map(c => c.word)).size).toBe(25);
  });

  it('has a valid key multiset for the starting team (9/8/7/1)', () => {
    const { board, startingTeam } = generateCodenamesBoard('red');
    const counts = keyCounts(board);
    expect(startingTeam).toBe('red');
    expect(counts.red).toBe(9); // starting team
    expect(counts.blue).toBe(8);
    expect(counts.neutral).toBe(7);
    expect(counts.assassin).toBe(1);
  });

  it('gives the OTHER starting team the 9-count when requested', () => {
    const { board } = generateCodenamesBoard('blue');
    const counts = keyCounts(board);
    expect(counts.blue).toBe(9);
    expect(counts.red).toBe(8);
  });

  it('starts all cells unrevealed', () => {
    expect(generateCodenamesBoard().board.every(c => !c.revealed)).toBe(true);
  });

  it('draws only from the word bank, which has at least 25 distinct words', () => {
    expect(new Set(CODENAMES_WORD_BANK).size).toBeGreaterThanOrEqual(25);
    const { board } = generateCodenamesBoard();
    expect(board.every(c => CODENAMES_WORD_BANK.includes(c.word))).toBe(true);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/codenames-board-preset.test.ts`
Expected: FAIL — `Cannot find module '../codenames-board-preset'`.

- [ ] **Step 3: Write the implementation**

```ts
// codenames-board-preset.ts
import { CODENAMES_KEY_COUNTS, type CodenamesCell, type CodenamesKey, type CodenamesTeam } from './codenames-state';

// A static Italian-leaning word bank (≥ 50 distinct single words). Fixed const, not i18n.
export const CODENAMES_WORD_BANK: ReadonlyArray<string> = [
  'MARE', 'MONTE', 'SOLE', 'LUNA', 'STELLA', 'FIUME', 'BOSCO', 'CASTELLO', 'PONTE', 'CHIAVE',
  'DRAGO', 'REGINA', 'CAVALIERE', 'SCUDO', 'SPADA', 'CORONA', 'TESORO', 'NAVE', 'FARO', 'ISOLA',
  'DESERTO', 'PIRAMIDE', 'FUOCO', 'GHIACCIO', 'VENTO', 'TEMPESTA', 'ORO', 'ARGENTO', 'FERRO', 'PIETRA',
  'GATTO', 'CANE', 'LUPO', 'VOLPE', 'AQUILA', 'SERPENTE', 'RAGNO', 'APE', 'PESCE', 'BALENA',
  'MELA', 'PANE', 'VINO', 'MIELE', 'SALE', 'PEPE', 'ZUCCHERO', 'CAFFE', 'LATTE', 'FORMAGGIO',
  'ROBOT', 'RAZZO', 'PIANETA', 'GALASSIA', 'COMETA', 'MOTORE', 'CIRCUITO', 'CODICE', 'RETE', 'SCHERMO',
];

function shuffle<T>(input: readonly T[]): T[] {
  const a = [...input];
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1));
    const tmp = a[i];
    a[i] = a[j];
    a[j] = tmp;
  }
  return a;
}

export function generateCodenamesBoard(startingTeam?: CodenamesTeam): {
  board: CodenamesCell[];
  startingTeam: CodenamesTeam;
} {
  const start: CodenamesTeam = startingTeam ?? (Math.random() < 0.5 ? 'red' : 'blue');
  const other: CodenamesTeam = start === 'red' ? 'blue' : 'red';

  const keys: CodenamesKey[] = [
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.starting).fill(start),
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.other).fill(other),
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.neutral).fill('neutral'),
    ...Array<CodenamesKey>(CODENAMES_KEY_COUNTS.assassin).fill('assassin'),
  ];

  const words = shuffle(CODENAMES_WORD_BANK).slice(0, 25);
  const shuffledKeys = shuffle(keys);

  const board: CodenamesCell[] = words.map((word, i) => ({
    word,
    key: shuffledKeys[i],
    revealed: false,
  }));

  return { board, startingTeam: start };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/codenames-board-preset.test.ts`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/codenames-board-preset.ts" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/codenames-board-preset.test.ts"
git commit -m "feat(session-live): #2790 Codenames L2 board preset + word bank"
```

---

## Task 3: Host-edit hook (immediate PUT)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/use-codenames-state-editor.ts`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/use-codenames-state-editor.test.tsx`

**Interfaces:**
- Consumes: `CodenamesGameState`, `parseCodenamesGameState`, `CODENAMES_STATE_VERSION` from `./codenames-state`; `generateCodenamesBoard` from `./codenames-board-preset`; `useLiveSessionStore` from `@/lib/stores/live-session-store`; `useUpdateLiveGameState` from `@/hooks/mutations/useUpdateLiveGameState`.
- Produces: `useCodenamesStateEditor(sessionId: string): CodenamesStateEditor` where
  ```ts
  interface CodenamesStateEditor {
    state: CodenamesGameState | null;
    initializeState: () => void;
    regenerateBoard: () => void;
    revealCell: (index: number) => void;
    setClue: (word: string, number: number) => void;
    clearClue: () => void;
    switchTeam: () => void;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// use-codenames-state-editor.test.tsx
import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';

import { useCodenamesStateEditor } from '../use-codenames-state-editor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

const mutateMock = vi.fn<[unknown], void>();
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: mutateMock }),
}));

const SID = 'sess-1';
beforeEach(() => {
  mutateMock.mockReset();
  useLiveSessionStore.getState().reset();
});
function current() {
  return useLiveSessionStore.getState().gameState as import('../codenames-state').CodenamesGameState | null;
}

describe('useCodenamesStateEditor', () => {
  it('initializeState seeds a 25-cell board + a currentTeam + null clue', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    const s = current();
    expect(s?.game).toBe('codenames');
    expect(s?.board).toHaveLength(25);
    expect(['red', 'blue']).toContain(s?.currentTeam);
    expect(s?.clue).toBeNull();
  });

  it('reveals a cell (idempotent) and PUTs immediately (no debounce)', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    mutateMock.mockClear();
    act(() => result.current.revealCell(3));
    expect(current()?.board[3].revealed).toBe(true);
    expect(mutateMock).toHaveBeenCalledTimes(1); // immediate, no timer
    act(() => result.current.revealCell(3)); // idempotent
    expect(current()?.board[3].revealed).toBe(true);
  });

  it('setClue clamps number >= 0; clearClue nulls it', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setClue('MARE', -2));
    expect(current()?.clue).toEqual({ word: 'MARE', number: 0 });
    act(() => result.current.clearClue());
    expect(current()?.clue).toBeNull();
  });

  it('switchTeam flips currentTeam and clears the clue', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.initializeState());
    act(() => result.current.setClue('MARE', 2));
    const before = current()!.currentTeam;
    act(() => result.current.switchTeam());
    expect(current()?.currentTeam).toBe(before === 'red' ? 'blue' : 'red');
    expect(current()?.clue).toBeNull();
  });

  it('mutators are no-ops when state is null (except initializeState)', () => {
    const { result } = renderHook(() => useCodenamesStateEditor(SID));
    act(() => result.current.revealCell(0));
    expect(current()).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/use-codenames-state-editor.test.tsx`
Expected: FAIL — `Cannot find module '../use-codenames-state-editor'`.

- [ ] **Step 3: Write the implementation**

```ts
// use-codenames-state-editor.ts
'use client';

import { useCallback, useMemo } from 'react';

import { useUpdateLiveGameState } from '@/hooks/mutations/useUpdateLiveGameState';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';

import { generateCodenamesBoard } from './codenames-board-preset';
import {
  CODENAMES_STATE_VERSION,
  parseCodenamesGameState,
  oppositeTeam,
  type CodenamesGameState,
} from './codenames-state';

export interface CodenamesStateEditor {
  state: CodenamesGameState | null;
  initializeState: () => void;
  regenerateBoard: () => void;
  revealCell: (index: number) => void;
  setClue: (word: string, number: number) => void;
  clearClue: () => void;
  switchTeam: () => void;
}

function freshState(): CodenamesGameState {
  const { board, startingTeam } = generateCodenamesBoard();
  return {
    v: CODENAMES_STATE_VERSION,
    game: 'codenames',
    board,
    currentTeam: startingTeam,
    clue: null,
  };
}

export function useCodenamesStateEditor(sessionId: string): CodenamesStateEditor {
  const raw = useLiveSessionStore(s => s.gameState);
  const state = useMemo(() => parseCodenamesGameState(raw), [raw]);
  const { mutate } = useUpdateLiveGameState(sessionId);

  // Discrete edits: optimistic write + IMMEDIATE PUT (no debounce — a tap must never be dropped).
  const commit = useCallback(
    (next: CodenamesGameState) => {
      useLiveSessionStore.getState().setGameState(next);
      mutate(next);
    },
    [mutate]
  );

  const readState = useCallback(
    (): CodenamesGameState | null => parseCodenamesGameState(useLiveSessionStore.getState().gameState),
    []
  );

  const initializeState = useCallback(() => commit(freshState()), [commit]);
  const regenerateBoard = useCallback(() => {
    if (readState() == null) return;
    commit(freshState());
  }, [commit, readState]);

  const revealCell = useCallback(
    (index: number) => {
      const cur = readState();
      if (cur == null || index < 0 || index >= cur.board.length) return;
      if (cur.board[index].revealed) return; // idempotent
      const board = cur.board.map((c, i) => (i === index ? { ...c, revealed: true } : c));
      commit({ ...cur, board });
    },
    [commit, readState]
  );

  const setClue = useCallback(
    (word: string, number: number) => {
      const cur = readState();
      if (cur == null) return;
      commit({ ...cur, clue: { word, number: number < 0 ? 0 : Math.trunc(number) } });
    },
    [commit, readState]
  );

  const clearClue = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, clue: null });
  }, [commit, readState]);

  const switchTeam = useCallback(() => {
    const cur = readState();
    if (cur == null) return;
    commit({ ...cur, currentTeam: oppositeTeam(cur.currentTeam), clue: null });
  }, [commit, readState]);

  return { state, initializeState, regenerateBoard, revealCell, setClue, clearClue, switchTeam };
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/use-codenames-state-editor.test.tsx`
Expected: PASS (5 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/use-codenames-state-editor.ts" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/use-codenames-state-editor.test.tsx"
git commit -m "feat(session-live): #2790 Codenames L2 host-edit hook (immediate PUT)"
```

---

## Task 4: CodenamesWordGrid + palette

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/codenames-palette.ts`
- Create: `apps/web/src/components/features/session-live/flavors/codenames/CodenamesWordGrid.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesWordGrid.test.tsx`

**Interfaces:**
- Consumes: `CodenamesCell`, `CodenamesKey` from `./codenames-state`; `codenamesKeyColor` from `./codenames-palette`.
- Produces: `CodenamesWordGrid` with props
  ```ts
  interface CodenamesWordGridProps {
    board: CodenamesCell[];
    editable: boolean;
    perspective: 'operative' | 'spymaster';
    onRevealCell?: (index: number) => void;
    revealAriaTemplate: string; // "Rivela {word}"
  }
  ```

- [ ] **Step 1: Write the palette**

```ts
// codenames-palette.ts
import type { CodenamesKey } from './codenames-state';

// The 4 Codenames key colours — inline hsl() (token-lint safe escape, like catan-palette).
const KEY_HSL: Record<CodenamesKey, string> = {
  red: 'hsl(0, 65%, 52%)',
  blue: 'hsl(215, 60%, 52%)',
  neutral: 'hsl(38, 30%, 72%)',
  assassin: 'hsl(0, 0%, 18%)',
};

export function codenamesKeyColor(key: CodenamesKey): string {
  return KEY_HSL[key];
}
```

- [ ] **Step 2: Write the failing test**

```tsx
// CodenamesWordGrid.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CodenamesWordGrid } from '../CodenamesWordGrid';
import { generateCodenamesBoard } from '../codenames-board-preset';

const { board } = generateCodenamesBoard('red');
const labels = { revealAriaTemplate: 'Rivela {word}' };

describe('CodenamesWordGrid', () => {
  it('renders 25 word cells', () => {
    const { container } = render(
      <CodenamesWordGrid board={board} editable={false} perspective="operative" {...labels} />
    );
    expect(container.querySelectorAll('[data-slot="codenames-cell"]')).toHaveLength(25);
  });

  it('read-only mode exposes no buttons', () => {
    const { queryByRole } = render(
      <CodenamesWordGrid board={board} editable={false} perspective="operative" {...labels} />
    );
    expect(queryByRole('button')).toBeNull();
  });

  it('host mode: clicking an unrevealed cell fires onRevealCell with its index', async () => {
    const onRevealCell = vi.fn();
    const { container } = render(
      <CodenamesWordGrid board={board} editable perspective="operative" onRevealCell={onRevealCell} {...labels} />
    );
    const firstCell = container.querySelector('[data-slot="codenames-cell"]') as HTMLElement;
    await userEvent.click(firstCell);
    expect(onRevealCell).toHaveBeenCalledWith(0);
  });

  it('spymaster perspective tints covered cells by key (data-key present); operative does not', () => {
    const spy = render(
      <CodenamesWordGrid board={board} editable={false} perspective="spymaster" {...labels} />
    );
    const op = render(
      <CodenamesWordGrid board={board} editable={false} perspective="operative" {...labels} />
    );
    // spymaster exposes each covered cell's key; operative hides it for covered cells
    expect(spy.container.querySelector('[data-slot="codenames-cell"][data-key]')).not.toBeNull();
    expect(op.container.querySelector('[data-slot="codenames-cell"][data-key]')).toBeNull();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesWordGrid.test.tsx`
Expected: FAIL — `Cannot find module '../CodenamesWordGrid'`.

- [ ] **Step 4: Write the implementation**

```tsx
// CodenamesWordGrid.tsx
'use client';

import { type ReactElement } from 'react';

import { codenamesKeyColor } from './codenames-palette';
import type { CodenamesCell } from './codenames-state';

export interface CodenamesWordGridProps {
  readonly board: CodenamesCell[];
  readonly editable: boolean;
  readonly perspective: 'operative' | 'spymaster';
  readonly onRevealCell?: (index: number) => void;
  readonly revealAriaTemplate: string;
}

export function CodenamesWordGrid({
  board,
  editable,
  perspective,
  onRevealCell,
  revealAriaTemplate,
}: CodenamesWordGridProps): ReactElement {
  return (
    <div
      data-slot="codenames-board"
      role="grid"
      aria-label="Codenames"
      className="grid grid-cols-5 gap-1"
    >
      {board.map((cell, i) => {
        // Show the key colour when the cell is revealed, OR (for the spymaster view) always.
        const showKey = cell.revealed || perspective === 'spymaster';
        const bg = showKey ? codenamesKeyColor(cell.key) : undefined;
        const aria = revealAriaTemplate.replace('{word}', cell.word);
        const common = {
          'data-slot': 'codenames-cell',
          'data-index': String(i),
          ...(showKey ? { 'data-key': cell.key } : {}),
          'data-revealed': cell.revealed ? 'true' : 'false',
          className: [
            'flex min-h-10 items-center justify-center rounded p-1 text-center text-[11px] font-semibold',
            showKey ? 'text-white' : 'bg-card text-foreground',
            cell.revealed ? 'opacity-90 ring-2 ring-border-strong' : '',
          ].join(' '),
          style: bg ? { backgroundColor: bg } : undefined,
        };
        const content = <span className="truncate">{cell.word}</span>;

        if (editable && !cell.revealed) {
          return (
            <button
              key={i}
              type="button"
              aria-label={aria}
              onClick={() => onRevealCell?.(i)}
              {...common}
            >
              {content}
            </button>
          );
        }
        return (
          <div key={i} role="gridcell" {...common}>
            {content}
          </div>
        );
      })}
    </div>
  );
}
```

> Note: `text-white` is allowed here because each such cell also sets a coloured `backgroundColor` (the mockup `.e-bg` exemption — see CLAUDE.md token rules). The 4 key colours are the only non-token colours, applied inline via the palette.

- [ ] **Step 5: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesWordGrid.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/codenames-palette.ts" "apps/web/src/components/features/session-live/flavors/codenames/CodenamesWordGrid.tsx" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesWordGrid.test.tsx"
git commit -m "feat(session-live): #2790 Codenames L3 word grid + key palette"
```

---

## Task 5: CodenamesTeamTracker

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/CodenamesTeamTracker.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesTeamTracker.test.tsx`

**Interfaces:**
- Consumes: `CodenamesGameState`, `teamCounts` from `./codenames-state`; `codenamesKeyColor` from `./codenames-palette`.
- Produces: `CodenamesTeamTracker` with props
  ```ts
  interface CodenamesTeamTrackerProps {
    board: CodenamesGameState['board'];
    currentTeam: CodenamesGameState['currentTeam'];
    labels: { redLabel: string; blueLabel: string; foundTemplate: string /* "{found}/{total}" */; turnLabel: string };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// CodenamesTeamTracker.test.tsx
import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';

import { CodenamesTeamTracker } from '../CodenamesTeamTracker';
import { generateCodenamesBoard } from '../codenames-board-preset';

const { board } = generateCodenamesBoard('red'); // red = 9, blue = 8
const labels = { redLabel: 'Rossi', blueLabel: 'Blu', foundTemplate: '{found}/{total}', turnLabel: 'Al turno' };

describe('CodenamesTeamTracker', () => {
  it('shows found/total per team derived from the board', () => {
    const b = board.map((c, i) => (c.key === 'red' && i === board.findIndex(x => x.key === 'red') ? { ...c, revealed: true } : c));
    const { container } = render(<CodenamesTeamTracker board={b} currentTeam="red" labels={labels} />);
    expect(container.querySelector('[data-team="red"]')?.textContent).toContain('1/9');
    expect(container.querySelector('[data-team="blue"]')?.textContent).toContain('0/8');
  });

  it('marks the current team', () => {
    const { container } = render(<CodenamesTeamTracker board={board} currentTeam="blue" labels={labels} />);
    expect(container.querySelector('[data-team="blue"][data-current="true"]')).not.toBeNull();
    expect(container.querySelector('[data-team="red"][data-current="true"]')).toBeNull();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesTeamTracker.test.tsx`
Expected: FAIL — `Cannot find module '../CodenamesTeamTracker'`.

- [ ] **Step 3: Write the implementation**

```tsx
// CodenamesTeamTracker.tsx
'use client';

import { type ReactElement } from 'react';

import { codenamesKeyColor } from './codenames-palette';
import { teamCounts, type CodenamesGameState, type CodenamesTeam } from './codenames-state';

export interface CodenamesTeamTrackerProps {
  readonly board: CodenamesGameState['board'];
  readonly currentTeam: CodenamesGameState['currentTeam'];
  readonly labels: { redLabel: string; blueLabel: string; foundTemplate: string; turnLabel: string };
}

export function CodenamesTeamTracker({
  board,
  currentTeam,
  labels,
}: CodenamesTeamTrackerProps): ReactElement {
  const teams: Array<{ id: CodenamesTeam; label: string }> = [
    { id: 'red', label: labels.redLabel },
    { id: 'blue', label: labels.blueLabel },
  ];
  return (
    <div data-slot="codenames-teams" className="flex gap-2">
      {teams.map(({ id, label }) => {
        const { total, found } = teamCounts(board, id);
        const isCurrent = currentTeam === id;
        return (
          <div
            key={id}
            data-team={id}
            data-current={isCurrent ? 'true' : 'false'}
            className={[
              'flex flex-1 items-center gap-2 rounded-lg border px-2 py-1.5',
              isCurrent ? 'border-border-strong bg-muted' : 'border-border bg-card',
            ].join(' ')}
          >
            <span
              aria-hidden="true"
              className="h-3 w-3 shrink-0 rounded-full"
              style={{ backgroundColor: codenamesKeyColor(id) }}
            />
            <span className="text-xs font-semibold text-foreground">{label}</span>
            {isCurrent && (
              <span className="rounded bg-background px-1 text-[10px] uppercase text-muted-foreground">
                {labels.turnLabel}
              </span>
            )}
            <span className="ml-auto tabular-nums text-sm font-bold text-foreground">
              {labels.foundTemplate.replace('{found}', String(found)).replace('{total}', String(total))}
            </span>
          </div>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesTeamTracker.test.tsx`
Expected: PASS (2 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/CodenamesTeamTracker.tsx" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesTeamTracker.test.tsx"
git commit -m "feat(session-live): #2790 Codenames L3 team tracker"
```

---

## Task 6: CodenamesCurrentClueStrip

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/CodenamesCurrentClueStrip.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesCurrentClueStrip.test.tsx`

**Interfaces:**
- Consumes: `CodenamesClue`, `CodenamesTeam` from `./codenames-state`.
- Produces: `CodenamesCurrentClueStrip` with props
  ```ts
  interface CodenamesCurrentClueStripProps {
    clue: CodenamesClue | null;
    currentTeam: CodenamesTeam;
    editable: boolean;
    onSetClue?: (word: string, number: number) => void;
    onClearClue?: () => void;
    onSwitchTeam?: () => void;
    labels: { noClue: string; wordPlaceholder: string; numberAria: string; giveClue: string; endTurn: string };
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// CodenamesCurrentClueStrip.test.tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { CodenamesCurrentClueStrip } from '../CodenamesCurrentClueStrip';

const labels = { noClue: 'Nessun indizio', wordPlaceholder: 'Parola', numberAria: 'Numero', giveClue: 'Dai indizio', endTurn: 'Fine turno' };

describe('CodenamesCurrentClueStrip', () => {
  it('read-only shows the active clue as WORD : NUMBER', () => {
    render(<CodenamesCurrentClueStrip clue={{ word: 'MARE', number: 3 }} currentTeam="red" editable={false} labels={labels} />);
    expect(screen.getByText(/MARE/)).toBeInTheDocument();
    expect(screen.getByText(/3/)).toBeInTheDocument();
  });

  it('read-only with no clue shows the empty label + no inputs', () => {
    render(<CodenamesCurrentClueStrip clue={null} currentTeam="red" editable={false} labels={labels} />);
    expect(screen.getByText('Nessun indizio')).toBeInTheDocument();
    expect(screen.queryByRole('textbox')).toBeNull();
  });

  it('host: typing a word + clicking give fires onSetClue', async () => {
    const onSetClue = vi.fn();
    render(<CodenamesCurrentClueStrip clue={null} currentTeam="red" editable onSetClue={onSetClue} labels={labels} />);
    await userEvent.type(screen.getByRole('textbox'), 'MARE');
    await userEvent.click(screen.getByRole('button', { name: 'Dai indizio' }));
    expect(onSetClue).toHaveBeenCalled();
    expect(onSetClue.mock.calls[0][0]).toBe('MARE');
  });

  it('host: end-turn button fires onSwitchTeam', async () => {
    const onSwitchTeam = vi.fn();
    render(<CodenamesCurrentClueStrip clue={null} currentTeam="red" editable onSwitchTeam={onSwitchTeam} labels={labels} />);
    await userEvent.click(screen.getByRole('button', { name: 'Fine turno' }));
    expect(onSwitchTeam).toHaveBeenCalledOnce();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesCurrentClueStrip.test.tsx`
Expected: FAIL — `Cannot find module '../CodenamesCurrentClueStrip'`.

- [ ] **Step 3: Write the implementation**

```tsx
// CodenamesCurrentClueStrip.tsx
'use client';

import { type ReactElement, useState } from 'react';

import type { CodenamesClue, CodenamesTeam } from './codenames-state';

export interface CodenamesCurrentClueStripProps {
  readonly clue: CodenamesClue | null;
  readonly currentTeam: CodenamesTeam;
  readonly editable: boolean;
  readonly onSetClue?: (word: string, number: number) => void;
  readonly onClearClue?: () => void;
  readonly onSwitchTeam?: () => void;
  readonly labels: {
    noClue: string;
    wordPlaceholder: string;
    numberAria: string;
    giveClue: string;
    endTurn: string;
  };
}

export function CodenamesCurrentClueStrip({
  clue,
  editable,
  onSetClue,
  onSwitchTeam,
  labels,
}: CodenamesCurrentClueStripProps): ReactElement {
  const [word, setWord] = useState('');
  const [num, setNum] = useState(1);

  return (
    <div data-slot="codenames-clue" className="flex flex-wrap items-center gap-2 rounded-lg border border-border bg-card p-2">
      {clue != null ? (
        <span data-slot="codenames-clue-active" className="text-sm font-bold text-foreground">
          {clue.word} : <span className="tabular-nums">{clue.number}</span>
        </span>
      ) : (
        <span className="text-xs text-muted-foreground">{labels.noClue}</span>
      )}

      {editable && (
        <span className="ml-auto flex items-center gap-1">
          <input
            type="text"
            aria-label={labels.wordPlaceholder}
            placeholder={labels.wordPlaceholder}
            value={word}
            onChange={e => setWord(e.target.value)}
            className="w-24 rounded border border-border bg-background px-2 py-1 text-xs text-foreground"
          />
          <input
            type="number"
            min={0}
            aria-label={labels.numberAria}
            value={num}
            onChange={e => setNum(Number(e.target.value))}
            className="w-14 rounded border border-border bg-background px-2 py-1 text-xs tabular-nums text-foreground"
          />
          <button
            type="button"
            onClick={() => onSetClue?.(word.trim(), num)}
            className="rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
          >
            {labels.giveClue}
          </button>
          <button
            type="button"
            onClick={() => onSwitchTeam?.()}
            className="rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
          >
            {labels.endTurn}
          </button>
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesCurrentClueStrip.test.tsx`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/CodenamesCurrentClueStrip.tsx" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesCurrentClueStrip.test.tsx"
git commit -m "feat(session-live): #2790 Codenames L3 current-clue strip"
```

---

## Task 7: CodenamesLiveFlavor container (self-builds labels)

**Files:**
- Create: `apps/web/src/components/features/session-live/flavors/codenames/CodenamesLiveFlavor.tsx`
- Test: `apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesLiveFlavor.test.tsx`

**Interfaces:**
- Consumes: `CodenamesWordGrid`, `CodenamesTeamTracker`, `CodenamesCurrentClueStrip`, `parseCodenamesGameState`, `codenamesWinner`, `useCodenamesStateEditor`; `useLiveSessionStore`; `hasRequiredRole`, `ParticipantRole`; `LiveSessionDto`; `useIntl` + `useTranslation`.
- Produces: `CodenamesLiveFlavor` + `CodenamesLiveFlavorProps` (game-agnostic `FlavorProps`):
  ```ts
  interface CodenamesLiveFlavorProps {
    session: LiveSessionDto; viewerRole: ParticipantRole; sessionId: string;
    className?: string; livePoints?: ReadonlyMap<string, number> | null; phaseName?: string | null;
  }
  ```

- [ ] **Step 1: Write the failing test**

```tsx
// CodenamesLiveFlavor.test.tsx
import { describe, expect, it, beforeEach, vi } from 'vitest';
import { render } from '@testing-library/react';
import { IntlProvider } from 'react-intl';
import { axe, toHaveNoViolations } from 'jest-axe';

import { CodenamesLiveFlavor } from '../CodenamesLiveFlavor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { generateCodenamesBoard } from '../codenames-board-preset';

expect.extend(toHaveNoViolations);
vi.mock('@/hooks/mutations/useUpdateLiveGameState', () => ({
  useUpdateLiveGameState: () => ({ mutate: vi.fn() }),
}));

const session = {
  id: 's1', sessionCode: 'ABC', gameId: null, gameName: 'Codenames', gameSlug: 'codenames',
  createdByUserId: 'u1', status: 'InProgress', visibility: 'Private', groupId: null,
  createdAt: '', startedAt: '', pausedAt: null, completedAt: null, updatedAt: '', lastSavedAt: null,
  currentTurnIndex: 0, currentTurnPlayerId: 'p1', agentMode: 'None', notes: null,
  players: [
    { id: 'p1', userId: null, displayName: 'Marco', avatarUrl: null, color: 'Red', role: 'Host', teamId: null, totalScore: 5, currentRank: 1, joinedAt: '', isActive: true },
    { id: 'p2', userId: null, displayName: 'Anna', avatarUrl: null, color: 'Blue', role: 'Player', teamId: null, totalScore: 3, currentRank: 2, joinedAt: '', isActive: false },
  ],
  teams: [], roundScores: [], scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
} as const;

function renderFlavor(props: Partial<Parameters<typeof CodenamesLiveFlavor>[0]> = {}) {
  return render(
    <IntlProvider locale="en" messages={{}} onError={() => {}}>
      <CodenamesLiveFlavor session={session} viewerRole="Player" sessionId="s1" {...props} />
    </IntlProvider>
  );
}
beforeEach(() => useLiveSessionStore.getState().reset());

describe('CodenamesLiveFlavor', () => {
  it('renders the leaderboard even with null gameState; no board', () => {
    const { container } = renderFlavor();
    expect(container.querySelectorAll('[data-slot="codenames-leaderboard-row"]')).toHaveLength(2);
    expect(container.querySelector('[data-slot="codenames-board"]')).toBeNull();
  });

  it('host sees the init CTA when gameState is null', () => {
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelector('[data-slot="codenames-init"]')).not.toBeNull();
  });

  it('renders the board + teams + clue when gameState is present', () => {
    const { board } = generateCodenamesBoard('red');
    useLiveSessionStore.getState().setGameState({ v: 1, game: 'codenames', board, currentTeam: 'red', clue: null });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(container.querySelectorAll('[data-slot="codenames-cell"]')).toHaveLength(25);
    expect(container.querySelector('[data-slot="codenames-teams"]')).not.toBeNull();
    expect(container.querySelector('[data-slot="codenames-clue"]')).not.toBeNull();
  });

  it('has no axe violations (host, populated)', async () => {
    const { board } = generateCodenamesBoard('red');
    useLiveSessionStore.getState().setGameState({ v: 1, game: 'codenames', board, currentTeam: 'red', clue: { word: 'MARE', number: 2 } });
    const { container } = renderFlavor({ viewerRole: 'Host' });
    expect(await axe(container)).toHaveNoViolations();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesLiveFlavor.test.tsx`
Expected: FAIL — `Cannot find module '../CodenamesLiveFlavor'`.

- [ ] **Step 3: Write the implementation**

```tsx
// CodenamesLiveFlavor.tsx
'use client';

import { type ReactElement, useState } from 'react';

import { useIntl } from 'react-intl';

import { useTranslation } from '@/hooks/useTranslation';
import type { LiveSessionDto } from '@/lib/api/schemas/live-sessions.schemas';
import { hasRequiredRole, type ParticipantRole } from '@/lib/session-live/participant-role';

import { CodenamesCurrentClueStrip } from './CodenamesCurrentClueStrip';
import { CodenamesTeamTracker } from './CodenamesTeamTracker';
import { CodenamesWordGrid } from './CodenamesWordGrid';
import { codenamesWinner } from './codenames-state';
import { useCodenamesStateEditor } from './use-codenames-state-editor';

export interface CodenamesLiveFlavorProps {
  readonly session: LiveSessionDto;
  readonly viewerRole: ParticipantRole;
  readonly sessionId: string;
  readonly className?: string;
  readonly livePoints?: ReadonlyMap<string, number> | null;
  readonly phaseName?: string | null;
}

const K = 'pages.sessionLive.flavor.codenames';

export function CodenamesLiveFlavor({
  session,
  viewerRole,
  sessionId,
  className,
  livePoints,
}: CodenamesLiveFlavorProps): ReactElement {
  const { t } = useTranslation();
  const intl = useIntl();
  const isHost = hasRequiredRole(viewerRole, 'Host');
  const editor = useCodenamesStateEditor(sessionId);
  const state = editor.state;
  const [spymaster, setSpymaster] = useState(false);

  const tmpl = (id: string, fallback: string) => (intl.messages[`${K}.${id}`] as string) ?? fallback;
  const scoreOf = (playerId: string): number =>
    livePoints?.get(playerId) ?? session.players.find(p => p.id === playerId)?.totalScore ?? 0;
  const sorted = [...session.players].sort((a, b) => scoreOf(b.id) - scoreOf(a.id));
  const winner = state != null ? codenamesWinner(state) : null;

  return (
    <section
      aria-label={t(`${K}.panelAriaLabel`)}
      data-slot="codenames-flavor-live"
      className={`flex flex-col gap-4 ${className ?? ''}`.trim()}
    >
      {/* Leaderboard (ungated — from scoring) */}
      <div data-slot="codenames-leaderboard" className="flex flex-col gap-2">
        <h3 className="text-xs font-semibold uppercase tracking-wide text-muted-foreground">
          {t(`${K}.leaderboardHeading`)}
        </h3>
        <ul role="list" className="flex flex-col gap-1">
          {sorted.map((player, idx) => (
            <li
              key={player.id}
              data-slot="codenames-leaderboard-row"
              className={[
                'flex items-center gap-2 rounded-lg px-2 py-1.5',
                idx === 0 ? 'border border-entity-session/40 bg-entity-session/10' : 'border border-transparent bg-card',
              ].join(' ')}
            >
              <span className="min-w-0 flex-1 truncate text-xs font-medium text-foreground">
                {player.displayName}
                {idx === 0 && <span aria-hidden="true"> 🏆</span>}
              </span>
              <span className="shrink-0 tabular-nums text-sm font-bold text-foreground">
                {scoreOf(player.id)}
              </span>
            </li>
          ))}
        </ul>
      </div>

      {state != null ? (
        <>
          {winner != null && (
            <p role="status" data-slot="codenames-gameover" className="rounded-lg bg-muted px-3 py-2 text-sm font-semibold text-foreground">
              {tmpl('winnerTemplate', '{team} wins').replace(
                '{team}',
                winner === 'red' ? t(`${K}.redLabel`) : t(`${K}.blueLabel`)
              )}
            </p>
          )}

          <CodenamesTeamTracker
            board={state.board}
            currentTeam={state.currentTeam}
            labels={{
              redLabel: t(`${K}.redLabel`),
              blueLabel: t(`${K}.blueLabel`),
              foundTemplate: tmpl('foundTemplate', '{found}/{total}'),
              turnLabel: t(`${K}.turnLabel`),
            }}
          />

          {isHost && (
            <button
              type="button"
              onClick={() => setSpymaster(s => !s)}
              aria-pressed={spymaster}
              className="self-start rounded-md border border-border bg-background px-2 py-1 text-xs font-semibold text-foreground hover:bg-muted"
            >
              {spymaster ? t(`${K}.perspectiveSpymaster`) : t(`${K}.perspectiveOperative`)}
            </button>
          )}

          <CodenamesWordGrid
            board={state.board}
            editable={isHost}
            perspective={isHost && spymaster ? 'spymaster' : 'operative'}
            onRevealCell={editor.revealCell}
            revealAriaTemplate={tmpl('revealAriaTemplate', 'Reveal {word}')}
          />

          <CodenamesCurrentClueStrip
            clue={state.clue}
            currentTeam={state.currentTeam}
            editable={isHost}
            onSetClue={editor.setClue}
            onClearClue={editor.clearClue}
            onSwitchTeam={editor.switchTeam}
            labels={{
              noClue: t(`${K}.noClue`),
              wordPlaceholder: t(`${K}.cluePlaceholder`),
              numberAria: t(`${K}.clueNumberAria`),
              giveClue: t(`${K}.giveClue`),
              endTurn: t(`${K}.endTurn`),
            }}
          />

          {isHost && (
            <button
              type="button"
              onClick={editor.regenerateBoard}
              className="self-start text-xs text-muted-foreground underline hover:text-foreground"
            >
              {t(`${K}.regenerate`)}
            </button>
          )}
        </>
      ) : isHost ? (
        <button
          type="button"
          data-slot="codenames-init"
          onClick={editor.initializeState}
          className="self-start rounded-lg border border-entity-session/40 bg-entity-session/10 px-3 py-2 text-sm font-semibold text-entity-session hover:bg-entity-session/20"
        >
          {t(`${K}.initBoardCta`)}
        </button>
      ) : (
        <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
          {t(`${K}.viewerWaiting`)}
        </p>
      )}
    </section>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm exec vitest run src/components/features/session-live/flavors/codenames/__tests__/CodenamesLiveFlavor.test.tsx`
Expected: PASS (4 tests, incl. axe).

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/flavors/codenames/CodenamesLiveFlavor.tsx" "apps/web/src/components/features/session-live/flavors/codenames/__tests__/CodenamesLiveFlavor.test.tsx"
git commit -m "feat(session-live): #2790 Codenames L3 flavor container (self-builds labels)"
```

---

## Task 8: Wire into the registry + i18n

**Files:**
- Modify: `apps/web/src/components/features/session-live/FlavorRenderer.tsx`
- Modify: `apps/web/src/locales/it.json`, `apps/web/src/locales/en.json`
- Test: `apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx` (extend)

**Interfaces:**
- Consumes: `CodenamesLiveFlavor` (a `FlavorProps` component) from Task 7. `FlavorRenderer` is already game-agnostic (from #2788) — this is a purely additive entry, no interface change.

- [ ] **Step 1: Add the FLAVOR_MAP entry**

In `FlavorRenderer.tsx`, add a module-scope lazy component alongside the existing ones:

```tsx
const CodenamesLiveFlavorLazy: FlavorComponent = dynamic(
  () => import('./flavors/codenames/CodenamesLiveFlavor').then(m => ({ default: m.CodenamesLiveFlavor })),
  { ssr: false, loading: () => <FlavorLoadingSkeleton /> }
);
```

and add `codenames: { live: CodenamesLiveFlavorLazy }` to `FLAVOR_MAP`.

- [ ] **Step 2: Extend the FlavorRenderer guard test**

In `FlavorRenderer.test.tsx`, add to the `hasFlavor` assertions:

```ts
expect(hasFlavor('codenames')).toBe(true);
```

- [ ] **Step 3: Add the i18n keys**

In `src/locales/it.json`, under `pages.sessionLive.flavor`, add a `"codenames"` sibling:

```json
"codenames": {
  "panelAriaLabel": "Codenames",
  "leaderboardHeading": "Classifica",
  "initBoardCta": "Genera griglia",
  "viewerWaiting": "In attesa dell'host…",
  "redLabel": "Rossi",
  "blueLabel": "Blu",
  "turnLabel": "Al turno",
  "foundTemplate": "{found}/{total}",
  "revealAriaTemplate": "Rivela {word}",
  "perspectiveOperative": "Vista: Agente",
  "perspectiveSpymaster": "Vista: Spymaster",
  "noClue": "Nessun indizio attivo",
  "cluePlaceholder": "Parola",
  "clueNumberAria": "Numero indizio",
  "giveClue": "Dai indizio",
  "endTurn": "Fine turno",
  "regenerate": "Rigenera griglia",
  "winnerTemplate": "Vincono i {team}"
}
```

Mirror in `src/locales/en.json` with English copy (`"leaderboardHeading": "Standings"`, `"initBoardCta": "Generate grid"`, `"viewerWaiting": "Waiting for the host…"`, `"redLabel": "Red"`, `"blueLabel": "Blue"`, `"turnLabel": "On turn"`, `"perspectiveOperative": "View: Operative"`, `"perspectiveSpymaster": "View: Spymaster"`, `"noClue": "No active clue"`, `"cluePlaceholder": "Word"`, `"clueNumberAria": "Clue number"`, `"giveClue": "Give clue"`, `"endTurn": "End turn"`, `"regenerate": "Regenerate grid"`, `"winnerTemplate": "{team} wins"`; `foundTemplate`/`revealAriaTemplate` identical).

- [ ] **Step 4: Typecheck + run affected suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run \
  src/components/features/session-live/flavors/codenames \
  src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
```
Expected: typecheck clean; all suites PASS.

- [ ] **Step 5: Commit**

```bash
git add "apps/web/src/components/features/session-live/FlavorRenderer.tsx" "apps/web/src/components/features/session-live/__tests__/FlavorRenderer.test.tsx" "apps/web/src/locales/it.json" "apps/web/src/locales/en.json"
git commit -m "feat(session-live): #2790 wire Codenames flavor into the registry + i18n"
```

---

## Task 9: Final verification

- [ ] **Step 1: Full typecheck + all flavor suites**

```bash
rm -rf .next/types
pnpm typecheck
pnpm exec vitest run src/components/features/session-live/flavors src/components/features/session-live/__tests__/FlavorRenderer.test.tsx
```
Expected: typecheck clean; all Catan + Wingspan + Codenames flavor tests PASS.

- [ ] **Step 2: Lint the touched files**

```bash
pnpm exec eslint --max-warnings=0 "src/components/features/session-live/flavors/codenames/**/*.{ts,tsx}" "src/components/features/session-live/FlavorRenderer.tsx"
```
Expected: no errors (key colours are inline `hsl()` via the palette; `text-white` only on coloured-bg cells).

- [ ] **Step 3: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2790-codenames-l2-l3
gh pr create --base main-dev --head feature/issue-2790-codenames-l2-l3 \
  --title "feat(session-live): #2790 Codenames L2+L3 flavor (word grid + team trackers + clue)" \
  --body "Implements the Codenames flavor per docs/superpowers/specs/2026-07-17-codenames-l2-l3-flavor-design.md. FE-only; reuses the game-agnostic plumbing (one FLAVOR_MAP entry). Closes #2790."
```

---

## Self-Review

**1. Spec coverage:**
- L2 schema + derivations (assassin/counts/winner) → Task 1. ✅
- Board preset (25 distinct, 9/8/7/1, one assassin) → Task 2. ✅
- Editor immediate-PUT (reveal/clue/team/regenerate, no-op on null except init) → Task 3. ✅
- WordGrid (perspective, host tap, palette) → Task 4. TeamTracker (derived) → Task 5. ClueStrip → Task 6. ✅
- Container (leaderboard ungated, board gated, self-builds labels, game-over derived, perspective local) → Task 7. ✅
- Wiring (FLAVOR_MAP + i18n) → Task 8. ✅
- VP-from-scoring invariant → Tasks 5/7 (read `livePoints`/`totalScore`; team counts from board, not scores). ✅
- `currentTeam` stored → Task 1 schema. Immediate PUT → Task 3. ✅
- Testing (unit + component + jest-axe) → Tasks 1–7. ✅
- No backend / no FlavorRenderer refactor → additive Task 8. ✅

**2. Placeholder scan:** No TBD/TODO; every code step has complete code. ✅

**3. Type consistency:** `CodenamesGameState`, `CodenamesCell`, `CodenamesClue`, `CodenamesTeam`, `CodenamesKey`, `parseCodenamesGameState`, `teamCounts`, `codenamesWinner`, `oppositeTeam`, `generateCodenamesBoard`, `useCodenamesStateEditor`, and the component prop interfaces are used identically across Tasks 1→8. The `teamCounts` call in `CodenamesTeamTracker` passes a full `CodenamesGameState` literal (with `v/game/clue`) — matches the signature. ✅

**Known follow-ups (out of scope):** clue-history persistence; guess timer; free-text word importer; deriving `currentTeam` from phases; Codenames summary flavor.
