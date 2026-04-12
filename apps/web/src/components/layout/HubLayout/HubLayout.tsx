'use client';

import { GalleryHorizontal, LayoutGrid, List, Search } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface FilterChip {
  id: string;
  label: string;
  count?: number;
}

export interface HubLayoutProps {
  searchPlaceholder?: string;
  filterChips?: FilterChip[];
  activeFilterId?: string;
  onFilterChange?: (id: string) => void;
  searchValue?: string;
  onSearchChange?: (value: string) => void;
  viewMode?: 'grid' | 'list' | 'carousel';
  onViewModeChange?: (mode: 'grid' | 'list' | 'carousel') => void;
  showViewToggle?: boolean;
  topActions?: React.ReactNode;
  children: React.ReactNode;
}

const VIEW_MODES = [
  { mode: 'grid', Icon: LayoutGrid, label: 'Vista griglia' },
  { mode: 'list', Icon: List, label: 'Vista lista' },
  { mode: 'carousel', Icon: GalleryHorizontal, label: 'Vista carosello' },
] as const;

export function HubLayout({
  searchPlaceholder = 'Cerca...',
  filterChips,
  activeFilterId,
  onFilterChange,
  searchValue = '',
  onSearchChange,
  viewMode = 'grid',
  onViewModeChange,
  showViewToggle = false,
  topActions,
  children,
}: HubLayoutProps) {
  return (
    <div className="flex flex-col min-h-0">
      {/* Header area */}
      <div className="flex flex-col gap-2 px-4 pt-3 pb-2">
        {/* Search bar + view toggle row */}
        <div className="flex items-center gap-2">
          {/* Search bar */}
          <div className="relative flex-1">
            <Search
              className="absolute left-3 top-1/2 -translate-y-1/2 text-[var(--nh-text-muted,#94a3b8)] pointer-events-none"
              size={16}
              aria-hidden="true"
            />
            <input
              type="search"
              placeholder={searchPlaceholder}
              value={searchValue}
              onChange={e => onSearchChange?.(e.target.value)}
              aria-label={searchPlaceholder}
              className={cn(
                'w-full h-10 pl-10 pr-4',
                'bg-white/90 border border-black/[0.07] rounded-2xl shadow-sm',
                'text-sm text-[var(--nh-text-primary,#1a1a1a)]',
                'placeholder:text-[var(--nh-text-muted,#94a3b8)]',
                'outline-none focus-visible:ring-2 focus-visible:ring-[var(--nh-text-primary,#1a1a1a)]/20',
                'transition-shadow'
              )}
            />
          </div>

          {/* Optional top actions (e.g. "Nuova sessione" button) */}
          {topActions && <div className="flex items-center shrink-0">{topActions}</div>}

          {/* View mode toggle */}
          {showViewToggle && (
            <div
              className="flex items-center gap-0.5 shrink-0"
              role="group"
              aria-label="Modalità di visualizzazione"
            >
              {VIEW_MODES.map(({ mode, Icon, label }) => (
                <button
                  key={mode}
                  type="button"
                  aria-label={label}
                  aria-pressed={viewMode === mode}
                  onClick={() => onViewModeChange?.(mode)}
                  className={cn(
                    'p-1.5 rounded-lg transition-colors',
                    viewMode === mode
                      ? 'text-[var(--nh-text-primary,#1a1a1a)]'
                      : 'text-[var(--nh-text-muted,#94a3b8)] hover:text-[var(--nh-text-secondary,#5a4a35)]'
                  )}
                >
                  <Icon size={18} />
                </button>
              ))}
            </div>
          )}
        </div>

        {/* Filter chips */}
        {filterChips && filterChips.length > 0 && (
          <div
            className="flex gap-2 overflow-x-auto scrollbar-none py-1"
            role="list"
            aria-label="Filtri"
          >
            {filterChips.map(chip => {
              const isActive = chip.id === activeFilterId;
              return (
                <button
                  key={chip.id}
                  type="button"
                  aria-pressed={isActive}
                  onClick={() => onFilterChange?.(chip.id)}
                  className={cn(
                    'inline-flex items-center shrink-0',
                    'rounded-2xl px-3 py-1 text-xs font-bold font-[Quicksand]',
                    'border transition-colors',
                    isActive
                      ? 'bg-[var(--nh-text-primary,#1a1a1a)] text-white border-transparent'
                      : [
                          'bg-[var(--nh-bg-surface,#fffcf8)]',
                          'border-black/[0.07]',
                          'text-[var(--nh-text-secondary,#5a4a35)]',
                          'hover:border-black/[0.14]',
                        ]
                  )}
                >
                  {chip.label}
                  {chip.count !== undefined && (
                    <span className="ml-1 text-[10px] opacity-70">{chip.count}</span>
                  )}
                </button>
              );
            })}
          </div>
        )}
      </div>

      {/* Content area */}
      <div className="flex-1 min-h-0">{children}</div>
    </div>
  );
}
