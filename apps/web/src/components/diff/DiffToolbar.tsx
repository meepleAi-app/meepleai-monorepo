import React from 'react';
import { DiffStatistics as DiffStats } from '../../lib/diffProcessor';
import { DiffStatistics } from './DiffStatistics';
import { DiffSearchInput } from './DiffSearchInput';
import { DiffNavigationControls } from './DiffNavigationControls';

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
  compact = false
}: DiffToolbarProps) {
  return (
    <div className={`diff-toolbar ${compact ? 'diff-toolbar--compact' : ''}`}>
      <div className="diff-toolbar-section diff-toolbar-section--stats">
        <DiffStatistics statistics={statistics} compact={compact} />
      </div>

      {showNavigation && (
        <>
          <div className="diff-toolbar-section diff-toolbar-section--search">
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
