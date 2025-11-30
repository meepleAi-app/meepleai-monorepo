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

      const scrollContainer = screen.getByText('Old Version').parentElement?.nextSibling as HTMLElement;
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
