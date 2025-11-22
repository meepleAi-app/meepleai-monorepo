import React, { useState, useCallback } from 'react';
import { ProcessedDiff, CollapsibleSection } from '../../lib/diffProcessor';
import { DiffCodePanel } from './DiffCodePanel';

export interface SideBySideDiffViewProps {
  processedDiff: ProcessedDiff;
  searchQuery: string;
  currentChangeIndex: number;
  collapsibleSections: Map<string, CollapsibleSection>;
  onToggleSection: (sectionKey: string) => void;
  maxHeight?: string;
}

/**
 * Side-by-side diff view with synchronized scrolling
 * Shows old and new versions in parallel columns
 */
export function SideBySideDiffView({
  processedDiff,
  searchQuery,
  currentChangeIndex,
  collapsibleSections,
  onToggleSection,
  maxHeight = '600px'
}: SideBySideDiffViewProps) {
  const [leftScrollTop, setLeftScrollTop] = useState<number>();
  const [rightScrollTop, setRightScrollTop] = useState<number>();
  const [lastScrolledSide, setLastScrolledSide] = useState<'old' | 'new' | null>(null);

  const handleLeftScroll = useCallback((scrollTop: number) => {
    if (lastScrolledSide !== 'old') {
      setLastScrolledSide('old');
      setRightScrollTop(scrollTop);
    }
  }, [lastScrolledSide]);

  const handleRightScroll = useCallback((scrollTop: number) => {
    if (lastScrolledSide !== 'new') {
      setLastScrolledSide('new');
      setLeftScrollTop(scrollTop);
    }
  }, [lastScrolledSide]);

  const oldSections = Array.from(collapsibleSections.values()).filter(
    s => s.startLine <= processedDiff.oldLines.length
  );
  const newSections = Array.from(collapsibleSections.values()).filter(
    s => s.startLine <= processedDiff.newLines.length
  );

  return (
    <div className="side-by-side-diff-view" style={{ maxHeight }}>
      <div className="diff-panels-container">
        <DiffCodePanel
          side="old"
          lines={processedDiff.oldLines}
          collapsibleSections={oldSections}
          onToggleSection={(startLine) => onToggleSection(`old-${startLine}`)}
          searchQuery={searchQuery}
          highlightedChangeId={processedDiff.changes[currentChangeIndex]?.id}
          onScroll={handleLeftScroll}
          syncScrollTop={lastScrolledSide === 'new' ? rightScrollTop : undefined}
        />
        <DiffCodePanel
          side="new"
          lines={processedDiff.newLines}
          collapsibleSections={newSections}
          onToggleSection={(startLine) => onToggleSection(`new-${startLine}`)}
          searchQuery={searchQuery}
          highlightedChangeId={processedDiff.changes[currentChangeIndex]?.id}
          onScroll={handleRightScroll}
          syncScrollTop={lastScrolledSide === 'old' ? leftScrollTop : undefined}
        />
      </div>
    </div>
  );
}
