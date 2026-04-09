'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import { LibraryHubCarousel } from './LibraryHubCarousel';

export interface WishlistGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
}

interface WishlistCarouselSectionProps {
  games: WishlistGame[];
  totalCount: number;
}

export function WishlistCarouselSection({ games, totalCount }: WishlistCarouselSectionProps) {
  if (games.length === 0) return null;

  return (
    <LibraryHubCarousel
      title="La tua wishlist"
      count={totalCount}
      seeAllHref="/library?tab=wishlist"
      seeAllLabel="Gestisci wishlist"
      entity="wishlist"
    >
      {games.map(game => (
        <div key={game.id} className="min-w-[200px] max-w-[200px] shrink-0 snap-start">
          <MeepleCard
            variant="grid"
            entity="game"
            title={game.title}
            subtitle={game.subtitle}
            imageUrl={game.imageUrl}
            rating={game.rating}
            ratingMax={10}
            status="wishlist"
          />
        </div>
      ))}
    </LibraryHubCarousel>
  );
}
