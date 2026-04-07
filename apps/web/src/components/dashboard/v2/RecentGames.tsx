'use client';

import Link from 'next/link';

import {
  MeepleCard,
  MeepleCardSkeleton,
  type MeepleCardMetadata,
} from '@/components/ui/data-display/meeple-card';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface GameItem {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  metadata?: MeepleCardMetadata[];
  rating?: number;
}

export interface RecentGamesProps {
  games: GameItem[];
  loading?: boolean;
  className?: string;
}

// ─── Loading skeletons ────────────────────────────────────────────────────────

function LoadingSkeletons() {
  return (
    <>
      <MeepleCardSkeleton variant="grid" />
      <MeepleCardSkeleton variant="grid" />
      <MeepleCardSkeleton variant="grid" />
    </>
  );
}

// ─── Empty state ──────────────────────────────────────────────────────────────

function EmptyState() {
  return (
    <div className="col-span-full flex flex-col items-center gap-3 py-10 text-center text-muted-foreground">
      <p className="text-sm font-nunito">La tua libreria è vuota</p>
      <Link href="/library" className="text-xs font-semibold text-primary hover:underline">
        Esplora la libreria →
      </Link>
    </div>
  );
}

// ─── Public component ─────────────────────────────────────────────────────────

export function RecentGames({ games, loading = false, className }: RecentGamesProps) {
  return (
    <section data-testid="recent-games" className={cn('', className)}>
      {/* Section header */}
      <div className="flex items-center justify-between mb-3">
        <h3 className="font-quicksand font-bold text-sm uppercase tracking-wide text-foreground">
          📚 Giochi Recenti
        </h3>
        <Link
          href="/library"
          className="text-xs text-primary font-semibold hover:underline transition-colors"
        >
          Vedi tutti →
        </Link>
      </div>

      {/* Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3.5">
        {loading ? (
          <LoadingSkeletons />
        ) : games.length === 0 ? (
          <EmptyState />
        ) : (
          games.map(game => (
            <MeepleCard
              key={game.id}
              entity="game"
              variant="grid"
              title={game.title}
              subtitle={game.subtitle}
              imageUrl={game.imageUrl}
              metadata={game.metadata}
              rating={game.rating}
              ratingMax={10}
            />
          ))
        )}
      </div>
    </section>
  );
}
