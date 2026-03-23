'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';
import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export interface GameItem {
  id: string;
  title: string;
  subtitle?: string;
  imageUrl?: string;
  mechanics?: Array<{ icon: string; tooltip: string }>;
  state?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' };
  linkedEntities?: Array<{ entityType: MeepleEntityType; count: number }>;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  metadata?: Array<{ icon?: any; label?: string; value?: string }>;
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
      <MeepleCard entity="game" variant="grid" title="" loading={true} />
      <MeepleCard entity="game" variant="grid" title="" loading={true} />
      <MeepleCard entity="game" variant="grid" title="" loading={true} />
    </>
  );
}

// ─── Empty state ──────────────────────────────────────────────────────────────

function EmptyState() {
  return (
    <div className="col-span-full flex flex-col items-center gap-3 py-10 text-center text-muted-foreground">
      <p className="text-sm font-nunito">La tua libreria è vuota</p>
      <Link href="/games" className="text-xs font-semibold text-primary hover:underline">
        Esplora il catalogo →
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
              id={game.id}
              entityId={game.id}
              title={game.title}
              subtitle={game.subtitle}
              imageUrl={game.imageUrl}
              linkedEntities={game.linkedEntities}
              metadata={game.metadata}
              rating={game.rating}
              ratingMax={10}
              showInfoButton
              loading={loading}
            />
          ))
        )}
      </div>
    </section>
  );
}
