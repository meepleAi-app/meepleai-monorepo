/**
 * Polymorphic scoring strategy types.
 *
 * Asse A backend `ScoreType` enum mirror, plus per-strategy data shape DTOs.
 * Source of truth: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Enums/ScoreType.cs`
 * Wire contract: `UpdateSessionScoresCommand` (asse A v2.1 SHIPPED in PR #1917).
 * Asse D follow-up P1 (#1899).
 *
 * @module components/sessions/score-strategies/types
 */

/**
 * Polymorphic scoring discriminator.
 *
 * Must stay aligned with the backend enum. Any new variant requires:
 * - a matching `IScoringStrategy` implementation server-side, and
 * - a dedicated editor component + dispatcher case here.
 */
export type ScoreType = 'Points' | 'BinaryWin' | 'Objectives' | 'Ranking';

/**
 * Standard numeric points per player. Higher wins.
 */
export interface PointsScoreData {
  scores: { playerId: string; points: number }[];
}

/**
 * Cooperative / single-winner / multi-winner flag per player.
 */
export interface BinaryWinScoreData {
  results: { playerId: string; isWinner: boolean }[];
}

/**
 * Objectives-style scoring (e.g. Tikal, Concordia card scoring).
 * Each player has a checklist of completed objective names.
 */
export interface ObjectivesScoreData {
  completedByPlayer: { playerId: string; objectives: string[] }[];
}

/**
 * Ranked finish (1st, 2nd, ...). Positions are 1-indexed and sequential.
 */
export interface RankingScoreData {
  positions: { playerId: string; position: number }[];
}

/**
 * Mapped lookup from `ScoreType` discriminator to the matching data shape.
 * Lets dispatchers narrow safely from a runtime `scoringType`.
 */
export type ScoreDataByType = {
  Points: PointsScoreData;
  BinaryWin: BinaryWinScoreData;
  Objectives: ObjectivesScoreData;
  Ranking: RankingScoreData;
};

/**
 * Minimal player shape consumed by every strategy editor.
 *
 * `id` MUST match the backend session participant identifier (see
 * `UpdateSessionScoresCommand` → strategy resolver). `displayName` is the
 * user-visible label; callers should already have applied guest fallback /
 * localization.
 */
export interface PlayerOption {
  id: string;
  displayName: string;
  avatar?: string;
}

/**
 * Generic strategy editor props shape. Kept as a stand-alone helper so
 * individual strategies can extend it (e.g. `ObjectivesEditor` adds
 * `availableObjectives`).
 */
export interface ScoreStrategyProps<T extends ScoreType> {
  players: readonly PlayerOption[];
  initialData?: ScoreDataByType[T];
  onChange: (data: ScoreDataByType[T]) => void;
  disabled?: boolean;
}
