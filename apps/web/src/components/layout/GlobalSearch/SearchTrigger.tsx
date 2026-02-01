/**
 * SearchTrigger - Mobile search icon button
 * Issue #3289 - Phase 3: GlobalSearch Component
 *
 * Features:
 * - Icon button for mobile viewports
 * - Expands to full search input when clicked
 * - Keyboard shortcut hint (Ctrl+K)
 * - WCAG 2.1 AA accessible
 */

'use client';

import { forwardRef } from 'react';
import { Search } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export interface SearchTriggerProps {
  /** Click handler to expand search */
  onClick: () => void;
  /** Whether search is expanded */
  isExpanded?: boolean;
  /** Show keyboard shortcut hint */
  showHint?: boolean;
  /** Additional className */
  className?: string;
}

/**
 * SearchTrigger Component
 *
 * Compact search button for mobile or collapsed states.
 * Shows search icon with optional keyboard shortcut hint.
 */
export const SearchTrigger = forwardRef<HTMLButtonElement, SearchTriggerProps>(
  ({ onClick, isExpanded = false, showHint = true, className }, ref) => {
    return (
      <Button
        ref={ref}
        variant="ghost"
        size="sm"
        onClick={onClick}
        aria-label="Apri ricerca"
        aria-expanded={isExpanded}
        className={cn(
          'relative flex items-center gap-2',
          'h-9 px-3',
          'text-muted-foreground hover:text-foreground',
          'transition-colors duration-200',
          className
        )}
      >
        <Search className="h-4 w-4" aria-hidden="true" />

        {/* Desktop: Show placeholder text */}
        <span className="hidden sm:inline text-sm">Cerca...</span>

        {/* Keyboard shortcut hint (desktop only) */}
        {showHint && (
          <kbd
            className={cn(
              'hidden sm:inline-flex items-center gap-1',
              'h-5 px-1.5 rounded border border-border/50',
              'text-[10px] font-medium text-muted-foreground',
              'bg-muted/50'
            )}
          >
            <span className="text-xs">⌘</span>K
          </kbd>
        )}
      </Button>
    );
  }
);

SearchTrigger.displayName = 'SearchTrigger';
