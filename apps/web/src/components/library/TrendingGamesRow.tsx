'use client';

import Link from 'next/link';

import { cn } from '@/lib/utils';

interface TrendingGame {
  id: string;
  title: string;
  imageUrl?: string;
  rating: number;
  playerRange: string;
}

interface TrendingGamesRowProps {
  games?: TrendingGame[];
  className?: string;
}

// Display-only fallback data — no IDs (cards link to /games catalog instead)
const FALLBACK_TRENDING: Omit<TrendingGame, 'id'>[] = [
  { title: 'Terraforming Mars', rating: 4.8, playerRange: '1-5' },
  { title: 'Scythe', rating: 4.5, playerRange: '1-5' },
  { title: 'Wingspan', rating: 4.3, playerRange: '1-5' },
  { title: 'Everdell', rating: 4.2, playerRange: '1-4' },
  { title: 'Spirit Island', rating: 4.9, playerRange: '1-4' },
];

export function TrendingGamesRow({ games, className }: TrendingGamesRowProps) {
  const hasRealData = !!games;
  const items = games ?? FALLBACK_TRENDING;

  return (
    <div className={cn('w-full', className)}>
      <div className="mb-3 flex items-center justify-between">
        <h3 className="flex items-center gap-2 font-quicksand text-base font-bold">
          <span>🔥</span> Popolari questa settimana
        </h3>
        <Link href="/library" className="text-sm font-semibold text-primary hover:underline">
          Vedi tutti →
        </Link>
      </div>
      <div className="flex gap-3 overflow-x-auto pb-2 scrollbar-none" role="list">
        {items.map((game, i) => {
          const cardContent = (
            <>
              <div className="relative flex aspect-square items-center justify-center bg-gradient-to-br from-primary/20 to-primary/5 text-2xl">
                <span className="absolute left-1 top-1 flex h-5 w-5 items-center justify-center rounded-md bg-primary font-quicksand text-[10px] font-extrabold text-white">
                  {i + 1}
                </span>
                <span className="text-2xl opacity-50">🎲</span>
              </div>
              <div className="p-2">
                <p className="truncate font-quicksand text-xs font-bold">{game.title}</p>
                <p className="text-[10px] text-muted-foreground">
                  ★ {game.rating} · {game.playerRange}
                </p>
              </div>
            </>
          );

          const cardClass =
            'flex w-[110px] flex-shrink-0 flex-col overflow-hidden rounded-xl border-l-[3px] border-l-primary bg-card shadow-warm-sm transition-all duration-300 hover:-translate-y-1 hover:shadow-warm-md';

          // Real data: link to game detail. Fallback: link to catalog browse.
          const href = hasRealData ? `/library/games/${(game as TrendingGame).id}` : '/library';

          return (
            <Link key={`trending-${i}`} href={href} role="listitem" className={cardClass}>
              {cardContent}
            </Link>
          );
        })}
      </div>
    </div>
  );
}
