'use client';

import { useRouter } from 'next/navigation';

import type { TrendingGameDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

interface TrendingWidgetProps {
  games: TrendingGameDto[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

export function TrendingWidget({ games, isLoading, error, onRetry }: TrendingWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={2}
      accentColor={C.kb}
      className="flex flex-col"
      onClick={() => router.push('/games')}
    >
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      {error ? (
        <div className="flex items-center gap-2 mt-1">
          <p className="text-[10px] text-muted-foreground flex-1">Errore nel caricamento</p>
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              onRetry();
            }}
            className="text-[9px] font-bold px-2 py-1 rounded border border-border hover:bg-muted/30 transition-colors"
          >
            Riprova
          </button>
        </div>
      ) : (
        <div className="flex gap-3 mt-1 overflow-hidden">
          {isLoading
            ? Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="flex flex-col items-center gap-1 shrink-0">
                  <div className="w-9 h-12 rounded-md bg-muted/60 animate-pulse" />
                  <div className="w-9 h-2 rounded bg-muted/40 animate-pulse" />
                </div>
              ))
            : games.slice(0, 6).map(game => (
                <div
                  key={game.gameId}
                  className="flex flex-col items-center gap-1 cursor-pointer shrink-0 group/card"
                  onClick={e => {
                    e.stopPropagation();
                    router.push(`/games/${game.gameId}`);
                  }}
                >
                  {game.thumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={game.thumbnailUrl}
                      alt={game.title}
                      className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all"
                    />
                  ) : (
                    <div
                      className="w-9 h-12 rounded-md flex items-center justify-center text-lg"
                      style={{ background: 'rgba(255,255,255,0.06)' }}
                    >
                      🎲
                    </div>
                  )}
                  <span className="font-quicksand text-[8px] font-bold text-center w-9 truncate">
                    {game.title}
                  </span>
                </div>
              ))}
        </div>
      )}
    </BentoWidget>
  );
}
