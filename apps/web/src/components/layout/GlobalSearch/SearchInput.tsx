/**
 * SearchInput - Expandable search input field
 * Issue #3289 - Phase 3: GlobalSearch Component
 *
 * Features:
 * - Expandable animation on mobile
 * - Always visible on desktop
 * - Clear button
 * - Keyboard navigation
 * - 300ms debounce
 */

'use client';

import { forwardRef, useRef, useEffect } from 'react';
import { Search, X, Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface SearchInputProps {
  /** Current search value */
  value: string;
  /** Value change handler */
  onChange: (value: string) => void;
  /** Called when user submits search */
  onSubmit?: () => void;
  /** Called when input is cleared */
  onClear?: () => void;
  /** Called when escape is pressed */
  onEscape?: () => void;
  /** Whether search is loading results */
  isLoading?: boolean;
  /** Whether input is expanded (mobile) */
  isExpanded?: boolean;
  /** Placeholder text */
  placeholder?: string;
  /** Auto-focus when expanded */
  autoFocus?: boolean;
  /** Additional className */
  className?: string;
}

/**
 * SearchInput Component
 *
 * Search input with clear button, loading state, and keyboard handling.
 */
export const SearchInput = forwardRef<HTMLInputElement, SearchInputProps>(
  (
    {
      value,
      onChange,
      onSubmit,
      onClear,
      onEscape,
      isLoading = false,
      isExpanded = true,
      placeholder = 'Cerca giochi, documenti, sessioni...',
      autoFocus = false,
      className,
    },
    ref
  ) => {
    const inputRef = useRef<HTMLInputElement>(null);

    // Forward ref or use internal ref
    const actualRef = (ref as React.RefObject<HTMLInputElement>) || inputRef;

    // Auto-focus when expanded
    useEffect(() => {
      if (isExpanded && autoFocus && actualRef.current) {
        actualRef.current.focus();
      }
    }, [isExpanded, autoFocus, actualRef]);

    // Handle key events
    const handleKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
      if (e.key === 'Escape') {
        e.preventDefault();
        onEscape?.();
      } else if (e.key === 'Enter') {
        e.preventDefault();
        onSubmit?.();
      }
    };

    // Handle clear
    const handleClear = () => {
      onChange('');
      onClear?.();
      actualRef.current?.focus();
    };

    return (
      <div
        className={cn(
          'relative flex items-center',
          'w-full',
          className
        )}
      >
        {/* Search icon */}
        <div className="absolute left-3 flex items-center pointer-events-none">
          {isLoading ? (
            <Loader2
              className="h-4 w-4 text-muted-foreground animate-spin"
              aria-hidden="true"
            />
          ) : (
            <Search
              className="h-4 w-4 text-muted-foreground"
              aria-hidden="true"
            />
          )}
        </div>

        {/* Input field */}
        <input
          ref={actualRef}
          type="search"
          value={value}
          onChange={(e) => onChange(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder={placeholder}
          aria-label={placeholder}
          autoComplete="off"
          autoCorrect="off"
          autoCapitalize="off"
          spellCheck="false"
          className={cn(
            'w-full h-10',
            'pl-10 pr-10',
            'rounded-lg border border-border/50',
            'bg-background/50 dark:bg-muted/30',
            'text-sm text-foreground placeholder:text-muted-foreground',
            'transition-all duration-200',
            'focus:outline-none focus:ring-2 focus:ring-[hsl(262_83%_62%)] focus:ring-offset-1',
            'focus:border-transparent',
            // Remove browser default search styling
            '[&::-webkit-search-cancel-button]:hidden',
            '[&::-webkit-search-decoration]:hidden'
          )}
        />

        {/* Clear button */}
        {value && (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            onClick={handleClear}
            className="absolute right-1 h-8 w-8 hover:bg-muted"
            aria-label="Cancella ricerca"
          >
            <X className="h-4 w-4" aria-hidden="true" />
          </Button>
        )}
      </div>
    );
  }
);

SearchInput.displayName = 'SearchInput';
