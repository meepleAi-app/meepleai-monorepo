'use client';

/**
 * GlobalSearch Component
 *
 * Command palette for RAG Dashboard navigation.
 * Triggered with Cmd+K (Mac) or Ctrl+K (Windows).
 */

import React, { useState, useEffect, useCallback, useMemo } from 'react';

import { Search, Command as CommandIcon } from 'lucide-react';

import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
} from '@/components/ui/navigation/command';

import {
  SEARCH_INDEX,
  GROUP_LABELS,
  searchSections,
  getSectionsGrouped,
} from './search-index';

import type { ViewMode } from './types';
import type { SearchableSection, SearchGroup } from './search-index';

// =============================================================================
// Recent Searches Hook
// =============================================================================

const RECENT_SEARCHES_KEY = 'rag-dashboard-recent-searches';
const MAX_RECENT = 5;

function useRecentSearches() {
  const [recent, setRecent] = useState<string[]>([]);

  useEffect(() => {
    try {
      const stored = localStorage.getItem(RECENT_SEARCHES_KEY);
      if (stored) {
        const parsed = JSON.parse(stored);
        if (Array.isArray(parsed)) {
          setRecent(parsed.slice(0, MAX_RECENT));
        }
      }
    } catch {
      // Ignore localStorage errors
    }
  }, []);

  const addRecent = useCallback((sectionId: string) => {
    setRecent((prev) => {
      const updated = [sectionId, ...prev.filter((id) => id !== sectionId)].slice(0, MAX_RECENT);
      try {
        localStorage.setItem(RECENT_SEARCHES_KEY, JSON.stringify(updated));
      } catch {
        // Ignore localStorage errors
      }
      return updated;
    });
  }, []);

  const clearRecent = useCallback(() => {
    setRecent([]);
    try {
      localStorage.removeItem(RECENT_SEARCHES_KEY);
    } catch {
      // Ignore localStorage errors
    }
  }, []);

  return { recent, addRecent, clearRecent };
}

// =============================================================================
// GlobalSearch Component
// =============================================================================

interface GlobalSearchProps {
  /** Current view mode to filter results */
  viewMode?: ViewMode;
  /** Callback when a section is selected */
  onSelectSection?: (sectionId: string) => void;
  /** Callback to open a section's accordion */
  onOpenSection?: (sectionId: string) => void;
}

export function GlobalSearch({
  viewMode = 'technical',
  onSelectSection,
  onOpenSection,
}: GlobalSearchProps) {
  const [open, setOpen] = useState(false);
  const [query, setQuery] = useState('');
  const { recent, addRecent } = useRecentSearches();

  // Handle keyboard shortcut (Cmd+K / Ctrl+K)
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key === 'k') {
        e.preventDefault();
        setOpen((prev) => !prev);
      }
    };

    document.addEventListener('keydown', handleKeyDown);
    return () => document.removeEventListener('keydown', handleKeyDown);
  }, []);

  // Reset query when dialog closes
  useEffect(() => {
    if (!open) {
      setQuery('');
    }
  }, [open]);

  // Search results
  const results = useMemo(
    () => searchSections(query, { viewMode, limit: 15 }),
    [query, viewMode]
  );

  // Group results for display
  const groupedResults = useMemo(() => getSectionsGrouped(results), [results]);

  // Get recent sections
  const recentSections = useMemo(
    () =>
      recent
        .map((id) => SEARCH_INDEX.find((s) => s.id === id))
        .filter((s): s is SearchableSection => {
          if (!s) return false;
          if (viewMode === 'business' && s.technicalOnly) return false;
          if (viewMode === 'technical' && s.businessOnly) return false;
          return true;
        })
        .slice(0, 3),
    [recent, viewMode]
  );

  // Handle section selection
  const handleSelect = useCallback(
    (sectionId: string) => {
      addRecent(sectionId);
      setOpen(false);

      // Scroll to section
      const element = document.getElementById(sectionId);
      if (element) {
        element.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }

      // Open accordion if needed
      if (onOpenSection) {
        onOpenSection(sectionId);
      }

      // Notify parent
      if (onSelectSection) {
        onSelectSection(sectionId);
      }
    },
    [addRecent, onSelectSection, onOpenSection]
  );

  // Render search item
  const renderItem = (section: SearchableSection) => (
    <CommandItem
      key={section.id}
      value={section.id}
      onSelect={() => handleSelect(section.id)}
      className="flex items-center gap-3"
    >
      <span className="text-lg" role="img" aria-hidden="true">
        {section.icon}
      </span>
      <div className="flex flex-col">
        <span className="font-medium">{section.title}</span>
        <span className="text-xs text-muted-foreground">{section.description}</span>
      </div>
    </CommandItem>
  );

  // Check if we have results in a group
  const hasResults = (group: SearchGroup) => groupedResults[group].length > 0;

  return (
    <>
      {/* Search Trigger Button */}
      <button
        onClick={() => setOpen(true)}
        className="flex items-center gap-2 rounded-lg border border-input bg-background px-3 py-2 text-sm text-muted-foreground transition-colors hover:bg-accent hover:text-accent-foreground"
        aria-label="Open search (Cmd+K)"
      >
        <Search className="h-4 w-4" />
        <span className="hidden md:inline">Search sections...</span>
        <kbd className="pointer-events-none hidden h-5 select-none items-center gap-1 rounded border bg-muted px-1.5 font-mono text-[10px] font-medium opacity-100 md:flex">
          <span className="text-xs">⌘</span>K
        </kbd>
      </button>

      {/* Command Dialog */}
      <CommandDialog open={open} onOpenChange={setOpen}>
        <CommandInput
          placeholder="Search sections..."
          value={query}
          onValueChange={setQuery}
        />
        <CommandList>
          <CommandEmpty>
            <div className="flex flex-col items-center gap-2 py-6">
              <CommandIcon className="h-10 w-10 text-muted-foreground" />
              <p className="text-sm text-muted-foreground">No sections found.</p>
              <p className="text-xs text-muted-foreground">
                Try searching for &quot;cost&quot;, &quot;query&quot;, or &quot;architecture&quot;
              </p>
            </div>
          </CommandEmpty>

          {/* Recent Searches (only when no query) */}
          {!query && recentSections.length > 0 && (
            <CommandGroup heading="Recent">
              {recentSections.map(renderItem)}
            </CommandGroup>
          )}

          {/* Search Results by Group */}
          {hasResults('understand') && (
            <CommandGroup heading={`${GROUP_LABELS.understand.icon} ${GROUP_LABELS.understand.label}`}>
              {groupedResults.understand.map(renderItem)}
            </CommandGroup>
          )}

          {hasResults('explore') && (
            <CommandGroup heading={`${GROUP_LABELS.explore.icon} ${GROUP_LABELS.explore.label}`}>
              {groupedResults.explore.map(renderItem)}
            </CommandGroup>
          )}

          {hasResults('compare') && (
            <CommandGroup heading={`${GROUP_LABELS.compare.icon} ${GROUP_LABELS.compare.label}`}>
              {groupedResults.compare.map(renderItem)}
            </CommandGroup>
          )}

          {hasResults('build') && (
            <CommandGroup heading={`${GROUP_LABELS.build.icon} ${GROUP_LABELS.build.label}`}>
              {groupedResults.build.map(renderItem)}
            </CommandGroup>
          )}

          {hasResults('optimize') && (
            <CommandGroup heading={`${GROUP_LABELS.optimize.icon} ${GROUP_LABELS.optimize.label}`}>
              {groupedResults.optimize.map(renderItem)}
            </CommandGroup>
          )}
        </CommandList>

        {/* Footer with keyboard hints */}
        <div className="flex items-center justify-between border-t px-3 py-2 text-xs text-muted-foreground">
          <div className="flex items-center gap-4">
            <span className="flex items-center gap-1">
              <kbd className="rounded border bg-muted px-1">↑↓</kbd> Navigate
            </span>
            <span className="flex items-center gap-1">
              <kbd className="rounded border bg-muted px-1">↵</kbd> Select
            </span>
            <span className="flex items-center gap-1">
              <kbd className="rounded border bg-muted px-1">esc</kbd> Close
            </span>
          </div>
          <span>
            {viewMode === 'technical' ? 'Technical' : 'Business'} View
          </span>
        </div>
      </CommandDialog>
    </>
  );
}

export default GlobalSearch;
