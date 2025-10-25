import React from 'react';
import { DiffLine } from '../../lib/diffProcessor';
import { PrismHighlighter } from './PrismHighlighter';

export interface DiffCodeBlockProps {
  line: DiffLine;
  isHighlighted: boolean;
  searchQuery: string;
}

/**
 * Individual line in diff view with syntax highlighting
 * Memoized to prevent unnecessary re-renders on large diffs
 */
export const DiffCodeBlock = React.memo<DiffCodeBlockProps>(
  ({ line, isHighlighted, searchQuery }) => {
    const isEmpty = !line.content && line.lineNumber === null;

    if (isEmpty) {
      // Empty placeholder for added/deleted lines on opposite panel
      return (
        <div className="diff-line diff-line--empty" aria-hidden="true">
          <span className="diff-line-content">&nbsp;</span>
        </div>
      );
    }

    return (
      <div
        className={`diff-line diff-line--${line.type} ${isHighlighted ? 'diff-line--highlighted' : ''}`}
        data-line-number={line.lineNumber}
      >
        <pre className="diff-line-content">
          <PrismHighlighter
            code={line.content}
            language="json"
            lineType={line.type}
          />
        </pre>
      </div>
    );
  },
  (prevProps, nextProps) => {
    // Custom comparison to optimize re-renders
    return (
      prevProps.line.content === nextProps.line.content &&
      prevProps.line.type === nextProps.line.type &&
      prevProps.isHighlighted === nextProps.isHighlighted &&
      prevProps.searchQuery === nextProps.searchQuery
    );
  }
);

DiffCodeBlock.displayName = 'DiffCodeBlock';
