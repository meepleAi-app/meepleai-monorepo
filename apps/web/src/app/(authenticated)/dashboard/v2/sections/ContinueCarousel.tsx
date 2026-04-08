'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

export interface ContinueCarouselGame {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  players?: string;
  duration?: string;
}

interface ContinueCarouselProps {
  games: ContinueCarouselGame[];
}

export function ContinueCarousel({ games }: ContinueCarouselProps) {
  if (games.length === 0) return null;

  return (
    <section className="mb-7">
      <div className="mb-3.5 flex items-center justify-between">
        <h3 className="flex items-center gap-3 font-quicksand text-[1.1rem] font-extrabold">
          <span
            aria-hidden
            className="inline-block h-[18px] w-1 rounded-sm"
            style={{ background: 'hsl(25 95% 45%)' }}
          />
          Continua a giocare
        </h3>
        <Link
          href="/library"
          className="rounded-lg px-3 py-1.5 text-[0.78rem] font-bold text-[hsl(25_95%_40%)] transition-colors hover:bg-[hsla(25,95%,45%,0.08)]"
        >
          Vedi tutto →
        </Link>
      </div>
      <div className="grid grid-cols-5 gap-4">
        {games.slice(0, 5).map(game => (
          <MeepleCard
            key={game.id}
            variant="grid"
            entity="game"
            title={game.title}
            subtitle={game.subtitle}
            imageUrl={game.imageUrl}
            rating={game.rating}
            metadata={[
              ...(game.players ? [{ icon: '👥', label: game.players }] : []),
              ...(game.duration ? [{ icon: '⏱', label: game.duration }] : []),
            ]}
          />
        ))}
      </div>
    </section>
  );
}
