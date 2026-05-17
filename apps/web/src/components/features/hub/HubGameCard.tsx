/**
 * HubGameCard — public catalog card variant for `/hub/games` (#1166).
 * Pure presentational. Cover gradient + entity chip + title + rating + publisher.
 * Click → /games/[id] (router push via Next Link).
 */
'use client';

import Link from 'next/link';

import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

export interface HubGameCardProps {
  readonly game: SharedGame;
  readonly onClick?: (id: string) => void;
}

function formatYear(y: number): string {
  return y > 0 ? String(y) : '—';
}

export function HubGameCard({ game, onClick }: HubGameCardProps) {
  const rating = game.averageRating ?? null;
  return (
    <Link
      href={`/games/${game.id}`}
      data-slot="hub-game-card"
      onClick={() => onClick?.(game.id)}
      className="group flex flex-col overflow-hidden rounded-xl border border-border bg-card transition-shadow hover:shadow-md focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-foreground/25"
    >
      <div
        aria-hidden="true"
        className="relative flex aspect-[5/3] items-center justify-center bg-gradient-to-br from-[hsl(var(--c-game)/0.18)] to-[hsl(var(--c-game)/0.04)] text-4xl"
      >
        {game.thumbnailUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img src={game.thumbnailUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          <span>🎲</span>
        )}
        <span className="absolute right-2 top-2 inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-game)/0.95)] px-2 py-0.5 font-mono text-[9px] font-extrabold uppercase tracking-wider text-background">
          🎲 Game
        </span>
        {rating != null && (
          <span className="absolute bottom-2 right-2 inline-flex items-center gap-1 rounded-full bg-card/90 px-2 py-0.5 font-mono text-[10px] font-extrabold text-foreground backdrop-blur">
            ★ {rating.toFixed(1)}
          </span>
        )}
      </div>
      <div className="flex flex-col gap-1 p-3">
        <h3 className="line-clamp-1 font-bold font-[Quicksand] text-sm text-foreground">
          {game.title}
        </h3>
        <div className="font-mono text-[10px] text-muted-foreground">
          {formatYear(game.yearPublished)}
          {game.hasKnowledgeBase && (
            <span className="ml-2 inline-flex items-center rounded bg-[hsl(var(--c-kb)/0.15)] px-1.5 py-0.5 font-bold text-[9px] uppercase tracking-wider text-[hsl(var(--c-kb))]">
              KB
            </span>
          )}
        </div>
      </div>
    </Link>
  );
}
