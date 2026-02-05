/**
 * SimilarGamesCarousel - Display similar games with horizontal scroll
 * Issue #3353: Similar Games Discovery with RAG
 *
 * Features:
 * - Horizontal scrollable carousel of similar games
 * - Shows similarity score and reason
 * - Links to game detail page
 * - Responsive design with touch support
 * - Loading and empty states
 */

'use client';

import React from 'react';

import { ChevronLeft, ChevronRight, Star, Users, Clock, Sparkles } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/data-display/tooltip';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { SimilarGameDto } from '@/lib/api/clients/gamesClient';
import { cn } from '@/lib/utils';

export interface SimilarGamesCarouselProps {
  /** List of similar games to display */
  games: SimilarGameDto[];
  /** Whether the data is loading */
  isLoading?: boolean;
  /** Title of the source game (for display) */
  sourceGameTitle?: string;
  /** Custom class name */
  className?: string;
  /** Link builder for game detail page */
  getLinkHref?: (gameId: string) => string;
}

/**
 * Formats similarity score as percentage
 */
function formatSimilarity(score: number): string {
  return `${Math.round(score * 100)}%`;
}

/**
 * Formats player count range
 */
function formatPlayers(min: number | null, max: number | null): string {
  if (min === null && max === null) return '-';
  if (min === max) return `${min}`;
  if (min === null) return `1-${max}`;
  if (max === null) return `${min}+`;
  return `${min}-${max}`;
}

/**
 * Formats playing time
 */
function formatTime(minutes: number | null): string {
  if (minutes === null) return '-';
  if (minutes < 60) return `${minutes} min`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
}

export function SimilarGamesCarousel({
  games,
  isLoading = false,
  sourceGameTitle,
  className,
  getLinkHref = (gameId) => `/library/games/${gameId}`,
}: SimilarGamesCarouselProps) {
  const scrollContainerRef = React.useRef<HTMLDivElement>(null);
  const [canScrollLeft, setCanScrollLeft] = React.useState(false);
  const [canScrollRight, setCanScrollRight] = React.useState(false);

  // Check scroll state
  const checkScrollState = React.useCallback(() => {
    const container = scrollContainerRef.current;
    if (!container) return;

    setCanScrollLeft(container.scrollLeft > 0);
    setCanScrollRight(
      container.scrollLeft < container.scrollWidth - container.clientWidth - 10
    );
  }, []);

  React.useEffect(() => {
    checkScrollState();
    const container = scrollContainerRef.current;
    if (container) {
      container.addEventListener('scroll', checkScrollState);
      window.addEventListener('resize', checkScrollState);
      return () => {
        container.removeEventListener('scroll', checkScrollState);
        window.removeEventListener('resize', checkScrollState);
      };
    }
  }, [checkScrollState, games]);

  const scroll = (direction: 'left' | 'right') => {
    const container = scrollContainerRef.current;
    if (!container) return;

    const cardWidth = 280; // Approximate card width + gap
    const scrollAmount = direction === 'left' ? -cardWidth * 2 : cardWidth * 2;
    container.scrollBy({ left: scrollAmount, behavior: 'smooth' });
  };

  // Loading state
  if (isLoading) {
    return (
      <div className={cn('space-y-4', className)}>
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-primary" />
          <h3 className="text-lg font-semibold">Giochi Simili</h3>
        </div>
        <div className="flex gap-4 overflow-hidden">
          {[1, 2, 3, 4].map((i) => (
            <div key={i} className="w-[260px] flex-shrink-0">
              <Skeleton className="h-[200px] w-full rounded-lg" />
              <Skeleton className="mt-2 h-4 w-3/4" />
              <Skeleton className="mt-1 h-3 w-1/2" />
            </div>
          ))}
        </div>
      </div>
    );
  }

  // Empty state
  if (games.length === 0) {
    return (
      <div className={cn('space-y-4', className)}>
        <div className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-primary" />
          <h3 className="text-lg font-semibold">Giochi Simili</h3>
        </div>
        <div className="rounded-lg border border-dashed p-8 text-center">
          <p className="text-muted-foreground">
            Nessun gioco simile trovato{sourceGameTitle ? ` per "${sourceGameTitle}"` : ''}.
          </p>
        </div>
      </div>
    );
  }

  return (
    <TooltipProvider>
      <div className={cn('space-y-4', className)}>
        {/* Header */}
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Sparkles className="h-5 w-5 text-primary" />
            <h3 className="text-lg font-semibold">Giochi Simili</h3>
            <Badge variant="secondary" className="ml-2">
              {games.length}
            </Badge>
          </div>

          {/* Navigation buttons */}
          <div className="flex gap-1">
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={() => scroll('left')}
              disabled={!canScrollLeft}
              aria-label="Scorri a sinistra"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              className="h-8 w-8"
              onClick={() => scroll('right')}
              disabled={!canScrollRight}
              aria-label="Scorri a destra"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>
        </div>

        {/* Carousel */}
        <div className="relative">
          {/* Left gradient overlay */}
          {canScrollLeft && (
            <div className="pointer-events-none absolute left-0 top-0 z-10 h-full w-12 bg-gradient-to-r from-background to-transparent" />
          )}

          {/* Scrollable container */}
          <div
            ref={scrollContainerRef}
            className="flex gap-4 overflow-x-auto pb-4 scrollbar-thin scrollbar-track-transparent scrollbar-thumb-muted"
            style={{ scrollbarWidth: 'thin' }}
          >
            {games.map((game) => (
              <SimilarGameCard
                key={game.id}
                game={game}
                href={getLinkHref(game.id)}
              />
            ))}
          </div>

          {/* Right gradient overlay */}
          {canScrollRight && (
            <div className="pointer-events-none absolute right-0 top-0 z-10 h-full w-12 bg-gradient-to-l from-background to-transparent" />
          )}
        </div>
      </div>
    </TooltipProvider>
  );
}

/**
 * Individual similar game card
 */
interface SimilarGameCardProps {
  game: SimilarGameDto;
  href: string;
}

function SimilarGameCard({ game, href }: SimilarGameCardProps) {
  const hasImage = game.thumbnailUrl && game.thumbnailUrl.length > 0;

  return (
    <Link href={href} className="block flex-shrink-0">
      <Card className="w-[260px] overflow-hidden transition-all hover:shadow-lg hover:scale-[1.02]">
        {/* Image */}
        <div className="relative h-[140px] bg-muted">
          {hasImage && game.thumbnailUrl ? (
            <Image
              src={game.thumbnailUrl}
              alt={game.title}
              fill
              className="object-cover"
              sizes="260px"
            />
          ) : (
            <div className="flex h-full items-center justify-center">
              <span className="text-4xl">🎲</span>
            </div>
          )}

          {/* Similarity badge */}
          <Tooltip>
            <TooltipTrigger asChild>
              <div className="absolute right-2 top-2">
                <Badge
                  variant="default"
                  className={cn(
                    'font-semibold',
                    game.similarityScore >= 0.7
                      ? 'bg-green-500 hover:bg-green-600'
                      : game.similarityScore >= 0.5
                        ? 'bg-yellow-500 hover:bg-yellow-600'
                        : 'bg-orange-500 hover:bg-orange-600'
                  )}
                >
                  {formatSimilarity(game.similarityScore)} simile
                </Badge>
              </div>
            </TooltipTrigger>
            <TooltipContent side="left" className="max-w-[200px]">
              <p className="text-sm">{game.similarityReason}</p>
            </TooltipContent>
          </Tooltip>
        </div>

        <CardContent className="p-3">
          {/* Title */}
          <h4 className="line-clamp-2 font-medium leading-tight" title={game.title}>
            {game.title}
          </h4>

          {/* Stats */}
          <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
            {/* Players */}
            <Tooltip>
              <TooltipTrigger asChild>
                <span className="flex items-center gap-1">
                  <Users className="h-3 w-3" />
                  {formatPlayers(game.minPlayers, game.maxPlayers)}
                </span>
              </TooltipTrigger>
              <TooltipContent>Numero giocatori</TooltipContent>
            </Tooltip>

            {/* Duration */}
            <Tooltip>
              <TooltipTrigger asChild>
                <span className="flex items-center gap-1">
                  <Clock className="h-3 w-3" />
                  {formatTime(game.playingTimeMinutes)}
                </span>
              </TooltipTrigger>
              <TooltipContent>Durata partita</TooltipContent>
            </Tooltip>

            {/* Rating */}
            {game.averageRating && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="flex items-center gap-1">
                    <Star className="h-3 w-3 fill-yellow-400 text-yellow-400" />
                    {game.averageRating.toFixed(1)}
                  </span>
                </TooltipTrigger>
                <TooltipContent>Valutazione media BGG</TooltipContent>
              </Tooltip>
            )}

            {/* Complexity */}
            {game.complexityRating && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <span className="flex items-center gap-1 text-purple-600 dark:text-purple-400">
                    {game.complexityRating.toFixed(1)}/5
                  </span>
                </TooltipTrigger>
                <TooltipContent>Complessità</TooltipContent>
              </Tooltip>
            )}
          </div>

          {/* Similarity reason */}
          <p className="mt-2 line-clamp-2 text-xs text-muted-foreground">
            {game.similarityReason}
          </p>
        </CardContent>
      </Card>
    </Link>
  );
}
