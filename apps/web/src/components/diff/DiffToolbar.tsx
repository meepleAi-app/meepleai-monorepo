import React from 'react';

import { DiffNavigationControls } from './DiffNavigationControls';
import { DiffSearchInput } from './DiffSearchInput';
import { DiffStatistics } from './DiffStatistics';
import { DiffStatistics as DiffStats } from '../../lib/diffProcessor';

export interface DiffToolbarProps {
  statistics: DiffStats;
  searchQuery: string;
  onSearchChange: (query: string) => void;
  currentChangeIndex: number;
  totalChanges: number;
  onNavigatePrev: () => void;
  onNavigateNext: () => void;
  showNavigation: boolean;
  compact?: boolean;
}

/**
 * Toolbar with statistics, search, and navigation controls
 * Positioned above the diff view
 * Updated to use Tailwind classes consistent with shadcn UI
 */
export function DiffToolbar({
  statistics,
  searchQuery,
  onSearchChange,
  currentChangeIndex,
  totalChanges,
  onNavigatePrev,
  onNavigateNext,
  showNavigation,
  compact = false,
}: DiffToolbarProps) {
  return (
    <div
      className={`diff-toolbar flex flex-wrap items-center gap-4 p-4 border-b ${compact ? 'diff-toolbar--compact p-2 gap-2' : ''}`}
    >
      <div className="diff-toolbar-section diff-toolbar-section--stats">
        <DiffStatistics statistics={statistics} compact={compact} />
      </div>

      {showNavigation && (
        <>
          <div className="diff-toolbar-section diff-toolbar-section--search flex-1 min-w-64">
            <DiffSearchInput
              value={searchQuery}
              onChange={onSearchChange}
              matchCount={totalChanges}
            />
          </div>

          <div className="diff-toolbar-section diff-toolbar-section--navigation">
            <DiffNavigationControls
              currentIndex={currentChangeIndex}
              totalChanges={totalChanges}
              onPrev={onNavigatePrev}
              onNext={onNavigateNext}
            />
          </div>
        </>
      )}
    </div>
  );
}
