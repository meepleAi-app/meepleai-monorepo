/**
 * Shared Game Detail Page
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 *
 * Public page showing full details of a shared game from the community catalog.
 * Uses MeepleCard (hero, flippable) + MeepleInfoCard (readOnly) + ContributorsSection.
 */

'use client';

import { use, useMemo } from 'react';

import { AlertCircle, Clock, Gauge, Users } from 'lucide-react';

import { GameRelationships } from '@/components/game-detail/GameRelationships';
import { ContributorsSection } from '@/components/shared-games/ContributorsSection';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useSharedGame } from '@/hooks/queries';

interface SharedGamePageProps {
  params: Promise<{
    id: string;
  }>;
}

export default function SharedGamePage({ params }: SharedGamePageProps) {
  const { id } = use(params);
  const { data: game, isLoading, error } = useSharedGame(id);

  // Build metadata for MeepleCard
  const metadata = useMemo(() => {
    if (!game) return [];
    const items = [];

    if (game.minPlayers && game.maxPlayers) {
      const players =
        game.minPlayers === game.maxPlayers
          ? `${game.minPlayers}`
          : `${game.minPlayers}-${game.maxPlayers}`;
      items.push({ icon: Users, value: players });
    }

    if (game.playingTimeMinutes) {
      items.push({ icon: Clock, value: `${game.playingTimeMinutes} min` });
    }

    if (game.complexityRating) {
      items.push({ icon: Gauge, value: `${game.complexityRating.toFixed(1)}/5` });
    }

    return items;
  }, [game]);

  // Loading state
  if (isLoading) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="flex flex-col items-center gap-6 lg:flex-row lg:items-start lg:justify-center">
          <Skeleton className="h-[560px] w-full max-w-[420px]" />
          <Skeleton className="h-[560px] w-full max-w-[420px]" />
        </div>
      </div>
    );
  }

  // Error state
  if (error || !game) {
    return (
      <div className="container mx-auto px-4 py-8">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {error?.message || 'Failed to load game details. Please try again.'}
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Cards Section - MeepleCard (hero, flippable) + MeepleInfoCard */}
      <section className="mb-8 flex flex-col items-center justify-center gap-6 lg:flex-row lg:items-start">
        {/* Flippable Game Card */}
        <MeepleCard
          entity="game"
          variant="hero"
          title={game.title}
          subtitle={
            (game.publishers && game.publishers.length > 0 ? game.publishers[0].name : '') +
            (game.yearPublished ? ` (${game.yearPublished})` : '')
          }
          imageUrl={game.imageUrl || undefined}
          rating={game.averageRating ?? undefined}
          ratingMax={10}
          metadata={metadata}
          flippable
          flipData={{
            description: game.description || undefined,
            categories: game.categories,
            mechanics: game.mechanics,
            designers: game.designers,
            publishers: game.publishers,
            complexityRating: game.complexityRating,
            minAge: game.minAge,
          }}
        />

        {/* Info Card - KB & Social (readOnly for public page) */}
        <div className="w-full max-w-[420px] flex-shrink-0 space-y-6">
          <MeepleInfoCard gameId={id} gameTitle={game.title} readOnly />
        </div>
      </section>

      {/* Game Relationships (returns null if none) */}
      <div className="mx-auto max-w-4xl">
        <GameRelationships gameId={id} gameName={game.title} />
      </div>

      {/* Contributors Section */}
      <div className="mx-auto max-w-4xl">
        <ContributorsSection gameId={id} />
      </div>
    </div>
  );
}
