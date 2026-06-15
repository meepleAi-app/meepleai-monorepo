/**
 * Barrel for the session-live scoring renderer feature (G5a, issue #2373).
 *
 * Public surface stops at the dispatcher + the read-side variant panels +
 * the type contract. Internal implementation details (mockup mappers, debug
 * attribute helpers) MUST NOT be re-exported here — they stay co-located
 * with their consumer.
 *
 * Plan: `docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md`
 */

export type {
  ScoringPanelData,
  PointsPanelData,
  RankingPanelData,
  BinaryWinPanelData,
  ObjectivesPanelData,
  ScoringPlayerView,
  ScoringComputation,
} from './types';

export { SCORING_VARIANT_LABELS } from './types';

export {
  ScoringPanelRenderer,
  type ScoringPanelRendererLabels,
  type ScoringPanelRendererProps,
} from './ScoringPanelRenderer';
export {
  ScoringPanelEmpty,
  type ScoringPanelEmptyLabels,
  type ScoringPanelEmptyProps,
} from './ScoringPanelEmpty';

export { PointsPanel, type PointsPanelLabels, type PointsPanelProps } from './variants/PointsPanel';
export {
  RankingPanel,
  type RankingPanelLabels,
  type RankingPanelProps,
} from './variants/RankingPanel';
export {
  BinaryWinPanel,
  type BinaryWinPanelLabels,
  type BinaryWinPanelProps,
} from './variants/BinaryWinPanel';
export {
  ObjectivesPanel,
  type ObjectivesPanelLabels,
  type ObjectivesPanelProps,
} from './variants/ObjectivesPanel';
