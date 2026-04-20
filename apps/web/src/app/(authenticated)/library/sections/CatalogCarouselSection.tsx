'use client';

import { useCallback, useMemo } from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { useChatPanel, type ChatGameContext } from '@/hooks/useChatPanel';

import { LibraryHubCarousel } from './LibraryHubCarousel';

export interface CatalogGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  /** KB fields from UserLibraryEntry */
  kbIndexedCount?: number;
  kbProcessingCount?: number;
  kbCardCount?: number;
}

interface CatalogCarouselSectionProps {
  games: CatalogGame[];
  totalCount: number;
}

export function CatalogCarouselSection({ games, totalCount }: CatalogCarouselSectionProps) {
  const { open } = useChatPanel();

  const openChat = useCallback(
    (ctx: ChatGameContext) => {
      open(ctx);
    },
    [open]
  );

  const buildCatalogPips = useCallback(
    (game: CatalogGame): ManaPip[] => {
      const indexed = game.kbIndexedCount ?? 0;
      const processing = game.kbProcessingCount ?? 0;

      return [
        {
          entityType: 'kb' as const,
          count: game.kbCardCount ?? 0,
          colorOverride: getKbPipColor({ kbIndexedCount: indexed, kbProcessingCount: processing }),
          ...(indexed > 0
            ? {
                onCreate: () =>
                  openChat({
                    id: game.id,
                    name: game.title,
                    pdfCount: game.kbCardCount ?? 0,
                    kbStatus: 'ready',
                    imageUrl: game.imageUrl,
                  }),
                createLabel: 'Chatta con AI',
              }
            : {}),
        },
      ];
    },
    [openChat]
  );

  const pipsMap = useMemo(
    () => new Map(games.map(g => [g.id, buildCatalogPips(g)])),
    [games, buildCatalogPips]
  );

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
            manaPips={pipsMap.get(game.id)}
          />
        </div>
      ))}
    </LibraryHubCarousel>
  );
}
