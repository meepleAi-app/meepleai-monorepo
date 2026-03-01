'use client';

/**
 * ShowcaseSidebar — Navigation panel
 *
 * Shows search input, categories, and component list.
 * Links navigate to /showcase/[component-id].
 */

import { useState, useMemo } from 'react';

import { Search, Layers, Gamepad2 } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';

import type { ShowcaseStory, ShowcaseCategory } from './types';

interface ShowcaseSidebarProps {
  stories: ShowcaseStory[];
}

const CATEGORY_ORDER: ShowcaseCategory[] = [
  'Data Display',
  'Navigation',
  'Feedback',
  'Tags',
  'Animations',
  'Gates',
];

export function ShowcaseSidebar({ stories }: ShowcaseSidebarProps) {
  const pathname = usePathname();
  const [query, setQuery] = useState('');

  const filtered = useMemo(() => {
    if (!query.trim()) return stories;
    const q = query.toLowerCase();
    return stories.filter(
      (s) => s.title.toLowerCase().includes(q) || s.category.toLowerCase().includes(q)
    );
  }, [stories, query]);

  const grouped = useMemo(() => {
    const map = new Map<ShowcaseCategory, ShowcaseStory[]>();
    for (const cat of CATEGORY_ORDER) {
      const items = filtered.filter((s) => s.category === cat);
      if (items.length > 0) map.set(cat, items);
    }
    return map;
  }, [filtered]);

  return (
    <div className="flex h-full flex-col">
      {/* Header */}
      <div className="border-b border-border/60 px-4 py-3">
        <Link
          href="/showcase"
          className="flex items-center gap-2 text-sm font-semibold text-foreground hover:text-primary transition-colors"
        >
          <Gamepad2 className="h-4 w-4 text-amber-500" />
          <span className="font-quicksand">MeepleAI Showcase</span>
        </Link>
        <p className="mt-0.5 text-[10px] text-muted-foreground">v0.1-dev</p>
      </div>

      {/* Search */}
      <div className="px-3 py-2">
        <div className="relative">
          <Search className="absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-muted-foreground" />
          <input
            type="text"
            placeholder="Search components..."
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            className="h-8 w-full rounded-md border border-border/60 bg-background pl-8 pr-3 text-xs text-foreground placeholder:text-muted-foreground focus:outline-none focus:ring-1 focus:ring-ring"
          />
        </div>
      </div>

      {/* Component Tree */}
      <nav className="flex-1 overflow-y-auto px-2 py-1 space-y-1">
        {grouped.size === 0 ? (
          <p className="px-2 py-4 text-center text-xs text-muted-foreground">No components found</p>
        ) : (
          Array.from(grouped.entries()).map(([category, items]) => (
            <div key={category}>
              <div className="flex items-center gap-1.5 px-2 py-1.5 mt-2 first:mt-0">
                <Layers className="h-3 w-3 text-muted-foreground/60" />
                <span className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground/70">
                  {category}
                </span>
              </div>
              {items.map((story) => {
                const href = `/showcase/${story.id}`;
                const isActive = pathname === href;
                return (
                  <Link
                    key={story.id}
                    href={href}
                    className={cn(
                      'flex items-center rounded-md px-3 py-1.5 text-xs transition-colors',
                      isActive
                        ? 'bg-amber-100 text-amber-900 font-medium dark:bg-amber-900/30 dark:text-amber-200'
                        : 'text-foreground/80 hover:bg-muted hover:text-foreground'
                    )}
                  >
                    {story.title}
                  </Link>
                );
              })}
            </div>
          ))
        )}
      </nav>

      {/* Footer */}
      <div className="border-t border-border/60 px-4 py-2">
        <p className="text-[10px] text-muted-foreground">
          {stories.length} component{stories.length !== 1 ? 's' : ''}
        </p>
      </div>
    </div>
  );
}
