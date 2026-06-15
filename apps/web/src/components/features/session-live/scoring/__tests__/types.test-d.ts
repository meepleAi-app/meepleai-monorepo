/**
 * Compile-time tests for `ScoringPanelData` discriminated union exhaustiveness.
 *
 * These assertions FAIL at compile time (not at runtime) when a new ScoreType
 * variant is added without a matching `ScoringPanelData` member. The TypeScript
 * compiler enforces the contract via the `never` narrowing in the default arm.
 *
 * Run via `pnpm tsc --noEmit` or the project's `pnpm typecheck` script — Vitest
 * does NOT execute `.test-d.ts` files (extension is convention only).
 *
 * Issue #2373 — sub-issue G5a of epic #2354.
 */

import type { ScoreType } from '@/components/sessions/score-strategies/types';

import type {
  ScoringPanelData,
  PointsPanelData,
  RankingPanelData,
  BinaryWinPanelData,
  ObjectivesPanelData,
  ScoringPlayerView,
} from '../types';

// -----------------------------------------------------------------------------
// A1 — Exhaustive switch over ScoringPanelData. A new variant breaks compile.
// -----------------------------------------------------------------------------
function _exhaustiveSwitch(data: ScoringPanelData): string {
  switch (data.scoringType) {
    case 'Points':
      return data.players[0]?.displayName ?? '';
    case 'Ranking':
      return data.ranking[0]?.displayName ?? '';
    case 'BinaryWin':
      return data.collective.goalLabel;
    case 'Objectives':
      return data.objectives[0]?.label ?? '';
    default: {
      // If a new variant is added to `ScoringPanelData`, this assignment fails:
      //   Type 'XYZ' is not assignable to type 'never'.
      const _exhaustive: never = data;
      return _exhaustive;
    }
  }
}

// -----------------------------------------------------------------------------
// A2 — Discriminator narrowing.
// -----------------------------------------------------------------------------
function _narrowPoints(data: ScoringPanelData): PointsPanelData | undefined {
  if (data.scoringType === 'Points') {
    // `data` is narrowed to PointsPanelData — `data.players` is accessible.
    return { scoringType: 'Points', players: data.players };
  }
  return undefined;
}

// -----------------------------------------------------------------------------
// A3 — ScoringPlayerView contract: id + displayName required, score optional.
// -----------------------------------------------------------------------------
const _player: ScoringPlayerView = {
  id: 'p-1',
  displayName: 'Marco',
  score: 42,
  hue: 220,
};
void _player;

// -----------------------------------------------------------------------------
// A4 — RankingPanelData enforces `rank: number` on each ranking entry.
// -----------------------------------------------------------------------------
const _ranking: RankingPanelData = {
  scoringType: 'Ranking',
  ranking: [
    { id: 'p-1', displayName: 'Marco', rank: 1 },
    { id: 'p-2', displayName: 'Luca', rank: 2 },
  ],
};
void _ranking;

// -----------------------------------------------------------------------------
// A5 — BinaryWinPanelData requires the `collective` block.
// -----------------------------------------------------------------------------
const _binary: BinaryWinPanelData = {
  scoringType: 'BinaryWin',
  collective: {
    goalLabel: 'Cure trovate',
    goalValue: 2,
    goalMax: 4,
    failLabel: 'Focolai',
    failValue: 5,
    failMax: 8,
  },
  categories: [
    { id: 'cure', label: 'Cure', computation: 'Count', weight: 1 },
    { id: 'epidemics', label: 'Epidemie', computation: 'Count', weight: -1 },
  ],
};
void _binary;

// -----------------------------------------------------------------------------
// A6 — ObjectivesPanelData requires `done: boolean` on each objective.
// -----------------------------------------------------------------------------
const _objectives: ObjectivesPanelData = {
  scoringType: 'Objectives',
  objectives: [
    { id: 'o-1', label: 'Recluta 3 lavoratori', done: true },
    { id: 'o-2', label: 'Costruisci 2 edifici', done: false, progress: '1/2' },
  ],
};
void _objectives;

// -----------------------------------------------------------------------------
// A7 — Cross-reference: every ScoreType in the upstream enum has a matching
//      ScoringPanelData member (this is the inverse of A1 — enforces no orphan
//      ScoreType variants).
// -----------------------------------------------------------------------------
type _CoverageCheck = ScoringPanelData['scoringType'] extends ScoreType
  ? ScoreType extends ScoringPanelData['scoringType']
    ? true
    : 'ERROR: ScoreType variant is missing from ScoringPanelData'
  : 'ERROR: ScoringPanelData contains a variant not in ScoreType';

const _coverage: _CoverageCheck = true;
void _coverage;
