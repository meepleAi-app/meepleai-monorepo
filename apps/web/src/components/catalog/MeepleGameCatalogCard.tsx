/**
 * MeepleGameCatalogCard - Shared Catalog Card using MeepleCard
 * Issue #3334 - MeepleCard Integration with SharedGameCatalog
 *
 * Adapter component that wraps MeepleCard for SharedGameCatalog usage.
 * Provides:
 * - MeepleCard entity="game" with catalog-specific styling
 * - "Add to Library" action button
 * - "Already in Library" badge
 * - Complexity stars in metadata
 * - Players and playtime metadata
 *
 * @example
 * ```tsx
 * <MeepleGameCatalogCard
 *   game={sharedGame}
 *   variant="grid"
 *   onAdd={handleAddToLibrary}
 * />
 * ```
 */

'use client';

import { useState } from 'react';

import { Users, Clock, BarChart2 } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { useGameInLibraryStatus, useAddGameToLibrary } from '@/hooks/queries';
import type { SharedGame, SharedGameDetail } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface MeepleGameCatalogCardProps {
  /** Shared game data */
  game: SharedGame | SharedGameDetail;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Click handler for card navigation */
  onClick?: (gameId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Format complexity as stars (e.g., "●●●○○")
 */
function formatComplexity(complexity: number | null | undefined): string {
  if (!complexity) return 'N/A';
  const filled = Math.round(complexity);
  const empty = 5 - filled;
  return '●'.repeat(filled) + '○'.repeat(empty);
}

/**
 * Format players range
 */
function formatPlayers(min: number | null | undefined, max: number | null | undefined): string {
  if (!min && !max) return 'N/A';
  if (min === max) return `${min}`;
  return `${min || '?'}-${max || '?'}`;
}

/**
 * Format playtime
 */
function formatPlaytime(minutes: number | null | undefined): string {
  if (!minutes) return 'N/A';
  if (minutes >= 60) {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleGameCatalogCard({
  game,
  variant = 'grid',
  onClick,
  className,
}: MeepleGameCatalogCardProps) {
  const [isAdding, setIsAdding] = useState(false);

  // Check if game is already in user's library
  const { data: status, isLoading: statusLoading } = useGameInLibraryStatus(game.id);
  const inLibrary = status?.inLibrary || false;

  // Mutation for adding game to library
  const addMutation = useAddGameToLibrary();

  const handleAddToLibrary = async () => {
    setIsAdding(true);
    try {
      await addMutation.mutateAsync({ gameId: game.id });
      toast.success(`${game.title} aggiunto alla libreria!`);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Errore durante l\'aggiunta alla libreria'
      );
    } finally {
      setIsAdding(false);
    }
  };

  // Build metadata array
  const metadata = [
    { icon: Users, value: formatPlayers(game.minPlayers, game.maxPlayers) },
    { icon: Clock, value: formatPlaytime(game.playingTimeMinutes) },
  ];

  // Add complexity if available
  if (game.complexityRating) {
    metadata.push({
      icon: BarChart2,
      value: formatComplexity(game.complexityRating),
    });
  }

  // Build subtitle with publisher/year if available
  const subtitleParts: string[] = [];
  if (game.yearPublished) {
    subtitleParts.push(String(game.yearPublished));
  }
  if (game.bggId) {
    subtitleParts.push(`BGG: ${game.bggId}`);
  }
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : undefined;

  // Build actions for featured variant
  const showActions = variant === 'featured' || variant === 'hero';
  const actions = showActions && !statusLoading
    ? [
        {
          label: inLibrary ? 'Nella Libreria' : isAdding ? 'Aggiunta...' : 'Aggiungi',
          primary: !inLibrary,
          disabled: inLibrary || isAdding,
          onClick: inLibrary ? undefined : handleAddToLibrary,
        },
      ]
    : undefined;

  // Build badge
  const badge = inLibrary && !statusLoading ? 'In Libreria' : undefined;

  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title={game.title}
      subtitle={subtitle}
      imageUrl={game.imageUrl || undefined}
      rating={game.averageRating || undefined}
      ratingMax={10}
      metadata={metadata}
      badge={badge}
      actions={actions}
      loading={statusLoading}
      onClick={onClick ? () => onClick(game.id) : undefined}
      className={className}
      data-testid={`catalog-game-card-${game.id}`}
    />
  );
}

/**
 * MeepleGameCatalogCard Skeleton for loading state
 */
export function MeepleGameCatalogCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title=""
      loading
      data-testid="catalog-game-card-skeleton"
    />
  );
}

export default MeepleGameCatalogCard;
