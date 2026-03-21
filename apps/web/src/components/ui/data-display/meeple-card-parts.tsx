'use client';

/**
 * MeepleCard visual subcomponents
 * Extracted from meeple-card.tsx for maintainability (Issue #5241)
 */

import React from 'react';

import Image from 'next/image';

import { DiceIcon3D } from '@/components/ui/icons/dice-icon-3d';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';

import { StatusBadge } from './meeple-card-features/StatusBadge';
import {
  entityColors,
  coverVariants,
  meepleCardVariants,
  contentVariants,
} from './meeple-card-styles';

import type { MeepleCardAction, MeepleCardMetadata, MeepleCardProps } from './meeple-card';
import type { MeepleEntityType, MeepleCardVariant } from './meeple-card-styles';

// ============================================================================
// EntityIndicator
// ============================================================================

/**
 * Entity type indicator (left border + badge)
 */
export function EntityIndicator({
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
  const color = customColor || entityColors[entity].hsl;
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

// ============================================================================
// VerticalTagStack
// ============================================================================

/**
 * Vertical tag stack (top-left of card)
 * Issue #4062: Stacks entity badge, status, and custom badge vertically
 * with compact sizing (80px max), truncation, and hover tooltips.
 */
export function VerticalTagStack({
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
  const color = customColor || entityColors[entity].hsl;
  const name = entityColors[entity].name;

  return (
    <TooltipProvider delayDuration={300}>
      <div
        className="absolute top-1.5 left-1.5 sm:top-2 sm:left-2.5 z-10 flex flex-col gap-1 sm:gap-1.5"
        data-testid="meeple-card-tag-stack"
      >
        {/* Entity type badge (highest priority) */}
        <Tooltip>
          <TooltipTrigger asChild>
            <span
              className="max-w-[60px] sm:max-w-[80px] truncate px-1.5 sm:px-2 py-px sm:py-0.5 text-[8px] sm:text-[9px] font-extrabold uppercase tracking-[0.04em] text-white rounded-[5px] sm:rounded-[6px] shadow-sm cursor-default"
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
          <StatusBadge status={status} showIcon={showStatusIcon} className="max-w-[80px]" />
        )}

        {/* Custom badge */}
        {badge && (
          <Tooltip>
            <TooltipTrigger asChild>
              <span className="max-w-[80px] truncate bg-card/90 backdrop-blur-sm px-2 py-0.5 rounded-md text-[10px] font-semibold text-muted-foreground border border-border/50 cursor-default">
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

// ============================================================================
// CoverImage
// ============================================================================

/**
 * Cover image with optional gradient overlay and entity-specific placeholders
 */
export function CoverImage({
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
  const color = customColor || entityColors[entity].hsl;

  // Treat placehold.co URLs as "no image" - they return SVGs that Next.js Image can't optimize
  const isPlaceholder = src?.includes('placehold.co');
  const hasImage = src && !isPlaceholder;

  // v2: Entity-specific aspect ratios (Issue #4604)
  // chatSession uses shorter 4:3 ratio (like playing card), others use tall 7:10
  // Mobile: shorter aspect ratios for 2-col grid density
  const aspectRatioClass =
    variant === 'grid'
      ? entity === 'chatSession'
        ? 'aspect-[4/3]'
        : 'aspect-square sm:aspect-[7/10]'
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
    toolkit: 'linear-gradient(135deg, hsl(142,30%,85%), hsl(142,50%,75%))',
    tool: 'linear-gradient(135deg, hsl(195,30%,85%), hsl(195,50%,70%))',
    kb: 'linear-gradient(135deg, hsl(174,30%,85%), hsl(174,50%,70%))',
    collection: 'linear-gradient(135deg, hsl(20,40%,85%), hsl(20,60%,72%))',
    group: 'linear-gradient(135deg, hsl(280,30%,85%), hsl(280,45%,72%))',
    location: 'linear-gradient(135deg, hsl(200,30%,85%), hsl(200,50%,72%))',
    expansion: 'linear-gradient(135deg, hsl(290,30%,85%), hsl(290,50%,72%))',
    achievement: 'linear-gradient(135deg, hsl(45,50%,85%), hsl(45,80%,70%))',
    note: 'linear-gradient(135deg, hsl(40,20%,85%), hsl(40,30%,72%))',
    custom: 'linear-gradient(135deg, hsl(220,15%,85%), hsl(220,15%,70%))',
  };

  // Entity emoji fallbacks (when no image)
  const entityEmoji: Record<MeepleEntityType, string> = {
    game: '', // Uses DiceIcon3D component instead
    chatSession: '\uD83D\uDCAC',
    agent: '\uD83E\uDD16',
    player: '\uD83D\uDC64',
    session: '\uD83C\uDFB2',
    event: '\uD83C\uDFC6',
    toolkit: '\uD83D\uDEE0\uFE0F',
    tool: '\uD83D\uDD27',
    kb: '\uD83D\uDCC4',
    collection: '\uD83D\uDCDA',
    group: '\uD83D\uDC65',
    location: '\uD83D\uDCCD',
    expansion: '\uD83D\uDCE6',
    achievement: '\uD83C\uDFC6',
    note: '\uD83D\uDCDD',
    custom: '\uD83C\uDFB2',
  };

  return (
    <div className={cn(coverVariants({ variant }), aspectRatioClass)}>
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
            'group-hover:scale-[1.06]'
          )}
          loading="lazy"
        />
      ) : (
        /* v2 Placeholder: gradient background + entity icon */
        <div
          className="absolute inset-0 flex items-center justify-center"
          style={{
            background: entityGradients[entity],
          }}
        >
          {entity === 'game' ? (
            <DiceIcon3D
              size={variant === 'grid' ? 'md' : variant === 'featured' ? 'lg' : 'sm'}
              className="opacity-60"
            />
          ) : (
            <span className="text-6xl opacity-50" aria-hidden="true">
              {entityEmoji[entity]}
            </span>
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
                  : `linear-gradient(to top, hsla(${color}, 0.15), transparent 60%)`,
          }}
          aria-hidden="true"
        />
      )}

      {/* Shimmer effect on hover — v2 (Issue #4604) */}
      {showOverlay && (
        <div
          className="absolute inset-0 pointer-events-none group-hover:animate-mc-shimmer"
          style={{
            background:
              'linear-gradient(105deg, transparent 40%, rgba(255,255,255,0.25) 50%, transparent 60%)',
            transform: 'translateX(-100%)',
          }}
          aria-hidden="true"
        />
      )}
    </div>
  );
}

// ============================================================================
// MetadataChips
// ============================================================================

/**
 * Metadata chips display
 */
export function MetadataChips({
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
    : 'text-muted-foreground text-[10px] sm:text-xs bg-muted px-1.5 sm:px-2 py-px sm:py-0.5 rounded-md';

  return (
    <div className={cn('flex flex-wrap gap-2', className)}>
      {metadata.map((item, index) => {
        const content = (
          <>
            {item.icon && <item.icon className="w-3 h-3 opacity-70" aria-hidden="true" />}
            <span>{item.label || item.value}</span>
          </>
        );

        if (item.onClick) {
          return (
            <button
              key={index}
              type="button"
              className={cn(
                'flex items-center gap-1 cursor-pointer hover:opacity-80 transition-opacity',
                chipClass
              )}
              onClick={e => {
                e.stopPropagation();
                item.onClick!();
              }}
            >
              {content}
            </button>
          );
        }

        return (
          <span key={index} className={cn('flex items-center gap-1', chipClass)}>
            {content}
          </span>
        );
      })}
    </div>
  );
}

// ============================================================================
// ActionButtons
// ============================================================================

/**
 * Action buttons for featured/hero variants
 */
export function ActionButtons({
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
  const color = customColor || entityColors[entity].hsl;

  return (
    <div className={cn('flex gap-2 mt-3 min-w-0', className)}>
      {actions.map((action, index) => (
        <button
          key={index}
          onClick={e => {
            e.stopPropagation();
            action.onClick?.();
          }}
          disabled={action.disabled}
          title={action.label}
          className={cn(
            'flex-1 min-w-0 py-2 px-3 md:px-4 rounded-lg font-quicksand font-semibold',
            'text-xs md:text-sm truncate',
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

// ============================================================================
// RatingDisplay
// ============================================================================

/**
 * Rating stars display
 */
export function RatingDisplay({
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
    <div
      className={cn('flex items-center gap-0.5', className)}
      aria-label={`Rating: ${rating} out of ${max}`}
    >
      {Array.from({ length: 5 }).map((_, i) => (
        <span
          key={i}
          className={cn(
            'text-[0.78rem]',
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
      <span className="text-[0.7rem] font-semibold text-muted-foreground ml-1">
        {rating.toFixed(1)}
      </span>
    </div>
  );
}

// ============================================================================
// MeepleCardSkeleton
// ============================================================================

/**
 * Loading skeleton
 */
export function MeepleCardSkeleton({ variant = 'grid' }: { variant?: MeepleCardVariant }) {
  return (
    <div
      className={cn(meepleCardVariants({ variant }), 'animate-pulse pointer-events-none')}
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
