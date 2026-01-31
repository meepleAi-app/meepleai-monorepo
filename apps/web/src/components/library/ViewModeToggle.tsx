/**
 * ViewModeToggle Component (Issue #2866)
 *
 * Toggle between grid and list view modes for the library.
 * Features:
 * - Grid view icon (default)
 * - List view icon
 * - Accessible toggle buttons
 */

'use client';

import React from 'react';

import { Grid3X3, List } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

export type ViewMode = 'grid' | 'list';

export interface ViewModeToggleProps {
  /** Current view mode */
  viewMode: ViewMode;
  /** Callback when view mode changes */
  onViewModeChange: (mode: ViewMode) => void;
  /** Additional className */
  className?: string;
}

export function ViewModeToggle({ viewMode, onViewModeChange, className }: ViewModeToggleProps) {
  return (
    <div className={cn('flex items-center gap-1 rounded-lg border p-1', className)}>
      <Button
        variant={viewMode === 'grid' ? 'secondary' : 'ghost'}
        size="icon"
        className="h-8 w-8"
        onClick={() => onViewModeChange('grid')}
        aria-label="Vista a griglia"
        aria-pressed={viewMode === 'grid'}
      >
        <Grid3X3 className="h-4 w-4" />
      </Button>
      <Button
        variant={viewMode === 'list' ? 'secondary' : 'ghost'}
        size="icon"
        className="h-8 w-8"
        onClick={() => onViewModeChange('list')}
        aria-label="Vista a lista"
        aria-pressed={viewMode === 'list'}
      >
        <List className="h-4 w-4" />
      </Button>
    </div>
  );
}
