'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import { LibraryHubCarousel } from './LibraryHubCarousel';

export interface ContinuePlayingGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  lastPlayedLabel: string;
}

interface ContinuePlayingSectionProps {
  games: ContinuePlayingGame[];
}

export function ContinuePlayingSection({ games }: ContinuePlayingSectionProps) {
  if (games.length === 0) return null;

  return (
    <LibraryHubCarousel title="Continua a giocare" count={games.length} entity="session">
      {games.map(game => (
        <div
          key={game.id}
          className="flex min-w-[200px] max-w-[200px] shrink-0 snap-start flex-col gap-2"
        >
          <MeepleCard
            variant="grid"
            entity="game"
            title={game.title}
            subtitle={game.subtitle}
            imageUrl={game.imageUrl}
            rating={game.rating}
            ratingMax={10}
          />
          <div className="rounded-[12px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] px-3 py-2 text-[0.72rem] font-semibold text-[var(--nh-text-secondary)] shadow-[var(--shadow-warm-sm)]">
            <span aria-hidden className="mr-1.5">
              ▶
            </span>
            {game.lastPlayedLabel}
          </div>
        </div>
      ))}
    </LibraryHubCarousel>
  );
}
