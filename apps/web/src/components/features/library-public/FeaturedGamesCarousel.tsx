'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

export interface FeaturedGame {
  gameId: string;
  title: string;
  publisher?: string;
  coverUrl?: string;
  averageRating?: number;
  ratingCount?: number;
  playerCount?: { min: number; max: number };
}

interface FeaturedGamesCarouselProps {
  games: FeaturedGame[];
  className?: string;
}

/**
 * Featured games carousel — horizontal scroll list of 4-6 MeepleCard hero/grid.
 *
 * #2208 DS-17-10 sub-issue: NEW primitive for sp3-library-public route.
 * Mockup parity ref: `admin-mockups/design_files/sp3-library-public.jsx` line 304.
 * Cards use entity=game variant grid (standard catalog presentation).
 *
 * NOTE: MeepleCard primitive has no `href` prop. Wrap each card in a
 * <Link> (Next.js client navigation) to make it clickable.
 */
export function FeaturedGamesCarousel({ games, className }: FeaturedGamesCarouselProps) {
  if (games.length === 0) {
    return (
      <p className={cn('text-sm italic text-muted-foreground', className)}>
        Nessun gioco in evidenza al momento.
      </p>
    );
  }

  return (
    <ul
      className={cn(
        'flex gap-4 overflow-x-auto pb-2 [scrollbar-width:thin] [&::-webkit-scrollbar]:h-2',
        className
      )}
      aria-label="Giochi in evidenza"
    >
      {games.map(game => (
        <li key={game.gameId} className="w-[260px] shrink-0 sm:w-[280px]">
          <Link href={`/shared-games/${game.gameId}`} className="block">
            <MeepleCard
              entity="game"
              variant="grid"
              title={game.title}
              subtitle={game.publisher}
              imageUrl={game.coverUrl}
              rating={game.averageRating}
              ratingMax={10}
            />
          </Link>
        </li>
      ))}
    </ul>
  );
}
