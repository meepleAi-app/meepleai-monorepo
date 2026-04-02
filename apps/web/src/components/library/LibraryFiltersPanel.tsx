'use client';

import { FilterChipsRow } from '@/components/ui/FilterChipsRow';
import { ViewToggle } from '@/components/ui/ViewToggle';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

// ── Filter chip definitions ─────────────────────────────────────────────────

export const LIBRARY_FILTER_CHIPS = [
  { id: 'all', label: 'Tutti' },
  { id: 'recent', label: 'Recenti' },
  { id: 'most-played', label: 'Meno recenti' },
  { id: 'rating', label: 'Rating \u2193' },
  { id: 'players-2-4', label: '2-4 giocatori' },
  { id: 'under-60', label: '< 60 min' },
  { id: 'strategy', label: 'Strategici' },
];

// ── Filter logic ────────────────────────────────────────────────────────────

export function applyFilter(items: UserLibraryEntry[], filterId: string): UserLibraryEntry[] {
  switch (filterId) {
    case 'recent':
      return [...items].sort(
        (a, b) => new Date(b.addedAt).getTime() - new Date(a.addedAt).getTime()
      );
    case 'rating':
      return [...items].sort((a, b) => (b.averageRating ?? 0) - (a.averageRating ?? 0));
    case 'players-2-4':
      return items.filter(
        g => g.minPlayers != null && g.maxPlayers != null && g.minPlayers <= 4 && g.maxPlayers >= 2
      );
    case 'under-60':
      return items.filter(g => g.playingTimeMinutes != null && g.playingTimeMinutes <= 60);
    case 'most-played':
      // playCount not yet on UserLibraryEntry DTO — sort by addedAt (oldest first = likely most played)
      return [...items].sort(
        (a, b) => new Date(a.addedAt).getTime() - new Date(b.addedAt).getTime()
      );
    case 'strategy':
      // category/mechanics not yet on UserLibraryEntry DTO — requires backend GetUserGamesQuery filter
      // For now, return all items (chip acts as no-op until backend wired)
      return items;
    default:
      return items;
  }
}

// ── Component ────────────────────────────────────────────────────────────────

export interface LibraryFiltersPanelProps {
  chips: { id: string; label: string }[];
  activeFilter: string;
  onFilterChange: (filterId: string) => void;
  viewMode: 'grid' | 'list';
  onViewChange: (mode: 'grid' | 'list') => void;
  isMobile: boolean;
}

/**
 * LibraryFiltersPanel — filter chips row + optional view toggle (desktop only).
 */
export function LibraryFiltersPanel({
  chips,
  activeFilter,
  onFilterChange,
  viewMode,
  onViewChange,
  isMobile,
}: LibraryFiltersPanelProps) {
  return (
    <div className="flex items-center gap-3">
      <div className="min-w-0 flex-1">
        <FilterChipsRow chips={chips} activeId={activeFilter} onSelect={onFilterChange} />
      </div>
      {/* ViewToggle — desktop only */}
      {!isMobile && <ViewToggle view={viewMode} onViewChange={onViewChange} />}
    </div>
  );
}
