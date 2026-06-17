/**
 * `session-live/scoring` barrel — polymorphic scoring renderer (G5a #2375).
 *
 * The primitive lives in `ScoringPanelRenderer.tsx` (shipped via PR #2419).
 * This barrel exposes the public surface for orchestrator wiring (#2421).
 */

export { ScoringPanelRenderer } from '@/components/features/session-live/scoring/ScoringPanelRenderer';
export type {
  BinaryWinScoringData,
  ObjectivesScoringData,
  ObjectiveScoringItem,
  PointsScoringData,
  RankingScoringData,
  ScoringPanelData,
  ScoringPanelRendererLabels,
  ScoringPanelRendererProps,
  ScoringPlayerEntry,
} from '@/components/features/session-live/scoring/ScoringPanelRenderer';
