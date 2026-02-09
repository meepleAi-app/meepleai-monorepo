/**
 * CollectionFilters - Inline Filter Chips Component
 * Issue #3649 - User Collection Dashboard Enhancement
 *
 * Filter chips directly above the collection grid:
 * - Category: Dropdown with game categories
 * - Has PDF: Toggle chip (Yes/No/All)
 * - Has Chat: Toggle chip (Yes/No/All)
 * - Players: Range selector (optional, future enhancement)
 *
 * @example
 * ```tsx
 * <CollectionFilters
 *   filters={currentFilters}
 *   categories={availableCategories}
 *   onFilterChange={handleFilterChange}
 * />
 * ```
 */

'use client';

import { motion } from 'framer-motion';
import {
  FileText,
  MessageSquare,
  Tag,
  X,
  ChevronDown,
} from 'lucide-react';

import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import type { CollectionFilters as CollectionFiltersType } from '@/types/collection';

// ============================================================================
// Types
// ============================================================================

export interface CollectionFiltersProps {
  filters: CollectionFiltersType;
  categories?: string[];
  onFilterChange: (filters: CollectionFiltersType) => void;
  className?: string;
}

type ToggleState = boolean | null;

// ============================================================================
// Helper Functions
// ============================================================================

function getToggleLabel(value: ToggleState): string {
  if (value === true) return 'Sì';
  if (value === false) return 'No';
  return 'Tutti';
}

function cycleToggle(current: ToggleState): ToggleState {
  if (current === null) return true;
  if (current === true) return false;
  return null;
}

// ============================================================================
// FilterChip Sub-Component
// ============================================================================

interface FilterChipProps {
  icon: React.ReactNode;
  label: string;
  value: string;
  isActive: boolean;
  onClick: () => void;
  onClear?: () => void;
}

function FilterChip({
  icon,
  label,
  value,
  isActive,
  onClick,
  onClear,
}: FilterChipProps) {
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.95 }}
      animate={{ opacity: 1, scale: 1 }}
      className="flex items-center"
    >
      <Button
        variant={isActive ? 'default' : 'outline'}
        size="sm"
        onClick={onClick}
        className={cn(
          'gap-1.5 h-8 px-3 transition-all',
          isActive && 'pr-2',
          !isActive && 'hover:bg-muted'
        )}
        data-testid={`filter-chip-${label.toLowerCase().replace(/\s+/g, '-')}`}
      >
        {icon}
        <span className="text-xs font-medium">{label}:</span>
        <span className={cn('text-xs', isActive ? 'font-semibold' : 'text-muted-foreground')}>
          {value}
        </span>
        {isActive && onClear && (
          <span
            role="button"
            tabIndex={0}
            onClick={(e) => {
              e.stopPropagation();
              onClear();
            }}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.stopPropagation();
                e.preventDefault();
                onClear();
              }
            }}
            className="ml-1 p-0.5 rounded-full hover:bg-primary-foreground/20 transition-colors cursor-pointer"
            aria-label={`Rimuovi filtro ${label}`}
          >
            <X className="h-3 w-3" />
          </span>
        )}
      </Button>
    </motion.div>
  );
}

// ============================================================================
// CategoryDropdown Sub-Component
// ============================================================================

interface CategoryDropdownProps {
  value: string | null;
  categories: string[];
  onChange: (category: string | null) => void;
}

function CategoryDropdown({ value, categories, onChange }: CategoryDropdownProps) {
  const isActive = value !== null;

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant={isActive ? 'default' : 'outline'}
          size="sm"
          className={cn('gap-1.5 h-8 px-3', !isActive && 'hover:bg-muted')}
          data-testid="filter-chip-category"
        >
          <Tag className="h-3.5 w-3.5" />
          <span className="text-xs font-medium">Categoria:</span>
          <span className={cn('text-xs', isActive ? 'font-semibold' : 'text-muted-foreground')}>
            {value || 'Tutte'}
          </span>
          <ChevronDown className="h-3 w-3 ml-0.5" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="min-w-[140px]">
        <DropdownMenuItem
          onClick={() => onChange(null)}
          className={cn(!value && 'bg-muted')}
        >
          Tutte
        </DropdownMenuItem>
        {categories.map((category) => (
          <DropdownMenuItem
            key={category}
            onClick={() => onChange(category)}
            className={cn(value === category && 'bg-muted')}
          >
            {category}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

// ============================================================================
// CollectionFilters Component
// ============================================================================

const DEFAULT_CATEGORIES = ['Strategy', 'Family', 'Party', 'Abstract', 'Thematic', 'Cooperative'];

export function CollectionFilters({
  filters,
  categories = DEFAULT_CATEGORIES,
  onFilterChange,
  className,
}: CollectionFiltersProps) {
  const handleToggleFilter = (key: 'hasPdf' | 'hasActiveChat') => {
    // eslint-disable-next-line security/detect-object-injection
    const newValue = cycleToggle(filters[key]);
    onFilterChange({
      ...filters,
      [key]: newValue,
    });
  };

  const handleClearFilter = (key: 'hasPdf' | 'hasActiveChat') => {
    onFilterChange({
      ...filters,
      [key]: null,
    });
  };

  const handleCategoryChange = (category: string | null) => {
    onFilterChange({
      ...filters,
      category,
    });
  };

  const hasActiveFilters = filters.hasPdf !== null || filters.hasActiveChat !== null || filters.category !== null;

  return (
    <section
      className={cn('flex flex-wrap items-center gap-2', className)}
      data-testid="collection-filters"
      aria-label="Filtri collezione"
    >
      {/* Category Dropdown */}
      <CategoryDropdown
        value={filters.category}
        categories={categories}
        onChange={handleCategoryChange}
      />

      {/* Has PDF Toggle */}
      <FilterChip
        icon={<FileText className="h-3.5 w-3.5" />}
        label="PDF"
        value={getToggleLabel(filters.hasPdf)}
        isActive={filters.hasPdf !== null}
        onClick={() => handleToggleFilter('hasPdf')}
        onClear={() => handleClearFilter('hasPdf')}
      />

      {/* Has Chat Toggle */}
      <FilterChip
        icon={<MessageSquare className="h-3.5 w-3.5" />}
        label="Chat"
        value={getToggleLabel(filters.hasActiveChat)}
        isActive={filters.hasActiveChat !== null}
        onClick={() => handleToggleFilter('hasActiveChat')}
        onClear={() => handleClearFilter('hasActiveChat')}
      />

      {/* Clear All Filters */}
      {hasActiveFilters && (
        <motion.div
          initial={{ opacity: 0, scale: 0.8 }}
          animate={{ opacity: 1, scale: 1 }}
        >
          <Button
            variant="ghost"
            size="sm"
            onClick={() =>
              onFilterChange({
                hasPdf: null,
                hasActiveChat: null,
                category: null,
              })
            }
            className="h-8 px-2 text-xs text-muted-foreground hover:text-foreground"
            data-testid="clear-all-filters"
          >
            <X className="h-3.5 w-3.5 mr-1" />
            Rimuovi filtri
          </Button>
        </motion.div>
      )}
    </section>
  );
}

export default CollectionFilters;
