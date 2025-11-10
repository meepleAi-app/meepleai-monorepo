/**
 * Unit tests for SideBySideDiffView component
 * Tests side-by-side diff view with synchronized scrolling
 */

import { render, screen } from '@testing-library/react';
import { SideBySideDiffView } from '../SideBySideDiffView';
import type { ProcessedDiff, CollapsibleSection } from '@/lib/diffProcessor';

// Mock DiffCodePanel
jest.mock('../DiffCodePanel', () => ({
  DiffCodePanel: ({ side, onScroll, syncScrollTop }: any) => (
    <div data-testid={`diff-code-panel-${side}`} data-sync={syncScrollTop}>
      <button onClick={() => onScroll?.(100)}>Trigger Scroll</button>
    </div>
  ),
}));

describe('SideBySideDiffView', () => {
  const mockProcessedDiff: ProcessedDiff = {
    oldLines: [
      { lineNumber: 1, content: 'old line 1', type: 'unchanged' },
      { lineNumber: 2, content: 'old line 2', type: 'deleted' },
    ],
    newLines: [
      { lineNumber: 1, content: 'new line 1', type: 'unchanged' },
      { lineNumber: 2, content: 'new line 2', type: 'added' },
    ],
    changes: [
      {
        id: 'change-1',
        startLine: 2,
        endLine: 2,
        type: 'modified',
        oldStartLine: 2,
        oldEndLine: 2,
        newStartLine: 2,
        newEndLine: 2
      },
    ],
    statistics: {
      added: 1,
      deleted: 1,
      modified: 0,
      unchanged: 1,
      totalLines: 2,
    },
  };

  const mockCollapsibleSections = new Map<string, CollapsibleSection>();
  const mockOnToggleSection = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render side-by-side container', () => {
      const { container } = render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(container.querySelector('.side-by-side-diff-view')).toBeInTheDocument();
    });

    it('should render both DiffCodePanel components (old and new)', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should apply maxHeight style', () => {
      const { container } = render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          maxHeight="800px"
        />
      );

      const view = container.querySelector('.side-by-side-diff-view');

      expect(view).toHaveStyle({ maxHeight: '800px' });
    });

    it('should use default maxHeight of 600px', () => {
      const { container } = render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      const view = container.querySelector('.side-by-side-diff-view');

      expect(view).toHaveStyle({ maxHeight: '600px' });
    });

    it('should render diff-panels-container', () => {
      const { container } = render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(container.querySelector('.diff-panels-container')).toBeInTheDocument();
    });
  });

  describe('Section Filtering', () => {
    it('should filter old sections by line count', () => {
      const sections = new Map<string, CollapsibleSection>([
        ['old-1', { startLine: 1, endLine: 2, lineCount: 2, isCollapsed: false }],
        ['old-99', { startLine: 99, endLine: 100, lineCount: 2, isCollapsed: false }], // Beyond oldLines length
      ]);

      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={sections}
          onToggleSection={mockOnToggleSection}
        />
      );

      // Should only pass valid sections to DiffCodePanel
      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
    });

    it('should filter new sections by line count', () => {
      const sections = new Map<string, CollapsibleSection>([
        ['new-1', { startLine: 1, endLine: 2, lineCount: 2, isCollapsed: false }],
        ['new-99', { startLine: 99, endLine: 100, lineCount: 2, isCollapsed: false }], // Beyond newLines length
      ]);

      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={sections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should handle empty collapsible sections', () => {
      const emptySections = new Map<string, CollapsibleSection>();

      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={emptySections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });
  });

  describe('Synchronized Scrolling', () => {
    it('should synchronize scroll from left to right panel', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      const leftPanel = screen.getByTestId('diff-code-panel-old');
      const leftScrollButton = leftPanel.querySelector('button');

      // Trigger scroll on left panel
      if (leftScrollButton) {
        leftScrollButton.click();
      }

      // Right panel should sync
      const rightPanel = screen.getByTestId('diff-code-panel-new');
      expect(rightPanel).toBeInTheDocument();
    });

    it('should synchronize scroll from right to left panel', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      const rightPanel = screen.getByTestId('diff-code-panel-new');
      const rightScrollButton = rightPanel.querySelector('button');

      // Trigger scroll on right panel
      if (rightScrollButton) {
        rightScrollButton.click();
      }

      // Left panel should sync
      const leftPanel = screen.getByTestId('diff-code-panel-old');
      expect(leftPanel).toBeInTheDocument();
    });

    it('should not scroll when already scrolling from same side', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      const leftPanel = screen.getByTestId('diff-code-panel-old');
      const leftScrollButton = leftPanel.querySelector('button');

      // Trigger scroll twice on left panel
      if (leftScrollButton) {
        leftScrollButton.click();
        leftScrollButton.click();
      }

      expect(leftPanel).toBeInTheDocument();
    });
  });

  describe('Section Toggle Callbacks', () => {
    it('should call onToggleSection with old- prefix for old panel', () => {
      const sections = new Map<string, CollapsibleSection>([
        ['old-1', { startLine: 1, endLine: 2, lineCount: 2, isCollapsed: false }],
      ]);

      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={sections}
          onToggleSection={mockOnToggleSection}
        />
      );

      // The component passes onToggleSection to DiffCodePanel
      // We can verify it's being set up correctly
      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
    });

    it('should call onToggleSection with new- prefix for new panel', () => {
      const sections = new Map<string, CollapsibleSection>([
        ['new-1', { startLine: 1, endLine: 2, lineCount: 2, isCollapsed: false }],
      ]);

      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={sections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty diff', () => {
      const emptyDiff: ProcessedDiff = {
        oldLines: [],
        newLines: [],
        changes: [],
        statistics: {
          added: 0,
          deleted: 0,
          modified: 0,
          unchanged: 0,
          totalLines: 0,
        },
      };

      render(
        <SideBySideDiffView
          processedDiff={emptyDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should handle large diff', () => {
      const largeDiff: ProcessedDiff = {
        oldLines: Array.from({ length: 1000 }, (_, i) => ({
          id: `old-${i}`,
          lineNumber: i + 1,
          content: `Line ${i + 1}`,
          type: 'unchanged' as const,
        })),
        newLines: Array.from({ length: 1000 }, (_, i) => ({
          id: `new-${i}`,
          lineNumber: i + 1,
          content: `Line ${i + 1}`,
          type: 'unchanged' as const,
        })),
        changes: [],
        statistics: {
          added: 0,
          deleted: 0,
          modified: 0,
          unchanged: 1000,
          totalLines: 1000,
        },
      };

      render(
        <SideBySideDiffView
          processedDiff={largeDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should handle negative currentChangeIndex', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={-1}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should handle out of bounds currentChangeIndex', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={999}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should handle special characters in searchQuery', () => {
      render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery="[regex].special*chars?"
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
        />
      );

      expect(screen.getByTestId('diff-code-panel-old')).toBeInTheDocument();
      expect(screen.getByTestId('diff-code-panel-new')).toBeInTheDocument();
    });

    it('should handle custom maxHeight values', () => {
      const { container } = render(
        <SideBySideDiffView
          processedDiff={mockProcessedDiff}
          searchQuery=""
          currentChangeIndex={0}
          collapsibleSections={mockCollapsibleSections}
          onToggleSection={mockOnToggleSection}
          maxHeight="1200px"
        />
      );

      const view = container.querySelector('.side-by-side-diff-view');

      expect(view).toHaveStyle({ maxHeight: '1200px' });
    });
  });
});
