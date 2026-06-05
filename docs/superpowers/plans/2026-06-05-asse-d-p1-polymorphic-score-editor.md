# Asse D Follow-up P1 — Polymorphic ScoreType Editor (FE)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implementare FE polymorphic score editor primitive che consume asse A backend `IScoringStrategy` (ScoreType: Points/BinaryWin/Objectives/Ranking). Wire-through `UpdateSessionScoresCommand` da `/sessions/live/[sessionId]/scores` page.

**Architecture:** Component primitive `PolymorphicScoreEditor` con strategy dispatch (4 sub-components) + TanStack Query mutation hook + integrazione `scores/page.tsx` esistente. Riusa `@dnd-kit/sortable` per Ranking strategy.

**Tech Stack:** Next.js 16 · React 19 · Zustand `useLiveSessionStore` (esistente) · TanStack Query · `@dnd-kit/core+sortable+utilities` (esistenti) · Vitest + RTL

**Branch**: `feature/asse-d-followup-p1-polymorphic-editor` (just created)
**Parent issue**: [#1899](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899) (asse D follow-up P1)
**Backend dependency**: asse A v2.1 SHIPPED (PR #1917) — `UpdateSessionScoresCommand` + `IScoringStrategy` + `ScoringStrategyFactory` + `MAX_LIVE_SESSIONS_EXCEEDED` exception
**Effort target**: M ~4-6h subagent execution

---

## Decisioni lockate (DEC-1..DEC-4)

| ID | Decisione | Rationale |
|----|-----------|-----------|
| **DEC-1** | Solo P1 (4-6h scope), defer P2/P3/P4 | CRIT-1: 4 priorità in 1 sessione non fattibile |
| **DEC-2** | Tutti 4 strategies MVP (Points/BinaryWin/Objectives/Ranking) | Coverage 100% catalog, sblocca FULL polymorphic asse A |
| **DEC-3** | Mantieni route esistente `/sessions/live/[sessionId]` | No breaking change links |
| **DEC-4** | Component primitive in `components/sessions/` + integrate scores/page.tsx | Riusabile per future contexts (summary, drawer) |

---

## File Structure

### New files
- `apps/web/src/components/sessions/PolymorphicScoreEditor.tsx` (main dispatcher)
- `apps/web/src/components/sessions/score-strategies/PointsEditor.tsx` (numeric input per player)
- `apps/web/src/components/sessions/score-strategies/BinaryWinEditor.tsx` (radio Win/Lose per player)
- `apps/web/src/components/sessions/score-strategies/ObjectivesEditor.tsx` (checklist obiettivi per player)
- `apps/web/src/components/sessions/score-strategies/RankingEditor.tsx` (drag-reorder position 1..N)
- `apps/web/src/components/sessions/score-strategies/types.ts` (shared types ScoreType, ScoreData)
- `apps/web/src/components/sessions/score-strategies/index.ts` (barrel)
- `apps/web/src/hooks/use-update-session-scores.ts` (TanStack Query mutation)
- `apps/web/src/components/sessions/__tests__/PolymorphicScoreEditor.test.tsx`
- `apps/web/src/components/sessions/score-strategies/__tests__/*.test.tsx` (4 strategy tests)
- `apps/web/src/hooks/__tests__/use-update-session-scores.test.tsx`

### Modified files
- `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx` (integrate PolymorphicScoreEditor, defer to ScoreBoard if scoringType=Points legacy)

---

## Work Packages

| WP | Scope | Effort | Critical | Task |
|----|-------|--------|----------|------|
| **WP1** | Types + IScoringStrategy frontend mirror | S (~30min) | YES | T1 |
| **WP2** | PointsEditor + BinaryWinEditor (simple) | M (~1h) | NO (parallel) | T2 |
| **WP3** | ObjectivesEditor + RankingEditor (DnD) | M (~1.5h) | NO (parallel) | T3 |
| **WP4** | PolymorphicScoreEditor dispatcher + types barrel | S (~30min) | YES (closes T2+T3) | T4 |
| **WP5** | useUpdateSessionScores TanStack Query mutation | S (~30min) | YES | T5 |
| **WP6** | Integrate scores/page.tsx + scoring_type-aware routing | M (~45min) | YES (chiude) | T6 |
| **WP7** | E2E skeleton + acceptance | S (~30min) | YES | T7 |

**Total**: 7 task, ~4-6h effort

---

## WP1 — Types foundation

### Task 1: Frontend ScoreType + ScoreData types

**Files:**
- Create: `apps/web/src/components/sessions/score-strategies/types.ts`

```typescript
/**
 * Asse A backend ScoreType enum mirror.
 * Aligned with apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ScoreType.cs
 */
export type ScoreType = 'Points' | 'BinaryWin' | 'Objectives' | 'Ranking';

// Strategy-specific score data shapes (JSON serialized server-side)
export interface PointsScoreData {
  scores: { playerId: string; points: number }[];
}

export interface BinaryWinScoreData {
  results: { playerId: string; isWinner: boolean }[];
}

export interface ObjectivesScoreData {
  completedByPlayer: { playerId: string; objectives: string[] }[];
}

export interface RankingScoreData {
  positions: { playerId: string; position: number }[];
}

export type ScoreDataByType = {
  Points: PointsScoreData;
  BinaryWin: BinaryWinScoreData;
  Objectives: ObjectivesScoreData;
  Ranking: RankingScoreData;
};

export interface PlayerOption {
  id: string;
  displayName: string;
  avatar?: string;
}

export interface ScoreStrategyProps<T extends ScoreType> {
  players: readonly PlayerOption[];
  initialData?: ScoreDataByType[T];
  onChange: (data: ScoreDataByType[T]) => void;
  disabled?: boolean;
}
```

- [ ] Commit `feat(sessions): asse D P1 ScoreType+ScoreData types mirror backend (T1)`

---

## WP2 — Points + BinaryWin strategies

### Task 2: PointsEditor + BinaryWinEditor components

**Files:**
- Create: `score-strategies/PointsEditor.tsx`
- Create: `score-strategies/BinaryWinEditor.tsx`
- Tests: 2 test files (5+ test each)

**PointsEditor**: numeric input per player, validate non-negative, expose onChange con PointsScoreData

**BinaryWinEditor**: radio group Win/Lose per player, expose onChange con BinaryWinScoreData

Acceptance:
- 5 unit test PointsEditor (default value, change handler, negative validation, multiple players, disabled state)
- 5 unit test BinaryWinEditor (default, change handler, multi-winner cooperative, all-lose, disabled)

- [ ] Commit `feat(sessions): asse D P1 PointsEditor + BinaryWinEditor (T2)`

---

## WP3 — Objectives + Ranking strategies

### Task 3: ObjectivesEditor + RankingEditor components

**Files:**
- Create: `score-strategies/ObjectivesEditor.tsx`
- Create: `score-strategies/RankingEditor.tsx`
- Tests: 2 test files

**ObjectivesEditor**:
- Props extends: `availableObjectives: string[]` (list di nomi obiettivi disponibili)
- Per ogni player: checklist toggle obiettivi completati
- Validate: no duplicates per player
- onChange: ObjectivesScoreData con `completedByPlayer[].objectives[]`

**RankingEditor**:
- Use `@dnd-kit/sortable` per drag-reorder players
- Position 1..N derived from order
- Validate: distinct positions, no gaps
- onChange: RankingScoreData con `positions[].position`

Acceptance:
- 5 unit test ObjectivesEditor (toggle, multi-toggle, all-toggled, none-toggled, validate no duplicate UI)
- 5 unit test RankingEditor (initial order, reorder after drag, positions sequential, disabled state, keyboard accessible)

- [ ] Commit `feat(sessions): asse D P1 ObjectivesEditor + RankingEditor with DnD (T3)`

---

## WP4 — Dispatcher

### Task 4: PolymorphicScoreEditor dispatcher

**Files:**
- Create: `components/sessions/PolymorphicScoreEditor.tsx`
- Create: `components/sessions/index.ts` (barrel)
- Test: `__tests__/PolymorphicScoreEditor.test.tsx`

```tsx
'use client';

import type { ScoreType, ScoreDataByType, PlayerOption } from './score-strategies/types';

import { PointsEditor } from './score-strategies/PointsEditor';
import { BinaryWinEditor } from './score-strategies/BinaryWinEditor';
import { ObjectivesEditor } from './score-strategies/ObjectivesEditor';
import { RankingEditor } from './score-strategies/RankingEditor';

export interface PolymorphicScoreEditorProps {
  scoringType: ScoreType;
  players: readonly PlayerOption[];
  initialData?: any;  // narrowed inside dispatch
  availableObjectives?: string[];  // required for Objectives
  onChange: (scoringType: ScoreType, data: any) => void;
  disabled?: boolean;
}

export function PolymorphicScoreEditor({
  scoringType,
  players,
  initialData,
  availableObjectives,
  onChange,
  disabled,
}: PolymorphicScoreEditorProps) {
  switch (scoringType) {
    case 'Points':
      return (
        <PointsEditor
          players={players}
          initialData={initialData}
          onChange={data => onChange('Points', data)}
          disabled={disabled}
        />
      );
    case 'BinaryWin':
      return (
        <BinaryWinEditor
          players={players}
          initialData={initialData}
          onChange={data => onChange('BinaryWin', data)}
          disabled={disabled}
        />
      );
    case 'Objectives':
      if (!availableObjectives) {
        throw new Error('availableObjectives required for Objectives scoring');
      }
      return (
        <ObjectivesEditor
          players={players}
          availableObjectives={availableObjectives}
          initialData={initialData}
          onChange={data => onChange('Objectives', data)}
          disabled={disabled}
        />
      );
    case 'Ranking':
      return (
        <RankingEditor
          players={players}
          initialData={initialData}
          onChange={data => onChange('Ranking', data)}
          disabled={disabled}
        />
      );
    default: {
      const _exhaustive: never = scoringType;
      throw new Error(`Unknown scoring type: ${_exhaustive}`);
    }
  }
}
```

Acceptance:
- 5 unit test dispatcher (each ScoreType renders correct strategy + throws on unknown + Objectives requires availableObjectives)

- [ ] Commit `feat(sessions): asse D P1 PolymorphicScoreEditor dispatcher (T4)`

---

## WP5 — TanStack Query mutation

### Task 5: useUpdateSessionScores hook

**Files:**
- Create: `apps/web/src/hooks/use-update-session-scores.ts`
- Test: `__tests__/use-update-session-scores.test.tsx`

```typescript
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { getApiBase } from '@/lib/api/core/httpClient';

import type { ScoreType } from '@/components/sessions/score-strategies/types';

interface UpdateSessionScoresPayload {
  sessionId: string;
  scoringType: ScoreType;
  scoreData: unknown;  // strategy-specific, validated server-side
}

interface UpdateSessionScoresResult {
  sessionId: string;
  scoringType: ScoreType;
  computedWinnerId: string | null;
}

export function useUpdateSessionScores() {
  const queryClient = useQueryClient();
  
  return useMutation<UpdateSessionScoresResult, Error, UpdateSessionScoresPayload>({
    mutationFn: async ({ sessionId, scoringType, scoreData }) => {
      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/v1/game-sessions/${sessionId}/scores-polymorphic`, {
        method: 'PUT',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          scoringType,
          scoreData: JSON.stringify(scoreData),  // backend expects JSON string
        }),
      });
      
      if (res.status === 403) {
        throw new Error('Not authorized to update scores (IDOR guard)');
      }
      if (res.status === 400) {
        const error = await res.json();
        throw new Error(`Validation failed: ${JSON.stringify(error)}`);
      }
      if (!res.ok) {
        throw new Error(`Update scores failed: ${res.status}`);
      }
      
      return await res.json();
    },
    onSuccess: (data, { sessionId }) => {
      // Invalidate queries that depend on session state
      queryClient.invalidateQueries({ queryKey: ['session', sessionId] });
      queryClient.invalidateQueries({ queryKey: ['live-session', sessionId] });
    },
  });
}
```

Acceptance:
- 5 unit test hook (success path, 403 IDOR, 400 validation, 500 generic, onSuccess invalidates queries)

- [ ] Commit `feat(sessions): asse D P1 useUpdateSessionScores mutation hook (T5)`

---

## WP6 — Integrate scores/page.tsx

### Task 6: Wire PolymorphicScoreEditor in scores page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/scores/page.tsx`
- Test: update existing test (if any) per nuovo polymorphic path

**Strategy**:
- Read `session.scoringType` da live-session-store o query
- IF `scoringType === 'Points'` AND user wants legacy ScoreBoard: fallback to existing ScoreBoard (per backward compat)
- ELSE: render PolymorphicScoreEditor con scoringType
- Wire onChange → useUpdateSessionScores mutation (debounce 500ms)
- Show autosave indicator + error toast on failure

```tsx
'use client';

import { use, useCallback, useState } from 'react';
import { useDebouncedCallback } from 'use-debounce';

import { AutosaveIndicator } from '@/components/session/live/AutosaveIndicator';
import { ScoreBoard } from '@/components/session/live/ScoreBoard';  // legacy
import { PolymorphicScoreEditor } from '@/components/sessions/PolymorphicScoreEditor';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { useUpdateSessionScores } from '@/hooks/use-update-session-scores';

interface LiveSessionScoresPageProps {
  params: Promise<{ sessionId: string }>;
}

export default function LiveSessionScoresPage({ params }: LiveSessionScoresPageProps) {
  const { sessionId } = use(params);
  const players = useLiveSessionStore(s => s.players);
  const scoringType = useLiveSessionStore(s => s.scoringType) ?? 'Points';  // default backward compat
  const isHost = players.find(p => p.isHost)?.isHost ?? false;
  
  const mutation = useUpdateSessionScores();
  
  const debouncedSave = useDebouncedCallback(
    (scoringType, data) => mutation.mutate({ sessionId, scoringType, scoreData: data }),
    500
  );

  // Legacy Points fallback (preserves existing UX for non-polymorphic sessions)
  if (scoringType === 'Points' && players.length > 0 && !isHost) {
    return (
      <div className="space-y-2">
        <div className="flex justify-end px-4 pt-2">
          <AutosaveIndicator />
        </div>
        <ScoreBoard sessionId={sessionId} isHost={isHost} />
      </div>
    );
  }

  // Polymorphic editor (host-mode + all non-Points types)
  return (
    <div className="space-y-2 p-4">
      <div className="flex justify-end">
        <AutosaveIndicator />
      </div>
      <PolymorphicScoreEditor
        scoringType={scoringType}
        players={players.map(p => ({ id: p.id, displayName: p.displayName ?? 'Player' }))}
        onChange={debouncedSave}
        disabled={mutation.isPending}
      />
      {mutation.isError && (
        <div role="alert" className="text-sm text-[hsl(var(--c-danger))]">
          {mutation.error?.message ?? 'Errore salvataggio score'}
        </div>
      )}
    </div>
  );
}
```

NB: `use-debounce` package — verifica presenza. Se assente, scrivere mini-helper inline.

Acceptance:
- Existing scores page test verde
- Polymorphic editor renders when scoringType !== 'Points' or host mode
- Legacy ScoreBoard preserved for backward compat

- [ ] Commit `feat(sessions): asse D P1 wire PolymorphicScoreEditor in scores page (T6)`

---

## WP7 — Acceptance + close

### Task 7: E2E skeleton + acceptance close

**Files:**
- Create: `apps/web/e2e/asse-d-p1-polymorphic-scoring.spec.ts` (skeleton)
- Modify: `CLAUDE.md` (asse D follow-up P1 status)

```typescript
import { test, expect } from '@playwright/test';

test.describe('Asse D P1 polymorphic ScoreType editor (skeleton)', () => {
  test.skip(({ browserName }) => browserName !== 'chromium', 'Chromium-only');

  test('scores page renders polymorphic editor for non-Points scoring', async ({ page }) => {
    // Full E2E flow requires authenticated session + seeded live session
    // Skeleton only — full test deferred to P4 cross-asse E2E infra
    await page.goto('/sessions/live/test-session-id/scores');
    const editor = page.locator('[data-testid="polymorphic-score-editor"]');
    // Skip assertion until auth+seeded
  });
});
```

Acceptance:
- Plan v2 doc shipped
- 7 task TDD shipped
- ~25+ unit test passing (5 types + 5*4 strategies + 5 dispatcher + 5 hook)
- No regression on existing scores page test
- CLAUDE.md update con stato P1 shipped

- [ ] Commit `docs(asse-d): #1899 P1 polymorphic ScoreType editor COMPLETE (T7)`

---

## Self-Review Checklist

**Spec coverage**:
- [x] DEC-1 only P1 in sessione → WP1-WP7 in 7 task
- [x] DEC-2 tutti 4 strategies → WP2 + WP3
- [x] DEC-3 mantieni route → WP6 modifica esistente
- [x] DEC-4 component primitive → WP1+WP4 in `components/sessions/`

**Backend dependency check**:
- asse A v2.1 SHIPPED (PR #1917) → `UpdateSessionScoresCommand` + IDOR guard + `MAX_LIVE_SESSIONS_EXCEEDED`
- `useUpdateSessionScores` hook (WP5) consume endpoint `PUT /api/v1/game-sessions/{id}/scores-polymorphic`

**Effort verification**:
- WP1: 30min
- WP2: 1h
- WP3: 1.5h
- WP4: 30min
- WP5: 30min
- WP6: 45min
- WP7: 30min
- **Total**: ~4.5h ✓ in target

---

## Changelog

- **2026-06-05 v1**: initial plan post-discovery. Asse D follow-up P1 lockato via spec-panel. DEC-1..DEC-4 confirmed. 7 task TDD, ~4-6h.
