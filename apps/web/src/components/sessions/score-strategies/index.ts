/**
 * Barrel re-exports for the polymorphic scoring strategy primitives.
 *
 * Asse D follow-up P1 (#1899) T4.
 */

export { BinaryWinEditor } from './BinaryWinEditor';
export type { BinaryWinEditorProps } from './BinaryWinEditor';

export { ObjectivesEditor } from './ObjectivesEditor';
export type { ObjectivesEditorProps } from './ObjectivesEditor';

export { PointsEditor } from './PointsEditor';
export type { PointsEditorProps } from './PointsEditor';

export { RankingEditor } from './RankingEditor';
export type { RankingEditorProps } from './RankingEditor';

export type {
  BinaryWinScoreData,
  ObjectivesScoreData,
  PlayerOption,
  PointsScoreData,
  RankingScoreData,
  ScoreDataByType,
  ScoreStrategyProps,
  ScoreType,
} from './types';
