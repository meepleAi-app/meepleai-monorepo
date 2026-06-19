# Issue #2389 Block B — Renderer wire-up Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the polymorphic `scoringType`/`scoreData` store selector into `SessionLiveView`'s read-only `ScoringPanelRenderer`, replacing the hardcoded `kind: 'Points'` adapter shipped by PR #2423, with REST hydration race-guard, observability on malformed JSON, and an accessible empty-state placeholder.

**Architecture:** New pure-function adapter `mapScoreDataToPanelData(scoringType, scoreData, players, options) → ScoringPanelData | null` in `apps/web/src/lib/session-live/`, consumed by `SessionLiveView` via `useMemo` from store selectors. REST hydration `useEffect` pre-populates the store from `sessionQuery.data` only when SignalR has not already hydrated (`getState().scoringType != null` guard). Both desktop right-column score tab and mobile bottom-sheet score case use the same memo with a conditional gate that renders either the polymorphic renderer or an `aria-live` loading placeholder.

**Tech Stack:** Next.js 16 App Router, React 19, TypeScript, Vitest + @testing-library/react, Zustand store (`useLiveSessionStore`), Zod schemas (`GameSessionDto`), react-intl, ESLint.

**Spec:** `docs/superpowers/specs/2026-06-19-issue-2389-block-b-renderer-wire-up-design.md`

**Branch:** `feature/issue-2389-block-b-renderer-wire-up` (parent: `main-dev`, already created)

**Effort:** ~7-9h focused (1 day)

---

## File Structure

**NEW files:**

| Path | Responsibility | LOC |
|------|----------------|-----|
| `apps/web/src/lib/session-live/mvp-objectives-catalogue.ts` | Single export `MVP_OBJECTIVES_CATALOGUE: readonly string[]` — placeholder shared by editor + renderer until real game-level catalogue ships. | ~10 |
| `apps/web/src/lib/session-live/score-data-to-panel-data.ts` | Pure function `mapScoreDataToPanelData()` — narrows polymorphic `scoreData` (editor shape) to `ScoringPanelData` (renderer shape) per variant. Returns `null` on null inputs. | ~95 |
| `apps/web/src/lib/session-live/__tests__/score-data-to-panel-data.test.ts` | 16 Vitest cases: null gates (3), happy path per variant (4), displayName fallback (1), missing-player padding (4), Objectives catalogue edge (2), empty players list (2). | ~225 |

**MODIFIED files:**

| Path | Change | LOC diff |
|------|--------|----------|
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | Add 4 imports + 3 store selectors + REST hydration `useEffect` + replace lines 947-959 memo + add a11y placeholder around 2 mount sites (lines 1242-1244 desktop, 1103-1112 mobile). | ~60 lines net |
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx` | Add `useLiveSessionStore` import + 10 new tests + `beforeEach` store reset + i18n loading label key. | ~210 lines new |
| `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` | Replace inline `MVP_OBJECTIVES_CATALOGUE` constant with import from new lib module (lines 73-80 → 1 import). | ~−10 net |
| `apps/web/src/lib/api/schemas/games.schemas.ts` | Add 2 optional nullable fields `scoringType?: string \| null` and `scoreData?: string \| null` to `GameSessionDtoSchema` (catch-up Block A BE evolution which the FE schema missed). | ~+2 |
| `apps/web/src/locales/it.json` | Add 1 i18n key `pages.sessionLive.scoring.loadingLabel`. | ~+1 |
| `apps/web/src/locales/en.json` | Add 1 i18n key `pages.sessionLive.scoring.loadingLabel` (English translation). | ~+1 |
| `CLAUDE.md` | Add bullet under "Session live shell (epic #2354)" referencing #2389 Block B. | ~+2 |

**Total:** 3 new files, 6 modified files, ~570 LOC net (mostly test code).

---

### Task 1: Hoist `MVP_OBJECTIVES_CATALOGUE` to shared lib module

**Files:**
- Create: `apps/web/src/lib/session-live/mvp-objectives-catalogue.ts`
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx:70-82`

- [ ] **Step 1.1: Create the catalogue module**

Create `apps/web/src/lib/session-live/mvp-objectives-catalogue.ts`:

```typescript
/**
 * MVP placeholder objectives catalogue.
 *
 * Issue #2389 Block B (T1): hoisted out of `scores/page.tsx` so the read-only
 * `ScoringPanelRenderer` adapter and the mutable `PolymorphicScoreEditor` share
 * the same labels until real game-level catalogue wiring ships
 * (tracked follow-up: replace this stub with a per-game lookup).
 *
 * String entries double as objective IDs (id = label) — the adapter and editor
 * both rely on this identity. When the real catalogue arrives, IDs and labels
 * will diverge (id = GUID, label = i18n key).
 *
 * Do NOT modify entries without coordinating with the editor + renderer tests.
 */
export const MVP_OBJECTIVES_CATALOGUE: readonly string[] = [
  'Vittoria',
  'Sopravvivenza',
  'Tesoro',
  'Esplorazione',
  'Alleanza',
] as const;
```

Note: keep the same 5 entries currently in `scores/page.tsx:73-80` verbatim. If that file ships with different entries between the time this plan was written and execution, mirror those exactly — the test suite for the editor relies on the current set.

- [ ] **Step 1.2: Verify the source catalogue entries match**

Run: `grep -A 7 "MVP_OBJECTIVES_CATALOGUE" apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/scores/page.tsx`

Expected: identical 5 strings in the same order. If they differ, copy the source values into the new file instead.

- [ ] **Step 1.3: Update `scores/page.tsx` to import from new location**

Open `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx`. Replace lines 70-82 (the JSDoc block + the `const MVP_OBJECTIVES_CATALOGUE: readonly string[] = [...]`) with a single import added near the other imports at the top of the file:

```typescript
import { MVP_OBJECTIVES_CATALOGUE } from '@/lib/session-live/mvp-objectives-catalogue';
```

Leave all references to `MVP_OBJECTIVES_CATALOGUE` in JSX (line 137 `availableObjectives={MVP_OBJECTIVES_CATALOGUE}`) untouched — the symbol now resolves via import.

- [ ] **Step 1.4: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors.

- [ ] **Step 1.5: Run scores page tests if they exist**

Run: `cd apps/web && pnpm test scores/page 2>&1 | tail -20`
Expected: either "No test files found" (file has no dedicated test) or all green.

- [ ] **Step 1.6: Commit**

```bash
git add apps/web/src/lib/session-live/mvp-objectives-catalogue.ts \
        apps/web/src/app/\(authenticated\)/sessions/live/\[sessionId\]/scores/page.tsx
git commit -m "refactor(session-live): #2389 Block B T1 hoist MVP_OBJECTIVES_CATALOGUE

Move the placeholder catalogue out of scores/page.tsx into a shared
lib module so both the read-only ScoringPanelRenderer adapter
(Block B) and the mutable PolymorphicScoreEditor (Asse D follow-up
P1 #1899) consume the same labels.

The MVP constant remains a placeholder until real game-level
catalogue wiring ships (follow-up issue to be filed).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 2: Extend `GameSessionDtoSchema` with `scoringType`+`scoreData`

**Files:**
- Modify: `apps/web/src/lib/api/schemas/games.schemas.ts:98-109`

**Context:** Block A BE evolution added `ScoringType` and `ScoreData` (string, nullable) to the backend `SessionDto`, but the FE Zod schema mirror was not updated. The REST hydration `useEffect` in Task 5 needs these fields type-safely.

- [ ] **Step 2.1: Extend `GameSessionDtoSchema`**

Open `apps/web/src/lib/api/schemas/games.schemas.ts`. Inside the `GameSessionDtoSchema = z.object({...})` block (lines 98-109), add two optional nullable fields just before the closing `});` on line 109:

```typescript
  durationMinutes: z.number().int().nonnegative(),
  // #2389 Block B: polymorphic scoring config exposed by Block A BE evolution.
  // Both fields are nullable strings (scoreData is a JSON-encoded payload).
  scoringType: z.string().nullable().optional(),
  scoreData: z.string().nullable().optional(),
});
```

The result must look like (showing only the changed portion):

```typescript
export const GameSessionDtoSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  status: z.string().min(1),
  startedAt: z.string().datetime({ offset: true }),
  completedAt: z.string().datetime({ offset: true }).nullable(),
  playerCount: z.number().int().positive(),
  players: z.array(SessionPlayerDtoSchema),
  winnerName: z.string().nullable(),
  notes: z.string().nullable(),
  durationMinutes: z.number().int().nonnegative(),
  scoringType: z.string().nullable().optional(),
  scoreData: z.string().nullable().optional(),
});
```

- [ ] **Step 2.2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors. `GameSessionDto` type inference now includes the 2 new optional fields.

- [ ] **Step 2.3: Run existing schema/client tests to confirm no regression**

Run: `cd apps/web && pnpm test games.schemas gamesClient 2>&1 | tail -10`
Expected: green (or no relevant test files; both fields are additive and optional).

- [ ] **Step 2.4: Commit**

```bash
git add apps/web/src/lib/api/schemas/games.schemas.ts
git commit -m "feat(api-schemas): #2389 Block B T2 mirror SessionDto scoringType+scoreData

Block A backend evolution added ScoringType and ScoreData (string,
nullable) to SessionDto but the FE Zod schema mirror was not updated.
Adds the two optional nullable fields to GameSessionDtoSchema so the
upcoming REST hydration useEffect (T5) can read them type-safely.

Both fields are additive and optional — no existing callsite breaks.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 3: Write failing adapter unit tests (RED)

**Files:**
- Create: `apps/web/src/lib/session-live/__tests__/score-data-to-panel-data.test.ts`

- [ ] **Step 3.1: Create the test file with 14 RED tests**

Create `apps/web/src/lib/session-live/__tests__/score-data-to-panel-data.test.ts`:

```typescript
/**
 * mapScoreDataToPanelData unit tests — Issue #2389 Block B (T3).
 *
 * Pure function: no React, no hook ceremony, no store mocking required.
 * Covers null gates, 4 happy-path variants, displayName fallback,
 * missing-player padding per variant, and Objectives catalogue edges.
 */

import { describe, expect, it } from 'vitest';

import { mapScoreDataToPanelData } from '@/lib/session-live/score-data-to-panel-data';
import type {
  ScoreDataByType,
  ScoreType,
} from '@/components/sessions/score-strategies/types';

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna', displayName: 'Anna B.' },
] as const;

const CATALOGUE = ['Vittoria', 'Tesoro'] as const;

// ─── Null gates ───────────────────────────────────────────────────────────────

describe('mapScoreDataToPanelData — null gates', () => {
  it('returns null when scoringType is null', () => {
    const result = mapScoreDataToPanelData(null, { scores: [] }, PLAYERS);
    expect(result).toBeNull();
  });

  it('returns null when scoreData is null', () => {
    const result = mapScoreDataToPanelData('Points', null, PLAYERS);
    expect(result).toBeNull();
  });

  it('returns null when both null', () => {
    const result = mapScoreDataToPanelData(null, null, PLAYERS);
    expect(result).toBeNull();
  });
});

// ─── Happy path per variant ───────────────────────────────────────────────────

describe('mapScoreDataToPanelData — happy path', () => {
  it('maps Points correctly', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [
        { playerId: 'p1', points: 10 },
        { playerId: 'p2', points: 7 },
      ],
    };
    const result = mapScoreDataToPanelData('Points', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Points',
      players: [
        { id: 'p1', displayName: 'Marco', score: 10 },
        { id: 'p2', displayName: 'Anna B.', score: 7 },
      ],
    });
  });

  it('maps BinaryWin correctly', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [
        { playerId: 'p1', isWinner: true },
        { playerId: 'p2', isWinner: false },
      ],
    };
    const result = mapScoreDataToPanelData('BinaryWin', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Anna B.', isWinner: false },
      ],
    });
  });

  it('maps Ranking correctly', () => {
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [
        { playerId: 'p1', position: 2 },
        { playerId: 'p2', position: 1 },
      ],
    };
    const result = mapScoreDataToPanelData('Ranking', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Ranking',
      players: [
        { id: 'p1', displayName: 'Marco', position: 2 },
        { id: 'p2', displayName: 'Anna B.', position: 1 },
      ],
    });
  });

  it('maps Objectives with catalogue', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['Vittoria'] },
        { playerId: 'p2', objectives: [] },
      ],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS, {
      availableObjectives: CATALOGUE,
    });
    expect(result).toEqual({
      kind: 'Objectives',
      players: [
        { id: 'p1', displayName: 'Marco', completedObjectives: ['Vittoria'] },
        { id: 'p2', displayName: 'Anna B.', completedObjectives: [] },
      ],
      objectives: [
        { id: 'Vittoria', label: 'Vittoria', done: true },
        { id: 'Tesoro', label: 'Tesoro', done: false },
      ],
    });
  });
});

// ─── displayName fallback ─────────────────────────────────────────────────────

describe('mapScoreDataToPanelData — displayName fallback', () => {
  it('falls back to name when displayName is undefined', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [{ playerId: 'p1', points: 5 }],
    };
    const players = [{ id: 'p1', name: 'Marco' }]; // no displayName
    const result = mapScoreDataToPanelData('Points', scoreData, players);
    expect(result).toEqual({
      kind: 'Points',
      players: [{ id: 'p1', displayName: 'Marco', score: 5 }],
    });
  });
});

// ─── Missing-player padding per variant ───────────────────────────────────────

describe('mapScoreDataToPanelData — missing-player padding', () => {
  it('pads Points missing player with score=0', () => {
    const scoreData: ScoreDataByType['Points'] = {
      scores: [{ playerId: 'p1', points: 10 }],
    };
    const result = mapScoreDataToPanelData('Points', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Points',
      players: [
        { id: 'p1', displayName: 'Marco', score: 10 },
        { id: 'p2', displayName: 'Anna B.', score: 0 },
      ],
    });
  });

  it('pads Ranking missing player with position=players.length', () => {
    const scoreData: ScoreDataByType['Ranking'] = {
      positions: [{ playerId: 'p1', position: 1 }],
    };
    const result = mapScoreDataToPanelData('Ranking', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'Ranking',
      players: [
        { id: 'p1', displayName: 'Marco', position: 1 },
        { id: 'p2', displayName: 'Anna B.', position: 2 },
      ],
    });
  });

  it('pads BinaryWin missing player with isWinner=false', () => {
    const scoreData: ScoreDataByType['BinaryWin'] = {
      results: [{ playerId: 'p1', isWinner: true }],
    };
    const result = mapScoreDataToPanelData('BinaryWin', scoreData, PLAYERS);
    expect(result).toEqual({
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Anna B.', isWinner: false },
      ],
    });
  });

  it('pads Objectives missing player with empty array', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [{ playerId: 'p1', objectives: ['Vittoria'] }],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS, {
      availableObjectives: CATALOGUE,
    });
    expect(result).toEqual({
      kind: 'Objectives',
      players: [
        { id: 'p1', displayName: 'Marco', completedObjectives: ['Vittoria'] },
        { id: 'p2', displayName: 'Anna B.', completedObjectives: [] },
      ],
      objectives: [
        { id: 'Vittoria', label: 'Vittoria', done: true },
        { id: 'Tesoro', label: 'Tesoro', done: false },
      ],
    });
  });
});

// ─── Objectives catalogue edges ───────────────────────────────────────────────

describe('mapScoreDataToPanelData — Objectives catalogue edges', () => {
  it('returns empty objectives array when no availableObjectives passed', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [{ playerId: 'p1', objectives: ['Vittoria'] }],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS);
    expect(result).toMatchObject({
      kind: 'Objectives',
      objectives: [],
    });
  });

  it('marks objective done=true when at least one player completed it', () => {
    const scoreData: ScoreDataByType['Objectives'] = {
      completedByPlayer: [
        { playerId: 'p1', objectives: ['Vittoria'] },
        { playerId: 'p2', objectives: [] },
      ],
    };
    const result = mapScoreDataToPanelData('Objectives', scoreData, PLAYERS, {
      availableObjectives: ['Vittoria', 'Sopravvivenza'],
    });
    expect(result).toMatchObject({
      objectives: [
        { id: 'Vittoria', label: 'Vittoria', done: true },
        { id: 'Sopravvivenza', label: 'Sopravvivenza', done: false },
      ],
    });
  });
});

// ─── Empty players list edge ─────────────────────────────────────────────────

describe('mapScoreDataToPanelData — empty players list', () => {
  it('returns empty players array when players list is empty (Points)', () => {
    const result = mapScoreDataToPanelData('Points', { scores: [] }, []);
    expect(result).toEqual({ kind: 'Points', players: [] });
  });

  it('returns empty players array when players list is empty (Ranking) without invalid position=0', () => {
    const result = mapScoreDataToPanelData('Ranking', { positions: [] }, []);
    expect(result).toEqual({ kind: 'Ranking', players: [] });
  });
});
```

- [ ] **Step 3.2: Run tests to confirm they fail RED**

Run: `cd apps/web && pnpm test score-data-to-panel-data 2>&1 | tail -20`
Expected: all 16 tests FAIL with import error "Cannot find module '@/lib/session-live/score-data-to-panel-data'".

- [ ] **Step 3.3: Commit the RED tests**

```bash
git add apps/web/src/lib/session-live/__tests__/score-data-to-panel-data.test.ts
git commit -m "test(session-live): #2389 Block B T3 adapter unit test scaffold (RED)

16 Vitest cases for mapScoreDataToPanelData:
  3 null gates + 4 happy-path variants + 1 displayName fallback +
  4 missing-player padding + 2 Objectives catalogue edges +
  2 empty-players-list edges (Points + Ranking).

All RED until T4 implements the adapter pure function.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 4: Implement adapter pure function (GREEN)

**Files:**
- Create: `apps/web/src/lib/session-live/score-data-to-panel-data.ts`

- [ ] **Step 4.1: Implement the adapter**

Create `apps/web/src/lib/session-live/score-data-to-panel-data.ts`:

```typescript
/**
 * mapScoreDataToPanelData — pure adapter from polymorphic scoreData
 * (editor / store shape) to ScoringPanelData (renderer discriminated union).
 *
 * Issue #2389 Block B (T4) — wires the polymorphic store selector into
 * SessionLiveView's read-only ScoringPanelRenderer.
 *
 * Returns null when scoringType is null OR scoreData is null (no SignalR
 * delivery yet AND no REST hydration). Caller MUST gate the renderer on null.
 *
 * For each variant, `players[]` is the master list: every player appears
 * in the output. Missing scoreData entries are padded with type-specific
 * defaults (Points→0, Ranking→players.length, BinaryWin→false, Objectives→[]).
 * Display name falls back to name per-player when displayName is undefined.
 *
 * Objectives variant: catalogue from `options.availableObjectives ?? []`.
 * Each catalogue entry becomes `{ id: label, label, done: anyPlayerCompleted }`.
 *
 * Pure function: no side effects, no implicit imports, deterministic.
 */

import type { ScoringPanelData } from '@/components/features/session-live';
import type {
  ScoreDataByType,
  ScoreType,
} from '@/components/sessions/score-strategies/types';

interface AdapterPlayer {
  readonly id: string;
  readonly name: string;
  readonly displayName?: string;
}

export interface MapScoreDataOptions {
  readonly availableObjectives?: ReadonlyArray<string>;
}

export function mapScoreDataToPanelData(
  scoringType: ScoreType | null,
  scoreData: ScoreDataByType[ScoreType] | null,
  players: ReadonlyArray<AdapterPlayer>,
  options?: MapScoreDataOptions
): ScoringPanelData | null {
  if (scoringType === null || scoreData === null) return null;

  switch (scoringType) {
    case 'Points': {
      const data = scoreData as ScoreDataByType['Points'];
      const scoresByPlayer = new Map(
        data.scores.map(s => [s.playerId, s.points])
      );
      return {
        kind: 'Points',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          score: scoresByPlayer.get(p.id) ?? 0,
        })),
      };
    }

    case 'BinaryWin': {
      const data = scoreData as ScoreDataByType['BinaryWin'];
      const winnerByPlayer = new Map(
        data.results.map(r => [r.playerId, r.isWinner])
      );
      return {
        kind: 'BinaryWin',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          isWinner: winnerByPlayer.get(p.id) ?? false,
        })),
      };
    }

    case 'Ranking': {
      const data = scoreData as ScoreDataByType['Ranking'];
      const positionByPlayer = new Map(
        data.positions.map(r => [r.playerId, r.position])
      );
      const lastPosition = players.length;
      return {
        kind: 'Ranking',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          position: positionByPlayer.get(p.id) ?? lastPosition,
        })),
      };
    }

    case 'Objectives': {
      const data = scoreData as ScoreDataByType['Objectives'];
      const objectivesByPlayer = new Map(
        data.completedByPlayer.map(r => [r.playerId, r.objectives])
      );
      const catalogue = options?.availableObjectives ?? [];
      return {
        kind: 'Objectives',
        players: players.map(p => ({
          id: p.id,
          displayName: p.displayName ?? p.name,
          completedObjectives: objectivesByPlayer.get(p.id) ?? [],
        })),
        objectives: catalogue.map(label => ({
          id: label,
          label,
          done: data.completedByPlayer.some(cb =>
            cb.objectives.includes(label)
          ),
        })),
      };
    }

    default:
      return assertNever(scoringType);
  }
}

function assertNever(value: never): never {
  throw new Error(
    `mapScoreDataToPanelData: unhandled scoringType "${value as string}"`
  );
}
```

- [ ] **Step 4.2: Run tests to confirm all 14 GREEN**

Run: `cd apps/web && pnpm test score-data-to-panel-data 2>&1 | tail -20`
Expected: 16 / 16 PASS.

- [ ] **Step 4.3: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors.

- [ ] **Step 4.4: Commit the adapter**

```bash
git add apps/web/src/lib/session-live/score-data-to-panel-data.ts
git commit -m "feat(session-live): #2389 Block B T4 implement scoreData adapter

Pure function mapScoreDataToPanelData() narrows polymorphic ScoreDataByType
(editor / store shape) to ScoringPanelData (renderer discriminated union).

- Null gate on scoringType OR scoreData.
- players[] master list; missing scoreData rows padded with variant
  defaults (Points=0, Ranking=players.length, BinaryWin=false,
  Objectives=[]).
- displayName falls back to name per-player.
- Objectives catalogue from options.availableObjectives; each entry
  becomes { id: label, label, done: anyCompleted }.
- assertNever exhaustiveness guard.

16 / 16 unit tests pass.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 5: Write failing SessionLiveView wire tests (RED)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx`

**Context:** Existing file has 67+ tests using `IntlProvider` with a `MESSAGES` map (see lines 110-213). Mock pattern uses `vi.fn()` for `useSession` (line 68). Pre-existing mocks for `useSession`, `useSessionLiveStream`, `next/navigation`, and the visual-test-fixture. The 10 new tests live in a new `describe('SessionLiveView — Block B (#2389) scoring wire-up', ...)` block appended after the existing test suites.

- [ ] **Step 5.1: Add the i18n loading label to MESSAGES**

Open `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx`. Inside the `MESSAGES` constant (around line 142, alongside other `scoring.*` keys), add:

```typescript
  'pages.sessionLive.scoring.loadingLabel': 'Caricamento punteggi…',
```

Place it between `'pages.sessionLive.scoring.playerCount': ...` and the `actionLog` block.

- [ ] **Step 5.2: Import `useLiveSessionStore`**

Near the top of the file with the other imports (after the existing `import type` lines around line 38), add:

```typescript
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { ScoreDataByType } from '@/components/sessions/score-strategies/types';
```

- [ ] **Step 5.3: Add a `beforeEach` store reset to the top-level setup**

Find the existing top-level `beforeEach` (search the file for `beforeEach(`). If there is one before the existing `describe(...)` blocks, append to it. If each `describe(...)` block has its own `beforeEach`, add a single setup-file-level reset at the top of the file just before the first `describe`:

```typescript
// Block B #2389: reset the polymorphic scoring slice between tests.
// Use the store's reset() action (which sets the full initial state) instead
// of setState({...partial}) — Zustand's setState merges by default, so a
// partial reset would leave stale fields (e.g. sessionId, players) from a
// prior test. reset() replaces the entire slice with initialState — see
// live-session-store.ts:177 for the action definition.
beforeEach(() => {
  useLiveSessionStore.getState().reset();
});
```

- [ ] **Step 5.4: Append the 10 new RED tests at the end of the file**

After the closing `});` of the last existing `describe(...)` block, append the new suite. The full block:

```typescript
// ─── Block B (#2389) scoring wire-up tests ────────────────────────────────────

describe('SessionLiveView — Block B (#2389) scoring wire-up', () => {
  beforeEach(() => {
    // Default useSession + useSessionLiveStream returns for these tests.
    useSessionMock.mockReturnValue({
      data: MOCK_SESSION_DTO,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });
    useSessionLiveStreamMock.mockReturnValue(mockLiveStreamResult);
  });

  // ── REST hydration (5) ──────────────────────────────────────────────────────

  it('calls setScoringConfig when DTO carries scoringType+scoreData', () => {
    const dtoWithConfig = {
      ...MOCK_SESSION_DTO,
      scoringType: 'Points',
      scoreData: JSON.stringify({
        scores: [{ playerId: 'player-001', points: 10 }],
      }),
    };
    useSessionMock.mockReturnValue({
      data: dtoWithConfig,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });

    renderWithIntl(<SessionLiveView />);

    expect(useLiveSessionStore.getState().scoringType).toBe('Points');
    expect(useLiveSessionStore.getState().scoreData).toEqual({
      scores: [{ playerId: 'player-001', points: 10 }],
    });
  });

  it('logs console.warn on malformed scoreData JSON', () => {
    const warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const dtoMalformed = {
      ...MOCK_SESSION_DTO,
      scoringType: 'Points',
      scoreData: 'not-valid-json',
    };
    useSessionMock.mockReturnValue({
      data: dtoMalformed,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });

    renderWithIntl(<SessionLiveView />);

    expect(warnSpy).toHaveBeenCalledWith(
      expect.stringContaining('[#2389]'),
      expect.objectContaining({ sessionId: MOCK_SESSION_DTO.id })
    );
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
    warnSpy.mockRestore();
  });

  it('does not call setScoringConfig when DTO has no scoringType (legacy session)', () => {
    // MOCK_SESSION_DTO has no scoringType/scoreData by default.
    renderWithIntl(<SessionLiveView />);
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
    expect(useLiveSessionStore.getState().scoreData).toBeNull();
  });

  it('does not overwrite SignalR-hydrated store on later REST resolve (race guard)', () => {
    // SignalR hydrates first.
    act(() => {
      useLiveSessionStore.setState({
        scoringType: 'Points',
        scoreData: {
          scores: [{ playerId: 'player-001', points: 99 }],
        } as ScoreDataByType['Points'],
      });
    });

    // REST resolves with stale snapshot.
    const dtoStale = {
      ...MOCK_SESSION_DTO,
      scoringType: 'Points',
      scoreData: JSON.stringify({
        scores: [{ playerId: 'player-001', points: 0 }],
      }),
    };
    useSessionMock.mockReturnValue({
      data: dtoStale,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });

    renderWithIntl(<SessionLiveView />);

    // SignalR data wins; REST does NOT overwrite.
    expect(useLiveSessionStore.getState().scoreData).toEqual({
      scores: [{ playerId: 'player-001', points: 99 }],
    });
  });

  it('does not call setScoringConfig when scoringType present but scoreData null', () => {
    const dtoPartial = {
      ...MOCK_SESSION_DTO,
      scoringType: 'Points',
      scoreData: null,
    };
    useSessionMock.mockReturnValue({
      data: dtoPartial,
      isLoading: false,
      isError: false,
      isSuccess: true,
      error: null,
      refetch: vi.fn(),
    });

    renderWithIntl(<SessionLiveView />);
    expect(useLiveSessionStore.getState().scoringType).toBeNull();
  });

  // ── Null gate + a11y placeholder (2) ────────────────────────────────────────

  it('renders aria-live placeholder when scoringType is null', () => {
    // Default beforeEach leaves store null; no DTO config either.
    renderWithIntl(<SessionLiveView />);

    const placeholder = document.querySelector(
      '[data-slot="scoring-panel-empty"]'
    );
    expect(placeholder).not.toBeNull();
    expect(placeholder?.getAttribute('role')).toBe('status');
    expect(placeholder?.getAttribute('aria-live')).toBe('polite');
  });

  it('placeholder shows the localized loading label text', () => {
    renderWithIntl(<SessionLiveView />);
    expect(screen.getByText('Caricamento punteggi…')).toBeInTheDocument();
  });

  // ── Variant mount via setScoringConfig action (4) ───────────────────────────

  it('mounts Points renderer when scoringType=Points', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Points',
        scoreData: { scores: [{ playerId: 'player-001', points: 10 }] },
      });
    });
    expect(
      document.querySelector('[data-slot="scoring-panel-points"]')
    ).not.toBeNull();
  });

  it('mounts BinaryWin renderer when scoringType=BinaryWin', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'BinaryWin',
        scoreData: { results: [{ playerId: 'player-001', isWinner: true }] },
      });
    });
    expect(
      document.querySelector('[data-slot="scoring-panel-binary-win"]')
    ).not.toBeNull();
  });

  it('mounts Ranking renderer when scoringType=Ranking', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Ranking',
        scoreData: { positions: [{ playerId: 'player-001', position: 1 }] },
      });
    });
    expect(
      document.querySelector('[data-slot="scoring-panel-ranking"]')
    ).not.toBeNull();
  });

  it('mounts Objectives renderer when scoringType=Objectives', () => {
    renderWithIntl(<SessionLiveView />);
    act(() => {
      useLiveSessionStore.getState().setScoringConfig({
        scoringType: 'Objectives',
        scoreData: {
          completedByPlayer: [{ playerId: 'player-001', objectives: [] }],
        },
      });
    });
    expect(
      document.querySelector('[data-slot="scoring-panel-objectives"]')
    ).not.toBeNull();
  });
});
```

- [ ] **Step 5.5: Run the new suite to confirm RED**

Run: `cd apps/web && pnpm test SessionLiveView 2>&1 | tail -30`
Expected: 10 new tests in `Block B (#2389) scoring wire-up` FAIL (placeholder not rendered, store not hydrated, etc.). The 67+ existing tests should still PASS — if they regress, the `beforeEach` store reset at step 5.3 may need broader scope; investigate before continuing.

- [ ] **Step 5.6: Commit the RED tests**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/\[id\]/live/_components/__tests__/SessionLiveView.test.tsx
git commit -m "test(session-live): #2389 Block B T5 SessionLiveView wire tests (RED)

11 new Vitest cases append-only to the existing suite:
  5 REST hydration (happy + malformed + legacy + race + partial),
  2 null gate / a11y placeholder (role + aria-live + label),
  4 variant mount via setScoringConfig action.

Adds beforeEach store reset to guarantee per-test isolation.
Adds i18n key pages.sessionLive.scoring.loadingLabel to MESSAGES.

All 11 RED until T6 wires the orchestrator.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 6: Wire REST hydration + adapter + a11y placeholder (GREEN)

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`

**Context:** The orchestrator is 1384 lines. This task touches 5 focused locations: imports (~line 71), store selectors (~line 290-300 area, near existing state hooks), REST hydration effect (~after `liveStream` declaration around line 339), `scoringPanelData` memo (replace lines 947-959), and 2 mount sites (desktop right column at lines 1242-1244, mobile drawer score case at lines 1103-1112).

- [ ] **Step 6.1: Add the 4 new imports**

Open `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx`.

Find the existing React import on line 71. The exact current line reads:

```typescript
import { useCallback, useMemo, useRef, useState, lazy, Suspense, type ReactElement } from 'react';
```

Replace it with the same names plus `useEffect` (preserve `type ReactElement` at the end):

```typescript
import { useCallback, useEffect, useMemo, useRef, useState, lazy, Suspense, type ReactElement } from 'react';
```

After the existing imports block (around line 129, right after `import { useToolkitRendererStore } from '@/lib/stores/toolkit-renderer-store';`), add:

```typescript
import { mapScoreDataToPanelData } from '@/lib/session-live/score-data-to-panel-data';
import { MVP_OBJECTIVES_CATALOGUE } from '@/lib/session-live/mvp-objectives-catalogue';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import type { ScoreDataByType, ScoreType } from '@/components/sessions/score-strategies/types';
```

- [ ] **Step 6.2: Add the 3 store selectors**

Find the `scoringPanelData` memo at lines 947-959. ABOVE that memo, add the 3 store selectors:

```typescript
// #2389 Block B: polymorphic scoring selectors from the live-session store.
// scoringType + scoreData are populated by SignalR (ScoringConfigured event)
// and/or REST hydration (useEffect below). null until either fires.
const scoringType = useLiveSessionStore(s => s.scoringType);
const scoreData = useLiveSessionStore(s => s.scoreData);
const setScoringConfig = useLiveSessionStore(s => s.setScoringConfig);
```

- [ ] **Step 6.3: Add the REST hydration `useEffect`**

Immediately AFTER the 3 selectors from step 6.2, add the REST hydration effect:

```typescript
// #2389 Block B: REST hydration with race guard + observability.
// Pre-populate the store from sessionQuery.data on initial mount so the
// renderer paints in ~300ms instead of waiting for the SignalR handshake.
// Skip if SignalR already populated to avoid stale REST overwriting fresh state.
useEffect(() => {
  const dto = sessionQuery.data;
  if (dto?.scoringType == null || dto.scoreData == null) return;
  if (useLiveSessionStore.getState().scoringType != null) return;
  try {
    const parsed = JSON.parse(dto.scoreData) as ScoreDataByType[ScoreType];
    setScoringConfig({
      scoringType: dto.scoringType as ScoreType,
      scoreData: parsed,
    });
  } catch (err) {
    console.warn('[#2389] malformed scoreData JSON, will rely on SignalR', {
      sessionId: dto.id,
      scoreDataLength: dto.scoreData?.length ?? 0,
      err,
    });
  }
}, [sessionQuery.data, setScoringConfig]);
```

- [ ] **Step 6.4: Replace the `scoringPanelData` memo**

Replace the existing memo (lines 947-959, currently hardcoded `{ kind: 'Points', ... }`) with the adapter call. The replacement, in full:

```typescript
// G5a #2375 + #2389 Block B: adapter wires polymorphic scoreData (from store)
// to ScoringPanelData (renderer discriminated union). Returns null when
// scoringType is null (no SignalR delivery + no REST hydration). Callers MUST
// gate the renderer on null and render the a11y placeholder instead.
const scoringPanelData = useMemo<ScoringPanelData | null>(
  () =>
    mapScoreDataToPanelData(
      scoringType,
      scoreData,
      activeSession?.players ?? [],
      { availableObjectives: MVP_OBJECTIVES_CATALOGUE }
    ),
  [scoringType, scoreData, activeSession?.players]
);
```

- [ ] **Step 6.5: Update desktop right column score tab with a11y placeholder**

Find the existing JSX block on lines 1242-1244:

```typescript
{tab === 'score' && (
  <ScoringPanelRenderer data={scoringPanelData} labels={scoringPanelLabels} className="p-3" />
)}
```

Replace with:

```typescript
{tab === 'score' && (
  scoringPanelData != null ? (
    <ScoringPanelRenderer
      data={scoringPanelData}
      labels={scoringPanelLabels}
      className="p-3"
    />
  ) : (
    <div
      role="status"
      aria-live="polite"
      data-slot="scoring-panel-empty"
      className="p-3 text-xs text-muted-foreground"
    >
      {t('pages.sessionLive.scoring.loadingLabel')}
    </div>
  )
)}
```

- [ ] **Step 6.6: Update mobile drawer score case with a11y placeholder**

The mobile drawer score case lives inside the `mobileSheetContent` `useMemo` (the dependency array is around line 1113-1131). The new `t('pages.sessionLive.scoring.loadingLabel')` call introduces a dependency on the `t` helper. Verify the dependency array already contains `t` — it should (the existing `actionLogLabels`/`notesLabels` memos use it). If `t` is missing, append it to the deps array. Otherwise `react-hooks/exhaustive-deps` will fail at T8 lint.

Find the mobile drawer switch case on lines 1103-1112:

```typescript
case 'score':
default:
  return (
    <ScoringPanelRenderer
      data={scoringPanelData}
      labels={scoringPanelLabels}
      className="p-2"
    />
  );
```

Replace with:

```typescript
case 'score':
default:
  return scoringPanelData != null ? (
    <ScoringPanelRenderer
      data={scoringPanelData}
      labels={scoringPanelLabels}
      className="p-2"
    />
  ) : (
    <div
      role="status"
      aria-live="polite"
      data-slot="scoring-panel-empty"
      className="p-2 text-xs text-muted-foreground"
    >
      {t('pages.sessionLive.scoring.loadingLabel')}
    </div>
  );
```

- [ ] **Step 6.7: Run the SessionLiveView tests to confirm all GREEN**

Run: `cd apps/web && pnpm test SessionLiveView 2>&1 | tail -40`
Expected: 67+ existing tests PASS, 11 new Block B tests PASS. Total 78+ green.

If a few existing tests fail because of store pollution, ensure the global `beforeEach` reset (step 5.3) is in scope. If they fail with new errors like "scoringPanelData not defined," double-check imports in step 6.1.

- [ ] **Step 6.8: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: 0 errors.

- [ ] **Step 6.9: Commit the wire**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/\[id\]/live/_components/SessionLiveView.tsx
git commit -m "feat(session-live): #2389 Block B T6 wire polymorphic adapter

Wires SessionLiveView to consume the polymorphic scoringType + scoreData
from useLiveSessionStore via the new mapScoreDataToPanelData adapter.

- 3 store selectors (scoringType, scoreData, setScoringConfig).
- REST hydration useEffect with race guard (skip if SignalR already
  populated the store) and console.warn observability on malformed JSON.
- scoringPanelData useMemo now calls the polymorphic adapter — hardcoded
  { kind: 'Points', ... } removed.
- 2 mount sites (desktop right column + mobile drawer score case) gate
  on scoringPanelData != null and render role=status aria-live=polite
  placeholder with new i18n loading label when null.

All 67+ existing SessionLiveView tests pass; 11 new Block B tests pass.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 7: Add i18n loading label to it.json + en.json

**Files:**
- Modify: `apps/web/src/locales/it.json:3209-3218` block
- Modify: `apps/web/src/locales/en.json` (mirror)

- [ ] **Step 7.1: Add the key to it.json**

Open `apps/web/src/locales/it.json`. Inside the `"sessionLive"."scoring"` block (currently lines 3209-3218), add a new key after `"playerCount"`:

The block before:
```json
      "scoring": {
        "title": "Punteggi",
        "scoreLabel": "Punteggio: {score}",
        "winnerLabel": "Vincitore",
        "myScoreLabel": "Il tuo punteggio",
        "incrementAriaLabel": "Aumenta punteggio di {playerName}",
        "decrementAriaLabel": "Diminuisci punteggio di {playerName}",
        "scoreInputAriaLabel": "Inserisci punteggio per {playerName}",
        "playerCount": "{count, plural, =0 {Nessun giocatore} =1 {1 giocatore} other {# giocatori}}"
      },
```

After (adds `loadingLabel`):
```json
      "scoring": {
        "title": "Punteggi",
        "scoreLabel": "Punteggio: {score}",
        "winnerLabel": "Vincitore",
        "myScoreLabel": "Il tuo punteggio",
        "incrementAriaLabel": "Aumenta punteggio di {playerName}",
        "decrementAriaLabel": "Diminuisci punteggio di {playerName}",
        "scoreInputAriaLabel": "Inserisci punteggio per {playerName}",
        "playerCount": "{count, plural, =0 {Nessun giocatore} =1 {1 giocatore} other {# giocatori}}",
        "loadingLabel": "Caricamento punteggi…"
      },
```

- [ ] **Step 7.2: Add the key to en.json**

Open `apps/web/src/locales/en.json`. Find the corresponding `"sessionLive"."scoring"` block (use grep `grep -n '"scoring":' apps/web/src/locales/en.json` if needed). Add the same key with English text:

```json
        "loadingLabel": "Loading scores…"
```

Place it in the same logical position (after the last existing scoring entry in the block).

- [ ] **Step 7.3: Run typecheck + tests**

Run: `cd apps/web && pnpm typecheck && pnpm test SessionLiveView 2>&1 | tail -10`
Expected: 0 type errors. 78+ tests green (67 existing + 11 new). (The test file already has the key in its `MESSAGES` map per T5 step 5.1.)

- [ ] **Step 7.4: Commit the i18n change**

```bash
git add apps/web/src/locales/it.json apps/web/src/locales/en.json
git commit -m "i18n(session-live): #2389 Block B T7 add scoring.loadingLabel key

Italian default 'Caricamento punteggi…' + English 'Loading scores…'.
Consumed by the new a11y aria-live placeholder in SessionLiveView
when scoringPanelData is null (before SignalR / REST hydrate the store).

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 8: Typecheck + lint sweep

**Files:** N/A (runs full FE quality gate).

- [ ] **Step 8.1: Run typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -15`
Expected: 0 errors.

- [ ] **Step 8.2: Run lint**

Run: `cd apps/web && pnpm lint 2>&1 | tail -25`
Expected: 0 new errors. The ESLint rule `local/no-store-scores-direct` should NOT fire on any of the new selectors (`scoringType`, `scoreData`, `setScoringConfig`) — only on `s.scores` reads.

If new warnings appear that are unrelated to Block B (pre-existing legacy), document them and continue. If new warnings are caused by Block B (unused import, missing dep array), fix inline.

- [ ] **Step 8.3: Run the full FE test suite (smoke)**

Run: `cd apps/web && pnpm test 2>&1 | tail -20`
Expected: full suite green. If unrelated tests regress, investigate before continuing.

- [ ] **Step 8.4: If any inline fixes were made, commit them**

```bash
git add apps/web/
git commit -m "chore(session-live): #2389 Block B T8 typecheck + lint sweep

Quality gate pre-PR.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

If no fixes were needed, skip the commit.

---

### Task 9: Update CLAUDE.md + file 4 tracking follow-up issues

**Files:**
- Modify: `CLAUDE.md` (root)
- GitHub: file 4 issues via `gh issue create`

- [ ] **Step 9.1: Update CLAUDE.md**

Open `CLAUDE.md` at the repo root. Find the section `### Session live shell (epic #2354)` (search for the heading). Append a new bullet under it:

```markdown
- **G5a polymorphic wire — Issue #2389**: Block A (PR #2428, merged 2026-06-17) shipped the polymorphic store contract + SignalR `ScoringConfigured` event + `useSessionScores` hook extension + `setScoringConfig` action + ESLint rule `local/no-store-scores-direct` (warn). Block B (this branch) wires `SessionLiveView` to consume `scoringType`+`scoreData` via the new `mapScoreDataToPanelData` adapter (`apps/web/src/lib/session-live/score-data-to-panel-data.ts`), with REST hydration race guard, `console.warn` on malformed JSON, and an `aria-live` placeholder for the null window. Block C scheduled +14 days post-Block-B merge to `main-dev` to delete the deprecated `scores: Record<string, number>` field and promote the ESLint rule from `warn` to `error`. Spec: [`2026-06-19-issue-2389-block-b-renderer-wire-up-design.md`](./docs/superpowers/specs/2026-06-19-issue-2389-block-b-renderer-wire-up-design.md). Plan: [`2026-06-19-issue-2389-block-b-renderer-wire-up.md`](./docs/superpowers/plans/2026-06-19-issue-2389-block-b-renderer-wire-up.md).
```

- [ ] **Step 9.2: File the 4 follow-up issues on GitHub**

Run each `gh issue create` command separately. Use the GitHub CLI bound to the current repo.

**⚠ Shell**: the heredoc syntax `$(cat <<'EOF' ... EOF)` is Bash-only. On Windows, run these in **Git Bash** (via the Bash tool) — NOT PowerShell. Alternatively, write the body to a temp file and use `gh issue create --body-file body.md`.

**Issue 1 — Block B+ editor swap:**

```bash
gh issue create \
  --title "#2389 Block B+ — PolymorphicScoreEditor host swap + mutation wire" \
  --label "feature,session-live,frontend" \
  --body "$(cat <<'EOF'
Follow-up to #2389 Block B (renderer wire-up).

## Scope

Wire the **mutable** counterpart to the read-only ScoringPanelRenderer that Block B shipped:

1. Role-based component swap in SessionLiveView score tab: if `viewerRole === 'Host'`, mount `PolymorphicScoreEditor` (from `components/sessions/`); otherwise mount `ScoringPanelRenderer` (Block B default).
2. Wire `useUpdateSessionScores` mutation hook (shipped in Asse D follow-up P1 #1899) via `onScoreChange` callback on the editor.
3. Toast feedback on 403 (\`Permesso negato\`) and 429 (rate limit) responses from the polymorphic endpoint.
4. Inline `useDebouncedCallback` 500ms for autosave (mirror of existing `scores/page.tsx` pattern).
5. Layout responsiveness: editor + renderer share the same tab container.

## Out-of-scope

- Backend changes (Block A already ships the mutation endpoint).
- EndgameDialog finalScores adapter (separate follow-up).

## Effort

~2-3 days.

## Blocks / blocked by

Blocked by: #2389 Block B merge.
Blocks: Block C scope reduction (some consumers may switch to editor selector earlier).
EOF
)"
```

**Issue 2 — EndgameDialog finalScores adapter:**

```bash
gh issue create \
  --title "#2389 follow-up — EndgameDialog polymorphic finalScores adapter" \
  --label "feature,session-live,frontend" \
  --body "$(cat <<'EOF'
Follow-up to #2389 Block B.

## Scope

Replace the hardcoded \`{ playerName, score, isWinner: false }\` mapping in
\`SessionLiveView.tsx\` lines 1364-1368 (the \`EndgameDialog.finalScores\` prop)
with a polymorphic adapter that computes the winner per ScoreType variant:

- Points → sorted DESC by score; \`isWinner: true\` for the leader.
- Ranking → \`position === 1\` is the winner.
- BinaryWin → \`isWinner\` mirrors the scoreData flag.
- Objectives → \`max(completedObjectives.length)\` is the winner; tie-break by player order.

Reuse \`mapScoreDataToPanelData\` if possible, or introduce a new dedicated adapter
in \`lib/session-live/score-data-to-endgame-summary.ts\` if the output shape diverges enough.

## Effort

~0.5 day.

## Blocks / blocked by

Blocked by: #2389 Block B merge (depends on the adapter pattern shipped there).
EOF
)"
```

**Issue 3 — Real Objectives catalogue wiring:**

```bash
gh issue create \
  --title "#2389 follow-up — Real Objectives catalogue (replace MVP_OBJECTIVES_CATALOGUE)" \
  --label "feature,session-live,backend,frontend" \
  --body "$(cat <<'EOF'
Follow-up to #2389 Block B.

## Scope

Replace the placeholder \`MVP_OBJECTIVES_CATALOGUE\` constant
(\`apps/web/src/lib/session-live/mvp-objectives-catalogue.ts\`) with a real
game-level catalogue lookup.

Options to explore:
1. Add \`objectivesCatalogue: string[]\` field to \`SessionDto\` exposed by the BE
   when scoringType === 'Objectives'. The adapter consumes it via the existing
   \`availableObjectives\` option.
2. Add a per-game catalogue API + React Query hook \`useGameObjectivesCatalogue(gameId)\`.

## Effort

~1 day (depends on BE pattern choice).

## Blocks / blocked by

Blocked by: #2389 Block B merge.

## References

- Block B adapter: \`apps/web/src/lib/session-live/score-data-to-panel-data.ts\`
- MVP catalogue stub: \`apps/web/src/lib/session-live/mvp-objectives-catalogue.ts\`
EOF
)"
```

**Issue 4 — Legacy participant score endpoint deprecation:**

```bash
gh issue create \
  --title "#2389 follow-up — Deprecate legacy PUT /participants/{id}/score endpoint" \
  --label "tech-debt,session-live,frontend,backend" \
  --body "$(cat <<'EOF'
Follow-up to #2389 Block B.

## Scope

The legacy endpoint \`PUT /api/v1/game-sessions/{id}/participants/{playerId}/score\`
is referenced only by the dead \`_handleScoreUpdate\` callback in
\`SessionLiveView.tsx\` lines 507-580. After Block B+ editor swap ships
(see follow-up), the dead code can be removed and the BE endpoint marked
deprecated → removed in a future release.

## Steps

1. After Block B+ editor swap merges, delete \`_handleScoreUpdate\` and its
   \`pendingScoreRef\` / \`localScoreOverrides\` state.
2. BE: mark the endpoint \`[Obsolete]\` with a migration note pointing to
   \`PUT /scores-polymorphic\`.
3. After ~30 days, delete the endpoint + tests.

## Effort

~0.25 day.

## Blocks / blocked by

Blocked by: Block B+ editor swap ship.
EOF
)"
```

- [ ] **Step 9.3: Capture the 4 issue URLs**

Each `gh issue create` prints the URL. Record the 4 URLs for the PR description:

```bash
gh issue list --label "session-live" --search "#2389 follow-up OR #2389 Block B+" --limit 10 2>&1 | tail -10
```

- [ ] **Step 9.4: Commit CLAUDE.md update**

```bash
git add CLAUDE.md
git commit -m "docs(claude-md): #2389 Block B note + follow-up tracking

Adds G5a polymorphic wire bullet under Session live shell (epic #2354).
Links Block A PR #2428, Block B spec + plan, and Block C schedule.

Follow-up issues filed for editor swap, EndgameDialog adapter, real
catalogue wiring, and legacy endpoint deprecation.

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
```

---

### Task 10: Push branch + open PR to main-dev

**Files:** N/A (git ops).

- [ ] **Step 10.1: Push the branch**

Run: `git push -u origin feature/issue-2389-block-b-renderer-wire-up`
Expected: branch pushed; remote-tracking link set.

- [ ] **Step 10.2: Open the PR**

```bash
gh pr create --base main-dev --title "feat(session-live): #2389 Block B — scoringType selector wire-up" --body "$(cat <<'EOF'
## Summary

Wires `SessionLiveView` to consume polymorphic `scoringType` + `scoreData` from
the live-session store via a new pure-function adapter, replacing the hardcoded
`{ kind: 'Points' }` placeholder shipped by PR #2423 (G5a closure). Closes the
read-side of the #2389 store migration; Block C (+14 days post-merge) will
delete the deprecated `scores: Record<string, number>` field.

Closes: #2389 (Block B sub-issue scope only — Block C is a separate follow-up).
Spec: `docs/superpowers/specs/2026-06-19-issue-2389-block-b-renderer-wire-up-design.md`.
Plan: `docs/superpowers/plans/2026-06-19-issue-2389-block-b-renderer-wire-up.md`.

## Changes

- **NEW** `apps/web/src/lib/session-live/score-data-to-panel-data.ts` — pure adapter.
- **NEW** `apps/web/src/lib/session-live/mvp-objectives-catalogue.ts` — hoisted placeholder.
- **NEW** `apps/web/src/lib/session-live/__tests__/score-data-to-panel-data.test.ts` — 16 unit tests.
- **MOD** `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` — 3 store selectors + REST hydration `useEffect` (race guard + `console.warn`) + polymorphic memo + 2 a11y placeholder mount sites.
- **MOD** `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/__tests__/SessionLiveView.test.tsx` — +10 tests (5 hydration + 2 a11y + 4 variant mount via `setScoringConfig` action).
- **MOD** `apps/web/src/lib/api/schemas/games.schemas.ts` — add `scoringType` + `scoreData` to `GameSessionDtoSchema` (catch-up Block A BE evolution).
- **MOD** `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` — import `MVP_OBJECTIVES_CATALOGUE` from new location.
- **MOD** `apps/web/src/locales/{it,en}.json` — new key `pages.sessionLive.scoring.loadingLabel`.
- **MOD** `CLAUDE.md` — Session live shell bullet for #2389 Block B + Block C schedule.

## Design decisions (locked in spec)

| DEC | Choice | Rationale |
|-----|--------|-----------|
| DEC-1 SCOPE | Renderer-only wire-up | `ScoringPanelRenderer` is read-only by design; editor swap is multi-day separate scope. |
| DEC-2 ADAPTER | Pure function module + `useMemo` | Testable, decoupled, reusable from desktop + mobile mount sites. |
| DEC-3 NULL GATE | REST hydration + `getState()` race guard + `console.warn` + `aria-live` placeholder | ~300ms paint, no SignalR overwrite, observable failures, accessible empty state. |
| DEC-4 OBJECTIVES | Hoist `MVP_OBJECTIVES_CATALOGUE` to shared lib | Editor + adapter share placeholder until real catalogue ships. |

## Out-of-scope (documented gaps)

- `useUpdateSessionScores` mutation wire — filed as **Block B+ editor swap** follow-up.
- 403/429 toast — n/a without mutation wire.
- `EndgameDialog.finalScores` polymorphic adapter — separate follow-up.
- Legacy `PUT /participants/{id}/score` endpoint carve-out — separate follow-up (waits for editor swap).
- Real Objectives catalogue — separate follow-up (depends on BE pattern choice).

## Tests

- 16 unit tests for the adapter (4 variants + 3 null gates + 1 displayName fallback + 4 padding + 2 catalogue edges + 2 empty players).
- 11 integration tests for `SessionLiveView` (5 hydration including race-ordering + 2 a11y + 4 variant mount).
- 67+ existing `SessionLiveView.test.tsx` cases pass without modification.

## Test plan

- [x] `pnpm test score-data-to-panel-data` → 16 / 16 green
- [x] `pnpm test SessionLiveView` → 78+ / 78+ green
- [x] `pnpm typecheck` → 0 errors
- [x] `pnpm lint` → 0 new errors (existing `warn`-level `local/no-store-scores-direct` not affected)
- [ ] Manual smoke: open `/sessions/{id}/live`, verify polymorphic renderer mounts on store update; verify placeholder shows `Caricamento punteggi…` before first SignalR/REST hydration.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 10.3: Capture the PR URL**

The `gh pr create` command prints the PR URL on success. Record it. Verify in browser if needed.

---

## Self-Review

**1. Spec coverage:** Walked through every requirement in the spec:

| Spec requirement | Task |
|------------------|------|
| `score-data-to-panel-data.ts` adapter + 14 tests | T3 + T4 |
| Adapter padding defaults documented + tested | T4 (code), T3 (4 padding test cases) |
| `SessionLiveView.tsx` consumes adapter via `useMemo` | T6 step 6.4 |
| REST hydration `useEffect` | T6 step 6.3 |
| REST hydration race guard | T6 step 6.3 (`getState()` check) |
| `console.warn` on malformed JSON | T6 step 6.3 (catch block) |
| `aria-live` placeholder | T6 steps 6.5 + 6.6 |
| Desktop + mobile both apply null gate + placeholder | T6 steps 6.5 + 6.6 |
| `MVP_OBJECTIVES_CATALOGUE` hoisted; editor import updated | T1 |
| New i18n key `pages.sessionLive.scoring.loadingLabel` | T7 |
| All 67+ existing tests pass | T5 step 5.5 + T6 step 6.7 |
| 10 new SessionLiveView tests pass (5 + 2 + 4) — wait, plan has 5+2+4=11 | **fix needed** |
| Typecheck + lint clean | T8 |
| PR opened to main-dev | T10 |
| 4 follow-up tracking issues filed | T9 |

**Mismatch found:** Spec says 10 new tests (5 hydration + 2 a11y + 4 variant mount). Plan T5 actually writes 5 + 2 + 4 = 11. Need to reconcile.

Re-counting:
- REST hydration: 5 (happy, malformed, legacy, race, partial)
- Null gate + a11y: 2 (placeholder structure, label text)
- Variant mount: 4 (Points, BinaryWin, Ranking, Objectives)

Total = 11. The spec test count table line "Null gate + a11y | 2" is correct. The earlier paragraph "10 new" was stale from before the a11y test split. **Fix the spec to say 11**, or merge 2 a11y tests into 1 combined test.

→ Resolution: keep 11 tests; update the spec inline before commit.

**Also re-counted:** Existing tests: spec says "67+". After variant mount tests added, total green = 77+. Plan T6 step 6.7 says "77+ green". Consistent.

**2. Placeholder scan:** Searched plan for "TBD", "TODO", "implement later". None present.

**3. Type consistency:** `ScoreType`, `ScoreDataByType`, `ScoringPanelData`, `MVP_OBJECTIVES_CATALOGUE`, `setScoringConfig` used consistently across T3 → T4 → T5 → T6. `mapScoreDataToPanelData` signature stable.

**4. Issues filed at the right phase:** T9 files issues BEFORE T10 opens the PR, so the PR body can link them. Order correct.

**5. Critical inline fixes during self-review:**
- Update spec to say "11 new tests" (5+2+4) instead of "10". Going to fix inline before PR.
- T5 commit message says "10 new" — change to "11" to match.
