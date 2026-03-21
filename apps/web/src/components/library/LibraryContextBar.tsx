'use client';

import { useState } from 'react';

import { Search, ArrowUpDown, LayoutGrid, List } from 'lucide-react';

import { cn } from '@/lib/utils';

const FILTERS = ['Tutti', 'Posseduti', 'Wishlist', 'Prestati'] as const;

export function LibraryContextBar() {
  const [activeFilter, setActiveFilter] = useState<string>('Tutti');
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');

  return (
    <div className="flex items-center gap-2 w-full overflow-x-auto">
      <div className="flex items-center gap-1 shrink-0">
        {FILTERS.map(filter => (
          <button
            key={filter}
            type="button"
            onClick={() => setActiveFilter(filter)}
            className={cn(
              'px-3 py-1 rounded-full text-sm font-medium transition-colors whitespace-nowrap',
              activeFilter === filter
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted/50 text-muted-foreground hover:bg-muted'
            )}
          >
            {filter}
          </button>
        ))}
      </div>
      <div className="flex items-center gap-1 ml-auto shrink-0">
        <button
          type="button"
          className="p-1.5 rounded-lg hover:bg-muted transition-colors"
          aria-label="Ordina"
        >
          <ArrowUpDown className="w-4 h-4 text-muted-foreground" />
        </button>
        <button
          type="button"
          onClick={() => setViewMode(viewMode === 'grid' ? 'list' : 'grid')}
          className="p-1.5 rounded-lg hover:bg-muted transition-colors"
          aria-label={viewMode === 'grid' ? 'Vista lista' : 'Vista griglia'}
        >
          {viewMode === 'grid' ? (
            <List className="w-4 h-4 text-muted-foreground" />
          ) : (
            <LayoutGrid className="w-4 h-4 text-muted-foreground" />
          )}
        </button>
        <div className="relative hidden sm:block">
          <Search className="absolute left-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground pointer-events-none" />
          <input
            type="search"
            placeholder="Cerca..."
            className="pl-7 pr-2 py-1 rounded-full bg-muted/50 border border-border/50 text-sm w-28 focus:outline-none focus:ring-2 focus:ring-primary/30"
          />
        </div>
      </div>
    </div>
  );
}
