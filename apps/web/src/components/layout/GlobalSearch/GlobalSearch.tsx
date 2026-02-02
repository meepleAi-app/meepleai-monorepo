/**
 * GlobalSearch - Main search component
 * Issue #3289 - Phase 3: GlobalSearch Component
 *
 * Features:
 * - Mobile: Icon trigger → expandable input
 * - Desktop: Always visible input
 * - Keyboard shortcuts (Ctrl/Cmd + K)
 * - Results dropdown
 * - Recent searches
 * - Keyboard navigation
 */

'use client';

import { useRef, useEffect, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import { useLayout } from '@/components/layout/LayoutProvider';
import { useGlobalSearch } from '@/hooks/useGlobalSearch';
import { cn } from '@/lib/utils';

import { RecentSearches } from './RecentSearches';
import { SearchInput } from './SearchInput';
import { SearchResults, type SearchResult } from './SearchResults';
import { SearchTrigger } from './SearchTrigger';

export interface GlobalSearchProps {
  /** Additional className */
  className?: string;
}

/**
 * GlobalSearch Component
 *
 * Main search component with responsive behavior and keyboard navigation.
 */
export function GlobalSearch({ className }: GlobalSearchProps) {
  const router = useRouter();
  const { responsive } = useLayout();
  const { isMobile } = responsive;

  const containerRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);

  const {
    query,
    setQuery,
    results,
    isLoading,
    recentSearches,
    addRecentSearch,
    removeRecentSearch,
    clearRecentSearches,
    highlightedIndex,
    setHighlightedIndex,
    highlightPrevious,
    highlightNext,
    highlightedResult,
    clearSearch,
    isActive,
    setIsActive,
  } = useGlobalSearch();

  // Handle result selection
  const handleSelect = useCallback(
    (result: SearchResult) => {
      addRecentSearch(query);
      clearSearch();
      router.push(result.href);
    },
    [query, addRecentSearch, clearSearch, router]
  );

  // Handle recent search selection
  const handleRecentSelect = useCallback(
    (recentQuery: string) => {
      setQuery(recentQuery);
      inputRef.current?.focus();
    },
    [setQuery]
  );

  // Handle submit (Enter key)
  const handleSubmit = useCallback(() => {
    if (highlightedResult) {
      handleSelect(highlightedResult);
    } else if (query.trim()) {
      // Navigate to search results page
      addRecentSearch(query);
      router.push(`/search?q=${encodeURIComponent(query)}`);
      clearSearch();
    }
  }, [highlightedResult, query, handleSelect, addRecentSearch, router, clearSearch]);

  // Handle escape
  const handleEscape = useCallback(() => {
    if (query) {
      setQuery('');
    } else {
      setIsActive(false);
      inputRef.current?.blur();
    }
  }, [query, setQuery, setIsActive]);

  // Keyboard shortcut (Cmd/Ctrl + K)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setIsActive(true);
        inputRef.current?.focus();
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, [setIsActive]);

  // Close on click outside
  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (containerRef.current && !containerRef.current.contains(e.target as Node)) {
        setIsActive(false);
      }
    };

    if (isActive) {
      document.addEventListener('mousedown', handleClickOutside);
    }

    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, [isActive, setIsActive]);

  // Handle keyboard navigation
  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (!isActive) return;

      switch (e.key) {
        case 'ArrowDown':
          e.preventDefault();
          highlightNext();
          break;
        case 'ArrowUp':
          e.preventDefault();
          highlightPrevious();
          break;
        case 'Enter':
          e.preventDefault();
          handleSubmit();
          break;
        case 'Escape':
          e.preventDefault();
          handleEscape();
          break;
      }
    },
    [isActive, highlightNext, highlightPrevious, handleSubmit, handleEscape]
  );

  // Show dropdown when active and has query or recent searches
  const showDropdown = isActive && (query || recentSearches.length > 0);

  return (
    <div
      ref={containerRef}
      className={cn('relative', className)}
      onKeyDown={handleKeyDown}
    >
      {/* Mobile: Show trigger when not active */}
      {isMobile && !isActive && (
        <SearchTrigger
          onClick={() => setIsActive(true)}
          isExpanded={isActive}
          showHint={false}
        />
      )}

      {/* Desktop: Always show input, Mobile: Show when active */}
      {(!isMobile || isActive) && (
        <div
          className={cn(
            isMobile && 'fixed inset-x-4 top-4 z-50',
            isMobile && 'bg-background/95 dark:bg-card backdrop-blur-lg',
            isMobile && 'rounded-lg border border-border/50 shadow-lg p-2'
          )}
        >
          <SearchInput
            ref={inputRef}
            value={query}
            onChange={setQuery}
            onSubmit={handleSubmit}
            onEscape={handleEscape}
            isLoading={isLoading}
            autoFocus={isActive}
            className={cn(
              !isMobile && 'w-64 lg:w-80',
              isMobile && 'w-full'
            )}
          />
        </div>
      )}

      {/* Results dropdown */}
      {showDropdown && (
        <div
          className={cn(
            'absolute z-50',
            isMobile
              ? 'fixed inset-x-4 top-[72px]'
              : 'top-full left-0 right-0 mt-2',
            'bg-background/95 dark:bg-card backdrop-blur-lg',
            'border border-border/50 rounded-lg shadow-lg',
            'max-h-[400px] overflow-hidden'
          )}
        >
          {/* Recent searches (when no query) */}
          {!query && recentSearches.length > 0 && (
            <RecentSearches
              searches={recentSearches}
              onSelect={handleRecentSelect}
              onRemove={removeRecentSearch}
              onClearAll={clearRecentSearches}
            />
          )}

          {/* Search results */}
          {query && (
            <SearchResults
              results={results}
              isLoading={isLoading}
              query={query}
              highlightedIndex={highlightedIndex}
              onHighlightChange={setHighlightedIndex}
              onSelect={handleSelect}
            />
          )}
        </div>
      )}

      {/* Mobile overlay backdrop */}
      {isMobile && isActive && (
        <div
          className="fixed inset-0 bg-black/50 z-40"
          onClick={() => setIsActive(false)}
          aria-hidden="true"
        />
      )}
    </div>
  );
}
