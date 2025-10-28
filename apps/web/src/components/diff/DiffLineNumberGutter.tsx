import React from 'react';
import { DiffLine, CollapsibleSection } from '../../lib/diffProcessor';

export interface DiffLineNumberGutterProps {
  lines: DiffLine[];
  side: 'old' | 'new';
  collapsibleSections: CollapsibleSection[];
}

/**
 * Line number gutter for diff panel
 * Shows line numbers with appropriate styling for diff type
 */
export const DiffLineNumberGutter = React.memo<DiffLineNumberGutterProps>(
  ({ lines, side, collapsibleSections }) => {
    const isLineInCollapsedSection = (lineNum: number | null): boolean => {
      if (lineNum === null) return false;
      return collapsibleSections.some(
        section =>
          section.isCollapsed &&
          lineNum >= section.startLine &&
          lineNum <= section.endLine
      );
    };

    return (
      <div className="diff-line-numbers" aria-label={`${side} version line numbers`}>
        {lines.map((line, index) => {
          const isCollapsed = line.lineNumber !== null && isLineInCollapsedSection(line.lineNumber);

          if (isCollapsed) return null; // Hidden when section is collapsed

          return (
            <div
              key={index}
              className={`diff-line-number diff-line-number--${line.type}`}
              data-line-number={line.lineNumber}
            >
              {line.lineNumber !== null ? line.lineNumber : ''}
            </div>
          );
        })}
      </div>
    );
  }
);

DiffLineNumberGutter.displayName = 'DiffLineNumberGutter';
