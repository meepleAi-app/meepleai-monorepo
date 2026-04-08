/**
 * RecentLibraryCard Component (Issue #2612)
 * Issue #4858: Migrated to MeepleCard design system
 *
 * Compact card for dashboard "Recently Added" widget.
 * Delegates to MeepleLibraryGameCard adapter so navItems and drawers are wired.
 */

'use client';

import type { UserLibraryEntry } from '@/lib/api';

import { MeepleLibraryGameCard } from './MeepleLibraryGameCard';

export interface RecentLibraryCardProps {
  /** Library entry data */
  game: UserLibraryEntry;
}

/**
 * Compact card for dashboard widget showing recently added games.
 * Links to library page for full management.
 */
export function RecentLibraryCard({ game }: RecentLibraryCardProps) {
  return (
    <div data-testid="recent-library-card">
      <MeepleLibraryGameCard
        game={game}
        variant="compact"
        onConfigureAgent={() => {}}
        onUploadPdf={() => {}}
        onEditNotes={() => {}}
        onRemove={() => {}}
      />
    </div>
  );
}
