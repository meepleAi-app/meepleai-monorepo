'use client';

import { useRouter } from 'next/navigation';

import type { UserGameDto } from '@/lib/api/dashboard-client';

import { BentoWidget, WidgetLabel } from './BentoWidget';
import { C } from './dashboard-colors';

interface LibraryWidgetProps {
  games: UserGameDto[];
  totalCount: number;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}

export function LibraryWidget({
  games,
  totalCount,
  isLoading,
  error,
  onRetry,
}: LibraryWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      className="flex flex-col gap-0"
      onClick={() => router.push('/library')}
    >
      <WidgetLabel>La Tua Libreria</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 animate-pulse"
            >
              <div className="w-7 h-7 rounded-md bg-muted/60 shrink-0" />
              <div className="flex-1 h-3 rounded bg-muted/60" />
              <div className="w-8 h-3 rounded bg-muted/40" />
            </div>
          ))
        ) : error ? (
          <div className="flex flex-col items-center justify-center flex-1 gap-2 py-4 text-center">
            <p className="text-[11px] text-muted-foreground">Errore nel caricamento giochi</p>
            <button
              type="button"
              onClick={e => {
                e.stopPropagation();
                onRetry();
              }}
              className="text-[10px] font-bold px-3 py-1.5 rounded-lg border border-border hover:bg-muted/30 transition-colors"
            >
              Riprova
            </button>
          </div>
        ) : games.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">Nessun gioco in libreria ancora</p>
        ) : (
          games.slice(0, 6).map(game => (
            <div
              key={game.id}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0 group/row"
              onClick={e => {
                e.stopPropagation();
                router.push(`/library/${game.id}`);
              }}
            >
              {(game.thumbnailUrl ?? game.imageUrl) ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={game.thumbnailUrl ?? game.imageUrl ?? ''}
                  alt={game.title}
                  className="w-7 h-7 rounded-md object-cover shrink-0"
                />
              ) : (
                <div
                  className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center text-sm"
                  style={{ background: `${C.game}22` }}
                >
                  🎲
                </div>
              )}
              <span className="font-quicksand font-semibold text-[11px] flex-1 truncate text-foreground group-hover/row:text-primary transition-colors">
                {game.title}
              </span>
              {game.averageRating !== null && game.averageRating !== undefined && (
                <span
                  className="font-mono text-[9px] font-semibold shrink-0"
                  style={{ color: C.game }}
                >
                  ★ {game.averageRating.toFixed(1)}
                </span>
              )}
            </div>
          ))
        )}
      </div>
      <p className="text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>
        Vedi tutti {totalCount} →
      </p>
    </BentoWidget>
  );
}
