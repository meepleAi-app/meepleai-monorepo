/**
 * LibraryToolbar Component
 *
 * Toolbar for the Personal Library page with search input, game count, and view mode toggle.
 */

'use client';

import { Search } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface LibraryToolbarProps {
  /** Total number of games in the library */
  totalCount: number;
  /** Current search query string */
  searchQuery: string;
  /** Callback when search query changes */
  onSearchChange: (query: string) => void;
  /** Additional CSS classes */
  className?: string;
}

/**
 * LibraryToolbar — search + count + controls bar for the personal library page.
 *
 * @example
 * ```tsx
 * <LibraryToolbar
 *   totalCount={42}
 *   searchQuery=""
 *   onSearchChange={setQuery}
 * />
 * ```
 */
export function LibraryToolbar({
  totalCount,
  searchQuery,
  onSearchChange,
  className,
}: LibraryToolbarProps) {
  return (
    <div className={cn('flex items-center gap-3 mb-6', className)} data-testid="library-toolbar">
      {/* Search input */}
      <div className="relative flex-1 max-w-sm">
        <Search
          className="absolute left-2.5 top-1/2 -translate-y-1/2 w-3.5 h-3.5 text-[#8b949e]"
          aria-hidden="true"
        />
        <input
          type="search"
          value={searchQuery}
          onChange={e => onSearchChange(e.target.value)}
          placeholder="Cerca nella libreria..."
          className={cn(
            'w-full pl-8 pr-3 py-1.5 rounded-md',
            'bg-[#161b22] border border-[#30363d]',
            'text-sm text-[#c9d1d9] placeholder:text-[#8b949e]',
            'focus:outline-none focus:ring-1 focus:ring-[#58a6ff] focus:border-[#58a6ff]',
            'transition-colors duration-150'
          )}
          data-testid="library-search"
          aria-label="Cerca nella libreria"
        />
      </div>

      {/* Game count */}
      <span
        className="text-xs text-[#8b949e] whitespace-nowrap"
        data-testid="library-game-count"
        aria-label={`${totalCount} giochi nella libreria`}
      >
        {totalCount} {totalCount === 1 ? 'gioco' : 'giochi'}
      </span>
    </div>
  );
}

export default LibraryToolbar;
