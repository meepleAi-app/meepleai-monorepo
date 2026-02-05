/**
 * GameCatalogCard Component (Issue #2518)
 *
 * @deprecated Use MeepleGameCard from '@/components/ui/data-display/meeple-game-card' instead.
 * This component will be removed in a future version.
 * Migration: Replace with MeepleGameCard + onAddToLibrary callback.
 *
 * Displays a shared game card with:
 * - Cover image
 * - Title, BGG ID, complexity
 * - Players count, playtime
 * - Short description
 * - Categories/Mechanics tags
 * - Add to Library button
 * - "Already in Library" badge
 *
 * @see MeepleGameCard for the new frosted glass game card component
 */

'use client';

import { useState } from 'react';

import { Library, Plus, Check } from 'lucide-react';
import Image from 'next/image';

import { toast } from '@/components/layout/Toast';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardFooter, CardHeader } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useGameInLibraryStatus, useAddGameToLibrary } from '@/hooks/queries';
import type { SharedGame, SharedGameDetail } from '@/lib/api';

/**
 * @deprecated Use MeepleGameCardProps instead
 */
interface GameCatalogCardProps {
  game: SharedGame | SharedGameDetail;
}

export function GameCatalogCard({ game }: GameCatalogCardProps) {
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

  // Type guard to check if game has extended fields
  const hasExtendedFields = (g: SharedGame | SharedGameDetail): g is SharedGameDetail => {
    return 'categories' in g && 'mechanics' in g;
  };

  // Complexity stars (1-5)
  const complexityStars = game.complexityRating
    ? '●'.repeat(Math.round(game.complexityRating)) + '○'.repeat(5 - Math.round(game.complexityRating))
    : 'N/A';

  // Players range
  const playersRange =
    game.minPlayers && game.maxPlayers
      ? game.minPlayers === game.maxPlayers
        ? `${game.minPlayers} giocatori`
        : `${game.minPlayers}-${game.maxPlayers} giocatori`
      : 'N/A';

  // Playtime
  const playtime = game.playingTimeMinutes ? `${game.playingTimeMinutes} min` : 'N/A';

  return (
    <Card className="flex flex-col h-full hover:shadow-lg transition-shadow">
      <CardHeader className="p-0">
        {/* Cover Image */}
        <div className="relative w-full h-48 bg-muted">
          {game.imageUrl ? (
            <Image
              src={game.imageUrl}
              alt={game.title}
              fill
              className="object-cover rounded-t-lg"
              sizes="(max-width: 768px) 100vw, (max-width: 1024px) 50vw, 33vw"
            />
          ) : (
            <div className="flex items-center justify-center h-full text-muted-foreground">
              <Library className="h-16 w-16" />
            </div>
          )}
          {/* "Already in Library" Badge */}
          {!statusLoading && inLibrary && (
            <Badge className="absolute top-2 right-2 bg-green-600 text-white">
              <Check className="mr-1 h-3 w-3" />
              Nella Libreria
            </Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="flex-1 p-4 space-y-2">
        {/* Title */}
        <h3 className="font-semibold text-lg line-clamp-2">{game.title}</h3>

        {/* BGG ID */}
        {game.bggId && (
          <p className="text-sm text-muted-foreground">BGG ID: {game.bggId}</p>
        )}

        {/* Complexity */}
        <p className="text-sm">
          <span className="font-medium">Complessità: </span>
          {complexityStars}
        </p>

        {/* Players & Playtime */}
        <p className="text-sm text-muted-foreground">
          {playersRange} | {playtime}
        </p>

        {/* Description */}
        {game.description && (
          <p className="text-sm text-muted-foreground line-clamp-3">{game.description}</p>
        )}

        {/* Tags (Categories + Mechanics) */}
        {hasExtendedFields(game) && (game.categories.length > 0 || game.mechanics.length > 0) && (
          <div className="flex flex-wrap gap-1 pt-2">
            {game.categories.slice(0, 2).map((cat) => (
              <Badge key={cat.id} variant="secondary" className="text-xs">
                {cat.name}
              </Badge>
            ))}
            {game.mechanics.slice(0, 2).map((mech) => (
              <Badge key={mech.id} variant="outline" className="text-xs">
                {mech.name}
              </Badge>
            ))}
            {game.categories.length + game.mechanics.length > 4 && (
              <Badge variant="secondary" className="text-xs">
                +{game.categories.length + game.mechanics.length - 4}
              </Badge>
            )}
          </div>
        )}
      </CardContent>

      <CardFooter className="p-4 pt-0">
        {statusLoading ? (
          <Skeleton className="h-10 w-full" />
        ) : inLibrary ? (
          <Button variant="outline" disabled className="w-full">
            <Check className="mr-2 h-4 w-4" />
            Già nella Libreria
          </Button>
        ) : (
          <Button
            onClick={handleAddToLibrary}
            disabled={isAdding || addMutation.isPending}
            className="w-full"
          >
            <Plus className="mr-2 h-4 w-4" />
            {isAdding || addMutation.isPending ? 'Aggiunta in corso...' : 'Aggiungi alla Libreria'}
          </Button>
        )}
      </CardFooter>
    </Card>
  );
}

/**
 * GameCatalogCard Skeleton for loading state
 */
export function GameCatalogCardSkeleton() {
  return (
    <Card className="flex flex-col h-full">
      <Skeleton className="w-full h-48 rounded-t-lg rounded-b-none" />
      <CardContent className="flex-1 p-4 space-y-2">
        <Skeleton className="h-6 w-3/4" />
        <Skeleton className="h-4 w-1/2" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-2/3" />
        <div className="flex gap-1 pt-2">
          <Skeleton className="h-5 w-16" />
          <Skeleton className="h-5 w-16" />
        </div>
      </CardContent>
      <CardFooter className="p-4 pt-0">
        <Skeleton className="h-10 w-full" />
      </CardFooter>
    </Card>
  );
}
