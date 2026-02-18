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

import { cva, type VariantProps } from 'class-variance-authority';
import Image from 'next/image';

import { DiceIcon3D } from '@/components/ui/icons/dice-icon-3d';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { AgentModelInfo, type ModelParameters } from './meeple-card-features/AgentModelInfo';
import { AgentStatsDisplay, type AgentStats } from './meeple-card-features/AgentStatsDisplay';
import { AgentStatusBadge, type AgentStatus } from './meeple-card-features/AgentStatusBadge';
import { BulkSelectCheckbox } from './meeple-card-features/BulkSelectCheckbox';
import { ChatAgentInfo, type ChatAgent } from './meeple-card-features/ChatAgentInfo';
import { ChatGameContext, type ChatGame } from './meeple-card-features/ChatGameContext';
import { ChatStatsDisplay, type ChatStats } from './meeple-card-features/ChatStatsDisplay';
import { ChatStatusBadge, type ChatStatus } from './meeple-card-features/ChatStatusBadge';
import { ChatUnreadBadge } from './meeple-card-features/ChatUnreadBadge';
import { DragHandle, type DragData } from './meeple-card-features/DragHandle';
import { FlipCard, type MeepleCardFlipData } from './meeple-card-features/FlipCard';
import { HoverPreview } from './meeple-card-features/HoverPreview';
import { QuickActionsMenu } from './meeple-card-features/QuickActionsMenu';
import { StatusBadge } from './meeple-card-features/StatusBadge';
import { type TagConfig, type TagPresetKey } from './meeple-card-features/tag-presets';
import { TagStrip } from './meeple-card-features/TagStrip';
import { WishlistButton } from './meeple-card-features/WishlistButton';
// Issue #4030: New action components
import { MeepleCardInfoButton } from './meeple-card-info-button';
import { MeepleCardQuickActions } from './meeple-card-quick-actions';
// Issue #4361: Agent-specific display components
// Issue #4400: ChatSession-specific display components

import type { LucideIcon } from 'lucide-react';

// Feature components (Issue #3820)

// ============================================================================
// Types & Interfaces
// ============================================================================

/**
 * Supported entity types with semantic colors
 * Issue #4030: Extended from 5 to 7 types (removed collection, added session/agent/document/chatSession)
 */
export type MeepleEntityType = 'game' | 'player' | 'session' | 'agent' | 'document' | 'chatSession' | 'event' | 'custom';

/**
 * Layout variant options
 */
export type MeepleCardVariant = 'grid' | 'list' | 'compact' | 'featured' | 'hero';

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
  status?: 'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade' | Array<'owned' | 'wishlisted' | 'played' | 'borrowed' | 'for-trade'>;
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
  entityQuickActions?: Array<{
    icon: LucideIcon;
    label: string;
    onClick: () => void;
    disabled?: boolean;
    hidden?: boolean;
  }>;

  /** Feature: Info Button (always-visible navigation to detail page) */
  showInfoButton?: boolean;
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
}

// ============================================================================
// Entity Color Configuration
// ============================================================================

const entityColors: Record<MeepleEntityType, { hsl: string; name: string }> = {
  game: { hsl: '25 95% 45%', name: 'Game' },         // Orange
  player: { hsl: '262 83% 58%', name: 'Player' },    // Purple
  session: { hsl: '240 60% 55%', name: 'Session' },  // Indigo
  agent: { hsl: '38 92% 50%', name: 'Agent' },       // Amber
  document: { hsl: '210 40% 55%', name: 'Document' }, // Slate
  chatSession: { hsl: '220 80% 55%', name: 'Chat' }, // Blue
  event: { hsl: '350 89% 60%', name: 'Event' },      // Rose
  custom: { hsl: '220 70% 50%', name: 'Custom' },    // Blue (default)
};

// ============================================================================
// CVA Variants
// ============================================================================

const meepleCardVariants = cva(
  // Base styles for all variants — v2 warm styling (Issue #4604)
  [
    'group relative overflow-hidden cursor-pointer',
    'transition-all duration-[350ms] ease-[cubic-bezier(0.4,0,0.2,1)]',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
  ],
  {
    variants: {
      variant: {
        grid: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-card/95 backdrop-blur-[4px]',
          'dark:bg-card dark:backdrop-blur-none',
          'border border-border/50',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-xl)]',
          // Transform applied via inline style (Tailwind v4 issue)
        ],
        list: [
          'flex flex-row items-center gap-4 p-3 rounded-xl',
          'bg-card border border-border/50',
          '[box-shadow:var(--shadow-warm-sm)] hover:[box-shadow:var(--shadow-warm-md)]',
          'hover:translate-x-1',
        ],
        compact: [
          'flex flex-row items-center gap-2 p-2 rounded-lg',
          'bg-card/80 border border-border/30',
          'hover:bg-card',
        ],
        featured: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-card/95 backdrop-blur-[4px]',
          'dark:bg-card dark:backdrop-blur-none',
          'border border-border/50',
          '[box-shadow:var(--shadow-warm-md)] hover:[box-shadow:var(--shadow-warm-xl)]',
          // Transform applied via inline style (Tailwind v4 issue)
        ],
        hero: [
          'relative flex flex-col rounded-3xl overflow-hidden',
          'min-h-[320px]',
          '[box-shadow:var(--shadow-warm-xl)] hover:[box-shadow:var(--shadow-warm-2xl)]',
          'hover:scale-[1.01]',
        ],
      },
    },
    defaultVariants: {
      variant: 'grid',
    },
  }
);

const coverVariants = cva('relative overflow-hidden', {
  variants: {
    variant: {
      grid: 'rounded-t-2xl', // aspect-ratio applied conditionally per entity type
      list: 'w-16 h-16 rounded-lg flex-shrink-0',
      compact: 'w-10 h-10 rounded-md flex-shrink-0',
      featured: '', // aspect-ratio applied conditionally per entity type
      hero: 'absolute inset-0',
    },
  },
  defaultVariants: { variant: 'grid' },
});

const contentVariants = cva('', {
  variants: {
    variant: {
      grid: 'flex-1 flex flex-col p-4',
      list: 'flex-1 min-w-0 py-1',
      compact: 'flex-1 min-w-0',
      featured: 'flex-1 flex flex-col px-5 py-4',
      hero: 'relative z-10 mt-auto flex flex-col justify-end p-6',
    },
  },
  defaultVariants: { variant: 'grid' },
});

// ============================================================================
// Subcomponents
// ============================================================================

/**
 * Entity type indicator (left border + badge)
 */
function EntityIndicator({
  entity,
  variant,
  customColor,
  className,
}: {
  entity: MeepleEntityType;
  variant: MeepleCardVariant;
  customColor?: string;
  className?: string;
}) {
  // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
  const color = customColor || entityColors[entity].hsl;
  // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
  const name = entityColors[entity].name;

  // Hero/featured uses ribbon, others use left border
  if (variant === 'hero') {
    return (
      <span
        className={cn(
          'absolute top-4 right-[-2rem] z-20',
          'py-1 px-10 text-xs font-bold uppercase tracking-wider',
          'text-white shadow-md rotate-45',
          className
        )}
        style={{ backgroundColor: `hsl(${color})` }}
      >
        {name}
      </span>
    );
  }

  if (variant === 'featured' || variant === 'grid') {
    // Left border accent (4px → 6px on hover) — v2 style (Issue #4604)
    return (
      <span
        className={cn(
          'absolute left-0 top-0 bottom-0 w-1 group-hover:w-1.5 transition-all duration-250',
          className
        )}
        style={{ backgroundColor: `hsl(${color})` }}
        aria-hidden="true"
      />
    );
  }

  // List/compact: dot indicator
  return (
    <span
      className={cn('w-2 h-2 rounded-full flex-shrink-0', className)}
      style={{ backgroundColor: `hsl(${color})` }}
      aria-hidden="true"
    />
  );
}

/**
 * Vertical tag stack (top-left of card)
 * Issue #4062: Stacks entity badge, status, and custom badge vertically
 * with compact sizing (80px max), truncation, and hover tooltips.
 */
function VerticalTagStack({
  entity,
  customColor,
  status,
  showStatusIcon,
  badge,
}: {
  entity: MeepleEntityType;
  customColor?: string;
  status?: MeepleCardProps['status'];
  showStatusIcon?: boolean;
  badge?: string;
}) {
  // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
  const color = customColor || entityColors[entity].hsl;
  // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
  const name = entityColors[entity].name;

  return (
    <TooltipProvider delayDuration={300}>
      <div
        className="absolute top-3 left-4 z-10 flex flex-col gap-1.5"
        data-testid="meeple-card-tag-stack"
      >
        {/* Entity type badge (highest priority) */}
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              className="max-w-[80px] truncate px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider text-white rounded-md shadow-sm cursor-default"
              style={{ backgroundColor: `hsl(${color})` }}
            >
              {name}
            </span>
          </TooltipTrigger>
          <TooltipContent side="right" sideOffset={8}>
            {name}
          </TooltipContent>
        </Tooltip>

        {/* Status badge */}
        {status && (
          <StatusBadge
            status={status}
            showIcon={showStatusIcon}
            className="max-w-[80px]"
          />
        )}

        {/* Custom badge */}
        {badge && (
          <Tooltip>
            <TooltipTrigger asChild>
              <span
                className="max-w-[80px] truncate bg-card/90 backdrop-blur-sm px-2 py-0.5 rounded-md text-[10px] font-semibold text-muted-foreground border border-border/50 cursor-default"
              >
                {badge}
              </span>
            </TooltipTrigger>
            <TooltipContent side="right" sideOffset={8}>
              {badge}
            </TooltipContent>
          </Tooltip>
        )}
      </div>
    </TooltipProvider>
  );
}

/**
 * Cover image with optional gradient overlay and entity-specific placeholders
 */
function CoverImage({
  src,
  alt,
  variant,
  entity,
  customColor,
}: {
  src?: string;
  alt: string;
  variant: MeepleCardVariant;
  entity: MeepleEntityType;
  customColor?: string;
}) {
  // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
  const color = customColor || entityColors[entity].hsl;

  // Treat placehold.co URLs as "no image" - they return SVGs that Next.js Image can't optimize
  const isPlaceholder = src?.includes('placehold.co');
  const hasImage = src && !isPlaceholder;

  // v2: Entity-specific aspect ratios (Issue #4604)
  // chatSession uses shorter 4:3 ratio (like playing card), others use tall 7:10
  const aspectRatioClass =
    variant === 'grid'
      ? entity === 'chatSession'
        ? 'aspect-[4/3]'
        : 'aspect-[7/10]'
      : variant === 'featured'
        ? entity === 'chatSession'
          ? 'aspect-[3/1]'
          : 'aspect-[16/9]'
        : '';

  const showOverlay = variant === 'hero' || variant === 'featured' || variant === 'grid';

  // v2: Entity-specific gradient backgrounds for placeholders
  const entityGradients: Record<MeepleEntityType, string> = {
    game: 'linear-gradient(135deg, hsl(30,60%,85%), hsl(20,70%,75%))',
    chatSession: 'linear-gradient(135deg, hsl(220,30%,88%), hsl(220,50%,75%))',
    agent: 'linear-gradient(135deg, hsl(38,40%,85%), hsl(38,60%,70%))',
    player: 'linear-gradient(135deg, hsl(262,30%,85%), hsl(262,50%,75%))',
    session: 'linear-gradient(135deg, hsl(240,30%,85%), hsl(240,40%,70%))',
    event: 'linear-gradient(135deg, hsl(350,40%,85%), hsl(350,60%,70%))',
    document: 'linear-gradient(135deg, hsl(210,30%,85%), hsl(210,40%,70%))',
    custom: 'linear-gradient(135deg, hsl(220,30%,85%), hsl(220,40%,70%))',
  };

  // Entity emoji fallbacks (when no image)
  const entityEmoji: Record<MeepleEntityType, string> = {
    game: '', // Uses DiceIcon3D component instead
    chatSession: '💬',
    agent: '🤖',
    player: '👤',
    session: '🎲',
    event: '🏆',
    document: '📄',
    custom: '🎲',
  };

  return (
    <div className={cn(
      coverVariants({ variant }),
      aspectRatioClass,
    )}>
      {hasImage ? (
        /* Real image (game cover, agent icon, avatar, etc.) */
        <Image
          src={src}
          alt={alt}
          fill
          sizes={
            variant === 'hero'
              ? '100vw'
              : variant === 'featured'
                ? '(max-width: 768px) 100vw, 50vw'
                : variant === 'grid'
                  ? '(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 25vw'
                  : '64px'
          }
          className={cn(
            'object-cover transition-transform duration-500',
            'group-hover:scale-105' // v2: scale-105 = 1.05 (bracket notation issue in v4)
          )}
          loading="lazy"
        />
      ) : (
        /* v2 Placeholder: gradient background + entity icon */
        <div
          className="absolute inset-0 flex items-center justify-center"
          style={{
            // eslint-disable-next-line security/detect-object-injection
            background: entityGradients[entity],
          }}
        >
          {entity === 'game' ? (
            <DiceIcon3D size={variant === 'grid' ? 'md' : variant === 'featured' ? 'lg' : 'sm'} className="opacity-60" />
          ) : (
            // eslint-disable-next-line security/detect-object-injection
            <span className="text-6xl opacity-50" aria-hidden="true">{entityEmoji[entity]}</span>
          )}
        </div>
      )}

      {/* v2: Entity-colored gradient overlay (stronger than before) */}
      {showOverlay && (
        <div
          className="absolute inset-0 pointer-events-none"
          style={{
            background:
              variant === 'hero'
                ? `linear-gradient(180deg, transparent 0%, hsla(${color}, 0.1) 30%, hsla(${color}, 0.6) 70%, hsla(${color}, 0.9) 100%)`
                : variant === 'featured'
                  ? `linear-gradient(180deg, transparent 40%, hsla(${color}, 0.15) 70%, hsla(${color}, 0.4) 100%)`
                  : `linear-gradient(180deg, transparent 50%, hsl(var(--card)) 100%)`,
          }}
          aria-hidden="true"
        />
      )}

      {/* Shimmer effect on hover — v2 (Issue #4604) */}
      {showOverlay && (
        <div
          className="absolute inset-0 pointer-events-none group-hover:animate-mc-shimmer"
          style={{
            background: 'linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)',
            transform: 'translateX(-100%)',
          }}
          aria-hidden="true"
        />
      )}
    </div>
  );
}

/**
 * Metadata chips display
 */
function MetadataChips({
  metadata,
  variant,
  className,
}: {
  metadata: MeepleCardMetadata[];
  variant: MeepleCardVariant;
  className?: string;
}) {
  const isHero = variant === 'hero';
  const chipClass = isHero
    ? 'text-white/90 text-sm'
    : 'text-muted-foreground text-xs bg-muted px-2 py-0.5 rounded-md';

  return (
    <div className={cn('flex flex-wrap gap-2', className)}>
      {metadata.map((item, index) => (
        <span
          key={index}
          className={cn('flex items-center gap-1', chipClass)}
        >
          {item.icon && <item.icon className="w-3 h-3 opacity-70" aria-hidden="true" />}
          <span>{item.label || item.value}</span>
        </span>
      ))}
    </div>
  );
}

/**
 * Action buttons for featured/hero variants
 */
function ActionButtons({
  actions,
  entity,
  customColor,
  className,
}: {
  actions: MeepleCardAction[];
  entity: MeepleEntityType;
  customColor?: string;
  className?: string;
}) {
  // eslint-disable-next-line security/detect-object-injection -- entity is from typed MeepleEntityType union
  const color = customColor || entityColors[entity].hsl;

  return (
    <div className={cn('flex gap-2 mt-3', className)}>
      {actions.map((action, index) => (
        <button
          key={index}
          onClick={e => {
            e.stopPropagation();
            action.onClick?.();
          }}
          disabled={action.disabled}
          className={cn(
            'flex-1 py-2 px-4 rounded-lg font-quicksand font-semibold text-sm',
            'transition-all duration-200 hover:-translate-y-0.5',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
            'disabled:opacity-50 disabled:cursor-not-allowed disabled:hover:translate-y-0',
            action.primary
              ? 'text-white shadow-md hover:shadow-lg'
              : 'bg-muted text-foreground hover:bg-muted/80'
          )}
          style={action.primary ? { backgroundColor: `hsl(${color})` } : undefined}
        >
          {action.label}
        </button>
      ))}
    </div>
  );
}

/**
 * Rating stars display
 */
function RatingDisplay({
  rating,
  max = 5,
  className,
}: {
  rating: number;
  max?: number;
  className?: string;
}) {
  // Convert to 0-5 scale if needed
  const normalized = max === 10 ? rating / 2 : rating;
  const fullStars = Math.floor(normalized);
  const hasHalfStar = normalized % 1 >= 0.5;

  return (
    <div className={cn('flex items-center gap-1', className)} aria-label={`Rating: ${rating} out of ${max}`}>
      {Array.from({ length: 5 }).map((_, i) => (
        <span
          key={i}
          className={cn(
            'text-sm',
            i < fullStars
              ? 'text-amber-400'
              : i === fullStars && hasHalfStar
                ? 'text-amber-400/50'
                : 'text-muted-foreground/30'
          )}
        >
          {'\u2605'}
        </span>
      ))}
      <span className="text-xs text-muted-foreground ml-1">{rating.toFixed(1)}</span>
    </div>
  );
}

/**
 * Loading skeleton
 */
function MeepleCardSkeleton({ variant = 'grid' }: { variant?: MeepleCardVariant }) {
  return (
    <div
      className={cn(
        meepleCardVariants({ variant }),
        'animate-pulse pointer-events-none'
      )}
      data-testid="meeple-card-skeleton"
    >
      <div className={cn(coverVariants({ variant }), 'bg-muted')} />
      <div className={contentVariants({ variant })}>
        <div className="h-5 bg-muted rounded w-3/4 mb-2" />
        <div className="h-3 bg-muted rounded w-1/2 mb-3" />
        <div className="flex gap-2">
          <div className="h-4 bg-muted rounded w-12" />
          <div className="h-4 bg-muted rounded w-12" />
        </div>
      </div>
    </div>
  );
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

  // Mobile touch UX: Bottom sheet for actions (Issue #4604)
  const [showMobileActions, setShowMobileActions] = useState(false);
  const [isMobile, setIsMobile] = useState(false);
  const cardRef = useRef<HTMLDivElement | HTMLElement>(null);
  const hasMobileActions = isMobile && (hasQuickActions || entityQuickActions || showWishlistBtn || showInfoButton);

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

  // Mobile tap handler: 1st tap = show actions, 2nd tap = navigate
  const handleMobileClick = () => {
    if (hasMobileActions && !showMobileActions) {
      // 1st tap: Show bottom sheet
      setShowMobileActions(true);
    } else if (onClick) {
      // 2nd tap OR no actions: Navigate
      setShowMobileActions(false);
      onClick();
    }
  };

  // Desktop click handler: Navigate immediately
  const handleDesktopClick = () => {
    if (onClick) onClick();
  };

  // Unified click handler
  const handleCardClick = isMobile ? handleMobileClick : (isInteractive ? handleDesktopClick : undefined);

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
        variant === 'grid' && '[&:hover]:[-webkit-transform:translateY(-8px)] [&:hover]:[transform:translateY(-8px)]',
        variant === 'featured' && '[&:hover]:[-webkit-transform:translateY(-8px)] [&:hover]:[transform:translateY(-8px)]',
        selected && 'ring-2 ring-offset-2 bg-accent/10',
        selected && `ring-[hsl(${color})]`,
        className
      )}
      style={{
        // v2: Entity-colored outline for hover glow effect + hover transform
        '--mc-entity-color': `hsl(${color})`,
        outlineColor: `hsla(${color}, 0.4)`,
        willChange: 'transform, box-shadow, outline',
        // v2: Transform handled via CSS (globals.css [data-variant]:hover selectors)
      } as React.CSSProperties}
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

      {/* Entity indicator (left border for grid/featured, ribbon for hero, dot for list/compact) */}
      <EntityIndicator
        entity={entity}
        variant={variant}
        customColor={customColor}
      />

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
        {(entityQuickActions || showInfoButton || showWishlistBtn || hasQuickActions) && (
          <div className={cn(
            "absolute right-2.5 flex items-center gap-1.5 z-15",
            // Push actions below unread badge row for chatSession
            entity === 'chatSession' && unreadCount && unreadCount > 0 ? 'top-10' : 'top-2.5',
            // Hide on mobile/tablet (use bottom sheet instead)
            "hidden md:flex",
          )}>
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
            {showInfoButton && infoHref && (
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
                    : 'text-lg mb-0.5',
            variant !== 'hero' && 'text-card-foreground',
            (variant === 'grid' || variant === 'list') && 'line-clamp-2'
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
        {entity === 'agent' && variant !== 'compact' && (agentStatus || agentModel || (capabilities && capabilities.length > 0) || agentStats) && (
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
                {capabilities.map((cap) => (
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
            {agentStats && <AgentStatsDisplay stats={agentStats} layout="horizontal" className="text-muted-foreground" />}
          </div>
        )}

        {/* ChatSession-specific info (Issue #4400) */}
        {entity === 'chatSession' && variant !== 'compact' && (chatStatus || chatAgent || chatStats || chatGame) && (
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
            {chatStats && <ChatStatsDisplay stats={chatStats} layout="horizontal" className="text-muted-foreground" />}
            {/* Row 4: Message preview (hidden in grid) */}
            {variant !== 'grid' && chatPreview && (
              <p className="text-xs text-muted-foreground truncate" data-testid="chat-preview">
                <span className="font-semibold">{chatPreview.sender === 'user' ? 'You' : 'Agent'}:</span>{' '}
                {chatPreview.lastMessage}
              </p>
            )}
          </div>
        )}

        {/* ChatSession unread badge (positioned on card overlay) */}
        {entity === 'chatSession' && unreadCount !== undefined && unreadCount > 0 && (
          <ChatUnreadBadge count={unreadCount} />
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
          <ActionButtons
            actions={actions}
            entity={entity}
            customColor={customColor}
          />
        )}

        {/* Badge overlay (only for non-grid/non-featured/non-list variants without VerticalTagStack) */}
        {badge && variant !== 'grid' && variant !== 'featured' && variant !== 'list' && !isHeroOrFeatured && (
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
            'px-4 py-3',
            'border-t border-border/50',
            'bg-muted/60 dark:bg-muted/40',
            'rounded-b-2xl',
          )}
          data-testid="meeple-card-footer"
        >
          {metadata.map((item, index) => (
            <span
              key={index}
              className="flex items-center gap-1.5 text-xs text-foreground/70 dark:text-foreground/60"
            >
              {item.icon && <item.icon className="w-3.5 h-3.5" aria-hidden="true" />}
              <span className="font-nunito font-semibold">{item.label || item.value}</span>
            </span>
          ))}
        </div>
      )}

      {/* Mobile Touch UX: Bottom Sheet for Quick Actions (Issue #4604) */}
      {showMobileActions && hasMobileActions && (
        <div
          className={cn(
            "absolute bottom-0 inset-x-0 z-20",
            "bg-card/98 backdrop-blur-md border-t border-border",
            "p-3 rounded-b-2xl",
            "flex items-center justify-center gap-2 flex-wrap",
            // Slide up animation
            "animate-in slide-in-from-bottom-4 duration-200",
            // Touch-friendly sizing
            "min-h-[60px]",
            // Show only on mobile/tablet
            "md:hidden"
          )}
          onClick={(e) => e.stopPropagation()} // Prevent card click when interacting with buttons
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
                    "px-4 py-2.5 rounded-lg font-medium text-sm",
                    "min-h-[44px] min-w-[44px]", // Touch target
                    "transition-all duration-200",
                    // First action is primary, others secondary
                    idx === 0
                      ? "bg-primary text-primary-foreground hover:bg-primary/90"
                      : "bg-muted text-foreground hover:bg-muted/80",
                    action.disabled && "opacity-50 cursor-not-allowed",
                    action.hidden && "hidden"
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
          {showInfoButton && infoHref && (
            <a
              href={infoHref}
              className="px-4 py-2.5 rounded-lg bg-muted hover:bg-muted/80 text-sm font-medium min-h-[44px] flex items-center gap-2"
              onClick={() => setShowMobileActions(false)}
            >
              <span className="text-lg">ℹ️</span>
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
  if (flippable && flipData) {
    return (
      <FlipCard
        flipData={flipData}
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

export { MeepleCardSkeleton, entityColors };
export type { MeepleCardMetadata as MeepleMetadata, MeepleCardAction as MeepleAction };
export type { MeepleCardFlipData } from './meeple-card-features/FlipCard';
export type { AgentStatus } from './meeple-card-features/AgentStatusBadge';
export type { AgentStats } from './meeple-card-features/AgentStatsDisplay';
export type { ModelParameters } from './meeple-card-features/AgentModelInfo';
export type { ChatStatus } from './meeple-card-features/ChatStatusBadge';
export type { ChatAgent } from './meeple-card-features/ChatAgentInfo';
export type { ChatStats } from './meeple-card-features/ChatStatsDisplay';
export type { ChatGame } from './meeple-card-features/ChatGameContext';
