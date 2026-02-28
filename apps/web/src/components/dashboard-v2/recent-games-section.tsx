/**
 * RecentGamesSection — Issue #5097, Epic #5094
 *
 * Dashboard section: 3 recently-played games shown as compact list-cards.
 * Links to /library for the full collection.
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { ArrowRight, Gamepad2 } from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import type { UserGameDto } from '@/lib/api/dashboard-client';

// ─── Section header ───────────────────────────────────────────────────────────

function SectionHeader({ onViewAll }: { onViewAll?: () => void }) {
  return (
    <div className="flex items-center justify-between mb-3">
      <h3 className="font-quicksand text-sm font-bold text-foreground">
        🎲 Giochi recenti
      </h3>
      <Link
        href="/library"
        className="flex items-center gap-1 text-xs font-semibold font-nunito text-muted-foreground hover:text-foreground transition-colors"
        onClick={onViewAll}
      >
        Vedi tutti
        <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  );
}

// ─── List card ────────────────────────────────────────────────────────────────

function GameListCard({ game }: { game: UserGameDto }) {
  const lastPlayedLabel = game.lastPlayed
    ? formatDistanceToNow(new Date(game.lastPlayed), {
        addSuffix: true,
        locale: it,
      })
    : null;

  const playersLabel =
    game.minPlayers && game.maxPlayers
      ? `${game.minPlayers}–${game.maxPlayers} giocatori`
      : null;

  return (
    <Link
      href={`/library/${game.id}`}
      className="flex items-center gap-3 px-3 py-2.5 rounded-xl border border-border bg-surface hover:bg-accent/40 transition-colors group"
      style={{ borderLeftWidth: 3, borderLeftColor: 'hsl(25,95%,45%)' }}
    >
      {/* Thumbnail */}
      <div className="w-12 h-12 rounded-lg overflow-hidden bg-muted shrink-0 flex items-center justify-center">
        {game.thumbnailUrl ? (
          <img
            src={game.thumbnailUrl}
            alt={game.title}
            className="w-full h-full object-cover"
          />
        ) : (
          <Gamepad2 className="h-5 w-5 text-muted-foreground" />
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <p className="font-quicksand font-bold text-sm text-foreground truncate group-hover:text-[hsl(25,95%,45%)] transition-colors">
          {game.title}
        </p>
        <p className="text-xs text-muted-foreground font-nunito mt-0.5 truncate">
          {[
            game.playCount > 0 ? `${game.playCount} partite` : null,
            lastPlayedLabel,
            playersLabel,
          ]
            .filter(Boolean)
            .join(' · ')}
        </p>
      </div>

      {/* Badges */}
      <div className="flex flex-col items-end gap-1 shrink-0">
        {game.isOwned && (
          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-[hsl(25,95%,92%)] text-[hsl(25,95%,45%)] border border-[hsl(25,95%,45%)]">
            Libreria
          </span>
        )}
        {game.averageRating && (
          <span className="text-[10px] font-semibold text-muted-foreground">
            ⭐ {game.averageRating.toFixed(1)}
          </span>
        )}
      </div>
    </Link>
  );
}

// ─── Skeleton ────────────────────────────────────────────────────────────────

function GameListCardSkeleton() {
  return (
    <div className="flex items-center gap-3 px-3 py-2.5 rounded-xl border border-border">
      <Skeleton className="w-12 h-12 rounded-lg shrink-0" />
      <div className="flex-1 space-y-2">
        <Skeleton className="h-4 w-3/4" />
        <Skeleton className="h-3 w-1/2" />
      </div>
    </div>
  );
}

// ─── Empty state ──────────────────────────────────────────────────────────────

function EmptyGames() {
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center text-muted-foreground">
      <Gamepad2 className="h-8 w-8 opacity-30" />
      <p className="text-sm font-nunito">Nessun gioco in libreria</p>
      <Link
        href="/library?action=add"
        className="text-xs font-semibold text-[hsl(25,95%,45%)] hover:underline"
      >
        Aggiungi il tuo primo gioco →
      </Link>
    </div>
  );
}

// ─── Public component ─────────────────────────────────────────────────────────

interface RecentGamesSectionProps {
  games: UserGameDto[];
  isLoading: boolean;
}

export function RecentGamesSection({ games, isLoading }: RecentGamesSectionProps) {
  const recent = games.slice(0, 3);

  return (
    <section>
      <SectionHeader />
      <div className="space-y-2">
        {isLoading ? (
          <>
            <GameListCardSkeleton />
            <GameListCardSkeleton />
            <GameListCardSkeleton />
          </>
        ) : recent.length === 0 ? (
          <EmptyGames />
        ) : (
          recent.map((game) => <GameListCard key={game.id} game={game} />)
        )}
      </div>
    </section>
  );
}
