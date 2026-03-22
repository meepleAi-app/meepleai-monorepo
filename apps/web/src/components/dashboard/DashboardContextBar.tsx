'use client';

import { useState } from 'react';

import { Dice5, Search, Moon } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

export function DashboardContextBar() {
  const [searchFocused, setSearchFocused] = useState(false);

  return (
    <div className="flex items-center gap-2 w-full">
      {!searchFocused && (
        <>
          <Link
            href="/sessions/new"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-primary/10 text-primary text-sm font-medium hover:bg-primary/20 transition-colors whitespace-nowrap"
          >
            <Dice5 className="w-4 h-4" />
            Nuova Partita
          </Link>
          <Link
            href="/discover"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-muted text-muted-foreground text-sm font-medium hover:bg-muted/80 transition-colors whitespace-nowrap"
          >
            <Search className="w-4 h-4" />
            Cerca Gioco
          </Link>
          <Link
            href="/game-nights/new"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-muted text-muted-foreground text-sm font-medium hover:bg-muted/80 transition-colors whitespace-nowrap"
          >
            <Moon className="w-4 h-4" />
            Game Night
          </Link>
        </>
      )}
      <div className={cn('relative', searchFocused ? 'flex-1' : 'ml-auto')}>
        <Search className="absolute left-2.5 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground pointer-events-none" />
        <input
          type="search"
          placeholder="Cerca..."
          className={cn(
            'pl-8 pr-3 py-1.5 rounded-full bg-muted/50 border border-border/50 text-sm',
            'focus:outline-none focus:ring-2 focus:ring-primary/30 transition-all',
            'motion-reduce:transition-none',
            searchFocused ? 'w-full' : 'w-36'
          )}
          onFocus={() => setSearchFocused(true)}
          onBlur={() => setSearchFocused(false)}
        />
      </div>
    </div>
  );
}
