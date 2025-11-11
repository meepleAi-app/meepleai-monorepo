import React, { useRef, useEffect } from 'react';
import { DiffLine, CollapsibleSection } from '../../lib/diffProcessor';
import { DiffCodeBlock } from './DiffCodeBlock';
import { DiffLineNumberGutter } from './DiffLineNumberGutter';
import { CollapsibleUnchangedSection } from './CollapsibleUnchangedSection';

export interface DiffCodePanelProps {
  side: 'old' | 'new';
  lines: DiffLine[];
  collapsibleSections: CollapsibleSection[];
  onToggleSection: (startLine: number) => void;
  searchQuery: string;
  highlightedChangeId?: string;
  onScroll?: (scrollTop: number) => void;
  syncScrollTop?: number;
}

/**
 * Single panel (old or new) in side-by-side diff view
 * Contains line numbers + code blocks with syntax highlighting
 */
export function DiffCodePanel({
  side,
  lines,
  collapsibleSections,
  onToggleSection,
  searchQuery,
  highlightedChangeId,
  onScroll,
  syncScrollTop
}: DiffCodePanelProps) {
  const scrollContainerRef = useRef<HTMLDivElement>(null);

  // Sync scroll from other panel
  useEffect(() => {
    if (syncScrollTop !== undefined && scrollContainerRef.current) {
      scrollContainerRef.current.scrollTop = syncScrollTop;
    }
  }, [syncScrollTop]);

  const handleScroll = () => {
    if (onScroll && scrollContainerRef.current) {
      onScroll(scrollContainerRef.current.scrollTop);
    }
  };

  const isLineInCollapsedSection = (lineNum: number | null): boolean => {
    if (lineNum === null) return false;
    return collapsibleSections.some(
      section =>
        section.isCollapsed &&
        lineNum >= section.startLine &&
        lineNum <= section.endLine
    );
  };

  const renderLines = () => {
    const rendered: React.JSX.Element[] = [];
    let currentCollapsibleSection: CollapsibleSection | null = null;

    lines.forEach((line, index) => {
      // Check if this line starts a collapsible section
      const section = collapsibleSections.find(s => s.startLine === line.lineNumber);

      if (section && section.isCollapsed) {
        // Render collapsible toggle
        rendered.push(
          <CollapsibleUnchangedSection
            key={`section-${section.startLine}`}
            section={section}
            onToggle={() => onToggleSection(section.startLine)}
            side={side}
          />
        );
        currentCollapsibleSection = section;
        return;
      }

      // Check if line is within collapsed section
      if (line.lineNumber !== null && isLineInCollapsedSection(line.lineNumber)) {
        return; // Skip rendering
      }

      // Check if we're exiting a collapsible section
      if (currentCollapsibleSection && line.lineNumber === currentCollapsibleSection.endLine + 1) {
        currentCollapsibleSection = null;
      }

      // Render line
      // NOTE: Highlighting based on highlightedChangeId is not yet implemented
      // This will require mapping highlightedChangeId to specific line numbers
      // and applying appropriate CSS classes for visual highlighting
      const isHighlighted = false;
      rendered.push(
        <DiffCodeBlock
          key={`line-${index}`}
          line={line}
          isHighlighted={isHighlighted}
          searchQuery={searchQuery}
        />
      );
    });

    return rendered;
  };

  return (
    <div className={`diff-code-panel diff-code-panel--${side}`}>
      <div className="diff-panel-header">
        <h3 className="diff-panel-title">
          {side === 'old' ? 'Old Version' : 'New Version'}
        </h3>
      </div>
      <div
        ref={scrollContainerRef}
        className="diff-panel-content"
        onScroll={handleScroll}
      >
        <div className="diff-panel-inner">
          <DiffLineNumberGutter
            lines={lines}
            side={side}
            collapsibleSections={collapsibleSections.filter(s => s.isCollapsed)}
          />
          <div className="diff-code-blocks">
            {renderLines()}
          </div>
        </div>
      </div>
    </div>
  );
}
