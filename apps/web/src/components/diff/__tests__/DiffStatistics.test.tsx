/**
 * Unit tests for DiffStatistics component
 * Tests diff summary display in normal and compact modes
 */

import { render, screen } from '@testing-library/react';
import { DiffStatistics } from '../DiffStatistics';
import type { DiffStatistics as DiffStats } from '@/lib/diffProcessor';

describe('DiffStatistics', () => {
  const mockStats: DiffStats = {
    added: 10,
    deleted: 5,
    modified: 3,
    unchanged: 50,
    totalLines: 68,
  };

  describe('Normal Mode', () => {
    it('should render all statistics in normal mode', () => {
      render(<DiffStatistics statistics={mockStats} />);

      expect(screen.getByText('+10')).toBeInTheDocument();
      expect(screen.getByTestId('added-label')).toBeInTheDocument();

      expect(screen.getByText('-5')).toBeInTheDocument();
      expect(screen.getByTestId('deleted-label')).toBeInTheDocument();

      expect(screen.getByText('~3')).toBeInTheDocument();
      expect(screen.getByTestId('modified-label')).toBeInTheDocument();

      expect(screen.getByText('50')).toBeInTheDocument();
      expect(screen.getByTestId('unchanged-label')).toBeInTheDocument();

      expect(screen.getByText('68')).toBeInTheDocument();
      expect(screen.getByTestId('total-lines-label')).toBeInTheDocument();
    });

    it('should have testid for normal mode', () => {
      render(<DiffStatistics statistics={mockStats} />);

      expect(screen.getByTestId('diff-statistics')).toBeInTheDocument();
    });

    it('should apply correct CSS classes in normal mode', () => {
      const { container } = render(<DiffStatistics statistics={mockStats} />);

      expect(container.querySelector('.diff-statistics')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat-item--added')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat-item--deleted')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat-item--modified')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat-item--unchanged')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat-item--total')).toBeInTheDocument();
    });

    it('should show modified section when count > 0', () => {
      render(<DiffStatistics statistics={mockStats} />);

      expect(screen.getByText('~3')).toBeInTheDocument();
      expect(screen.getByTestId('modified-label')).toBeInTheDocument();
    });

    it('should hide modified section when count is 0', () => {
      const statsNoModified: DiffStats = {
        ...mockStats,
        modified: 0,
      };

      render(<DiffStatistics statistics={statsNoModified} />);

      expect(screen.queryByText('~0')).not.toBeInTheDocument();
      expect(screen.queryByText('Modified')).not.toBeInTheDocument();
    });

    it('should render stat values and labels separately', () => {
      const { container } = render(<DiffStatistics statistics={mockStats} />);

      const statValues = container.querySelectorAll('.diff-stat-value');
      const statLabels = container.querySelectorAll('.diff-stat-label');

      expect(statValues.length).toBeGreaterThan(0);
      expect(statLabels.length).toBeGreaterThan(0);
    });
  });

  describe('Compact Mode', () => {
    it('should render compact statistics', () => {
      render(<DiffStatistics statistics={mockStats} compact={true} />);

      expect(screen.getByText('+10')).toBeInTheDocument();
      expect(screen.getByText('-5')).toBeInTheDocument();
      expect(screen.getByText('~3')).toBeInTheDocument();
    });

    it('should not show labels in compact mode', () => {
      render(<DiffStatistics statistics={mockStats} compact={true} />);

      expect(screen.queryByText('Added')).not.toBeInTheDocument();
      expect(screen.queryByText('Deleted')).not.toBeInTheDocument();
      expect(screen.queryByText('Modified')).not.toBeInTheDocument();
    });

    it('should not show unchanged and total in compact mode', () => {
      render(<DiffStatistics statistics={mockStats} compact={true} />);

      // Only value is checked, not label
      const unchangedElement = screen.queryByText('Unchanged');
      const totalElement = screen.queryByText('Total Lines');

      expect(unchangedElement).not.toBeInTheDocument();
      expect(totalElement).not.toBeInTheDocument();
    });

    it('should apply compact CSS class', () => {
      const { container } = render(<DiffStatistics statistics={mockStats} compact={true} />);

      expect(container.querySelector('.diff-statistics--compact')).toBeInTheDocument();
    });

    it('should have title attributes for accessibility', () => {
      render(<DiffStatistics statistics={mockStats} compact={true} />);

      const added = screen.getByTitle('Added lines');
      const deleted = screen.getByTitle('Deleted lines');
      const modified = screen.getByTitle('Modified lines');

      expect(added).toBeInTheDocument();
      expect(deleted).toBeInTheDocument();
      expect(modified).toBeInTheDocument();
    });

    it('should hide modified in compact mode when count is 0', () => {
      const statsNoModified: DiffStats = {
        ...mockStats,
        modified: 0,
      };

      render(<DiffStatistics statistics={statsNoModified} compact={true} />);

      expect(screen.queryByText('~0')).not.toBeInTheDocument();
      expect(screen.queryByTitle('Modified lines')).not.toBeInTheDocument();
    });

    it('should apply correct CSS classes for compact mode', () => {
      const { container } = render(<DiffStatistics statistics={mockStats} compact={true} />);

      expect(container.querySelector('.diff-stat--added')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat--deleted')).toBeInTheDocument();
      expect(container.querySelector('.diff-stat--modified')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('should handle zero values', () => {
      const zeroStats: DiffStats = {
        added: 0,
        deleted: 0,
        modified: 0,
        unchanged: 0,
        totalLines: 0,
      };

      render(<DiffStatistics statistics={zeroStats} />);

      expect(screen.getByText('+0')).toBeInTheDocument();
      expect(screen.getByText('-0')).toBeInTheDocument();
      // Modified should be hidden when 0
      expect(screen.queryByText('~0')).not.toBeInTheDocument();
      // Multiple "0" values exist (unchanged and totalLines), use getAllByText
      const zeros = screen.getAllByText('0');
      expect(zeros.length).toBeGreaterThan(0);
    });

    it('should handle large numbers', () => {
      const largeStats: DiffStats = {
        added: 10000,
        deleted: 5000,
        modified: 2000,
        unchanged: 50000,
        totalLines: 67000,
      };

      render(<DiffStatistics statistics={largeStats} />);

      expect(screen.getByText('+10000')).toBeInTheDocument();
      expect(screen.getByText('-5000')).toBeInTheDocument();
      expect(screen.getByText('~2000')).toBeInTheDocument();
      expect(screen.getByText('50000')).toBeInTheDocument();
      expect(screen.getByText('67000')).toBeInTheDocument();
    });

    it('should handle all changes (no unchanged)', () => {
      const allChangesStats: DiffStats = {
        added: 20,
        deleted: 15,
        modified: 10,
        unchanged: 0,
        totalLines: 45,
      };

      render(<DiffStatistics statistics={allChangesStats} />);

      expect(screen.getByText('0')).toBeInTheDocument(); // Unchanged = 0
    });

    it('should default compact to false when not specified', () => {
      const { container } = render(<DiffStatistics statistics={mockStats} />);

      expect(container.querySelector('.diff-statistics--compact')).not.toBeInTheDocument();
      expect(screen.getByTestId('diff-statistics')).toBeInTheDocument();
    });

    it('should handle modified count of exactly 0', () => {
      const noModified: DiffStats = {
        added: 5,
        deleted: 3,
        modified: 0,
        unchanged: 20,
        totalLines: 28,
      };

      render(<DiffStatistics statistics={noModified} />);

      expect(screen.queryByText('Modified')).not.toBeInTheDocument();
      expect(screen.getByTestId('added-label')).toBeInTheDocument();
      expect(screen.getByTestId('deleted-label')).toBeInTheDocument();
    });
  });

  describe('Formatting', () => {
    it('should prefix added count with +', () => {
      render(<DiffStatistics statistics={mockStats} />);

      const addedValue = screen.getByText('+10');

      expect(addedValue).toBeInTheDocument();
      expect(addedValue.textContent).toMatch(/^\+/);
    });

    it('should prefix deleted count with -', () => {
      render(<DiffStatistics statistics={mockStats} />);

      const deletedValue = screen.getByText('-5');

      expect(deletedValue).toBeInTheDocument();
      expect(deletedValue.textContent).toMatch(/^-/);
    });

    it('should prefix modified count with ~', () => {
      render(<DiffStatistics statistics={mockStats} />);

      const modifiedValue = screen.getByText('~3');

      expect(modifiedValue).toBeInTheDocument();
      expect(modifiedValue.textContent).toMatch(/^~/);
    });

    it('should not prefix unchanged count', () => {
      render(<DiffStatistics statistics={mockStats} />);

      const unchangedValue = screen.getByText('50');

      expect(unchangedValue).toBeInTheDocument();
      expect(unchangedValue.textContent).not.toMatch(/^[+\-~]/);
    });

    it('should not prefix total count', () => {
      render(<DiffStatistics statistics={mockStats} />);

      const totalValue = screen.getByText('68');

      expect(totalValue).toBeInTheDocument();
      expect(totalValue.textContent).not.toMatch(/^[+\-~]/);
    });
  });

  describe('Compact vs Normal Mode Comparison', () => {
    it('should show more information in normal mode than compact', () => {
      const { rerender } = render(<DiffStatistics statistics={mockStats} compact={false} />);
      const normalText = screen.getByTestId('diff-statistics').textContent;

      rerender(<DiffStatistics statistics={mockStats} compact={true} />);
      const compactText = document.querySelector('.diff-statistics--compact')?.textContent;

      // Normal mode should have more text (labels)
      expect(normalText?.length ?? 0).toBeGreaterThan(compactText?.length ?? 0);
    });

    it('should maintain same added/deleted/modified values in both modes', () => {
      const { rerender } = render(<DiffStatistics statistics={mockStats} compact={false} />);

      expect(screen.getByText('+10')).toBeInTheDocument();
      expect(screen.getByText('-5')).toBeInTheDocument();
      expect(screen.getByText('~3')).toBeInTheDocument();

      rerender(<DiffStatistics statistics={mockStats} compact={true} />);

      expect(screen.getByText('+10')).toBeInTheDocument();
      expect(screen.getByText('-5')).toBeInTheDocument();
      expect(screen.getByText('~3')).toBeInTheDocument();
    });
  });
});
