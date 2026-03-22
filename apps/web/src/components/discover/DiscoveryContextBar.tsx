'use client';

import { useState } from 'react';

import { Search, SlidersHorizontal, Flame, Clock, Trophy } from 'lucide-react';

import { cn } from '@/lib/utils';

const SORT_OPTIONS = [
  { id: 'trending', label: 'Trending', icon: Flame },
  { id: 'new', label: 'Nuovi', icon: Clock },
  { id: 'top', label: 'Top', icon: Trophy },
] as const;

export function DiscoveryContextBar() {
  const [activeSort, setActiveSort] = useState('trending');

  return (
    <div className="flex items-center gap-2 w-full overflow-x-auto">
      <div className="flex items-center gap-1 shrink-0">
        {SORT_OPTIONS.map(({ id, label, icon: Icon }) => (
          <button
            key={id}
            type="button"
            onClick={() => setActiveSort(id)}
            className={cn(
              'flex items-center gap-1 px-3 py-1 rounded-full text-sm font-medium transition-colors whitespace-nowrap',
              activeSort === id
                ? 'bg-primary text-primary-foreground'
                : 'bg-muted/50 text-muted-foreground hover:bg-muted'
            )}
          >
            <Icon className="w-3.5 h-3.5" />
            {label}
          </button>
        ))}
      </div>
      <button
        type="button"
        className="p-1.5 rounded-lg hover:bg-muted transition-colors shrink-0"
        aria-label="Filtri avanzati"
      >
        <SlidersHorizontal className="w-4 h-4 text-muted-foreground" />
      </button>
      <div className="relative ml-auto shrink-0">
        <Search className="absolute left-2 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-muted-foreground pointer-events-none" />
        <input
          type="search"
          placeholder="Cerca catalogo..."
          className="pl-7 pr-2 py-1 rounded-full bg-muted/50 border border-border/50 text-sm w-32 focus:outline-none focus:ring-2 focus:ring-primary/30"
        />
      </div>
    </div>
  );
}
