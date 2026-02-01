/**
 * SearchResults - Dropdown displaying search results
 * Issue #3289 - Phase 3: GlobalSearch Component
 *
 * Features:
 * - Game, document, and session result types
 * - Keyboard navigation (arrow keys + enter)
 * - Loading and empty states
 * - Result type icons
 */

'use client';

import { forwardRef, useCallback, useEffect, useRef } from 'react';
import { Gamepad2, FileText, MessageSquare, ArrowRight } from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

/**
 * Search result item types
 */
export type SearchResultType = 'game' | 'document' | 'session';

/**
 * Search result item
 */
export interface SearchResult {
  id: string;
  type: SearchResultType;
  title: string;
  subtitle?: string;
  href: string;
  imageUrl?: string;
}

export interface SearchResultsProps {
  /** Search results to display */
  results: SearchResult[];
  /** Whether results are loading */
  isLoading?: boolean;
  /** Current search query (for highlighting) */
  query?: string;
  /** Currently highlighted index (keyboard nav) */
  highlightedIndex?: number;
  /** Called when index changes */
  onHighlightChange?: (index: number) => void;
  /** Called when result is selected */
  onSelect?: (result: SearchResult) => void;
  /** Additional className */
  className?: string;
}

/**
 * Get icon for result type
 */
function getResultIcon(type: SearchResultType) {
  switch (type) {
    case 'game':
      return Gamepad2;
    case 'document':
      return FileText;
    case 'session':
      return MessageSquare;
    default:
      return FileText;
  }
}

/**
 * Get label for result type
 */
function getResultTypeLabel(type: SearchResultType): string {
  switch (type) {
    case 'game':
      return 'Gioco';
    case 'document':
      return 'Documento';
    case 'session':
      return 'Sessione';
    default:
      return '';
  }
}

/**
 * SearchResults Component
 *
 * Dropdown displaying categorized search results with keyboard navigation.
 */
export const SearchResults = forwardRef<HTMLDivElement, SearchResultsProps>(
  (
    {
      results,
      isLoading = false,
      query = '',
      highlightedIndex = -1,
      onHighlightChange,
      onSelect,
      className,
    },
    ref
  ) => {
    const listRef = useRef<HTMLDivElement>(null);

    // Scroll highlighted item into view
    useEffect(() => {
      if (highlightedIndex >= 0 && listRef.current) {
        const items = listRef.current.querySelectorAll('[data-result-item]');
        const highlightedItem = items[highlightedIndex] as HTMLElement;
        if (highlightedItem) {
          highlightedItem.scrollIntoView({ block: 'nearest' });
        }
      }
    }, [highlightedIndex]);

    // Handle result click
    const handleResultClick = useCallback(
      (result: SearchResult) => {
        onSelect?.(result);
      },
      [onSelect]
    );

    // Handle keyboard events (passed from parent)
    const handleResultKeyDown = useCallback(
      (e: React.KeyboardEvent, result: SearchResult, index: number) => {
        if (e.key === 'Enter') {
          e.preventDefault();
          onSelect?.(result);
        }
      },
      [onSelect]
    );

    // Loading state
    if (isLoading) {
      return (
        <div
          ref={ref}
          className={cn(
            'absolute top-full left-0 right-0 mt-2',
            'bg-background/95 dark:bg-card backdrop-blur-lg',
            'border border-border/50 rounded-lg shadow-lg',
            'p-4 text-center',
            className
          )}
        >
          <div className="flex items-center justify-center gap-2 text-muted-foreground">
            <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
            <span className="text-sm">Ricerca in corso...</span>
          </div>
        </div>
      );
    }

    // Empty state
    if (results.length === 0 && query) {
      return (
        <div
          ref={ref}
          className={cn(
            'absolute top-full left-0 right-0 mt-2',
            'bg-background/95 dark:bg-card backdrop-blur-lg',
            'border border-border/50 rounded-lg shadow-lg',
            'p-6 text-center',
            className
          )}
        >
          <p className="text-sm text-muted-foreground">
            Nessun risultato per "{query}"
          </p>
        </div>
      );
    }

    // No results and no query (hidden)
    if (results.length === 0) {
      return null;
    }

    return (
      <div
        ref={ref}
        className={cn(
          'absolute top-full left-0 right-0 mt-2 z-50',
          'bg-background/95 dark:bg-card backdrop-blur-lg',
          'border border-border/50 rounded-lg shadow-lg',
          'max-h-[400px] overflow-y-auto',
          className
        )}
        role="listbox"
        aria-label="Risultati ricerca"
      >
        <div ref={listRef} className="py-2">
          {results.map((result, index) => {
            const Icon = getResultIcon(result.type);
            const isHighlighted = index === highlightedIndex;

            return (
              <Link
                key={result.id}
                href={result.href}
                data-result-item
                role="option"
                aria-selected={isHighlighted}
                onClick={() => handleResultClick(result)}
                onKeyDown={(e) => handleResultKeyDown(e, result, index)}
                onMouseEnter={() => onHighlightChange?.(index)}
                className={cn(
                  'flex items-center gap-3 px-4 py-3',
                  'transition-colors duration-100',
                  isHighlighted
                    ? 'bg-muted/80'
                    : 'hover:bg-muted/50',
                  'focus:outline-none focus:bg-muted/80'
                )}
              >
                {/* Result icon */}
                <div
                  className={cn(
                    'flex items-center justify-center',
                    'h-10 w-10 rounded-lg',
                    'bg-muted/50',
                    result.type === 'game' && 'bg-[hsl(262_83%_62%/0.1)]',
                    result.type === 'document' && 'bg-blue-500/10',
                    result.type === 'session' && 'bg-green-500/10'
                  )}
                >
                  <Icon
                    className={cn(
                      'h-5 w-5',
                      result.type === 'game' && 'text-[hsl(262_83%_62%)]',
                      result.type === 'document' && 'text-blue-500',
                      result.type === 'session' && 'text-green-500'
                    )}
                    aria-hidden="true"
                  />
                </div>

                {/* Result content */}
                <div className="flex-1 min-w-0">
                  <p className="text-sm font-medium text-foreground truncate">
                    {result.title}
                  </p>
                  {result.subtitle && (
                    <p className="text-xs text-muted-foreground truncate">
                      {result.subtitle}
                    </p>
                  )}
                </div>

                {/* Type badge */}
                <span className="text-xs text-muted-foreground px-2 py-0.5 bg-muted/50 rounded">
                  {getResultTypeLabel(result.type)}
                </span>

                {/* Arrow */}
                <ArrowRight
                  className="h-4 w-4 text-muted-foreground opacity-0 group-hover:opacity-100 transition-opacity"
                  aria-hidden="true"
                />
              </Link>
            );
          })}
        </div>
      </div>
    );
  }
);

SearchResults.displayName = 'SearchResults';
