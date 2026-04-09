'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

import { LibraryHubCarousel } from './LibraryHubCarousel';

export interface CatalogGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
}

interface CatalogCarouselSectionProps {
  games: CatalogGame[];
  totalCount: number;
}

export function CatalogCarouselSection({ games, totalCount }: CatalogCarouselSectionProps) {
  if (games.length === 0) return null;

  return (
    <LibraryHubCarousel title="Dal catalogo community" count={totalCount} entity="game">
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
            badge="Catalog"
          />
        </div>
      ))}
    </LibraryHubCarousel>
  );
}
