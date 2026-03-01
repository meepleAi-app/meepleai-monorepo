/**
 * SharedLibraryGameCard Component (Issue #2614)
 * Issue #4858: Migrated to MeepleCard design system
 *
 * Read-only game card for public shared library view.
 * Uses MeepleCard glassmorphic styling with entity="game".
 */

'use client';

import { Calendar } from 'lucide-react';

import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import type { SharedLibraryGame } from '@/lib/api/schemas/library.schemas';

interface SharedLibraryGameCardProps {
  game: SharedLibraryGame;
  showNotes?: boolean;
}

export function SharedLibraryGameCard({ game, showNotes = false }: SharedLibraryGameCardProps) {
  const metadata: MeepleCardMetadata[] = [
    ...(game.yearPublished
      ? [{ icon: Calendar, label: `${game.yearPublished}` }]
      : []),
  ];

  return (
    <MeepleCard
      entity="game"
      variant="grid"
      title={game.title}
      subtitle={game.publisher || undefined}
      imageUrl={game.imageUrl || undefined}
      badge={game.isFavorite ? 'Preferito' : undefined}
      metadata={metadata.length > 0 ? metadata : undefined}
      showPreview={showNotes && !!game.notes}
      previewData={
        showNotes && game.notes
          ? { description: game.notes }
          : undefined
      }
    />
  );
}
