'use client';

import { ChevronDown } from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

const COLLECTION_FILTERS = ['Tutti', 'Posseduti', 'Wishlist'] as const;
const PLAYER_FILTERS = ['2', '3', '4', '5+'] as const;
const SORT_OPTIONS = [
  { label: 'A-Z', value: 'az' },
  { label: 'Ultima giocata', value: 'lastPlayed' },
  { label: 'Valutazione', value: 'rating' },
] as const;

export type LibraryCollectionFilter = (typeof COLLECTION_FILTERS)[number];
export type LibrarySortValue = (typeof SORT_OPTIONS)[number]['value'];

interface LibraryFilterBarProps {
  activeFilter: LibraryCollectionFilter;
  onFilterChange: (f: LibraryCollectionFilter) => void;
  activePlayerFilter: string | null;
  onPlayerFilterChange: (f: string | null) => void;
  sortBy: LibrarySortValue;
  onSortChange: (s: LibrarySortValue) => void;
}

export function LibraryFilterBar({
  activeFilter,
  onFilterChange,
  activePlayerFilter,
  onPlayerFilterChange,
  sortBy,
  onSortChange,
}: LibraryFilterBarProps) {
  const currentSort = SORT_OPTIONS.find(s => s.value === sortBy);

  return (
    <div className="flex items-center gap-2 overflow-x-auto pb-1 scrollbar-hide">
      {/* Collection filters */}
      {COLLECTION_FILTERS.map(f => (
        <button
          key={f}
          onClick={() => onFilterChange(f)}
          className={cn(
            'px-3 py-1.5 rounded-full font-semibold text-xs whitespace-nowrap transition-colors shrink-0',
            activeFilter === f
              ? 'bg-[hsl(var(--e-game))] text-white'
              : 'bg-secondary text-muted-foreground hover:text-foreground hover:bg-secondary/80'
          )}
        >
          {f}
        </button>
      ))}

      <div className="w-px h-5 bg-border/50 mx-1 shrink-0" />

      {/* Player count filters */}
      {PLAYER_FILTERS.map(count => (
        <button
          key={count}
          onClick={() => onPlayerFilterChange(activePlayerFilter === count ? null : count)}
          className={cn(
            'px-3 py-1.5 rounded-full font-semibold text-xs whitespace-nowrap transition-colors shrink-0',
            activePlayerFilter === count
              ? 'bg-[hsl(var(--e-player))] text-white'
              : 'bg-secondary text-muted-foreground hover:text-foreground hover:bg-secondary/80'
          )}
        >
          {count}p
        </button>
      ))}

      <div className="w-px h-5 bg-border/50 mx-1 shrink-0" />

      {/* Sort dropdown */}
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button
            variant="ghost"
            size="sm"
            className="h-7 px-3 rounded-full bg-secondary text-muted-foreground hover:text-foreground font-semibold text-xs gap-1 shrink-0"
          >
            {currentSort?.label}
            <ChevronDown className="w-3.5 h-3.5" />
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end" className="min-w-[140px]">
          {SORT_OPTIONS.map(opt => (
            <DropdownMenuItem
              key={opt.value}
              onClick={() => onSortChange(opt.value)}
              className={cn('text-sm', sortBy === opt.value && 'bg-secondary')}
            >
              {opt.label}
            </DropdownMenuItem>
          ))}
        </DropdownMenuContent>
      </DropdownMenu>
    </div>
  );
}
