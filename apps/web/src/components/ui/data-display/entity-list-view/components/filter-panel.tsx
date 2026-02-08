/**
 * FilterPanel - Filter controls for EntityListView (MVP)
 *
 * Phase 4 MVP: Basic structure with expandable support for future filter types.
 * Full implementation with 4 filter types deferred to issue #3882.
 *
 * @module components/ui/data-display/entity-list-view/components/filter-panel
 */

'use client';

import React from 'react';
import { Filter } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface FilterPanelProps {
  /** Additional CSS classes */
  className?: string;
  /** Test ID */
  'data-testid'?: string;
}

/**
 * FilterPanel component (MVP - expandable)
 *
 * TODO (#3882): Implement full filter system with:
 * - SelectFilter (single/multi)
 * - CheckboxFilter (boolean toggle)
 * - RangeFilter (numeric slider)
 * - DateRangeFilter (date picker)
 */
export const FilterPanel = React.memo(function FilterPanel({
  className,
  'data-testid': testId,
}: FilterPanelProps) {
  return (
    <div
      className={cn('flex items-center gap-2 text-muted-foreground', className)}
      data-testid={testId || 'filter-panel'}
    >
      <Filter className="w-4 h-4" />
      <span className="text-sm">Filters (Phase 4 - Coming soon)</span>
    </div>
  );
});
