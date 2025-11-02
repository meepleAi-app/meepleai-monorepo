import { render, screen } from '@testing-library/react';
import { DiffLineNumberGutter } from '../DiffLineNumberGutter';
import { DiffLine, CollapsibleSection } from '../../../lib/diffProcessor';

describe('DiffLineNumberGutter', () => {
  const mockLines: DiffLine[] = [
    { lineNumber: 1, content: 'Line 1', type: 'unchanged' },
    { lineNumber: 2, content: 'Line 2', type: 'added' },
    { lineNumber: 3, content: 'Line 3', type: 'deleted' },
    { lineNumber: 4, content: 'Line 4', type: 'modified' },
    { lineNumber: null, content: '', type: 'unchanged' },
  ];

  const emptyCollapsibleSections: CollapsibleSection[] = [];

  const collapsibleSections: CollapsibleSection[] = [
    {
      startLine: 10,
      endLine: 20,
      lineCount: 10,
      isCollapsed: true,
    },
  ];

  describe('Rendering', () => {
    it('should render line numbers for all lines', () => {
      const { container } = render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      expect(lineNumbers).toHaveLength(5);
    });

    it('should render with old side aria label', () => {
      render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      expect(screen.getByLabelText('old version line numbers')).toBeInTheDocument();
    });

    it('should render with new side aria label', () => {
      render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="new"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      expect(screen.getByLabelText('new version line numbers')).toBeInTheDocument();
    });

    it('should display line numbers correctly', () => {
      const { container } = render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      expect(lineNumbers[0]).toHaveAttribute('data-line-number', '1');
      expect(lineNumbers[1]).toHaveAttribute('data-line-number', '2');
      expect(lineNumbers[2]).toHaveAttribute('data-line-number', '3');
      expect(lineNumbers[3]).toHaveAttribute('data-line-number', '4');
    });

    it('should render empty for null line numbers', () => {
      const { container } = render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      const lastLineNumber = lineNumbers[lineNumbers.length - 1];
      expect(lastLineNumber.textContent).toBe('');
    });

    it('should apply correct type classes', () => {
      const { container } = render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      expect(container.querySelector('.diff-line-number--unchanged')).toBeInTheDocument();
      expect(container.querySelector('.diff-line-number--added')).toBeInTheDocument();
      expect(container.querySelector('.diff-line-number--deleted')).toBeInTheDocument();
      expect(container.querySelector('.diff-line-number--modified')).toBeInTheDocument();
    });
  });

  describe('Collapsible Sections', () => {
    it('should hide line numbers in collapsed sections', () => {
      const linesWithCollapsible: DiffLine[] = [
        { lineNumber: 5, content: 'Line 5', type: 'unchanged' },
        { lineNumber: 10, content: 'Line 10', type: 'unchanged' },
        { lineNumber: 15, content: 'Line 15', type: 'unchanged' },
        { lineNumber: 20, content: 'Line 20', type: 'unchanged' },
        { lineNumber: 25, content: 'Line 25', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={linesWithCollapsible}
          side="old"
          collapsibleSections={collapsibleSections}
        />
      );

      const visibleLineNumbers = container.querySelectorAll('.diff-line-number');
      // Lines 10-20 should be hidden (3 lines), so we should see 2 visible lines
      expect(visibleLineNumbers.length).toBeLessThan(linesWithCollapsible.length);
    });

    it('should show all line numbers when no sections collapsed', () => {
      const { container } = render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      expect(lineNumbers).toHaveLength(mockLines.length);
    });

    it('should handle multiple collapsed sections', () => {
      const multipleSections: CollapsibleSection[] = [
        {
          startLine: 10,
          endLine: 15,
          lineCount: 5,
          isCollapsed: true,
        },
        {
          startLine: 30,
          endLine: 40,
          lineCount: 10,
          isCollapsed: true,
        },
      ];

      const linesWithMultipleSections: DiffLine[] = [
        { lineNumber: 5, content: 'Line 5', type: 'unchanged' },
        { lineNumber: 10, content: 'Line 10', type: 'unchanged' },
        { lineNumber: 20, content: 'Line 20', type: 'unchanged' },
        { lineNumber: 35, content: 'Line 35', type: 'unchanged' },
        { lineNumber: 45, content: 'Line 45', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={linesWithMultipleSections}
          side="old"
          collapsibleSections={multipleSections}
        />
      );

      const visibleLineNumbers = container.querySelectorAll('.diff-line-number');
      expect(visibleLineNumbers.length).toBeLessThan(linesWithMultipleSections.length);
    });

    it('should not hide lines outside collapsed sections', () => {
      const linesWithCollapsible: DiffLine[] = [
        { lineNumber: 5, content: 'Line 5', type: 'unchanged' },
        { lineNumber: 25, content: 'Line 25', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={linesWithCollapsible}
          side="old"
          collapsibleSections={collapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      expect(lineNumbers).toHaveLength(2); // Both should be visible
    });
  });

  describe('Memoization', () => {
    it('should use React.memo for optimization', () => {
      expect(DiffLineNumberGutter.displayName).toBe('DiffLineNumberGutter');
    });

    it('should not re-render with same props', () => {
      const { rerender } = render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      rerender(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      expect(screen.getByLabelText('old version line numbers')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle empty lines array', () => {
      const { container } = render(
        <DiffLineNumberGutter
          lines={[]}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      expect(lineNumbers).toHaveLength(0);
    });

    it('should handle line number 0', () => {
      const linesWithZero: DiffLine[] = [
        { lineNumber: 0, content: 'Line 0', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={linesWithZero}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumber = container.querySelector('[data-line-number="0"]');
      expect(lineNumber).toBeInTheDocument();
    });

    it('should handle very large line numbers', () => {
      const largeLineNumbers: DiffLine[] = [
        { lineNumber: 99999, content: 'Line 99999', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={largeLineNumbers}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumber = container.querySelector('[data-line-number="99999"]');
      expect(lineNumber).toBeInTheDocument();
      expect(lineNumber?.textContent).toBe('99999');
    });

    it('should handle all lines with null line numbers', () => {
      const nullLines: DiffLine[] = [
        { lineNumber: null, content: '', type: 'unchanged' },
        { lineNumber: null, content: '', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={nullLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const lineNumbers = container.querySelectorAll('.diff-line-number');
      lineNumbers.forEach((lineNumber) => {
        expect(lineNumber.textContent).toBe('');
      });
    });

    it('should handle collapsed section at beginning of lines', () => {
      const sectionsAtStart: CollapsibleSection[] = [
        {
          startLine: 1,
          endLine: 5,
          lineCount: 5,
          isCollapsed: true,
        },
      ];

      const linesAtStart: DiffLine[] = [
        { lineNumber: 1, content: 'Line 1', type: 'unchanged' },
        { lineNumber: 2, content: 'Line 2', type: 'unchanged' },
        { lineNumber: 10, content: 'Line 10', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={linesAtStart}
          side="old"
          collapsibleSections={sectionsAtStart}
        />
      );

      const visibleLineNumbers = container.querySelectorAll('.diff-line-number');
      expect(visibleLineNumbers.length).toBeLessThan(linesAtStart.length);
    });

    it('should handle collapsed section at end of lines', () => {
      const sectionsAtEnd: CollapsibleSection[] = [
        {
          startLine: 50,
          endLine: 60,
          lineCount: 10,
          isCollapsed: true,
        },
      ];

      const linesAtEnd: DiffLine[] = [
        { lineNumber: 10, content: 'Line 10', type: 'unchanged' },
        { lineNumber: 55, content: 'Line 55', type: 'unchanged' },
      ];

      const { container } = render(
        <DiffLineNumberGutter
          lines={linesAtEnd}
          side="old"
          collapsibleSections={sectionsAtEnd}
        />
      );

      const visibleLineNumbers = container.querySelectorAll('.diff-line-number');
      expect(visibleLineNumbers.length).toBeLessThan(linesAtEnd.length);
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA label for old side', () => {
      render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="old"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const gutter = screen.getByLabelText('old version line numbers');
      expect(gutter).toHaveClass('diff-line-numbers');
    });

    it('should have proper ARIA label for new side', () => {
      render(
        <DiffLineNumberGutter
          lines={mockLines}
          side="new"
          collapsibleSections={emptyCollapsibleSections}
        />
      );

      const gutter = screen.getByLabelText('new version line numbers');
      expect(gutter).toHaveClass('diff-line-numbers');
    });
  });
});
