import { render, screen, fireEvent } from '@testing-library/react';
import { DiffCodePanel } from '../DiffCodePanel';
import { DiffLine, CollapsibleSection } from '../../../lib/diffProcessor';

// Mock child components
vi.mock('../DiffCodeBlock', () => ({
  DiffCodeBlock: ({ line }: { line: DiffLine }) => (
    <div data-testid="diff-code-block" data-line-number={line.lineNumber}>
      {line.content}
    </div>
  ),
}));

vi.mock('../DiffLineNumberGutter', () => ({
  DiffLineNumberGutter: ({ side }: { side: string }) => (
    <div data-testid="diff-line-number-gutter" data-side={side}>
      Line Numbers
    </div>
  ),
}));

vi.mock('../CollapsibleUnchangedSection', () => ({
  CollapsibleUnchangedSection: ({
    section,
    onToggle,
  }: {
    section: CollapsibleSection;
    onToggle: () => void;
  }) => (
    <button
      data-testid="collapsible-section"
      onClick={onToggle}
      data-start-line={section.startLine}
    >
      {section.isCollapsed ? 'Expand' : 'Collapse'} {section.lineCount} lines
    </button>
  ),
}));

          searchQuery=""
        />
      );

      const scrollContainer = screen.getByText('Old Version').parentElement?.nextSibling as HTMLElement;
      if (scrollContainer) {
        fireEvent.scroll(scrollContainer);
      }

      expect(mockOnScroll).not.toHaveBeenCalled();
    });

    it('should sync scroll position when syncScrollTop is provided', () => {
      const { rerender } = render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
          syncScrollTop={0}
        />
      );

      rerender(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
          syncScrollTop={200}
        />
      );

      expect(screen.getByText('Old Version')).toBeInTheDocument();
    });
  });

  describe('Search Query', () => {
    it('should pass search query to child components', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery="test search"
        />
      );

      expect(screen.getAllByTestId('diff-code-block')).toHaveLength(mockLines.length);
    });

    it('should handle empty search query', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(screen.getAllByTestId('diff-code-block')).toHaveLength(mockLines.length);
    });
  });

  describe('Highlighted Change', () => {
    it('should render with highlighted change ID', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
          highlightedChangeId="change-1"
        />
      );

      expect(screen.getByText('Old Version')).toBeInTheDocument();
    });

    it('should render without highlighted change ID', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(screen.getByText('Old Version')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty lines array', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={[]}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(screen.queryByTestId('diff-code-block')).not.toBeInTheDocument();
    });

    it('should handle lines with null line numbers', () => {
      const linesWithNull: DiffLine[] = [
        { lineNumber: null, content: '', type: 'unchanged' },
      ];

      render(
        <DiffCodePanel
          side="old"
          lines={linesWithNull}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      expect(codeBlocks).toHaveLength(1);
    });

    it('should handle very large number of lines', () => {
      const manyLines: DiffLine[] = Array.from({ length: 1000 }, (_, i) => ({
        lineNumber: i + 1,
        content: `Line ${i + 1}`,
        type: 'unchanged' as const,
      }));

      render(
        <DiffCodePanel
          side="old"
          lines={manyLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      expect(codeBlocks).toHaveLength(1000);
    });

    it('should handle collapsed section at start of lines', () => {
      const sectionsAtStart: CollapsibleSection[] = [
        {
          startLine: 1,
          endLine: 2,
          lineCount: 2,
          isCollapsed: true,
        },
      ];

      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={sectionsAtStart}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      expect(codeBlocks.length).toBeLessThan(mockLines.length);
    });

    it('should handle collapsed section at end of lines', () => {
      const sectionsAtEnd: CollapsibleSection[] = [
        {
          startLine: 4,
          endLine: 5,
          lineCount: 2,
          isCollapsed: true,
        },
      ];

      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={sectionsAtEnd}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      expect(codeBlocks.length).toBeLessThan(mockLines.length);
    });
  });

  describe('Panel Styling', () => {
    it('should apply old side class', () => {
      const { container } = render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(container.querySelector('.diff-code-panel--old')).toBeInTheDocument();
    });

    it('should apply new side class', () => {
      const { container } = render(
        <DiffCodePanel
          side="new"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(container.querySelector('.diff-code-panel--new')).toBeInTheDocument();
    });
  });

  describe('Line Rendering Logic', () => {
    it('should render lines in correct order', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      expect(codeBlocks[0]).toHaveTextContent('Line 1');
      expect(codeBlocks[1]).toHaveTextContent('Line 2');
      expect(codeBlocks[2]).toHaveTextContent('Line 3');
    });

    it('should maintain line order with collapsed sections', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={collapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      const firstBlock = codeBlocks[0];
      const lastBlock = codeBlocks[codeBlocks.length - 1];

      expect(firstBlock).toHaveAttribute('data-line-number', '1');
      expect(lastBlock).toHaveAttribute('data-line-number', '5');
    });
  });
});
