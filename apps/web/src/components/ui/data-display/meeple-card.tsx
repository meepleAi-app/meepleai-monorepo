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

import React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';
import Image from 'next/image';

import { cn } from '@/lib/utils';

import type { LucideIcon } from 'lucide-react';

// Feature components (Issue #3820)
import { WishlistButton } from './meeple-card-features/WishlistButton';
import { QuickActionsMenu } from './meeple-card-features/QuickActionsMenu';
import { StatusBadge } from './meeple-card-features/StatusBadge';
import { HoverPreview } from './meeple-card-features/HoverPreview';
import { DragHandle } from './meeple-card-features/DragHandle';
import { BulkSelectCheckbox } from './meeple-card-features/BulkSelectCheckbox';

// ============================================================================
// Types & Interfaces
// ============================================================================

/**
 * Supported entity types with semantic colors
 */
export type MeepleEntityType = 'game' | 'player' | 'collection' | 'event' | 'custom';

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
  onFetchPreview?: (id: string) => Promise<any>;

  /** Feature: Drag & Drop (#3828) */
  draggable?: boolean;
  dragData?: { id: string; type: string; index: number };
  onDragStart?: (data: any) => void;
  onDragEnd?: (data: any) => void;

  /** Feature: Bulk Selection (#3829) */
  selectable?: boolean;
  selected?: boolean;
  onSelect?: (id: string, selected: boolean) => void;
}

// ============================================================================
// Entity Color Configuration
// ============================================================================

const entityColors: Record<MeepleEntityType, { hsl: string; name: string }> = {
  game: { hsl: '25 95% 45%', name: 'Game' },         // Orange
  player: { hsl: '262 83% 58%', name: 'Player' },    // Purple
  collection: { hsl: '168 76% 42%', name: 'Collection' }, // Teal
  event: { hsl: '350 89% 60%', name: 'Event' },      // Rose
  custom: { hsl: '220 70% 50%', name: 'Custom' },    // Blue (default)
};

// ============================================================================
// CVA Variants
// ============================================================================

const meepleCardVariants = cva(
  // Base styles for all variants
  [
    'group relative overflow-hidden cursor-pointer',
    'transition-all duration-300 ease-out',
    'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
  ],
  {
    variants: {
      variant: {
        grid: [
          'flex flex-col rounded-2xl',
          'bg-card/90 backdrop-blur-[12px] backdrop-saturate-[180%]',
          'dark:bg-card dark:backdrop-blur-none',
          'border border-border/50',
          'shadow-sm hover:shadow-lg',
          'hover:-translate-y-1',
        ],
        list: [
          'flex flex-row items-center gap-4 p-3 rounded-xl',
          'bg-card border border-border/50',
          'shadow-sm hover:shadow-md',
          'hover:translate-x-1',
        ],
        compact: [
          'flex flex-row items-center gap-2 p-2 rounded-lg',
          'bg-card/80 border border-border/30',
          'hover:bg-card',
        ],
        featured: [
          'flex flex-col rounded-2xl overflow-hidden',
          'bg-card border border-border/50',
          'shadow-md hover:shadow-xl',
          'hover:-translate-y-2',
        ],
        hero: [
          'relative rounded-3xl overflow-hidden',
          'min-h-[320px]',
          'shadow-xl hover:shadow-2xl',
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
      grid: 'aspect-[4/3] rounded-t-2xl',
      list: 'w-16 h-16 rounded-lg flex-shrink-0',
      compact: 'w-10 h-10 rounded-md flex-shrink-0',
      featured: 'aspect-[16/9]',
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
      featured: 'flex-1 flex flex-col p-5',
      hero: 'relative z-10 flex flex-col justify-end p-6 min-h-[320px]',
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
    return (
      <>
        {/* Left border accent */}
        <span
          className="absolute left-0 top-0 bottom-0 w-1 group-hover:w-1.5 transition-all duration-200"
          style={{ backgroundColor: `hsl(${color})` }}
          aria-hidden="true"
        />
        {/* Top badge */}
        <span
          className={cn(
            'absolute top-3 left-4 z-10',
            'px-2 py-0.5 text-[10px] font-bold uppercase tracking-wider',
            'text-white rounded-md shadow-sm',
            className
          )}
          style={{ backgroundColor: `hsl(${color})` }}
        >
          {name}
        </span>
      </>
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
 * Cover image with optional gradient overlay
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
  const placeholder =
    'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="400" height="300" viewBox="0 0 400 300"%3E%3Crect width="400" height="300" fill="%23334155"/%3E%3Ctext x="50%25" y="50%25" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="64" fill="%239CA3AF"%3E%F0%9F%8E%B2%3C/text%3E%3C/svg%3E';

  const showOverlay = variant === 'hero' || variant === 'featured' || variant === 'grid';

  return (
    <div className={coverVariants({ variant })}>
      <Image
        src={src || placeholder}
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
          'group-hover:scale-105'
        )}
        loading="lazy"
        placeholder="blur"
        blurDataURL={placeholder}
      />

      {/* Canvas-style gradient overlay (Direction C influence) */}
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
}: MeepleCardProps) {
  const coverSrc = entity === 'player' ? avatarUrl || imageUrl : imageUrl;
  const showActions = actions.length > 0 && (variant === 'featured' || variant === 'hero');
  const isHeroOrFeatured = variant === 'hero' || variant === 'featured';

  // Determine if card should be interactive
  // Avoid nested-interactive: don't make card clickable when it has action buttons
  const isInteractive = !!onClick && !showActions;

  // Feature visibility logic (Issue #3820)
  const color = customColor || entityColors[entity].hsl;
  const hasQuickActions = quickActions && quickActions.length > 0;
  const showWishlistBtn = showWishlist && !hasQuickActions; // Priority: quickActions > wishlist

  if (loading) {
    return <MeepleCardSkeleton variant={variant} />;
  }

  // Use div for interactive cards (article cannot have role="button")
  // Use article for semantic non-interactive cards
  const Component = isInteractive ? 'div' : 'article';

  // Card content JSX
  const cardContent = (
    <Component
      className={cn(
        meepleCardVariants({ variant }),
        selected && 'ring-2 ring-offset-2 bg-accent/10',
        selected && `ring-[hsl(${color})]`,
        className
      )}
      onClick={isInteractive ? onClick : undefined}
      role={isInteractive ? 'button' : undefined}
      tabIndex={isInteractive ? 0 : undefined}
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

      {/* Entity indicator */}
      <EntityIndicator
        entity={entity}
        variant={variant}
        customColor={customColor}
      />

      {/* Feature: Status Badge (below entity badge) */}
      {status && (
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
          dragData={dragData as any}
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
        {/* Feature: Top-right corner (Wishlist or QuickActions) */}
        {(showWishlistBtn || hasQuickActions) && (
          <div className="absolute top-3 right-3 flex gap-2 z-15">
            {hasQuickActions && (
              <QuickActionsMenu actions={quickActions} userRole={userRole} size="sm" />
            )}
            {showWishlistBtn && onWishlistToggle && (
              <WishlistButton
                gameId={testId || 'card'}
                isWishlisted={!!isWishlisted}
                onToggle={onWishlistToggle}
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
              ? 'text-2xl text-white mb-1 drop-shadow-lg'
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

        {/* Metadata */}
        {metadata.length > 0 && variant !== 'compact' && (
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

        {/* Badge overlay */}
        {badge && !isHeroOrFeatured && (
          <span className="absolute top-3 right-3 bg-card/90 backdrop-blur-sm px-2 py-0.5 rounded-md text-xs font-semibold text-muted-foreground border border-border/50">
            {badge}
          </span>
        )}
      </div>
    </Component>
  );

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
