/**
 * Co-located deterministic fixtures for the 4 `ScoringPanelData` variants.
 *
 * Consumed by:
 *   - `ScoringPanelRenderer.stories.tsx` (Storybook visual baselines)
 *   - unit tests (parametrize across variants)
 *   - the orchestrator (T7) when it bootstraps the renderer from
 *     `LiveSessionFixture` + a derived `scoringType` (default 'Points').
 *
 * Player IDs intentionally mirror the parent `VISUAL_TEST_FIXTURE_SESSION`
 * roster (`/lib/session-live/session-live-visual-test-fixture.ts`) so a
 * stitched Storybook scenario (LiveSessionFixture + ScoringPanelData) lines
 * up the same Marco/Anna/Luca/Sara names across both surfaces.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T8).
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T8
 *
 * @module components/features/session-live/scoring/__fixtures__/scoring-panel-data.fixture
 */

import type {
  BinaryWinPanelData,
  ObjectivesPanelData,
  PointsPanelData,
  RankingPanelData,
  ScoringPanelData,
} from '../types';

/** Sentinel IDs aligned with `VISUAL_TEST_FIXTURE_SESSION.players[]`. */
const PLAYER_MARCO = '00000000-0000-4000-8000-0000000000a1';
const PLAYER_ANNA = '00000000-0000-4000-8000-0000000000a2';
const PLAYER_LUCA = '00000000-0000-4000-8000-0000000000a3';
const PLAYER_SARA = '00000000-0000-4000-8000-0000000000a4';

// ---------------------------------------------------------------------------
// Points variant — Wingspan-style leaderboard with categories breakdown.
// ---------------------------------------------------------------------------

export const POINTS_PANEL_FIXTURE: PointsPanelData = {
  scoringType: 'Points',
  players: [
    { id: PLAYER_LUCA, displayName: 'Luca', score: 42, turnDelta: 7, hue: 200 },
    { id: PLAYER_MARCO, displayName: 'Marco', score: 35, turnDelta: 3, hue: 30 },
    { id: PLAYER_ANNA, displayName: 'Anna', score: 28, hue: 280 },
    { id: PLAYER_SARA, displayName: 'Sara', score: 18, hue: 340 },
  ],
  categories: [
    {
      id: 'birds',
      label: 'Uccelli',
      computation: 'Count',
      description: 'Numero di carte uccello giocate',
    },
    {
      id: 'eggs',
      label: 'Uova',
      computation: 'Sum',
      description: 'Somma uova deposte',
    },
    {
      id: 'food',
      label: 'Cibo',
      computation: 'Sum',
      description: 'Cibo accumulato sui tasselli',
    },
    {
      id: 'bonus',
      label: 'Bonus',
      computation: 'Custom',
      description: 'Carte bonus rivelate a fine partita',
    },
  ],
  breakdown: {
    [PLAYER_LUCA]: { birds: 12, eggs: 8, food: 14, bonus: 8 },
    [PLAYER_MARCO]: { birds: 10, eggs: 7, food: 11, bonus: 7 },
    [PLAYER_ANNA]: { birds: 8, eggs: 6, food: 9, bonus: 5 },
    [PLAYER_SARA]: { birds: 5, eggs: 4, food: 6, bonus: 3 },
  },
};

// ---------------------------------------------------------------------------
// Ranking variant — Power Grid-style ordinal finish.
// ---------------------------------------------------------------------------

export const RANKING_PANEL_FIXTURE: RankingPanelData = {
  scoringType: 'Ranking',
  meta: 'Posizioni finali',
  ranking: [
    { id: PLAYER_LUCA, displayName: 'Luca', rank: 1, sub: '42 punti · 17 città' },
    { id: PLAYER_MARCO, displayName: 'Marco', rank: 2, sub: '38 punti · 16 città' },
    { id: PLAYER_ANNA, displayName: 'Anna', rank: 3, sub: '32 punti · 15 città' },
    { id: PLAYER_SARA, displayName: 'Sara', rank: 4, sub: '21 punti · 12 città' },
  ],
};

// ---------------------------------------------------------------------------
// BinaryWin variant — Pandemic-style collective outcome.
// ---------------------------------------------------------------------------

export const BINARY_WIN_PANEL_FIXTURE: BinaryWinPanelData = {
  scoringType: 'BinaryWin',
  collective: {
    goalLabel: 'Cure trovate',
    goalValue: 2,
    goalMax: 4,
    goalHint: 'Servono 4 cure per vincere la partita',
    failLabel: 'Focolai',
    failValue: 5,
    failMax: 8,
    failHint: '8 focolai → sconfitta collettiva',
  },
  categories: [
    {
      id: 'cures',
      label: 'Cure',
      computation: 'Count',
      weight: 1,
      description: 'Ogni cura trovata fa avanzare verso la vittoria',
    },
    {
      id: 'epidemics',
      label: 'Epidemie',
      computation: 'Count',
      weight: -1,
      description: 'Le epidemie peggiorano il rischio focolai',
    },
    {
      id: 'researchers',
      label: 'Ricercatori dispiegati',
      computation: 'Sum',
      weight: 0,
      description: 'Stato neutrale — utile ma non vincolante',
    },
  ],
};

// ---------------------------------------------------------------------------
// Objectives variant — Tikal-style checklist.
// ---------------------------------------------------------------------------

export const OBJECTIVES_PANEL_FIXTURE: ObjectivesPanelData = {
  scoringType: 'Objectives',
  meta: 'Round 2 di 4',
  objectives: [
    { id: 'o-1', label: 'Recluta 3 esploratori', done: true },
    { id: 'o-2', label: 'Costruisci 2 accampamenti', done: false, progress: '1/2' },
    { id: 'o-3', label: 'Acquista 5 pezzi del tesoro', done: false, progress: '3/5' },
    { id: 'o-4', label: 'Disvela 4 mappe', done: true },
    { id: 'o-5', label: 'Raggiungi il livello 8 di reputazione', done: false },
  ],
};

// ---------------------------------------------------------------------------
// Lookup helpers
// ---------------------------------------------------------------------------

const FIXTURE_BY_TYPE: Readonly<Record<ScoringPanelData['scoringType'], ScoringPanelData>> = {
  Points: POINTS_PANEL_FIXTURE,
  Ranking: RANKING_PANEL_FIXTURE,
  BinaryWin: BINARY_WIN_PANEL_FIXTURE,
  Objectives: OBJECTIVES_PANEL_FIXTURE,
};

/**
 * Returns the canonical fixture for a given `ScoreType` discriminator.
 *
 * Useful for Storybook arg matrices + unit-test parametrization.
 * Throws at compile time if a new variant is added without a fixture (the
 * `Record` type forbids the new key from being missing).
 */
export function getScoringPanelFixture(
  scoringType: ScoringPanelData['scoringType']
): ScoringPanelData {
  return FIXTURE_BY_TYPE[scoringType];
}

/** Convenience: ordered list of all 4 fixtures (for Storybook Frame stories). */
export const SCORING_PANEL_FIXTURES: ReadonlyArray<ScoringPanelData> = [
  POINTS_PANEL_FIXTURE,
  RANKING_PANEL_FIXTURE,
  BINARY_WIN_PANEL_FIXTURE,
  OBJECTIVES_PANEL_FIXTURE,
];
