/**
 * Read-side type contract for session-live ScoringPanelRenderer (G5a, issue #2373).
 *
 * Sibling to the WRITE-side `ScoreChangePayload` discriminated union in
 * `@/components/sessions/score-strategies/types`. The renderer consumes this
 * shape from the orchestrator (`SessionLiveView`); the orchestrator derives it
 * from `useLiveSessionStore` + `LiveSessionFixture` (until the store-shape
 * migration tracked by a follow-up issue lands the per-variant payload).
 *
 * Dispatch table:
 *
 * | `data.scoringType` | Render component (read) | Render editor (write) |
 * |--------------------|-------------------------|------------------------|
 * | `Points`           | `PointsPanel`           | `PolymorphicScoreEditor scoringType='Points'` |
 * | `Ranking`          | `RankingPanel`          | `PolymorphicScoreEditor scoringType='Ranking'` (host only) |
 * | `BinaryWin`        | `BinaryWinPanel`        | `PolymorphicScoreEditor scoringType='BinaryWin'` (host only) |
 * | `Objectives`       | `ObjectivesPanel`       | `PolymorphicScoreEditor scoringType='Objectives'` (host only; `availableObjectives` required) |
 * | `null`             | `ScoringPanelEmpty`     | — |
 *
 * Plan: `docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md`
 *
 * @module components/features/session-live/scoring/types
 */

import type {
  BinaryWinScoreData,
  ObjectivesScoreData,
  PlayerOption,
  PointsScoreData,
  RankingScoreData,
  ScoreType,
} from '@/components/sessions/score-strategies/types';

/**
 * Read-side player view — mirrors the canonical mockup
 * `admin-mockups/design_files/sp4-session-skeleton-renderers.jsx` data.players shape.
 *
 * `displayName` is required (the orchestrator must derive it from `PlayerInfo.name`
 * until `useLiveSessionStore.PlayerInfo` ships a `displayName` field).
 *
 * `hue` drives the avatar gradient via a CSS custom property
 * (`style={{ '--avatar-hue': hue }}`) — the only place inline style is acceptable
 * under the token discipline (CLAUDE.md § Token Canonicalization).
 */
export interface ScoringPlayerView {
  readonly id: string;
  readonly displayName: string;
  /** Used by Points / Ranking variants. */
  readonly score?: number;
  /** Rank pill input for Ranking variant (1-indexed, sequential per backend invariant). */
  readonly rank?: number;
  /** Last-turn delta for Points variant; matches mockup `turnDelta`. */
  readonly turnDelta?: number;
  /** Subtitle line under the name (Ranking `sub`, e.g. tie-break score). */
  readonly sub?: string;
  /** Avatar hue (0–360) for the gradient swatch. */
  readonly hue?: number;
}

/**
 * Computation kind for category breakdown rows (mockup `comp` enum:
 * `Count | Sum | RankBased | Custom`).
 */
export type ScoringComputation = 'Count' | 'Sum' | 'RankBased' | 'Custom';

/**
 * Points variant (numeric score sum). Used by titles like Wingspan or Catan.
 */
export interface PointsPanelData {
  readonly scoringType: 'Points';
  readonly players: ReadonlyArray<ScoringPlayerView>;
  /** Optional category breakdown (Wingspan-style: birds/eggs/food/etc). */
  readonly categories?: ReadonlyArray<{
    readonly id: string;
    readonly label: string;
    readonly computation: ScoringComputation;
    readonly description?: string;
  }>;
  /** Per-category per-player values: `breakdown[playerId][categoryId] = number`. */
  readonly breakdown?: Readonly<Record<string, Readonly<Record<string, number>>>>;
  /**
   * Source-of-truth shape for the editor when host edits. Pass this through to
   * `PolymorphicScoreEditor.initialData` to seed the inputs.
   */
  readonly editorData?: PointsScoreData;
}

/**
 * Ranking variant (1st/2nd/3rd ordinal). Used by titles like Power Grid.
 *
 * `ranking` MUST be sorted ascending by `rank` (1 = best). The renderer does
 * not re-sort — invariant enforced by the orchestrator adapter.
 */
export interface RankingPanelData {
  readonly scoringType: 'Ranking';
  readonly meta?: string;
  readonly ranking: ReadonlyArray<ScoringPlayerView & { readonly rank: number }>;
  readonly editorData?: RankingScoreData;
}

/**
 * BinaryWin variant (collective win/lose toggle). Used by co-op titles like Pandemic.
 *
 * The renderer shows TWO meters (goal progress + fail progress) and a
 * categories list with `weight` badges:
 * - `weight > 0` → "vince" (win condition)
 * - `weight < 0` → "perde" (lose condition)
 * - `weight === 0` → "neutro"
 */
export interface BinaryWinPanelData {
  readonly scoringType: 'BinaryWin';
  readonly collective: {
    readonly goalLabel: string;
    readonly goalValue: number;
    readonly goalMax: number;
    readonly goalHint?: string;
    readonly failLabel: string;
    readonly failValue: number;
    readonly failMax: number;
    readonly failHint?: string;
  };
  readonly categories: ReadonlyArray<{
    readonly id: string;
    readonly label: string;
    readonly computation: ScoringComputation;
    /** `> 0` = win, `< 0` = lose, `0` = neutral. */
    readonly weight: number;
    readonly description?: string;
  }>;
  readonly editorData?: BinaryWinScoreData;
}

/**
 * Objectives variant (checklist of completed objectives). Used by titles like
 * Tikal or Concordia card scoring.
 */
export interface ObjectivesPanelData {
  readonly scoringType: 'Objectives';
  readonly meta?: string;
  readonly objectives: ReadonlyArray<{
    readonly id: string;
    readonly label: string;
    readonly done: boolean;
    /** Optional progress fraction (mockup `"2/3"` style). */
    readonly progress?: string;
  }>;
  readonly editorData?: ObjectivesScoreData;
}

/**
 * Top-level discriminated union consumed by `ScoringPanelRenderer`.
 *
 * The dispatcher uses an exhaustive `switch (data.scoringType)` so TypeScript
 * enforces a `never` fallback when a new variant is added.
 */
export type ScoringPanelData =
  | PointsPanelData
  | RankingPanelData
  | BinaryWinPanelData
  | ObjectivesPanelData;

/**
 * Documentation-grade dispatch labels. The switch is type-checked exhaustively
 * — these strings are NOT used at runtime for branching, only for debug attrs
 * (`data-score-type`) and Storybook story names.
 */
export const SCORING_VARIANT_LABELS: Readonly<Record<ScoreType, string>> = {
  Points: 'scoreType · Points',
  Ranking: 'scoreType · Ranking',
  BinaryWin: 'scoreType · BinaryWin',
  Objectives: 'scoreType · Objectives',
};

export type { ScoreType, PlayerOption };
