/**
 * CatalogGameCard Component (Issue #2874)
 *
 * Displays a shared game card with community statistics for the Shared Catalog.
 * Features:
 * - Cover image with lazy loading
 * - Title, players, complexity, duration display
 * - Community stats: Rating (stars), Total plays, Contributors
 * - "Already in Library" badge (green) if owned
 * - Hover overlay with "Aggiungi" button (orange)
 * - Click navigates to game detail page
 *
 * @see Issue #2874: [Shared Catalog] Catalog Game Cards with Community Stats
 */

'use client';

import { useState } from 'react';

import { Check, Clock, Library, Plus, Users, UserPlus, Gamepad2 } from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader } from '@/components/ui/data-display/card';
import { RatingStars } from '@/components/ui/data-display/rating-stars';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useGameInLibraryStatus, useAddGameToLibrary, useLibraryQuota } from '@/hooks/queries';
import type { SharedGame, SharedGameDetail } from '@/lib/api';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface CommunityStats {
  /** Total number of plays across all users */
  totalPlays?: number;
  /** Number of contributors to this game entry */
  contributorCount?: number;
}

export interface CatalogGameCardProps {
  /** The shared game data */
  game: SharedGame | SharedGameDetail;
  /** Optional community stats (may come from separate API or be embedded) */
  communityStats?: CommunityStats;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

/**
 * Format large numbers with K/M suffix
 */
function formatCount(count: number): string {
  if (count >= 1000000) {
    return `${(count / 1000000).toFixed(1)}M`;
  }
  if (count >= 1000) {
    return `${(count / 1000).toFixed(1)}K`;
  }
  return count.toString();
}

/**
 * Format complexity rating as filled/empty circles
 */
function formatComplexity(rating: number | null): string {
  if (rating === null) return 'N/A';
  const filled = Math.round(rating);
  return '●'.repeat(filled) + '○'.repeat(5 - filled);
}

/**
 * Format players range string
 */
function formatPlayers(min: number, max: number): string {
  if (min === max) return `${min}`;
  return `${min}-${max}`;
}

// ============================================================================
// Component
// ============================================================================

export function CatalogGameCard({
  game,
  communityStats,
  className,
}: CatalogGameCardProps) {
  const [isAdding, setIsAdding] = useState(false);

  // Check if game is already in user's library
  const { data: status, isLoading: statusLoading } = useGameInLibraryStatus(game.id);
  const inLibrary = status?.inLibrary || false;

  // Check library quota (Issue #2875)
  const { data: quota, isLoading: quotaLoading } = useLibraryQuota();
  const isQuotaExceeded = quota ? quota.remainingSlots <= 0 : false;

  // Mutation for adding game to library
  const addMutation = useAddGameToLibrary();

  const handleAddToLibrary = async (e: React.MouseEvent) => {
    e.preventDefault(); // Prevent Link navigation
    e.stopPropagation();

    setIsAdding(true);
    try {
      await addMutation.mutateAsync({ gameId: game.id });
      toast.success(`${game.title} aggiunto alla libreria!`);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : "Errore durante l'aggiunta alla libreria"
      );
    } finally {
      setIsAdding(false);
    }
  };

  // Derived values
  const playersText = formatPlayers(game.minPlayers, game.maxPlayers);
  const complexityText = formatComplexity(game.complexityRating);
  const durationText = game.playingTimeMinutes ? `${game.playingTimeMinutes} min` : 'N/A';

  return (
    <Link href={`/shared-games/${game.id}`} className="block">
      <Card
        data-testid="catalog-game-card"
        className={cn(
          'group relative flex flex-col h-full overflow-hidden',
          'transition-all duration-200',
          'hover:shadow-lg hover:-translate-y-1 hover:border-primary/30',
          className
        )}
      >
        <CardHeader className="p-0">
          {/* Cover Image Container */}
          <div className="relative w-full h-48 bg-muted overflow-hidden">
            {game.imageUrl && !game.imageUrl.includes('placehold.co') ? (
              <Image
                src={game.imageUrl}
                alt={game.title}
                fill
                className="object-cover transition-transform duration-300 group-hover:scale-105"
                sizes="(max-width: 768px) 100vw, (max-width: 1024px) 50vw, 33vw"
              />
            ) : (
              <div className="flex items-center justify-center h-full text-muted-foreground">
                <Library className="h-16 w-16" />
              </div>
            )}

            {/* "Already in Library" Badge */}
            {!statusLoading && inLibrary && (
              <Badge
                data-testid="library-badge"
                className="absolute top-2 right-2 bg-green-600 hover:bg-green-600 text-white shadow-md"
              >
                <Check className="mr-1 h-3 w-3" />
                Nella Libreria
              </Badge>
            )}

            {/* Hover Overlay with "Aggiungi" Button */}
            {!inLibrary && (
              <div
                className={cn(
                  'absolute inset-0 flex items-center justify-center',
                  'bg-black/50 opacity-0 group-hover:opacity-100',
                  'transition-opacity duration-200'
                )}
              >
                <Button
                  data-testid="add-to-library-button"
                  onClick={handleAddToLibrary}
                  disabled={isAdding || addMutation.isPending || statusLoading || quotaLoading || isQuotaExceeded}
                  className={cn(
                    'text-white shadow-lg',
                    isQuotaExceeded
                      ? 'bg-gray-400 hover:bg-gray-400 cursor-not-allowed'
                      : 'bg-orange-500 hover:bg-orange-600'
                  )}
                  size="lg"
                  title={isQuotaExceeded ? 'Hai raggiunto il limite di giochi in libreria' : undefined}
                >
                  <Plus className="mr-2 h-5 w-5" />
                  {isAdding || addMutation.isPending
                    ? 'Aggiunta...'
                    : isQuotaExceeded
                      ? 'Quota esaurita'
                      : 'Aggiungi'}
                </Button>
              </div>
            )}
          </div>
        </CardHeader>

        <CardContent className="flex-1 p-4 space-y-3">
          {/* Title */}
          <h3 className="font-semibold text-lg line-clamp-2 group-hover:text-primary transition-colors">
            {game.title}
          </h3>

          {/* Game Info Row: Players | Complexity | Duration */}
          <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted-foreground">
            {/* Players */}
            <span className="flex items-center gap-1">
              <Users className="h-4 w-4" />
              {playersText}
            </span>

            {/* Complexity */}
            <span className="flex items-center gap-1" title={`Complessità: ${game.complexityRating?.toFixed(1) ?? 'N/A'}`}>
              <span className="text-xs tracking-tight">{complexityText}</span>
            </span>

            {/* Duration */}
            <span className="flex items-center gap-1">
              <Clock className="h-4 w-4" />
              {durationText}
            </span>
          </div>

          {/* Community Stats Row */}
          <div className="flex flex-wrap items-center gap-x-4 gap-y-1 pt-2 border-t">
            {/* Rating Stars */}
            {game.averageRating !== null && game.averageRating !== undefined && (
              <div className="flex items-center gap-1.5">
                <RatingStars
                  rating={game.averageRating}
                  maxRating={10}
                  size="sm"
                  showValue
                />
              </div>
            )}

            {/* Total Plays */}
            {communityStats?.totalPlays !== undefined && communityStats.totalPlays > 0 && (
              <span className="flex items-center gap-1 text-sm text-muted-foreground" title="Partite totali">
                <Gamepad2 className="h-4 w-4" />
                {formatCount(communityStats.totalPlays)}
              </span>
            )}

            {/* Contributors */}
            {communityStats?.contributorCount !== undefined && communityStats.contributorCount > 0 && (
              <span className="flex items-center gap-1 text-sm text-muted-foreground" title="Contributori">
                <UserPlus className="h-4 w-4" />
                {formatCount(communityStats.contributorCount)}
              </span>
            )}

            {/* Fallback if no community stats */}
            {(!game.averageRating && !communityStats?.totalPlays && !communityStats?.contributorCount) && (
              <span className="text-sm text-muted-foreground italic">
                Statistiche non disponibili
              </span>
            )}
          </div>
        </CardContent>
      </Card>
    </Link>
  );
}

// ============================================================================
// Skeleton
// ============================================================================

/**
 * CatalogGameCard Skeleton for loading state
 */
export function CatalogGameCardSkeleton() {
  return (
    <Card className="flex flex-col h-full overflow-hidden">
      {/* Cover Skeleton */}
      <Skeleton className="w-full h-48 rounded-t-lg rounded-b-none" />

      <CardContent className="flex-1 p-4 space-y-3">
        {/* Title Skeleton */}
        <Skeleton className="h-6 w-3/4" />

        {/* Info Row Skeleton */}
        <div className="flex gap-3">
          <Skeleton className="h-4 w-12" />
          <Skeleton className="h-4 w-16" />
          <Skeleton className="h-4 w-14" />
        </div>

        {/* Community Stats Skeleton */}
        <div className="flex gap-4 pt-2 border-t">
          <Skeleton className="h-4 w-20" />
          <Skeleton className="h-4 w-12" />
          <Skeleton className="h-4 w-12" />
        </div>
      </CardContent>
    </Card>
  );
}
