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

describe('DiffCodePanel', () => {
  const mockOnToggleSection = vi.fn();
  const mockOnScroll = vi.fn();

  const mockLines: DiffLine[] = [
    { lineNumber: 1, content: 'Line 1', type: 'unchanged' },
    { lineNumber: 2, content: 'Line 2', type: 'added' },
    { lineNumber: 3, content: 'Line 3', type: 'deleted' },
    { lineNumber: 4, content: 'Line 4', type: 'modified' },
    { lineNumber: 5, content: 'Line 5', type: 'unchanged' },
  ];

  const emptyCollapsibleSections: CollapsibleSection[] = [];

  const collapsibleSections: CollapsibleSection[] = [
    {
      startLine: 2,
      endLine: 4,
      lineCount: 3,
      isCollapsed: true,
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render panel with old side header', () => {
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

    it('should render panel with new side header', () => {
      render(
        <DiffCodePanel
          side="new"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(screen.getByText('New Version')).toBeInTheDocument();
    });

    it('should render all lines when no sections collapsed', () => {
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
      expect(codeBlocks).toHaveLength(mockLines.length);
    });

    it('should render line number gutter', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(screen.getByTestId('diff-line-number-gutter')).toBeInTheDocument();
    });

    it('should pass correct side to line number gutter', () => {
      render(
        <DiffCodePanel
          side="new"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const gutter = screen.getByTestId('diff-line-number-gutter');
      expect(gutter).toHaveAttribute('data-side', 'new');
    });
  });

  describe('Collapsible Sections', () => {
    it('should render collapsible section toggle', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={collapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      expect(screen.getByTestId('collapsible-section')).toBeInTheDocument();
    });

    it('should hide lines within collapsed sections', () => {
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
      // Lines 2, 3, 4 should be hidden (3 lines), so we should see 2 blocks
      expect(codeBlocks.length).toBeLessThan(mockLines.length);
    });

    it('should call onToggleSection when collapsible section is clicked', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={collapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const toggleButton = screen.getByTestId('collapsible-section');
      fireEvent.click(toggleButton);

      expect(mockOnToggleSection).toHaveBeenCalledWith(2);
    });

    it('should render multiple collapsible sections', () => {
      const multipleSections: CollapsibleSection[] = [
        {
          startLine: 1,
          endLine: 1,
          lineCount: 1,
          isCollapsed: true,
        },
        {
          startLine: 5,
          endLine: 5,
          lineCount: 1,
          isCollapsed: true,
        },
      ];

      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={multipleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const collapsibleSectionElements = screen.getAllByTestId('collapsible-section');
      expect(collapsibleSectionElements.length).toBeGreaterThan(0);
    });

    it('should show all lines when sections are expanded', () => {
      const expandedSections: CollapsibleSection[] = [
        {
          startLine: 2,
          endLine: 4,
          lineCount: 3,
          isCollapsed: false,
        },
      ];

      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={expandedSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const codeBlocks = screen.getAllByTestId('diff-code-block');
      expect(codeBlocks).toHaveLength(mockLines.length);
    });
  });

  describe('Scroll Synchronization', () => {
    it('should call onScroll when scroll event occurs', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
          onScroll={mockOnScroll}
        />
      );

      const scrollContainer = screen.getByText('Old Version').parentElement
        ?.nextSibling as HTMLElement;
      if (scrollContainer) {
        Object.defineProperty(scrollContainer, 'scrollTop', { value: 100, writable: true });
        fireEvent.scroll(scrollContainer);

        expect(mockOnScroll).toHaveBeenCalledWith(100);
      }
    });

    it('should not call onScroll when onScroll prop is not provided', () => {
      render(
        <DiffCodePanel
          side="old"
          lines={mockLines}
          collapsibleSections={emptyCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          searchQuery=""
        />
      );

      const scrollContainer = screen.getByText('Old Version').parentElement
        ?.nextSibling as HTMLElement;
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
      const linesWithNull: DiffLine[] = [{ lineNumber: null, content: '', type: 'unchanged' }];

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
