/**
 * SearchBar - Search input with keyboard shortcut and clear button
 *
 * Features:
 * - Cmd/Ctrl + K keyboard shortcut to focus
 * - Clear button (X) when query is non-empty
 * - Search icon visual indicator
 * - Accessible with proper ARIA attributes
 *
 * @module components/ui/data-display/entity-list-view/components/search-bar
 *
 * @example
 * ```tsx
 * <SearchBar
 *   value={searchQuery}
 *   onChange={setSearchQuery}
 *   placeholder="Search games..."
 * />
 * ```
 */

'use client';

import React, { useEffect, useRef } from 'react';

import { Search, X } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface SearchBarProps {
  /** Current search value */
  value: string;
  /** Callback when value changes */
  onChange: (value: string) => void;
  /** Placeholder text */
  placeholder?: string;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

/**
 * SearchBar component with keyboard shortcut and clear button
 */
export const SearchBar = React.memo(function SearchBar({
  value,
  onChange,
  placeholder = 'Search...',
  className,
  'data-testid': testId,
}: SearchBarProps) {
  const inputRef = useRef<HTMLInputElement>(null);

  // Keyboard shortcut: Cmd/Ctrl + K to focus search
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        inputRef.current?.focus();
      }
    };

    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, []);

  return (
    <div className={cn('relative w-full', className)} data-testid={testId || 'search-bar'}>
      {/* Search Icon */}
      <Search
        className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground pointer-events-none"
        aria-hidden="true"
      />

      {/* Search Input */}
      <input
        ref={inputRef}
        type="search"
        role="searchbox"
        aria-label="Search"
        value={value}
        onChange={(e) => onChange(e.target.value)}
        placeholder={placeholder}
        className={cn(
          'w-full h-10 pl-10 pr-10 rounded-lg',
          'bg-muted/50 border border-border/50',
          'text-sm text-foreground placeholder:text-muted-foreground',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
          'transition-colors duration-200'
        )}
      />

      {/* Clear Button */}
      {value && (
        <button
          type="button"
          onClick={() => onChange('')}
          aria-label="Clear search"
          className={cn(
            'absolute right-3 top-1/2 -translate-y-1/2',
            'w-5 h-5 rounded-full',
            'flex items-center justify-center',
            'text-muted-foreground hover:text-foreground',
            'hover:bg-muted',
            'transition-colors duration-200',
            'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
          )}
        >
          <X className="w-4 h-4" />
        </button>
      )}
    </div>
  );
});
