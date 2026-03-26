/**
 * SidebarFilters - Glassmorphic filter sidebar for EntityListView list mode
 *
 * Consolidates search, entity type filter, status filter, and sort
 * into a fixed-width left sidebar (280px desktop, Sheet on mobile).
 *
 * @module components/ui/data-display/entity-list-view/components/sidebar-filters
 * @see Issue #4698
 */

'use client';

import React, { useState } from 'react';

import { Search, X, ChevronDown, Filter, SlidersHorizontal } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetTrigger } from '@/components/ui/sheet';
import { cn } from '@/lib/utils';

import type { SortOption } from '../entity-list-view.types';

// ============================================================================
// Entity Color Map
// ============================================================================

const ENTITY_COLORS: Record<string, string> = {
  game: 'hsl(25, 95%, 45%)',
  agent: 'hsl(38, 92%, 50%)',
  document: 'hsl(210, 40%, 55%)',
  session: 'hsl(240, 60%, 55%)',
  player: 'hsl(262, 83%, 58%)',
  chatSession: 'hsl(220, 80%, 55%)',
  event: 'hsl(350, 89%, 60%)',
};

const ENTITY_LABELS: Record<string, string> = {
  game: 'Games',
  agent: 'Agents',
  document: 'Documents',
  session: 'Sessions',
  player: 'Players',
  chatSession: 'Chats',
  event: 'Events',
};

// ============================================================================
// Types
// ============================================================================

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export interface SidebarFiltersProps<T = any> {
  /** Search query */
  searchQuery: string;
  /** Search change handler */
  onSearchChange: (query: string) => void;
  /** Search placeholder */
  searchPlaceholder?: string;
  /** Enable search */
  searchable?: boolean;
  /** Sort options */
  sortOptions?: SortOption<T>[];
  /** Current sort value */
  currentSort?: string;
  /** Sort change handler */
  onSortChange?: (value: string) => void;
  /** Active filter count */
  activeFilterCount?: number;
  /** Clear filters handler */
  onClearFilters?: () => void;
  /** Entity types to show in entity filter */
  entityTypes?: string[];
  /** Selected entity types */
  selectedEntityTypes?: string[];
  /** Entity type change handler */
  onEntityTypeChange?: (types: string[]) => void;
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

// ============================================================================
// Entity Type Checkbox
// ============================================================================

function EntityTypeCheckbox({
  entityType,
  checked,
  onChange,
}: {
  entityType: string;
  checked: boolean;
  onChange: (checked: boolean) => void;
}) {
  const color = ENTITY_COLORS[entityType] || 'hsl(30, 15%, 50%)';
  const label = ENTITY_LABELS[entityType] || entityType;

  return (
    <label
      className={cn(
        'flex items-center gap-3 py-1.5 px-2 rounded-md cursor-pointer',
        'transition-colors duration-150',
        'hover:bg-muted/40',
        checked && 'bg-muted/30'
      )}
      data-entity={entityType}
    >
      <div className="relative flex items-center">
        <input
          type="checkbox"
          checked={checked}
          onChange={e => onChange(e.target.checked)}
          className="sr-only peer"
          aria-label={`Filter by ${label}`}
        />
        <div
          className={cn(
            'w-4 h-4 rounded border-2 transition-all duration-150',
            'flex items-center justify-center',
            checked ? 'border-transparent' : 'border-muted-foreground/40'
          )}
          style={{
            backgroundColor: checked ? color : 'transparent',
            borderColor: checked ? color : undefined,
          }}
        >
          {checked && (
            <svg className="w-3 h-3 text-white" viewBox="0 0 12 12" fill="none">
              <path
                d="M2 6l3 3 5-5"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
              />
            </svg>
          )}
        </div>
      </div>
      <span
        className={cn(
          'text-sm font-nunito',
          checked ? 'text-foreground font-medium' : 'text-muted-foreground'
        )}
      >
        {label}
      </span>
      <div className="ml-auto w-2 h-2 rounded-full" style={{ backgroundColor: color }} />
    </label>
  );
}

// ============================================================================
// Sidebar Content (shared between desktop and mobile)
// ============================================================================

function SidebarContent<T>({
  searchQuery,
  onSearchChange,
  searchPlaceholder = 'Search...',
  searchable = true,
  sortOptions = [],
  currentSort,
  onSortChange,
  entityTypes = [],
  selectedEntityTypes = [],
  onEntityTypeChange,
  activeFilterCount = 0,
  onClearFilters,
}: SidebarFiltersProps<T>) {
  const [sortOpen, setSortOpen] = useState(false);
  const currentSortOption = sortOptions.find(o => o.value === currentSort) ?? sortOptions[0];

  return (
    <div className="space-y-5">
      {/* Search */}
      {searchable && (
        <div>
          <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2 block font-quicksand">
            Search
          </label>
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground pointer-events-none" />
            <input
              type="search"
              value={searchQuery}
              onChange={e => onSearchChange(e.target.value)}
              placeholder={searchPlaceholder}
              className={cn(
                'w-full h-9 pl-9 pr-8 rounded-lg',
                'bg-white/40 dark:bg-white/10 border border-border/50',
                'text-sm placeholder:text-muted-foreground',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                'backdrop-blur-sm',
                'transition-colors duration-200'
              )}
            />
            {searchQuery && (
              <button
                type="button"
                onClick={() => onSearchChange('')}
                aria-label="Clear search"
                className="absolute right-2 top-1/2 -translate-y-1/2 w-5 h-5 rounded-full flex items-center justify-center text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
              >
                <X className="w-3.5 h-3.5" />
              </button>
            )}
          </div>
        </div>
      )}

      {/* Entity Type Filter */}
      {entityTypes.length > 0 && onEntityTypeChange && (
        <div>
          <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2 block font-quicksand">
            Entity Type
          </label>
          <div className="space-y-0.5">
            {entityTypes.map(type => (
              <EntityTypeCheckbox
                key={type}
                entityType={type}
                checked={selectedEntityTypes.includes(type)}
                onChange={isChecked => {
                  if (isChecked) {
                    onEntityTypeChange([...selectedEntityTypes, type]);
                  } else {
                    onEntityTypeChange(selectedEntityTypes.filter(t => t !== type));
                  }
                }}
              />
            ))}
          </div>
        </div>
      )}

      {/* Sort */}
      {sortOptions.length > 0 && onSortChange && (
        <div>
          <label className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2 block font-quicksand">
            Sort By
          </label>
          <div className="relative">
            <button
              type="button"
              onClick={() => setSortOpen(!sortOpen)}
              className={cn(
                'w-full flex items-center justify-between',
                'h-9 px-3 rounded-lg',
                'bg-white/40 dark:bg-white/10 border border-border/50',
                'text-sm text-foreground',
                'hover:bg-white/60 dark:hover:bg-white/15',
                'backdrop-blur-sm',
                'transition-colors duration-200',
                'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring'
              )}
              aria-haspopup="listbox"
              aria-expanded={sortOpen}
            >
              <span className="font-nunito">{currentSortOption?.label || 'Select...'}</span>
              <ChevronDown
                className={cn(
                  'w-4 h-4 text-muted-foreground transition-transform',
                  sortOpen && 'rotate-180'
                )}
              />
            </button>
            {sortOpen && (
              <div className="absolute left-0 right-0 mt-1 z-50 rounded-lg bg-popover border border-border shadow-lg py-1">
                {sortOptions.map(option => (
                  <button
                    key={option.value}
                    type="button"
                    onClick={() => {
                      onSortChange(option.value);
                      setSortOpen(false);
                    }}
                    className={cn(
                      'w-full text-left px-3 py-1.5 text-sm font-nunito',
                      'transition-colors duration-100',
                      option.value === currentSort
                        ? 'bg-primary/10 text-primary font-medium'
                        : 'text-foreground hover:bg-muted/60'
                    )}
                  >
                    {option.label}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      )}

      {/* Clear All */}
      {(activeFilterCount > 0 || searchQuery) && onClearFilters && (
        <Button
          variant="ghost"
          size="sm"
          onClick={() => {
            onClearFilters();
            onSearchChange('');
          }}
          className="w-full text-xs text-muted-foreground hover:text-foreground"
        >
          <X className="w-3.5 h-3.5 mr-1.5" />
          Clear all filters
          {activeFilterCount > 0 && (
            <span className="ml-1.5 px-1.5 py-0.5 rounded-full bg-muted text-[10px]">
              {activeFilterCount}
            </span>
          )}
        </Button>
      )}
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function SidebarFilters<T>(props: SidebarFiltersProps<T>) {
  const { className, 'data-testid': testId } = props;

  return (
    <>
      {/* Desktop sidebar */}
      <aside
        className={cn('hidden lg:block w-[280px] flex-shrink-0', className)}
        data-testid={testId || 'sidebar-filters'}
      >
        <div
          className={cn(
            'sticky top-4 p-5 rounded-2xl',
            'bg-white/60 dark:bg-white/5',
            'backdrop-blur-xl saturate-[180%]',
            'border border-border/50',
            'shadow-sm'
          )}
        >
          <div className="flex items-center gap-2 mb-5">
            <SlidersHorizontal className="w-4 h-4 text-muted-foreground" />
            <h3 className="text-sm font-semibold font-quicksand text-foreground">Filters</h3>
          </div>
          <SidebarContent {...props} />
        </div>
      </aside>

      {/* Mobile: Sheet trigger */}
      <div className="lg:hidden">
        <Sheet>
          <SheetTrigger asChild>
            <Button variant="outline" size="sm" className="gap-2">
              <Filter className="w-4 h-4" />
              <span>Filters</span>
              {(props.activeFilterCount ?? 0) > 0 && (
                <span className="ml-1 px-1.5 py-0.5 rounded-full bg-primary text-primary-foreground text-xs">
                  {props.activeFilterCount}
                </span>
              )}
            </Button>
          </SheetTrigger>
          <SheetContent className="w-[320px] sm:w-[360px]">
            <SheetHeader>
              <SheetTitle className="font-quicksand">Filters</SheetTitle>
            </SheetHeader>
            <div className="mt-6">
              <SidebarContent {...props} />
            </div>
          </SheetContent>
        </Sheet>
      </div>
    </>
  );
}
