'use client';

import { useLayoutResponsive } from '@/components/layout/LayoutProvider';
import { FilterChipsRow } from '@/components/ui/FilterChipsRow';
import { ViewToggle } from '@/components/ui/ViewToggle';

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

interface LibraryFiltersPanelProps {
  activeFilter: string;
  onFilterChange: (filter: string) => void;
  viewMode: 'grid' | 'list';
  onViewModeChange: (mode: 'grid' | 'list') => void;
}

export function LibraryFiltersPanel({
  activeFilter,
  onFilterChange,
  viewMode,
  onViewModeChange,
}: LibraryFiltersPanelProps) {
  const { isMobile } = useLayoutResponsive();

  return (
    <div className="flex items-center gap-3">
      <div className="min-w-0 flex-1">
        <FilterChipsRow
          chips={LIBRARY_FILTER_CHIPS}
          activeId={activeFilter}
          onSelect={onFilterChange}
        />
      </div>
      {/* ViewToggle — desktop only */}
      {!isMobile && <ViewToggle view={viewMode} onViewChange={onViewModeChange} />}
    </div>
  );
}
