'use client';

import { useCallback, useMemo } from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { useChatPanel, type ChatGameContext } from '@/hooks/useChatPanel';

import { LibraryHubCarousel } from './LibraryHubCarousel';

export interface PersonalLibraryGame {
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

interface PersonalLibrarySectionProps {
  games: PersonalLibraryGame[];
  totalCount: number;
  onAddGame: () => void;
}

export function PersonalLibrarySection({
  games,
  totalCount,
  onAddGame,
}: PersonalLibrarySectionProps) {
  const { open } = useChatPanel();

  const openChat = useCallback(
    (ctx: ChatGameContext) => {
      open(ctx);
    },
    [open]
  );

  const buildLibraryPips = useCallback(
    (game: PersonalLibraryGame): ManaPip[] => {
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
    () => new Map(games.map(g => [g.id, buildLibraryPips(g)])),
    [games, buildLibraryPips]
  );

  return (
    <LibraryHubCarousel title="Libreria personale" count={totalCount} entity="game">
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
            manaPips={pipsMap.get(game.id)}
          />
        </div>
      ))}
      <button
        type="button"
        aria-label="Aggiungi gioco"
        onClick={onAddGame}
        className="flex min-h-[260px] min-w-[200px] max-w-[200px] shrink-0 snap-start flex-col items-center justify-center gap-2.5 rounded-[20px] border-2 border-dashed border-[rgba(160,120,60,0.3)] bg-[rgba(255,252,248,0.4)] p-6 font-nunito transition-all hover:-translate-y-1 hover:border-[hsl(25_95%_45%)] hover:bg-[rgba(255,252,248,0.8)] hover:shadow-[var(--shadow-warm-md)]"
      >
        <span
          aria-hidden
          className="flex h-12 w-12 items-center justify-center rounded-full text-2xl font-bold"
          style={{
            background: 'hsla(25, 95%, 45%, 0.1)',
            color: 'hsl(25 95% 40%)',
          }}
        >
          ＋
        </span>
        <span className="text-center font-quicksand text-[0.9rem] font-extrabold text-[var(--nh-text-primary)]">
          Aggiungi gioco
        </span>
        <span className="text-center text-[0.7rem] leading-snug text-[var(--nh-text-muted)]">
          Dal catalogo o dal tuo PDF
        </span>
      </button>
    </LibraryHubCarousel>
  );
}
