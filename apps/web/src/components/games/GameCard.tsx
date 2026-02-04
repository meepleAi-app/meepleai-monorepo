/**
 * GameCard Component (Issue #1830: UI-003)
 *
 * Dual-variant card component for game display:
 * - Grid: Compact 160x220px with cover image
 * - List: Full-width with thumbnail and detailed metadata
 *
 * Features:
 * - BGG rating stars (0-10 → 0-5 conversion)
 * - FAQ count badge
 * - Responsive hover animations
 * - Lazy-loaded images with blur placeholder
 * - Quicksand font for titles
 *
 * @see docs/wireframes page 3 "Catalogo Giochi (Hybrid View)"
 */

import React from 'react';

import { cva, type VariantProps } from 'class-variance-authority';
import { Users, Clock, Calendar, HelpCircle } from 'lucide-react';
import Image from 'next/image';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { RatingStars } from '@/components/ui/data-display/rating-stars';
import { Game } from '@/lib/api';
import { cn } from '@/lib/utils';

// ============================================================================
// Variants Configuration (class-variance-authority)
// ============================================================================

const gameCardVariants = cva(
  // Base styles (both variants)
  'group transition-all duration-200 hover:shadow-lg cursor-pointer',
  {
    variants: {
      variant: {
        grid: 'flex flex-col h-full',
        list: 'flex flex-row gap-4 items-start',
      },
    },
    defaultVariants: {
      variant: 'grid',
    },
  }
);

const imageContainerVariants = cva('relative overflow-hidden bg-muted', {
  variants: {
    variant: {
      grid: 'aspect-[3/4] rounded-t-lg',
      list: 'w-12 h-12 rounded-md flex-shrink-0',
    },
  },
  defaultVariants: {
    variant: 'grid',
  },
});

const contentVariants = cva('', {
  variants: {
    variant: {
      grid: 'flex-1 flex flex-col',
      list: 'flex-1 min-w-0',
    },
  },
  defaultVariants: {
    variant: 'grid',
  },
});

// ============================================================================
// Props Interface
// ============================================================================

export interface GameCardProps extends VariantProps<typeof gameCardVariants> {
  /** Game data */
  game: Game;
  /** Click handler */
  onClick?: () => void;
  /** Show rating stars (default: true if game has rating) */
  showRating?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Format player count range
 */
function formatPlayerCount(min: number | null, max: number | null): string {
  if (min === null && max === null) return '';
  if (min === max) return `${min}`;
  return `${min || '?'}–${max || '?'}`;
}

/**
 * Format play time range
 */
function formatPlayTime(min: number | null, max: number | null): string {
  if (min === null && max === null) return '';
  if (min === max) return `${min}`;
  return `${min || '?'}–${max || '?'}`;
}

/**
 * Get placeholder image for games without cover
 */
function getPlaceholderImage(): string {
  return 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="160" height="220" viewBox="0 0 160 220"%3E%3Crect width="160" height="220" fill="%23334155"/%3E%3Ctext x="50%25" y="50%25" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="48" fill="%239CA3AF"%3E%F0%9F%8E%B2%3C/text%3E%3C/svg%3E';
}

/**
 * Check if an image URL is external (not a data URI or local path)
 */
function isExternalImage(url: string | null | undefined): boolean {
  if (!url) return false;
  return url.startsWith('http://') || url.startsWith('https://');
}

// ============================================================================
// GameCard Component
// ============================================================================

export const GameCard = React.memo(function GameCard({
  game,
  variant = 'grid',
  onClick,
  showRating = true,
  className,
}: GameCardProps) {
  const hasClickHandler = !!onClick;
  // eslint-disable-next-line eqeqeq -- Intentional null/undefined coalescing check
  const hasRating = showRating && game.averageRating != null;
  // eslint-disable-next-line eqeqeq -- Intentional null/undefined coalescing check
  const hasFaqCount = game.faqCount != null && game.faqCount > 0;
  // Issue #2373: SharedGameCatalog integration - show catalog badge if linked
  const isFromCatalog = !!game.sharedGameId;

  return (
    <Card
      className={cn(
        gameCardVariants({ variant }),
        'hover:-translate-y-1 rounded-lg shadow-sm',
        className
      )}
      onClick={onClick}
      role={hasClickHandler ? 'button' : undefined}
      tabIndex={hasClickHandler ? 0 : undefined}
      onKeyDown={
        hasClickHandler
          ? e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                onClick();
              }
            }
          : undefined
      }
      aria-label={`Game: ${game.title}`}
    >
      {/* ========== Image/Cover ========== */}
      <div className={imageContainerVariants({ variant })}>
        <Image
          src={game.imageUrl || getPlaceholderImage()}
          alt={game.title}
          fill
          sizes={
            variant === 'grid' ? '(max-width: 640px) 50vw, (max-width: 1024px) 33vw, 25vw' : '48px'
          }
          className="object-cover transition-transform duration-200 group-hover:scale-105"
          loading="lazy"
          placeholder="blur"
          blurDataURL={getPlaceholderImage()}
          // Skip Next.js image optimization for external URLs to avoid DNS issues in Docker
          unoptimized={isExternalImage(game.imageUrl)}
        />

        {/* Grid: Top-right badges overlay */}
        {variant === 'grid' && (
          <div className="absolute top-2 right-2 flex flex-col gap-1">
            {isFromCatalog && (
              <Badge variant="default" className="text-xs shadow-sm">
                Catalogo
              </Badge>
            )}
            {game.bggId && !isFromCatalog && (
              <Badge variant="secondary" className="text-xs shadow-sm">
                BGG
              </Badge>
            )}
            {hasFaqCount && (
              <Badge className="text-xs shadow-sm flex items-center gap-1">
                <HelpCircle className="h-3 w-3" />
                {game.faqCount}
              </Badge>
            )}
          </div>
        )}
      </div>

      {/* ========== Content ========== */}
      <div className={contentVariants({ variant })}>
        <CardHeader className={variant === 'grid' ? 'p-4 pb-2' : 'p-0'}>
          <div className="flex items-start justify-between gap-2">
            <CardTitle
              className={cn(
                'font-quicksand font-semibold line-clamp-2',
                variant === 'grid' ? 'text-base' : 'text-lg'
              )}
            >
              {game.title}
            </CardTitle>

            {/* List: Right-side badges */}
            {variant === 'list' && (
              <div className="flex items-center gap-2 flex-shrink-0">
                {isFromCatalog && (
                  <Badge variant="default" className="text-xs">
                    Catalogo
                  </Badge>
                )}
                {game.bggId && !isFromCatalog && (
                  <Badge variant="secondary" className="text-xs">
                    BGG
                  </Badge>
                )}
                {hasFaqCount && (
                  <Badge className="text-xs flex items-center gap-1">
                    <HelpCircle className="h-3 w-3" />
                    {game.faqCount}
                  </Badge>
                )}
              </div>
            )}
          </div>

          {/* Publisher (optional) */}
          {game.publisher && variant === 'grid' && (
            <p className="text-xs text-muted-foreground line-clamp-1 mt-1">{game.publisher}</p>
          )}
        </CardHeader>

        <CardContent className={variant === 'grid' ? 'p-4 pt-0 mt-auto' : 'p-0 mt-1'}>
          {/* Rating Stars */}
          {hasRating && (
            <div className="mb-2">
              <RatingStars
                rating={game.averageRating ?? 0}
                maxRating={10}
                size={variant === 'grid' ? 'sm' : 'md'}
                showHalfStars
                showValue={variant === 'list'}
              />
            </div>
          )}

          {/* Metadata */}
          <div
            className={cn(
              'flex flex-wrap gap-3 text-xs text-muted-foreground',
              variant === 'list' && 'gap-4'
            )}
          >
            {/* Player Count */}
            {(game.minPlayers !== null || game.maxPlayers !== null) && (
              <div className="flex items-center gap-1">
                <Users className="h-3 w-3" />
                <span>{formatPlayerCount(game.minPlayers, game.maxPlayers)}</span>
              </div>
            )}

            {/* Play Time */}
            {(game.minPlayTimeMinutes !== null || game.maxPlayTimeMinutes !== null) && (
              <div className="flex items-center gap-1">
                <Clock className="h-3 w-3" />
                <span>{formatPlayTime(game.minPlayTimeMinutes, game.maxPlayTimeMinutes)} min</span>
              </div>
            )}

            {/* Year Published */}
            {game.yearPublished && (
              <div className="flex items-center gap-1">
                <Calendar className="h-3 w-3" />
                <span>{game.yearPublished}</span>
              </div>
            )}
          </div>

          {/* List variant: Publisher */}
          {game.publisher && variant === 'list' && (
            <p className="text-xs text-muted-foreground mt-2 line-clamp-1">{game.publisher}</p>
          )}
        </CardContent>
      </div>
    </Card>
  );
});
