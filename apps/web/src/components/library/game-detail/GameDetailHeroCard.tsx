'use client';

/**
 * GameDetailHeroCard — Hero card for game detail page (MeepleCard wrapper)
 *
 * Delegates rendering to MeepleCard hero variant while preserving
 * the same public interface (GameDetailHeroCardProps).
 *
 * Migration: Rewritten in-place from custom glassmorphic card to MeepleCard.
 */

import { useMemo } from 'react';

import { Clock, Gauge, Users } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';

export interface GameDetailHeroCardProps {
  gameDetail: LibraryGameDetail;
}

export function GameDetailHeroCard({ gameDetail }: GameDetailHeroCardProps) {
  const subtitle = useMemo(() => {
    const pub = gameDetail.gamePublisher ?? 'Publisher sconosciuto';
    const year = gameDetail.gameYearPublished ? ` (${gameDetail.gameYearPublished})` : '';
    return `${pub}${year}`;
  }, [gameDetail.gamePublisher, gameDetail.gameYearPublished]);

  const metadata = useMemo(() => {
    const items: MeepleCardMetadata[] = [];

    if (gameDetail.minPlayers && gameDetail.maxPlayers) {
      const players =
        gameDetail.minPlayers === gameDetail.maxPlayers
          ? `${gameDetail.minPlayers}`
          : `${gameDetail.minPlayers}-${gameDetail.maxPlayers}`;
      items.push({ icon: Users, value: players });
    }

    if (gameDetail.playingTimeMinutes) {
      items.push({ icon: Clock, value: `${gameDetail.playingTimeMinutes} min` });
    }

    if (gameDetail.complexityRating) {
      items.push({
        icon: Gauge,
        value: `${gameDetail.complexityRating.toFixed(1)}/5`,
      });
    }

    return items;
  }, [
    gameDetail.minPlayers,
    gameDetail.maxPlayers,
    gameDetail.playingTimeMinutes,
    gameDetail.complexityRating,
  ]);

  return (
    <div className="mx-auto max-w-6xl px-4 py-4">
      <MeepleCard
        entity="game"
        variant="hero"
        title={gameDetail.gameTitle}
        subtitle={subtitle}
        imageUrl={gameDetail.gameImageUrl ?? undefined}
        metadata={metadata}
        rating={gameDetail.averageRating ?? undefined}
        ratingMax={10}
        data-testid="game-detail-hero-card"
      />
    </div>
  );
}
