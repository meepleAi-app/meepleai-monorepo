'use client';

/**
 * GameDetailHeroCard — Compact horizontal hero card for game detail page
 *
 * Always-visible card at the top of the game detail page showing
 * image, title, publisher/year, rating, and key metadata.
 *
 * Glassmorphic design with game entity orange accent.
 */

import { useMemo } from 'react';

import { Clock, Gauge, Star, Users } from 'lucide-react';
import Image from 'next/image';

import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

export interface GameDetailHeroCardProps {
  gameDetail: LibraryGameDetail;
}

export function GameDetailHeroCard({ gameDetail }: GameDetailHeroCardProps) {
  const subtitle = useMemo(() => {
    const pub = gameDetail.gamePublisher ?? 'Publisher sconosciuto';
    const year = gameDetail.gameYearPublished ? ` (${gameDetail.gameYearPublished})` : '';
    return `${pub}${year}`;
  }, [gameDetail.gamePublisher, gameDetail.gameYearPublished]);

  const metaChips = useMemo(() => {
    const items: Array<{ key: string; icon: typeof Users; value: string }> = [];

    if (gameDetail.minPlayers && gameDetail.maxPlayers) {
      const players =
        gameDetail.minPlayers === gameDetail.maxPlayers
          ? `${gameDetail.minPlayers}`
          : `${gameDetail.minPlayers}-${gameDetail.maxPlayers}`;
      items.push({ key: 'players', icon: Users, value: players });
    }

    if (gameDetail.playingTimeMinutes) {
      items.push({ key: 'time', icon: Clock, value: `${gameDetail.playingTimeMinutes} min` });
    }

    if (gameDetail.complexityRating) {
      items.push({
        key: 'complexity',
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
      <div
        className={cn(
          'flex items-center gap-4 rounded-2xl p-4',
          'border-l-4 border-l-[hsl(25,95%,45%)]',
          'border border-white/20 dark:border-border/40',
          'bg-white/70 dark:bg-card/70 backdrop-blur-md',
          'shadow-sm'
        )}
      >
        {/* Game Image */}
        <div className="relative h-16 w-16 flex-shrink-0 overflow-hidden rounded-xl bg-muted">
          {gameDetail.gameImageUrl ? (
            <Image
              src={gameDetail.gameImageUrl}
              alt={gameDetail.gameTitle}
              fill
              className="object-cover"
              sizes="64px"
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center text-2xl opacity-40">
              🎲
            </div>
          )}
        </div>

        {/* Title + Subtitle */}
        <div className="min-w-0 flex-1">
          <h1 className="truncate font-quicksand text-lg font-bold text-foreground">
            {gameDetail.gameTitle}
          </h1>
          <p className="truncate font-nunito text-sm text-muted-foreground">{subtitle}</p>
        </div>

        {/* Metadata Chips */}
        <div className="hidden items-center gap-3 sm:flex">
          {metaChips.map(chip => {
            const Icon = chip.icon;
            return (
              <div key={chip.key} className="flex items-center gap-1 text-sm text-muted-foreground">
                <Icon className="h-3.5 w-3.5" />
                <span className="font-nunito">{chip.value}</span>
              </div>
            );
          })}
        </div>

        {/* Rating */}
        {gameDetail.averageRating != null && (
          <div className="flex flex-shrink-0 items-center gap-1.5 rounded-lg bg-[hsla(25,95%,45%,0.1)] px-3 py-1.5">
            <Star className="h-4 w-4 fill-[hsl(25,95%,45%)] text-[hsl(25,95%,45%)]" />
            <span className="font-quicksand text-base font-bold text-[hsl(25,95%,45%)]">
              {gameDetail.averageRating.toFixed(1)}
            </span>
            <span className="font-nunito text-xs text-muted-foreground">/10</span>
          </div>
        )}
      </div>
    </div>
  );
}
