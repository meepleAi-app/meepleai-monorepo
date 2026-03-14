'use client';

/**
 * ListPageHeader - Reusable header for list/grid pages
 *
 * Features:
 * - View toggle (grid/list) with localStorage persistence
 * - Sticky quick-filter chips
 * - Result count display
 */

import React, { useCallback, useEffect, useState } from 'react';

import { LayoutGrid, List } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface QuickFilter {
  key: string;
  label: string;
}

export interface ListPageHeaderProps {
  title: string;
  count: number;
  viewMode: 'grid' | 'list';
  onViewModeChange: (mode: 'grid' | 'list') => void;
  filters?: QuickFilter[];
  activeFilter?: string;
  onFilterChange?: (key: string) => void;
  extra?: React.ReactNode;
}

export function useViewPreference(domain: string): ['grid' | 'list', (m: 'grid' | 'list') => void] {
  const storageKey = `meeple-view-${domain}`;
  const [mode, setModeState] = useState<'grid' | 'list'>('grid');

  useEffect(() => {
    if (typeof window === 'undefined') return;
    const stored = localStorage.getItem(storageKey);
    if (stored === 'grid' || stored === 'list') setModeState(stored);
  }, [storageKey]);

  const setMode = useCallback(
    (m: 'grid' | 'list') => {
      setModeState(m);
      if (typeof window !== 'undefined') localStorage.setItem(storageKey, m);
    },
    [storageKey]
  );

  return [mode, setMode];
}

export function ListPageHeader({
  title,
  count,
  viewMode,
  onViewModeChange,
  filters,
  activeFilter,
  onFilterChange,
  extra,
}: ListPageHeaderProps) {
  return (
    <div className="sticky top-14 z-30 bg-background/95 backdrop-blur-sm -mx-4 px-4 sm:-mx-6 sm:px-6 lg:-mx-8 lg:px-8 pb-3 pt-2 border-b border-border/20">
      <div className="flex items-center justify-between gap-3 mb-2">
        <div className="flex items-baseline gap-2 min-w-0">
          <h2 className="text-lg font-semibold truncate">{title}</h2>
          <span className="text-sm text-muted-foreground shrink-0">({count})</span>
        </div>
        <div className="flex items-center gap-1 shrink-0">
          <button
            onClick={() => onViewModeChange('grid')}
            className={cn(
              'p-2 rounded-md transition-colors min-h-[36px] min-w-[36px] flex items-center justify-center',
              viewMode === 'grid' ? 'bg-primary text-primary-foreground' : 'hover:bg-muted text-muted-foreground'
            )}
            aria-label="Grid view"
            aria-pressed={viewMode === 'grid'}
          >
            <LayoutGrid size={16} />
          </button>
          <button
            onClick={() => onViewModeChange('list')}
            className={cn(
              'p-2 rounded-md transition-colors min-h-[36px] min-w-[36px] flex items-center justify-center',
              viewMode === 'list' ? 'bg-primary text-primary-foreground' : 'hover:bg-muted text-muted-foreground'
            )}
            aria-label="List view"
            aria-pressed={viewMode === 'list'}
          >
            <List size={16} />
          </button>
          {extra}
        </div>
      </div>
      {filters && filters.length > 0 && onFilterChange && (
        <div className="flex overflow-x-auto gap-2 flex-nowrap pb-1 -mb-1 scrollbar-hide">
          {filters.map(f => (
            <button
              key={f.key}
              onClick={() => onFilterChange(f.key)}
              className={cn(
                'rounded-full px-3 py-1.5 text-sm whitespace-nowrap transition-colors shrink-0',
                activeFilter === f.key
                  ? 'bg-primary text-primary-foreground'
                  : 'bg-muted/60 text-muted-foreground hover:bg-muted'
              )}
            >
              {f.label}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
