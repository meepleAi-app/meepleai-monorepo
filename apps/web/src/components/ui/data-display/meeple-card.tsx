'use client';

/**
 * MeepleCard - Universal Card Component System
 *
 * A polymorphic card component supporting multiple entity types and layout variants.
 * Combines structured information display (Direction A) with canvas-style visuals
 * (Direction C) for a distinctive MeepleAI aesthetic.
 *
 * @module components/ui/data-display/meeple-card
 * @see Issue #3326 - Core MeepleCard Component Implementation
 *
 * Features:
 * - Entity types: game, player, collection, event, custom
 * - Layout variants: grid, list, compact, featured, hero
 * - Color-coded entity indicators (left border + badge)
 * - Glass morphism with canvas-style cover overlay
 * - Full accessibility support (WCAG AA)
 * - React.memo optimized for performance
 *
 * @example
 * ```tsx
 * // Game card (grid layout)
 * <MeepleCard
 *   entity="game"
 *   variant="grid"
 *   title="Twilight Imperium"
 *   subtitle="Fantasy Flight Games"
 *   imageUrl="/games/ti4.jpg"
 *   metadata={[
 *     { icon: Users, value: "3-6" },
 *     { icon: Clock, value: "4-8h" },
 *   ]}
 * />
 *
 * // Player card (list layout)
 * <MeepleCard
 *   entity="player"
 *   variant="list"
 *   title="Marco Rossi"
 *   subtitle="@marco_games"
 *   avatarUrl="/avatars/marco.jpg"
 *   metadata={[{ label: "142 plays" }, { label: "Top 5%" }]}
 * />
 *
 * // Featured event (hero layout)
 * <MeepleCard
 *   entity="event"
 *   variant="hero"
 *   title="MeepleAI Tournament"
 *   subtitle="Grand Finals"
 *   imageUrl="/events/tournament.jpg"
 *   actions={[{ label: "Join", primary: true }]}
 * />
 * ```
 */

import React, { useState, useEffect, useRef } from 'react';

import { type VariantProps } from 'class-variance-authority';
import { Info } from 'lucide-react';

import { cn } from '@/lib/utils';

import { EntityLinkBadge } from './entity-link/entity-link-badge';
import { EntityLinkPreviewRow } from './entity-link/entity-link-preview-row';
import { ExtraMeepleCardDrawer } from './extra-meeple-card/ExtraMeepleCardDrawer';
import { AgentModelInfo, type ModelParameters } from './meeple-card-features/AgentModelInfo';
import { AgentStatsDisplay, type AgentStats } from './meeple-card-features/AgentStatsDisplay';
import { AgentStatusBadge, type AgentStatus } from './meeple-card-features/AgentStatusBadge';
import { BulkSelectCheckbox } from './meeple-card-features/BulkSelectCheckbox';
import { CardAgentAction } from './meeple-card-features/CardAgentAction';
import { CardNavigationFooter } from './meeple-card-features/CardNavigationFooter';
import { ChatAgentInfo, type ChatAgent } from './meeple-card-features/ChatAgentInfo';
import { ChatGameContext, type ChatGame } from './meeple-card-features/ChatGameContext';
import { ChatStatsDisplay, type ChatStats } from './meeple-card-features/ChatStatsDisplay';
import { ChatStatusBadge, type ChatStatus } from './meeple-card-features/ChatStatusBadge';
import { ChatUnreadBadge } from './meeple-card-features/ChatUnreadBadge';
import { DocumentStatusBadge } from './meeple-card-features/DocumentStatusBadge';
import { DragHandle, type DragData } from './meeple-card-features/DragHandle';
import { FlipCard, type MeepleCardFlipData } from './meeple-card-features/FlipCard';
import { GameBackContent } from './meeple-card-features/GameBackContent';
import { HoverPreview } from './meeple-card-features/HoverPreview';
import { QuickActionsMenu } from './meeple-card-features/QuickActionsMenu';
// Issue #4689: Navigation footer
// Issue #4777: Agent action footer
// Issue #4751: Session-specific display components
import { SessionActionButtons } from './meeple-card-features/SessionActionButtons';
import { SessionBackContent } from './meeple-card-features/SessionBackContent';
import { SessionScoreTable } from './meeple-card-features/SessionScoreTable';
import { SessionStatusBadge } from './meeple-card-features/SessionStatusBadge';
import { SessionTurnSequence } from './meeple-card-features/SessionTurnSequence';
// Issue #4758: Snapshot History Slider + Time Travel
import { SnapshotHistorySlider } from './meeple-card-features/SnapshotHistorySlider';
import { StatusBadge } from './meeple-card-features/StatusBadge';
import { type TagConfig, type TagPresetKey } from './meeple-card-features/tag-presets';
import { TagStrip } from './meeple-card-features/TagStrip';
import { TimeTravelOverlay } from './meeple-card-features/TimeTravelOverlay';
import { WishlistButton } from './meeple-card-features/WishlistButton';
// Issue #4030: New action components
import { MeepleCardInfoButton } from './meeple-card-info-button';
import {
  EntityIndicator,
  VerticalTagStack,
  CoverImage,
  MetadataChips,
  ActionButtons,
  RatingDisplay,
  MeepleCardSkeleton,
} from './meeple-card-parts';
import { MeepleCardQuickActions } from './meeple-card-quick-actions';
import {
  entityColors,
  DRAWER_ENTITY_TYPE_MAP,
  meepleCardVariants,
  contentVariants,
} from './meeple-card-styles';

// Issue #5025: Drawer integration
import type { EntityLinkType } from './entity-link/entity-link-types';
import type { MeepleEntityType, MeepleCardVariant } from './meeple-card-styles';

// Issue #5129: EntityLink components

// Issue #4361: Agent-specific display components
// Issue #4400: ChatSession-specific display components

import type { LucideIcon } from 'lucide-react';

// Feature components (Issue #3820)

// ============================================================================
// Types & Interfaces
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
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    icon: any;
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
  entityQuickActions?: import('./meeple-card-quick-actions').QuickAction[];

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

  /** Navigation links to related entities (rendered as icon footer) */
  navigateTo?: import('@/config/entity-navigation').ResolvedNavigationLink[];

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
  documentStatus?: import('./meeple-card-features/DocumentStatusBadge').DocumentIndexingStatus;

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
    status: import('./meeple-card-features/DocumentStatusBadge').DocumentIndexingStatus;
  }[];

  // ========== SESSION ENTITY FEATURES (Issue #4751) ==========

  /** Session lifecycle status */
  sessionStatus?: import('./meeple-card-features/session-types').SessionStatus;
  /** Players in the session */
  sessionPlayers?: import('./meeple-card-features/session-types').SessionPlayerInfo[];
  /** Round score entries */
  sessionRoundScores?: import('./meeple-card-features/session-types').SessionRoundScore[];
  /** Scoring configuration (dimensions, units) */
  sessionScoringConfig?: import('./meeple-card-features/session-types').SessionScoringConfig;
  /** Current turn info */
  sessionTurn?: import('./meeple-card-features/session-types').SessionTurnInfo;
  /** Context-sensitive action handlers */
  sessionActions?: import('./meeple-card-features/session-types').SessionActionHandlers;
  /** Is current user the session host */
  isSessionHost?: boolean;
  /** Navigate to previous/next turn callbacks */
  onPrevTurn?: () => void;
  onNextTurn?: () => void;

  // ========== SESSION BACK CONTENT (Issue #4752) ==========

  /** Session back content data (statistics, ranking, timeline) */
  sessionBackData?: import('./meeple-card-features/session-types').SessionBackData;

  // ========== GAME BACK CONTENT ==========

  /** Game back content data (stats, KB preview, quick actions) */
  gameBackData?: import('./meeple-card-features/GameBackContent').GameBackData;
  /** Game back content action handlers */
  gameBackActions?: import('./meeple-card-features/GameBackContent').GameBackActions;

  // ========== SNAPSHOT HISTORY + TIME TRAVEL (Issue #4758) ==========

  /** Session snapshots for history slider */
  sessionSnapshots?: import('./extra-meeple-card/types').SnapshotInfo[];
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
// Main Component
// ============================================================================

/**
 * MeepleCard - Universal card component for MeepleAI entities
 *
 * Renders a polymorphic card supporting games, players, collections, events,
 * and custom entity types with multiple layout variants.
 *
 * Performance: React.memo wrapper prevents unnecessary re-renders in grid/list views.
 * Hover transforms handled via CSS (globals.css [data-variant] selectors).
 */
export const MeepleCard = React.memo(function MeepleCard({
  id,
  entity,
  variant = 'grid',
  title,
  subtitle,
  imageUrl,
  avatarUrl,
  metadata = [],
  actions = [],
  rating,
  ratingMax = 5,
  badge,
  customColor,
  onClick,
  loading = false,
  className,
  'data-testid': testId,
  // Feature props (Issue #3820)
  showWishlist,
  isWishlisted,
  onWishlistToggle,
  quickActions,
  userRole,
  status,
  showStatusIcon,
  showPreview,
  previewData,
  onFetchPreview,
  draggable,
  dragData,
  onDragStart,
  onDragEnd,
  selectable,
  selected,
  onSelect,
  // Flip feature props
  flippable,
  flipData,
  isFlipped,
  onFlip,
  flipTrigger,
  detailHref,
  // Issue #4030: New action props
  entityQuickActions,
  showInfoButton,
  entityId,
  infoHref,
  infoTooltip,
  // Issue #4181: Vertical Tag Strip
  tags,
  maxVisibleTags = 3,
  showTagStrip,
  // Issue #4361: Agent entity features
  agentStatus,
  agentModel,
  agentStats,
  capabilities,
  // Issue #4400: ChatSession entity features
  chatStatus,
  chatAgent,
  chatGame,
  chatStats,
  chatPreview,
  unreadCount,
  navigateTo,
  // Issue #4777: Agent action footer
  hasAgent,
  agentId,
  onCreateAgent,
  // Issue #4751: Session entity features
  sessionStatus,
  sessionPlayers,
  sessionRoundScores,
  sessionScoringConfig: _sessionScoringConfig,
  sessionTurn,
  sessionActions,
  isSessionHost,
  onPrevTurn,
  onNextTurn,
  sessionBackData,
  gameBackData,
  gameBackActions,
  sessionSnapshots,
  currentSnapshotIndex,
  onSnapshotSelect,
  isTimeTravelMode,
  onTimeTravelToggle,
  // Issue #5001: Document / KB entity features
  documentStatus,
  // Issue #5129: EntityLink features
  linkCount,
  firstLinkPreview,
  onLinksClick,
  // Issue #5193: KB Cards worst-status badge
  kbCards,
}: MeepleCardProps) {
  const coverSrc = entity === 'player' ? avatarUrl || imageUrl : imageUrl;
  const showActions = actions.length > 0 && (variant === 'featured' || variant === 'hero');
  const isHeroOrFeatured = variant === 'hero' || variant === 'featured';

  // Determine if card should be interactive
  // Avoid nested-interactive: don't make card clickable when it has action buttons
  const isInteractive = !!onClick && !showActions;

  // Feature visibility logic (Issue #3820)
  // eslint-disable-next-line security/detect-object-injection
  const color = customColor || entityColors[entity].hsl;
  const hasQuickActions = quickActions && quickActions.length > 0;
  const showWishlistBtn = showWishlist && !hasQuickActions; // Priority: quickActions > wishlist

  // KB cards worst-status (Issue #5193): failed > processing > indexed > none
  const worstKbStatus =
    kbCards && kbCards.length > 0
      ? kbCards.some(k => k.status === 'failed')
        ? ('failed' as const)
        : kbCards.some(k => k.status === 'processing')
          ? ('processing' as const)
          : kbCards.some(k => k.status === 'indexed')
            ? ('indexed' as const)
            : ('none' as const)
      : null;

  // Drawer state (Issue #5025)
  const [drawerOpen, setDrawerOpen] = useState(false);

  // eslint-disable-next-line security/detect-object-injection
  const drawerEntityType = DRAWER_ENTITY_TYPE_MAP[entity];

  // Mobile touch UX: Bottom sheet for actions (Issue #4604)
  const [showMobileActions, setShowMobileActions] = useState(false);
  const [isMobile, setIsMobile] = useState(false);
  const cardRef = useRef<HTMLDivElement | HTMLElement>(null);
  const hasMobileActions =
    isMobile &&
    (hasQuickActions ||
      entityQuickActions ||
      showWishlistBtn ||
      (showInfoButton && (entityId || infoHref)));

  // Detect mobile/tablet viewport
  useEffect(() => {
    const mediaQuery = window.matchMedia('(max-width: 768px)');
    setIsMobile(mediaQuery.matches);

    const handler = (e: MediaQueryListEvent) => setIsMobile(e.matches);
    mediaQuery.addEventListener('change', handler);
    return () => mediaQuery.removeEventListener('change', handler);
  }, []);

  // Dismiss mobile actions on outside click
  useEffect(() => {
    if (!showMobileActions) return;

    const handleClickOutside = (event: MouseEvent) => {
      if (cardRef.current && !cardRef.current.contains(event.target as Node)) {
        setShowMobileActions(false);
      }
    };

    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [showMobileActions]);

  // Mobile tap handler: 1st tap = show actions, 2nd tap = navigate (unless flippable)
  const handleMobileClick = () => {
    // If card is flippable, don't handle tap (let FlipCard handle it)
    if (flippable) return;

    if (hasMobileActions && !showMobileActions) {
      // 1st tap: Show bottom sheet
      setShowMobileActions(true);
    } else if (onClick) {
      // 2nd tap OR no actions: Navigate
      setShowMobileActions(false);
      onClick();
    }
  };

  // Desktop click handler: Navigate immediately (unless flippable)
  const handleDesktopClick = () => {
    // If card is flippable, don't handle tap (let FlipCard handle it)
    if (flippable) return;
    if (onClick) onClick();
  };

  // Unified click handler
  const handleCardClick = isMobile
    ? handleMobileClick
    : isInteractive
      ? handleDesktopClick
      : undefined;

  if (loading) {
    return <MeepleCardSkeleton variant={variant} />;
  }

  // Use div for interactive cards (article cannot have role="button")
  // Use article for semantic non-interactive cards
  const Component = isInteractive ? 'div' : 'article';

  // Card content JSX
  const cardContent = (
    <Component
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      ref={cardRef as React.Ref<any>} // Polymorphic component requires any
      className={cn(
        meepleCardVariants({ variant }),
        // v2: Entity glow outline on hover (Issue #4604)
        // outline-2 with entity color at 40% opacity creates subtle colored halo
        variant !== 'compact' && 'hover:outline-2 hover:outline-offset-2',
        // v2: Hover lift effect (using @media to ensure it works)
        variant === 'grid' &&
          '[&:hover]:[-webkit-transform:translateY(-6px)] [&:hover]:[transform:translateY(-6px)]',
        variant === 'featured' &&
          '[&:hover]:[-webkit-transform:translateY(-6px)] [&:hover]:[transform:translateY(-6px)]',
        selected && 'ring-2 ring-offset-2 bg-accent/10',
        selected && `ring-[hsl(${color})]`,
        className
      )}
      style={
        {
          // v2: Entity-colored outline for hover glow effect + hover transform
          '--mc-entity-color': `hsl(${color})`,
          outlineColor: `hsla(${color}, 0.4)`,
          willChange: 'transform, box-shadow, outline',
          // v2: Transform handled via CSS (globals.css [data-variant]:hover selectors)
          viewTransitionName: entityId ? `meeple-card-${entityId}` : undefined,
        } as React.CSSProperties
      }
      onClick={handleCardClick}
      role={isInteractive || hasMobileActions ? 'button' : undefined}
      tabIndex={isInteractive || hasMobileActions ? 0 : undefined}
      onKeyDown={
        isInteractive
          ? e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick?.();
              }
            }
          : undefined
      }
      // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
      aria-label={`${entityColors[entity].name}: ${title}`}
      data-testid={testId || 'meeple-card'}
      data-entity={entity}
      data-variant={variant}
    >
      {/* Feature: Bulk Selection Checkbox (highest z-index) */}
      {selectable && (
        <BulkSelectCheckbox
          selectable={selectable}
          selected={!!selected}
          onSelect={onSelect || (() => {})}
          id={testId || 'card'}
          entityColor={color}
        />
      )}

      {/* Time Travel Overlay (Issue #4758) */}
      {entity === 'session' &&
        isTimeTravelMode &&
        sessionSnapshots &&
        currentSnapshotIndex != null &&
        sessionSnapshots[currentSnapshotIndex] && (
          <TimeTravelOverlay
            snapshot={sessionSnapshots[currentSnapshotIndex]}
            totalSnapshots={sessionSnapshots.length}
            isActive={isTimeTravelMode}
            onExit={() => onTimeTravelToggle?.(false)}
          />
        )}

      {/* Entity indicator (left border for grid/featured, ribbon for hero, dot for list/compact) */}
      <EntityIndicator entity={entity} variant={variant} customColor={customColor} />

      {/* Feature: Vertical Tag Strip (Issue #4181) - left-edge tag display */}
      {(showTagStrip || (tags && tags.length > 0)) && (
        <TagStrip
          tags={tags || []}
          maxVisible={maxVisibleTags}
          variant={
            variant === 'grid' || variant === 'featured'
              ? 'desktop'
              : variant === 'list'
                ? 'tablet'
                : 'mobile'
          }
        />
      )}

      {/* Vertical tag stack: entity badge + status + custom badge (grid/featured only) */}
      {(variant === 'grid' || variant === 'featured') && (
        <VerticalTagStack
          entity={entity}
          customColor={customColor}
          status={status}
          showStatusIcon={showStatusIcon}
          badge={badge}
        />
      )}

      {/* EntityLink badge — top-right corner (Issue #5129) */}
      {linkCount !== undefined &&
        linkCount > 0 &&
        (variant === 'grid' || variant === 'featured') && (
          <EntityLinkBadge count={linkCount} onClick={onLinksClick} />
        )}

      {/* Status Badge for non-grid/non-featured/non-list variants */}
      {status && variant !== 'grid' && variant !== 'featured' && variant !== 'list' && (
        <StatusBadge
          status={status}
          showIcon={showStatusIcon}
          className="absolute top-12 left-4 z-11"
        />
      )}

      {/* Feature: Drag Handle (list variant, left side) */}
      {draggable && variant === 'list' && dragData && (
        <DragHandle
          draggable={draggable}
          dragData={dragData as DragData}
          onDragStart={onDragStart}
          onDragEnd={onDragEnd}
          className="mr-2"
        />
      )}

      {/* Cover image (not for compact variant) */}
      {variant !== 'compact' && (
        <CoverImage
          src={coverSrc}
          alt={title}
          variant={variant}
          entity={entity}
          customColor={customColor}
        />
      )}

      {/* Content area */}
      <div className={contentVariants({ variant })}>
        {/* Feature: Top-right actions row (Issue #4030: QuickActions + InfoButton) */}
        {/* Desktop only: Show on hover. Mobile: Hidden (bottom sheet instead) */}
        {(entityQuickActions ||
          (showInfoButton && (entityId || infoHref)) ||
          showWishlistBtn ||
          hasQuickActions) && (
          <div
            className={cn(
              'absolute right-2.5 flex items-center gap-1.5 z-15',
              // Push actions below unread badge row for chatSession
              entity === 'chatSession' && unreadCount && unreadCount > 0 ? 'top-10' : 'top-2.5',
              // Hide on mobile (bottom sheet instead); desktop: opacity reveal on hover
              'hidden md:flex md:opacity-0 md:group-hover:opacity-100 md:transition-opacity md:duration-300'
            )}
          >
            {/* New entity quick actions (Issue #4030) */}
            {entityQuickActions && entityQuickActions.length > 0 && (
              <MeepleCardQuickActions
                actions={entityQuickActions}
                entityType={entity}
                customColor={customColor}
                size="sm"
              />
            )}

            {/* Legacy quick actions menu (Issue #3825) */}
            {hasQuickActions && !entityQuickActions && (
              <QuickActionsMenu actions={quickActions} userRole={userRole} size="sm" />
            )}

            {/* Wishlist button (if no entity quick actions) */}
            {showWishlistBtn && !entityQuickActions && onWishlistToggle && (
              <WishlistButton
                gameId={testId || 'card'}
                isWishlisted={!!isWishlisted}
                onToggle={onWishlistToggle}
                size="sm"
              />
            )}

            {/* Info button (Issue #4030 - always visible, rightmost) */}
            {/* Issue #5025: entityId present → button mode (opens drawer) */}
            {showInfoButton && entityId && drawerEntityType && (
              <MeepleCardInfoButton
                onClick={() => setDrawerOpen(true)}
                entityType={entity}
                customColor={customColor}
                tooltip={infoTooltip}
                size="sm"
              />
            )}
            {/* Backward compat: only infoHref, no entityId → link mode */}
            {showInfoButton && infoHref && !entityId && (
              <MeepleCardInfoButton
                href={infoHref}
                entityType={entity}
                customColor={customColor}
                tooltip={infoTooltip}
                size="sm"
              />
            )}
          </div>
        )}

        {/* Title */}
        <h3
          className={cn(
            'font-quicksand font-bold leading-tight',
            variant === 'hero'
              ? 'text-2xl text-white mb-1 [text-shadow:0_2px_8px_rgba(0,0,0,0.3)]'
              : variant === 'featured'
                ? 'text-xl mb-1'
                : variant === 'list'
                  ? 'text-base'
                  : variant === 'compact'
                    ? 'text-sm'
                    : 'text-[0.8rem] sm:text-[0.95rem] mb-0.5',
            variant !== 'hero' && 'text-card-foreground',
            variant === 'grid' && 'truncate',
            variant === 'list' && 'line-clamp-2'
          )}
        >
          {title}
        </h3>

        {/* Subtitle */}
        {subtitle && (
          <p
            className={cn(
              variant === 'hero'
                ? 'text-white/85 text-sm mb-2'
                : variant === 'grid'
                  ? 'text-muted-foreground text-[0.7rem] sm:text-[0.78rem] mt-px mb-0.5 sm:mb-1 truncate'
                  : 'text-muted-foreground text-sm mb-2',
              variant === 'compact' && 'text-xs mb-0'
            )}
          >
            {subtitle}
          </p>
        )}

        {/* Rating */}
        {rating !== undefined && variant !== 'compact' && (
          <RatingDisplay
            rating={rating}
            max={ratingMax}
            className={cn('mb-2', variant === 'hero' && 'text-white')}
          />
        )}

        {/* Agent-specific info (Issue #4361) */}
        {entity === 'agent' &&
          variant !== 'compact' &&
          (agentStatus ||
            agentModel ||
            (capabilities && capabilities.length > 0) ||
            agentStats) && (
            <div className="flex flex-col gap-1.5 mb-2" data-testid="agent-info-section">
              {/* Status + Model row */}
              {(agentStatus || agentModel) && (
                <div className="flex items-center gap-2 flex-wrap">
                  {agentStatus && <AgentStatusBadge status={agentStatus} size="sm" />}
                  {agentModel && (
                    <AgentModelInfo
                      modelName={agentModel.modelName}
                      parameters={agentModel.parameters}
                      size="sm"
                    />
                  )}
                </div>
              )}
              {/* Capabilities chips */}
              {capabilities && capabilities.length > 0 && (
                <div className="flex items-center gap-1 flex-wrap">
                  {capabilities.map(cap => (
                    <span
                      key={cap}
                      className="inline-flex items-center px-1.5 py-0.5 rounded text-[10px] font-semibold bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300"
                    >
                      {cap}
                    </span>
                  ))}
                </div>
              )}
              {/* Stats row */}
              {agentStats && (
                <AgentStatsDisplay
                  stats={agentStats}
                  layout="horizontal"
                  className="text-muted-foreground"
                />
              )}
            </div>
          )}

        {/* ChatSession-specific info (Issue #4400) */}
        {entity === 'chatSession' &&
          variant !== 'compact' &&
          (chatStatus || chatAgent || chatStats || chatGame) && (
            <div className="flex flex-col gap-1.5 mb-2" data-testid="chat-info-section">
              {/* Row 1: Status + Agent (grid: status only to save space) */}
              {(chatStatus || chatAgent) && (
                <div className="flex items-center gap-2 flex-wrap">
                  {chatStatus && <ChatStatusBadge status={chatStatus} size="sm" />}
                  {variant !== 'grid' && chatAgent && <ChatAgentInfo agent={chatAgent} />}
                </div>
              )}
              {/* Row 2: Game context (hidden in grid) */}
              {variant !== 'grid' && chatGame && <ChatGameContext game={chatGame} />}
              {/* Row 3: Stats */}
              {chatStats && (
                <ChatStatsDisplay
                  stats={chatStats}
                  layout="horizontal"
                  className="text-muted-foreground"
                />
              )}
              {/* Row 4: Message preview (hidden in grid) */}
              {variant !== 'grid' && chatPreview && (
                <p className="text-xs text-muted-foreground truncate" data-testid="chat-preview">
                  <span className="font-semibold">
                    {chatPreview.sender === 'user' ? 'You' : 'Agent'}:
                  </span>{' '}
                  {chatPreview.lastMessage}
                </p>
              )}
            </div>
          )}

        {/* ChatSession unread badge (positioned on card overlay) */}
        {entity === 'chatSession' && unreadCount !== undefined && unreadCount > 0 && (
          <ChatUnreadBadge count={unreadCount} />
        )}

        {/* Session-specific info (Issue #4751) */}
        {entity === 'session' &&
          variant !== 'compact' &&
          (sessionStatus || sessionPlayers || sessionTurn) && (
            <div className="flex flex-col gap-1.5 mb-2" data-testid="session-info-section">
              {/* Row 1: Session status badge */}
              {sessionStatus && <SessionStatusBadge status={sessionStatus} size="sm" />}
              {/* Row 2: Score table (grid/featured/hero only) */}
              {sessionPlayers && sessionPlayers.length > 0 && variant !== 'list' && (
                <SessionScoreTable
                  players={sessionPlayers}
                  roundScores={sessionRoundScores ?? []}
                  onEditScore={sessionActions?.onEditScore}
                  maxVisibleRounds={variant === 'grid' ? 3 : 5}
                />
              )}
              {/* Row 3: Turn sequence */}
              {sessionTurn &&
                sessionPlayers &&
                sessionPlayers.length > 0 &&
                sessionStatus !== 'completed' && (
                  <SessionTurnSequence
                    players={sessionPlayers}
                    turn={sessionTurn}
                    isHost={isSessionHost}
                    onPrevTurn={onPrevTurn}
                    onNextTurn={onNextTurn}
                  />
                )}
              {/* Row 4: Action buttons */}
              {sessionStatus && sessionActions && (
                <SessionActionButtons status={sessionStatus} actions={sessionActions} />
              )}
            </div>
          )}

        {/* Document-specific info (Issue #5001) */}
        {entity === 'kb' && documentStatus && variant !== 'compact' && (
          <div className="flex items-center gap-1.5 mb-2">
            <DocumentStatusBadge status={documentStatus} size="sm" />
          </div>
        )}

        {/* KB Cards worst-status badge (Issue #5193) */}
        {entity === 'game' && worstKbStatus && variant !== 'compact' && (
          <div className="flex items-center gap-1.5 mb-2" data-testid="meeple-card-kb-badge">
            <DocumentStatusBadge status={worstKbStatus} size="sm" />
            <span className="text-[10px] text-muted-foreground font-medium">
              {kbCards!.length} KB
            </span>
          </div>
        )}

        {/* Snapshot History Slider (Issue #4758) */}
        {entity === 'session' &&
          sessionSnapshots &&
          sessionSnapshots.length > 0 &&
          variant !== 'compact' && (
            <SnapshotHistorySlider
              snapshots={sessionSnapshots}
              currentIndex={currentSnapshotIndex}
              onSelect={onSnapshotSelect}
              isTimeTravelMode={isTimeTravelMode}
              onTimeTravelToggle={onTimeTravelToggle}
            />
          )}

        {/* Metadata (non-grid variants render inline; grid uses footer below) */}
        {metadata.length > 0 && variant !== 'compact' && variant !== 'grid' && (
          <MetadataChips
            metadata={metadata}
            variant={variant}
            className={variant === 'hero' ? 'mt-auto' : undefined}
          />
        )}

        {/* Actions (featured/hero only) */}
        {showActions && (
          <ActionButtons actions={actions} entity={entity} customColor={customColor} />
        )}

        {/* Badge overlay (only for non-grid/non-featured/non-list variants without VerticalTagStack) */}
        {badge &&
          variant !== 'grid' &&
          variant !== 'featured' &&
          variant !== 'list' &&
          !isHeroOrFeatured && (
            <span className="absolute top-3 right-3 bg-card/90 backdrop-blur-sm px-2 py-0.5 rounded-md text-xs font-semibold text-muted-foreground border border-border/50">
              {badge}
            </span>
          )}
      </div>

      {/* Grid footer: metadata info bar (~1/5 of card) */}
      {variant === 'grid' && metadata.length > 0 && (
        <div
          className={cn(
            'flex items-center justify-evenly gap-2',
            'px-3 py-2',
            'border-t border-border',
            'bg-muted/60 dark:bg-muted/40',
            // Only round bottom when there's no footer section below
            !(navigateTo && navigateTo.length > 0) &&
              !(entity === 'game' && hasAgent !== undefined) &&
              'rounded-b-2xl'
          )}
          data-testid="meeple-card-footer"
        >
          {metadata.map((item, index) => {
            const chipContent = (
              <>
                {item.icon && <item.icon className="w-3.5 h-3.5" aria-hidden="true" />}
                <span className="font-nunito">{item.label || item.value}</span>
              </>
            );
            if (item.onClick) {
              return (
                <button
                  key={index}
                  type="button"
                  className="flex items-center gap-1.5 text-[0.7rem] font-semibold text-foreground/65 dark:text-foreground/60 cursor-pointer hover:opacity-80 transition-opacity"
                  onClick={e => {
                    e.stopPropagation();
                    item.onClick!();
                  }}
                >
                  {chipContent}
                </button>
              );
            }
            return (
              <span
                key={index}
                className="flex items-center gap-1.5 text-[0.7rem] font-semibold text-foreground/65 dark:text-foreground/60"
              >
                {chipContent}
              </span>
            );
          })}
        </div>
      )}

      {/* Issue #4777: Agent action footer (Crea Agente / Chat) */}
      {entity === 'game' && hasAgent !== undefined && id && (
        <CardAgentAction
          hasAgent={hasAgent}
          agentId={agentId}
          gameId={id}
          onCreateAgent={onCreateAgent}
          variant={variant}
          hasNavFooter={!!(navigateTo && navigateTo.length > 0)}
        />
      )}

      {/* Navigation footer: links to related entities (Epic #4688, Issue #4689) */}
      {navigateTo && navigateTo.length > 0 && <CardNavigationFooter links={navigateTo} />}

      {/* EntityLink preview row — footer (Issue #5129) */}
      {firstLinkPreview && linkCount !== undefined && linkCount > 0 && (
        <EntityLinkPreviewRow
          linkType={firstLinkPreview.linkType}
          targetName={firstLinkPreview.targetName}
          totalCount={linkCount}
          onClick={onLinksClick}
        />
      )}

      {/* ExtraMeepleCardDrawer (Issue #5025) — rendered inside card, portal to body */}
      {entityId && drawerEntityType && (
        <ExtraMeepleCardDrawer
          entityType={drawerEntityType}
          entityId={entityId}
          open={drawerOpen}
          onClose={() => setDrawerOpen(false)}
        />
      )}

      {/* Mobile Touch UX: Bottom Sheet for Quick Actions (Issue #4604) */}
      {showMobileActions && hasMobileActions && (
        <div
          className={cn(
            'absolute bottom-0 inset-x-0 z-20',
            'bg-card/98 backdrop-blur-md border-t border-border',
            'p-3 rounded-b-2xl',
            'flex items-center justify-center gap-2 flex-wrap',
            // Slide up animation
            'animate-in slide-in-from-bottom-4 duration-200',
            // Touch-friendly sizing
            'min-h-[60px]',
            // Show only on mobile/tablet
            'md:hidden'
          )}
          onClick={e => e.stopPropagation()} // Prevent card click when interacting with buttons
        >
          {/* Entity Quick Actions */}
          {entityQuickActions && entityQuickActions.length > 0 && (
            <>
              {entityQuickActions.map((action, idx) => (
                <button
                  key={idx}
                  onClick={() => {
                    action.onClick();
                    setShowMobileActions(false);
                  }}
                  className={cn(
                    'px-4 py-2.5 rounded-lg font-medium text-sm',
                    'min-h-[44px] min-w-[44px]', // Touch target
                    'transition-all duration-200',
                    // First action is primary, others secondary
                    idx === 0
                      ? 'bg-primary text-primary-foreground hover:bg-primary/90'
                      : 'bg-muted text-foreground hover:bg-muted/80',
                    action.disabled && 'opacity-50 cursor-not-allowed',
                    action.hidden && 'hidden'
                  )}
                  disabled={action.disabled}
                >
                  {action.icon && <action.icon className="w-4 h-4 inline-block mr-2" />}
                  {action.label}
                </button>
              ))}
            </>
          )}

          {/* Info Button */}
          {/* Issue #5025: entityId → opens drawer; fallback infoHref → navigate */}
          {showInfoButton && entityId && drawerEntityType && (
            <button
              type="button"
              className="px-4 py-2.5 rounded-lg bg-muted hover:bg-muted/80 text-sm font-medium min-h-[44px] flex items-center gap-2"
              onClick={() => {
                setShowMobileActions(false);
                setDrawerOpen(true);
              }}
            >
              <Info className="h-4 w-4" aria-hidden="true" />
              {infoTooltip || 'Info'}
            </button>
          )}
          {showInfoButton && infoHref && !entityId && (
            <a
              href={infoHref}
              className="px-4 py-2.5 rounded-lg bg-muted hover:bg-muted/80 text-sm font-medium min-h-[44px] flex items-center gap-2"
              onClick={() => setShowMobileActions(false)}
            >
              <Info className="h-4 w-4" aria-hidden="true" />
              {infoTooltip || 'Info'}
            </a>
          )}

          {/* Wishlist Button */}
          {showWishlistBtn && onWishlistToggle && id && (
            <button
              onClick={() => {
                onWishlistToggle(id, !!isWishlisted);
                setShowMobileActions(false);
              }}
              className="px-4 py-2.5 rounded-lg bg-muted hover:bg-muted/80 text-sm font-medium min-h-[44px] flex items-center gap-2"
            >
              <span className="text-lg">{isWishlisted ? '❤️' : '🤍'}</span>
              {isWishlisted ? 'Wishlist' : 'Add'}
            </button>
          )}
        </div>
      )}
    </Component>
  );

  // Feature: Wrap with FlipCard if enabled (takes priority over HoverPreview)
  // Session entities can flip with sessionBackData even without flipData
  // Game entities can flip with gameBackData even without flipData
  if (flippable && (flipData || sessionBackData || gameBackData)) {
    // Session entities use custom back content (Issue #4752)
    const sessionBack =
      entity === 'session' && sessionStatus && sessionPlayers && sessionBackData ? (
        <SessionBackContent
          status={sessionStatus}
          players={sessionPlayers}
          backData={sessionBackData}
          entityColor={color}
          title={title}
          detailHref={detailHref}
        />
      ) : undefined;

    // Game entities use GameBackContent for stats + KB preview + actions
    const gameBack =
      entity === 'game' && gameBackData ? (
        <GameBackContent
          data={gameBackData}
          actions={gameBackActions}
          entityColor={color}
          title={title}
          detailHref={detailHref}
        />
      ) : undefined;

    return (
      <FlipCard
        flipData={flipData}
        customBackContent={sessionBack ?? gameBack}
        variant={variant}
        isFlipped={isFlipped}
        onFlip={onFlip}
        flipTrigger={flipTrigger}
        detailHref={detailHref}
        entityColor={color}
        entityName={entityColors[entity].name}
        title={title}
      >
        {cardContent}
      </FlipCard>
    );
  }

  // Feature: Wrap with HoverPreview if enabled
  if (showPreview && onFetchPreview && id) {
    return (
      <HoverPreview
        gameId={id}
        previewData={previewData}
        onFetchPreview={onFetchPreview}
        delay={500}
        disabled={loading}
      >
        {cardContent}
      </HoverPreview>
    );
  }

  return cardContent;
});

// ============================================================================
// Exports
// ============================================================================

export { MeepleCardSkeleton } from './meeple-card-parts';
export { entityColors } from './meeple-card-styles';
export type { MeepleEntityType, MeepleCardVariant } from './meeple-card-styles';
export type { MeepleCardMetadata as MeepleMetadata, MeepleCardAction as MeepleAction };
export type { MeepleCardFlipData } from './meeple-card-features/FlipCard';
export type { AgentStatus } from './meeple-card-features/AgentStatusBadge';
export type { AgentStats } from './meeple-card-features/AgentStatsDisplay';
export type { ModelParameters } from './meeple-card-features/AgentModelInfo';
export type { ChatStatus } from './meeple-card-features/ChatStatusBadge';
export type { ChatAgent } from './meeple-card-features/ChatAgentInfo';
export type { ChatStats } from './meeple-card-features/ChatStatsDisplay';
export type { ChatGame } from './meeple-card-features/ChatGameContext';
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
} from './meeple-card-features/session-types';
