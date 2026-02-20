/**
 * RecentLibraryCard Component (Issue #2612)
 * Issue #4858: Migrated to MeepleCard design system
 *
 * Compact MeepleCard for dashboard "Recently Added" widget.
 * Shows cover image, title, favorite badge, and "Added X ago" timestamp.
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Library } from 'lucide-react';
import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/primitives/button';
import type { UserLibraryEntry } from '@/lib/api';

export interface RecentLibraryCardProps {
  /** Library entry data */
  game: UserLibraryEntry;
}

/**
 * Compact card for dashboard widget showing recently added games.
 * Links to library page for full management.
 */
export function RecentLibraryCard({ game }: RecentLibraryCardProps) {
  const addedAgo = formatDistanceToNow(new Date(game.addedAt), {
    addSuffix: true,
    locale: it,
  });

  return (
    <div data-testid="recent-library-card">
      <MeepleCard
        entity="game"
        variant="compact"
        title={game.gameTitle}
        subtitle={`Aggiunto ${addedAgo}`}
        imageUrl={game.gameImageUrl || undefined}
        badge={game.isFavorite ? 'Preferito' : undefined}
        showInfoButton
        infoHref="/library"
        infoTooltip="Gestisci"
      />
    </div>
  );
}
