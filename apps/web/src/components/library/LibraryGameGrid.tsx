'use client';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import { cn } from '@/lib/utils';

// ── Game card renderer ──────────────────────────────────────────────────────

function LibraryGameCard({
  entry,
  variant,
}: {
  entry: UserLibraryEntry;
  variant: 'grid' | 'list';
}) {
  return (
    <MeepleCard
      id={entry.id}
      entity="game"
      variant={variant}
      title={entry.gameTitle}
      subtitle={entry.gamePublisher ?? (entry.isPrivateGame ? 'Gioco personalizzato' : '')}
      imageUrl={entry.gameImageUrl ?? undefined}
      rating={entry.averageRating ?? undefined}
      ratingMax={10}
      status={
        entry.currentState === 'Owned'
          ? 'owned'
          : entry.currentState === 'Wishlist'
            ? 'wishlisted'
            : undefined
      }
      metadata={[
        ...(entry.minPlayers != null && entry.maxPlayers != null
          ? [{ label: 'Giocatori', value: `${entry.minPlayers}-${entry.maxPlayers}` }]
          : []),
        ...(entry.playingTimeMinutes != null
          ? [{ label: 'Durata', value: `${entry.playingTimeMinutes} min` }]
          : []),
      ]}
      data-testid={`library-card-${entry.id}`}
    />
  );
}

// ── Game grid/list container ────────────────────────────────────────────────

interface LibraryGameGridProps {
  items: UserLibraryEntry[];
  effectiveView: 'grid' | 'list';
  emptyMessage: string;
}

export function LibraryGameGrid({ items, effectiveView, emptyMessage }: LibraryGameGridProps) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground py-4">{emptyMessage}</p>;
  }

  return (
    <div
      className={cn(
        effectiveView === 'grid'
          ? 'grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3'
          : 'flex flex-col gap-2'
      )}
    >
      {items.map(entry => (
        <LibraryGameCard key={entry.id} entry={entry} variant={effectiveView} />
      ))}
    </div>
  );
}
