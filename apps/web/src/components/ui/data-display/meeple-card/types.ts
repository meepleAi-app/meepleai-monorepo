/**
 * MeepleCard Type Definitions
 *
 * Centralizes all type exports for the MeepleCard component system.
 * Interfaces defined here were extracted from the monolithic meeple-card.tsx.
 *
 * @module components/ui/data-display/meeple-card/types
 */

import type React from 'react';

import type { ResolvedNavigationLink } from '@/config/entity-navigation';

import type { EntityLinkType } from '../entity-link/entity-link-types';
import type { SnapshotInfo } from '../extra-meeple-card/types';
import type { ModelParameters } from '../meeple-card-features/AgentModelInfo';
import type { AgentStats } from '../meeple-card-features/AgentStatsDisplay';
import type { AgentStatus } from '../meeple-card-features/AgentStatusBadge';
import type { ChatAgent } from '../meeple-card-features/ChatAgentInfo';
import type { ChatGame } from '../meeple-card-features/ChatGameContext';
import type { ChatStats } from '../meeple-card-features/ChatStatsDisplay';
import type { ChatStatus } from '../meeple-card-features/ChatStatusBadge';
import type { DocumentIndexingStatus } from '../meeple-card-features/DocumentStatusBadge';
import type { DragData } from '../meeple-card-features/DragHandle';
import type { MeepleCardFlipData } from '../meeple-card-features/FlipCard';
import type { GameBackData, GameBackActions } from '../meeple-card-features/GameBackContent';
import type { LinkedEntityInfo } from '../meeple-card-features/ManaLinkFooter';
import type { PrimaryAction } from '../meeple-card-features/PrimaryActions';
import type {
  SessionStatus,
  SessionPlayerInfo,
  SessionRoundScore,
  SessionScoringConfig,
  SessionTurnInfo,
  SessionActionHandlers,
  SessionBackData,
} from '../meeple-card-features/session-types';
import type { GlowState } from '../meeple-card-features/StatusGlow';
import type { TagConfig, TagPresetKey } from '../meeple-card-features/tag-presets';
import type { QuickAction } from '../meeple-card-quick-actions';
import type { meepleCardVariants } from '../meeple-card-styles';
import type { MeepleEntityType, MeepleCardVariant } from '../meeple-card-styles';
import type { VariantProps } from 'class-variance-authority';
import type { LucideIcon } from 'lucide-react';

// ============================================================================
// Cover Overlay Types (4-corner system)
// ============================================================================

/** Label displayed in the cover overlay top-left slot */
export interface CoverLabel {
  /** Label text */
  text: string;
  /** HSL color string (defaults to entity color) */
  color?: string;
  /** Primary label uses larger font (default false) */
  primary?: boolean;
}

/** Subtype icon displayed in the cover overlay bottom-left slot */
export interface SubtypeIcon {
  /** Icon content (emoji or React node) */
  icon: React.ReactNode;
  /** Tooltip text on hover */
  tooltip: string;
}

// ============================================================================
// Core Interfaces (defined in meeple-card.tsx)
// ============================================================================

/**
 * Metadata item for displaying card information
 */
export interface MeepleCardMetadata {
  /** Optional Lucide icon */
  icon?: LucideIcon;
  /** Display label/value */
  label?: string;
  /** Alternative: just a value (for icon + value pairs) */
  value?: string;
  /** Optional click handler (makes metadata chip interactive) */
  onClick?: () => void;
}

/**
 * Action button configuration
 */
export interface MeepleCardAction {
  /** Button label */
  label: string;
  /** Primary styling (filled vs outline) */
  primary?: boolean;
  /** Click handler */
  onClick?: () => void;
  /** Disabled state */
  disabled?: boolean;
}

/**
 * MeepleCard component props
 */
export interface MeepleCardProps extends VariantProps<typeof meepleCardVariants> {
  /** Unique entity ID (required for HoverPreview feature) */
  id?: string;
  /** Entity type determines color scheme */
  entity: MeepleEntityType;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Main title */
  title: string;
  /** Subtitle or secondary text */
  subtitle?: string;
  /** Cover image URL */
  imageUrl?: string;
  /** Avatar URL (for player entity, falls back to imageUrl) */
  avatarUrl?: string;
  /** Metadata items to display */
  metadata?: MeepleCardMetadata[];
  /** Action buttons (for featured/hero variants) */
  actions?: MeepleCardAction[];
  /** Rating value (0-5 or 0-10) */
  rating?: number;
  /** Max rating scale (default: 5) */
  ratingMax?: number;
  /** Badge text overlay */
  badge?: string;
  /** Custom entity color (HSL format, e.g., "262 83% 58%") */
  customColor?: string;
  /** Click handler for card */
  onClick?: () => void;
  /** Loading state */
  loading?: boolean;
  /** Additional CSS classes */
  className?: string;
  /** Test ID for testing */
  'data-testid'?: string;

  // ========== FEATURE EXTENSIONS (Issue #3820) ==========
  // All features opt-in (default disabled)

  /** Feature: Wishlist Button (#3824) */
  showWishlist?: boolean;
  isWishlisted?: boolean;
  onWishlistToggle?: (id: string, isWishlisted: boolean) => void;

  /** Feature: Quick Actions Menu (#3825) */
  quickActions?: Array<{
    icon: LucideIcon;
    label: string;
    onClick: () => void;
    disabled?: boolean;
    hidden?: boolean;
    adminOnly?: boolean;
    destructive?: boolean;
    separator?: boolean;
  }>;
  userRole?: 'user' | 'editor' | 'admin';

  /** Feature: Status Badge (#3826) */
  status?:
    | 'owned'
    | 'wishlisted'
    | 'played'
    | 'borrowed'
    | 'for-trade'
    | Array<'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade'>;
  showStatusIcon?: boolean;

  /** Feature: Hover Preview (#3827) */
  showPreview?: boolean;
  previewData?: {
    description?: string;
    designer?: string;
    complexity?: number;
    weight?: 'Light' | 'Medium' | 'Heavy';
    categories?: string[];
    mechanics?: string[];
  };
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  onFetchPreview?: (id: string) => Promise<any>;

  /** Feature: Drag & Drop (#3828) */
  draggable?: boolean;
  dragData?: { id: string; type: string; index: number };
  onDragStart?: (data: DragData) => void;
  onDragEnd?: (data: { from: number; to: number }) => void;

  /** Feature: Bulk Selection (#3829) */
  selectable?: boolean;
  selected?: boolean;
  onSelect?: (id: string, selected: boolean) => void;

  /** Feature: Flip Card (3D flip to show back content) */
  flippable?: boolean;
  flipData?: MeepleCardFlipData;
  isFlipped?: boolean;
  onFlip?: (flipped: boolean) => void;
  /** Flip trigger mode: 'card' = click anywhere, 'button' = dedicated button */
  flipTrigger?: 'card' | 'button';
  /** Link to entity detail page (shown on flip card back) */
  detailHref?: string;

  // ========== NEW FEATURES (Issue #4030) ==========

  /** Feature: Entity Quick Actions (hover-reveal buttons) */
  entityQuickActions?: QuickAction[];

  /** Feature: Info Button (always-visible navigation to detail page) */
  showInfoButton?: boolean;
  /**
   * Entity ID for drawer-based detail view (Issue #5025).
   * When present, the "i" button opens ExtraMeepleCardDrawer instead of navigating.
   * If absent, the button is not rendered (unless infoHref is set for backward compat).
   */
  entityId?: string;
  /** @deprecated Use entityId instead. Kept for backward compatibility. */
  infoHref?: string;
  infoTooltip?: string;

  // ========== FEATURE: VERTICAL TAG STRIP (Issue #4181) ==========

  /** Feature: Vertical Tag Strip (left-edge tag display) */
  tags?: (TagPresetKey | TagConfig)[];
  /** Max visible tags before overflow (default: 3) */
  maxVisibleTags?: number;
  /** Show tag strip (default: auto-detect from tags prop) */
  showTagStrip?: boolean;

  // ========== AGENT ENTITY FEATURES (Issue #4361) ==========

  /** Agent operational status (active/idle/training/error) */
  agentStatus?: AgentStatus;
  /** Agent AI model info (name + optional parameters) */
  agentModel?: { modelName: string; parameters?: ModelParameters };
  /** Agent invocation statistics */
  agentStats?: AgentStats;
  /** Agent capabilities list (e.g., ['rag', 'vision', 'code']) */
  capabilities?: string[];

  // ========== CHAT SESSION ENTITY FEATURES (Issue #4400) ==========

  /** Chat session status (active/waiting/archived/closed) */
  chatStatus?: ChatStatus;
  /** Chat agent info (name + model powering the chat) */
  chatAgent?: ChatAgent;
  /** Chat game context (game associated with chat) */
  chatGame?: ChatGame;
  /** Chat session statistics */
  chatStats?: ChatStats;
  /** Chat message preview */
  chatPreview?: { lastMessage: string; sender: 'user' | 'agent' };
  /** Unread message count */
  unreadCount?: number;

  // ========== NAVIGATION FOOTER (Epic #4688, Issue #4689) ==========

  /** @deprecated Use linkedEntities instead. Kept for backward compatibility. */
  navigateTo?: ResolvedNavigationLink[];

  // ========== MANA FEATURES ==========

  /** Mana-linked entities shown in link footer. When provided, replaces navigateTo. */
  linkedEntities?: LinkedEntityInfo[];
  /** Callback when a mana pip in the link footer is clicked */
  onManaPipClick?: (entityType: MeepleEntityType) => void;
  /** 1-2 primary actions shown on card front */
  primaryActions?: PrimaryAction[];
  /** Card glow state for status visualization */
  glowState?: GlowState;

  // ========== AGENT ACTION FOOTER (Issue #4777, #4999) ==========

  /** Whether this game entity already has an agent */
  hasAgent?: boolean;
  /** Agent ID for chat navigation (when hasAgent is true) */
  agentId?: string;
  /** Whether this game entity has a KB (at least one indexed document) */
  hasKb?: boolean;
  /** Callback to open agent creation wizard */
  onCreateAgent?: () => void;
  /** Callback to open add-to-collection wizard (replaces direct add) */
  onAddToCollection?: () => void;

  // ========== DOCUMENT / KB ENTITY FEATURES (Issue #5001) ==========

  /** KB document indexing status (drives DocumentStatusBadge) */
  documentStatus?: DocumentIndexingStatus;

  // ========== ENTITY LINKS (Issue #5129) ==========

  /** Total number of entity links — drives EntityLinkBadge corner overlay */
  linkCount?: number;
  /** Preview data for the first link — drives EntityLinkPreviewRow in card footer */
  firstLinkPreview?: { linkType: EntityLinkType; targetName: string };
  /** Called when user clicks the link badge or preview row */
  onLinksClick?: () => void;

  // ========== KB CARDS BADGE (Issue #5193) ==========

  /**
   * KB documents linked to this game entity.
   * Shows a worst-status badge (failed > processing > indexed > none) + count.
   * Only rendered when entity='game' and kbCards.length > 0.
   */
  kbCards?: {
    status: DocumentIndexingStatus;
  }[];

  // ========== COVER OVERLAY 4-CORNER SYSTEM ==========

  /** Top-left label stack in cover overlay */
  coverLabels?: CoverLabel[];
  /** Bottom-left subtype classification icons in cover overlay */
  subtypeIcons?: SubtypeIcon[];

  /**
   * @deprecated Use subtypeIcons instead. Kept for backward compatibility.
   * Maps to subtypeIcons[0] internally.
   */
  mechanicIcon?: React.ReactNode;

  /** Bottom-right state badge in cover overlay */
  stateLabel?: {
    text: string;
    variant: 'success' | 'warning' | 'error' | 'info';
  };

  // ========== SESSION ENTITY FEATURES (Issue #4751) ==========

  /** Session lifecycle status */
  sessionStatus?: SessionStatus;
  /** Players in the session */
  sessionPlayers?: SessionPlayerInfo[];
  /** Round score entries */
  sessionRoundScores?: SessionRoundScore[];
  /** Scoring configuration (dimensions, units) */
  sessionScoringConfig?: SessionScoringConfig;
  /** Current turn info */
  sessionTurn?: SessionTurnInfo;
  /** Context-sensitive action handlers */
  sessionActions?: SessionActionHandlers;
  /** Is current user the session host */
  isSessionHost?: boolean;
  /** Navigate to previous/next turn callbacks */
  onPrevTurn?: () => void;
  onNextTurn?: () => void;

  // ========== SESSION BACK CONTENT (Issue #4752) ==========

  /** Session back content data (statistics, ranking, timeline) */
  sessionBackData?: SessionBackData;

  // ========== GAME BACK CONTENT ==========

  /** Game back content data (stats, KB preview, quick actions) */
  gameBackData?: GameBackData;
  /** Game back content action handlers */
  gameBackActions?: GameBackActions;

  // ========== SNAPSHOT HISTORY + TIME TRAVEL (Issue #4758) ==========

  /** Session snapshots for history slider */
  sessionSnapshots?: SnapshotInfo[];
  /** Currently selected snapshot index (0-based) */
  currentSnapshotIndex?: number;
  /** Callback when a snapshot is selected */
  onSnapshotSelect?: (index: number) => void;
  /** Whether time travel mode is active */
  isTimeTravelMode?: boolean;
  /** Toggle time travel mode */
  onTimeTravelToggle?: (enabled: boolean) => void;
}

// ============================================================================
// Re-exports from feature modules
// ============================================================================

// Styles
export type { MeepleEntityType, MeepleCardVariant } from '../meeple-card-styles';

// Feature types
export type { MeepleCardFlipData } from '../meeple-card-features/FlipCard';
export type { AgentStatus } from '../meeple-card-features/AgentStatusBadge';
export type { AgentStats } from '../meeple-card-features/AgentStatsDisplay';
export type { ModelParameters } from '../meeple-card-features/AgentModelInfo';
export type { ChatStatus } from '../meeple-card-features/ChatStatusBadge';
export type { ChatAgent } from '../meeple-card-features/ChatAgentInfo';
export type { ChatStats } from '../meeple-card-features/ChatStatsDisplay';
export type { ChatGame } from '../meeple-card-features/ChatGameContext';
export type { DragData } from '../meeple-card-features/DragHandle';
export type { TagConfig, TagPresetKey } from '../meeple-card-features/tag-presets';
export type { DocumentIndexingStatus } from '../meeple-card-features/DocumentStatusBadge';
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
} from '../meeple-card-features/session-types';
export type { GameBackData, GameBackActions } from '../meeple-card-features/GameBackContent';
export type { SnapshotInfo } from '../extra-meeple-card/types';
export type { QuickAction } from '../meeple-card-quick-actions';
export type { EntityLinkType } from '../entity-link/entity-link-types';
export type { LinkedEntityInfo } from '../meeple-card-features/ManaLinkFooter';
export type { PrimaryAction } from '../meeple-card-features/PrimaryActions';
export type { GlowState } from '../meeple-card-features/StatusGlow';

// Aliased exports (backward compatibility with monolith)
export type { MeepleCardMetadata as MeepleMetadata };
export type { MeepleCardAction as MeepleAction };
