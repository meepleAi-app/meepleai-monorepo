/**
 * RecentSearches - Recent search queries list
 * Issue #3289 - Phase 3: GlobalSearch Component
 *
 * Features:
 * - Displays last 5 search queries
 * - Clear individual or all
 * - Persisted to localStorage
 * - Click to repeat search
 */

'use client';

import { Clock, X, Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface RecentSearchesProps {
  /** Recent search queries */
  searches: string[];
  /** Called when a search is clicked */
  onSelect: (query: string) => void;
  /** Called to remove a search */
  onRemove: (query: string) => void;
  /** Called to clear all searches */
  onClearAll: () => void;
  /** Additional className */
  className?: string;
}

/**
 * RecentSearches Component
 *
 * Displays recent search history with click-to-search and clear functionality.
 */
export function RecentSearches({
  searches,
  onSelect,
  onRemove,
  onClearAll,
  className,
}: RecentSearchesProps) {
  if (searches.length === 0) {
    return null;
  }

  return (
    <div
      className={cn(
        'py-2',
        className
      )}
    >
      {/* Header */}
      <div className="flex items-center justify-between px-4 py-2">
        <span className="text-xs font-medium text-muted-foreground uppercase tracking-wide">
          Ricerche recenti
        </span>
        <Button
          variant="ghost"
          size="sm"
          onClick={onClearAll}
          className="h-6 px-2 text-xs text-muted-foreground hover:text-destructive"
        >
          <Trash2 className="h-3 w-3 mr-1" aria-hidden="true" />
          Cancella tutto
        </Button>
      </div>

      {/* Search list */}
      <div className="space-y-0.5">
        {searches.map((query) => (
          <div
            key={query}
            className="flex items-center gap-2 px-4 py-2 hover:bg-muted/50 transition-colors group"
          >
            {/* Icon */}
            <Clock
              className="h-4 w-4 text-muted-foreground flex-shrink-0"
              aria-hidden="true"
            />

            {/* Query text (clickable) */}
            <button
              onClick={() => onSelect(query)}
              className="flex-1 text-left text-sm text-foreground hover:text-primary truncate focus:outline-none focus:underline"
            >
              {query}
            </button>

            {/* Remove button */}
            <Button
              variant="ghost"
              size="icon"
              onClick={(e) => {
                e.stopPropagation();
                onRemove(query);
              }}
              className={cn(
                'h-6 w-6 opacity-0 group-hover:opacity-100',
                'transition-opacity duration-150',
                'text-muted-foreground hover:text-destructive'
              )}
              aria-label={`Rimuovi "${query}" dalle ricerche recenti`}
            >
              <X className="h-3 w-3" aria-hidden="true" />
            </Button>
          </div>
        ))}
      </div>
    </div>
  );
}
