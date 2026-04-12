'use client';

import { cn } from '@/lib/utils';

export type LibraryFilterKey = 'all' | 'strategici' | 'familiari' | 'cooperativi' | 'party';
export type LibraryViewMode = 'carousels' | 'grid' | 'list';

interface LibraryFilterBarProps {
  activeFilter: LibraryFilterKey;
  onFilterChange: (filter: LibraryFilterKey) => void;
  activeView: LibraryViewMode;
  onViewChange: (view: LibraryViewMode) => void;
  sortLabel: string;
  onSortClick: () => void;
}

interface FilterChip {
  key: LibraryFilterKey;
  label: string;
}

const FILTERS: FilterChip[] = [
  { key: 'all', label: 'Tutti' },
  { key: 'strategici', label: 'Strategici' },
  { key: 'familiari', label: 'Familiari' },
  { key: 'cooperativi', label: 'Cooperativi' },
  { key: 'party', label: 'Party' },
];

const VIEWS: { key: LibraryViewMode; icon: string; label: string }[] = [
  { key: 'carousels', icon: '▦', label: 'carousels' },
  { key: 'grid', icon: '⊞', label: 'grid' },
  { key: 'list', icon: '≡', label: 'list' },
];

export function LibraryFilterBar({
  activeFilter,
  onFilterChange,
  activeView,
  onViewChange,
  sortLabel,
  onSortClick,
}: LibraryFilterBarProps) {
  return (
    <div className="mb-6 flex flex-wrap items-center gap-2.5 rounded-2xl border border-[var(--nh-border-default)] bg-[var(--nh-bg-elevated)] p-3 px-4 shadow-[var(--shadow-warm-sm)]">
      <span className="mr-1 text-[0.7rem] font-extrabold uppercase tracking-wider text-[var(--nh-text-muted)]">
        Filtra
      </span>
      {FILTERS.map(filter => {
        const active = filter.key === activeFilter;
        return (
          <button
            key={filter.key}
            type="button"
            aria-pressed={active}
            onClick={() => onFilterChange(filter.key)}
            className={cn(
              'rounded-full border px-3 py-1.5 font-nunito text-[0.76rem] font-bold transition-all',
              active
                ? 'border-[hsla(25,95%,45%,0.35)] text-[hsl(25_95%_38%)]'
                : 'border-transparent bg-[rgba(160,120,60,0.08)] text-[var(--nh-text-secondary)] hover:bg-[rgba(160,120,60,0.15)]'
            )}
            style={active ? { background: 'hsla(25, 95%, 45%, 0.12)' } : undefined}
          >
            {filter.label}
          </button>
        );
      })}
      <div className="flex-1" />
      <button
        type="button"
        onClick={onSortClick}
        className="flex items-center gap-1.5 rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] px-3 py-1.5 font-nunito text-[0.76rem] font-bold text-[var(--nh-text-secondary)]"
      >
        <span aria-hidden>⇅</span>
        {sortLabel}
      </button>
      <div className="flex gap-0.5 rounded-[10px] border border-[var(--nh-border-default)] bg-[var(--nh-bg-surface)] p-0.5">
        {VIEWS.map(view => {
          const active = view.key === activeView;
          return (
            <button
              key={view.key}
              type="button"
              aria-label={view.label}
              aria-pressed={active}
              onClick={() => onViewChange(view.key)}
              className={cn(
                'rounded-lg px-2.5 py-1 text-sm transition-all',
                active
                  ? 'bg-white text-[hsl(25_95%_40%)] shadow-[var(--shadow-warm-sm)]'
                  : 'text-[var(--nh-text-muted)] hover:text-[var(--nh-text-primary)]'
              )}
            >
              {view.icon}
            </button>
          );
        })}
      </div>
    </div>
  );
}
