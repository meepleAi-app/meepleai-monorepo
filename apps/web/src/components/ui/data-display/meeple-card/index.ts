/**
 * MeepleCard - Public API
 *
 * Re-exports the same public surface as the old monolithic meeple-card.tsx.
 * All existing imports (`from '../meeple-card'`) resolve here unchanged.
 *
 * @module components/ui/data-display/meeple-card
 */

// ============================================================================
// Component
// ============================================================================

export { MeepleCard } from './MeepleCard';

// ============================================================================
// Skeleton (from meeple-card-parts)
// ============================================================================

export { MeepleCardSkeleton } from '../meeple-card-parts';

// ============================================================================
// Style values
// ============================================================================

export { entityColors } from '../meeple-card-styles';

// ============================================================================
// Types — re-exported from types.ts (which aggregates all feature types)
// ============================================================================

// Core prop types
export type { MeepleCardProps, MeepleCardMetadata, MeepleCardAction } from './types';

// Aliased exports (backward compatibility)
export type { MeepleMetadata, MeepleAction } from './types';

// Style types
export type { MeepleEntityType, MeepleCardVariant } from './types';

// Feature types
export type { MeepleCardFlipData } from './types';
export type { AgentStatus } from './types';
export type { AgentStats } from './types';
export type { ModelParameters } from './types';
export type { ChatStatus } from './types';
export type { ChatAgent } from './types';
export type { ChatStats } from './types';
export type { ChatGame } from './types';
export type { DragData } from './types';
export type { TagConfig, TagPresetKey } from './types';
export type { KbIndexingStatus } from './types';
export type {
  SessionStatus,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
  SessionActionHandlers,
  SessionBackData,
  SessionTimelineEvent,
  SessionMediaCounts,
  PlayerColor,
  PlayerRole,
} from './types';
export type { GameBackData, GameBackActions } from './types';
export type { SnapshotInfo } from './types';
export type { QuickAction } from './types';
export type { EntityLinkType } from './types';
