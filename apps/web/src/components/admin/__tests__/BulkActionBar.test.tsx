/**
 * BulkActionBar Component Tests (Issue #912)
 *
 * Comprehensive test coverage for BulkActionBar component.
 * Tests rendering, interactions, accessibility, and edge cases.
 */

import React from 'react';
import { render, screen, fireEvent, within } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { Trash2, Download, Power, Archive } from 'lucide-react';
import { BulkActionBar, EmptyBulkActionBar } from '../BulkActionBar';
import type { BulkAction } from '../BulkActionBar';

describe('BulkActionBar', () => {
  // Mock actions
  const mockDelete = vi.fn();
  const mockExport = vi.fn();
  const mockClearSelection = vi.fn();

  const defaultActions: BulkAction[] = [
    {
      id: 'delete',
      label: 'Delete',
      icon: Trash2,
      variant: 'destructive',
      onClick: mockDelete,
    },
    {
      id: 'export',
      label: 'Export',
      icon: Download,
      variant: 'outline',
      onClick: mockExport,
    },
  ];

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render with selected items', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('3 items selected')).toBeInTheDocument();
      expect(screen.getByText('3 / 10')).toBeInTheDocument();
    });

    it('should not render when selectedCount is 0', () => {
      const { container } = render(
        <BulkActionBar
          selectedCount={0}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(container.firstChild).toBeNull();
    });

    it('should render all action buttons', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByTestId('bulk-action-bar-action-delete')).toBeInTheDocument();
      expect(screen.getByTestId('bulk-action-bar-action-export')).toBeInTheDocument();
    });

    it('should render Clear button', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByTestId('bulk-action-bar-clear')).toBeInTheDocument();
      expect(screen.getByLabelText('Clear selection')).toBeInTheDocument();
    });
  });

  describe('Item Labels', () => {
    it('should display plural label for multiple items', () => {
      render(
        <BulkActionBar
          selectedCount={5}
          totalCount={10}
          itemLabel="keys"
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('5 keys selected')).toBeInTheDocument();
    });

    it('should display singular label for single item', () => {
      render(
        <BulkActionBar
          selectedCount={1}
          totalCount={10}
          itemLabel="keys"
          itemLabelSingular="key"
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('1 key selected')).toBeInTheDocument();
    });

    it('should auto-generate singular label from plural', () => {
      render(
        <BulkActionBar
          selectedCount={1}
          totalCount={10}
          itemLabel="items"
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('1 item selected')).toBeInTheDocument();
    });

    it('should handle custom item labels', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          itemLabel="users"
          itemLabelSingular="user"
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('3 users selected')).toBeInTheDocument();
    });
  });

  describe('Progress Indicator', () => {
    it('should show progress bar by default', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toBeInTheDocument();
      expect(progressBar).toHaveAttribute('aria-valuenow', '30');
    });

    it('should calculate correct progress percentage', () => {
      render(
        <BulkActionBar
          selectedCount={7}
          totalCount={20}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '35');
      expect(screen.getByText('35%')).toBeInTheDocument();
    });

    it('should show 100% when all items selected', () => {
      render(
        <BulkActionBar
          selectedCount={15}
          totalCount={15}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '100');
      expect(screen.getByText('100%')).toBeInTheDocument();
    });

    it('should not show progress bar when showProgress is false', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          showProgress={false}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
    });
  });

  describe('Total Count Display', () => {
    it('should show total count by default', () => {
      render(
        <BulkActionBar
          selectedCount={5}
          totalCount={20}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('5 / 20')).toBeInTheDocument();
    });

    it('should hide total count when showTotal is false', () => {
      render(
        <BulkActionBar
          selectedCount={5}
          totalCount={20}
          showTotal={false}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('5')).toBeInTheDocument();
      expect(screen.queryByText('5 / 20')).not.toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    it('should call onClick handler with selectedCount', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const deleteButton = screen.getByTestId('bulk-action-bar-action-delete');
      fireEvent.click(deleteButton);

      expect(mockDelete).toHaveBeenCalledWith(3);
      expect(mockDelete).toHaveBeenCalledTimes(1);
    });

    it('should display action label with count by default', () => {
      render(
        <BulkActionBar
          selectedCount={5}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      // Check desktop view (hidden on mobile)
      expect(screen.getByText('Delete (5)')).toBeInTheDocument();
      expect(screen.getByText('Export (5)')).toBeInTheDocument();
    });

    it('should hide count in label when showCount is false', () => {
      const actionsWithoutCount: BulkAction[] = [
        {
          id: 'delete',
          label: 'Delete',
          icon: Trash2,
          variant: 'destructive',
          onClick: mockDelete,
          showCount: false,
        },
      ];

      render(
        <BulkActionBar
          selectedCount={5}
          totalCount={10}
          actions={actionsWithoutCount}
          onClearSelection={mockClearSelection}
        />
      );

      // Check that count is not shown in button aria-label
      const deleteButton = screen.getByTestId('bulk-action-bar-action-delete');
      expect(deleteButton).toHaveAttribute('aria-label', 'Delete');
      expect(screen.queryByText('Delete (5)')).not.toBeInTheDocument();
    });

    it('should disable action buttons when disabled prop is true', () => {
      const actionsWithDisabled: BulkAction[] = [
        {
          id: 'delete',
          label: 'Delete',
          icon: Trash2,
          variant: 'destructive',
          onClick: mockDelete,
          disabled: true,
        },
      ];

      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={actionsWithDisabled}
          onClearSelection={mockClearSelection}
        />
      );

      const deleteButton = screen.getByTestId('bulk-action-bar-action-delete');
      expect(deleteButton).toBeDisabled();
    });

    it('should render multiple action buttons', () => {
      const multipleActions: BulkAction[] = [
        {
          id: 'delete',
          label: 'Delete',
          icon: Trash2,
          variant: 'destructive',
          onClick: vi.fn(),
        },
        {
          id: 'export',
          label: 'Export',
          icon: Download,
          variant: 'outline',
          onClick: vi.fn(),
        },
        {
          id: 'archive',
          label: 'Archive',
          icon: Archive,
          variant: 'secondary',
          onClick: vi.fn(),
        },
      ];

      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={multipleActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByTestId('bulk-action-bar-action-delete')).toBeInTheDocument();
      expect(screen.getByTestId('bulk-action-bar-action-export')).toBeInTheDocument();
      expect(screen.getByTestId('bulk-action-bar-action-archive')).toBeInTheDocument();
    });

    it('should apply custom className to action buttons', () => {
      const actionsWithClass: BulkAction[] = [
        {
          id: 'delete',
          label: 'Delete',
          icon: Trash2,
          variant: 'destructive',
          onClick: mockDelete,
          className: 'custom-delete-class',
        },
      ];

      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={actionsWithClass}
          onClearSelection={mockClearSelection}
        />
      );

      const deleteButton = screen.getByTestId('bulk-action-bar-action-delete');
      expect(deleteButton).toHaveClass('custom-delete-class');
    });

    it('should set tooltip as title attribute', () => {
      const actionsWithTooltip: BulkAction[] = [
        {
          id: 'delete',
          label: 'Delete',
          icon: Trash2,
          variant: 'destructive',
          onClick: mockDelete,
          tooltip: 'Permanently delete selected items',
        },
      ];

      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={actionsWithTooltip}
          onClearSelection={mockClearSelection}
        />
      );

      const deleteButton = screen.getByTestId('bulk-action-bar-action-delete');
      expect(deleteButton).toHaveAttribute('title', 'Permanently delete selected items');
    });
  });

  describe('Clear Selection', () => {
    it('should call onClearSelection when Clear button is clicked', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const clearButton = screen.getByTestId('bulk-action-bar-clear');
      fireEvent.click(clearButton);

      expect(mockClearSelection).toHaveBeenCalledTimes(1);
    });
  });

  describe('Accessibility', () => {
    it('should have proper ARIA attributes', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const toolbar = screen.getByRole('toolbar');
      expect(toolbar).toHaveAttribute('aria-label', 'Bulk actions for 3 selected items');
    });

    it('should use custom aria-label when provided', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
          ariaLabel="Custom bulk actions toolbar"
        />
      );

      const toolbar = screen.getByRole('toolbar');
      expect(toolbar).toHaveAttribute('aria-label', 'Custom bulk actions toolbar');
    });

    it('should have accessible progress bar', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const progressBar = screen.getByRole('progressbar');
      expect(progressBar).toHaveAttribute('aria-valuenow', '30');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
      expect(progressBar).toHaveAttribute('aria-label', '30% of items selected');
    });

    it('should have accessible action buttons', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const deleteButton = screen.getByTestId('bulk-action-bar-action-delete');
      expect(deleteButton).toHaveAttribute('aria-label', 'Delete (3)');
    });
  });

  describe('Edge Cases', () => {
    it('should handle zero total count', () => {
      render(
        <BulkActionBar
          selectedCount={0}
          totalCount={0}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      // Should not render when selectedCount is 0
      expect(screen.queryByTestId('bulk-action-bar')).not.toBeInTheDocument();
    });

    it('should handle single item with totalCount of 1', () => {
      render(
        <BulkActionBar
          selectedCount={1}
          totalCount={1}
          itemLabel="items"
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('1 item selected')).toBeInTheDocument();
      expect(screen.getByText('1 / 1')).toBeInTheDocument();
      expect(screen.getByText('100%')).toBeInTheDocument();
    });

    it('should handle large numbers', () => {
      render(
        <BulkActionBar
          selectedCount={999}
          totalCount={1500}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      expect(screen.getByText('999 items selected')).toBeInTheDocument();
      expect(screen.getByText('999 / 1500')).toBeInTheDocument();
    });

    it('should handle empty actions array', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={[]}
          onClearSelection={mockClearSelection}
        />
      );

      // Should still render selection info and clear button
      expect(screen.getByText('3 items selected')).toBeInTheDocument();
      expect(screen.getByTestId('bulk-action-bar-clear')).toBeInTheDocument();
    });

    it('should apply custom className to container', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
          className="custom-bulk-bar"
        />
      );

      const toolbar = screen.getByTestId('bulk-action-bar');
      expect(toolbar).toHaveClass('custom-bulk-bar');
    });

    it('should use custom testId', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
          testId="custom-test-id"
        />
      );

      expect(screen.getByTestId('custom-test-id')).toBeInTheDocument();
      expect(screen.getByTestId('custom-test-id-clear')).toBeInTheDocument();
    });
  });
});

describe('EmptyBulkActionBar', () => {
  describe('Rendering', () => {
    it('should render with default message', () => {
      render(<EmptyBulkActionBar itemLabel="items" />);

      expect(screen.getByText('Select items to perform bulk actions')).toBeInTheDocument();
    });

    it('should render with custom message', () => {
      render(
        <EmptyBulkActionBar itemLabel="keys" message="Check boxes to enable bulk operations" />
      );

      expect(screen.getByText('Check boxes to enable bulk operations')).toBeInTheDocument();
    });

    it('should render with custom item label', () => {
      render(<EmptyBulkActionBar itemLabel="documents" />);

      expect(screen.getByText('Select documents to perform bulk actions')).toBeInTheDocument();
    });

    it('should have proper role and aria attributes', () => {
      render(<EmptyBulkActionBar itemLabel="items" />);

      const container = screen.getByRole('status');
      expect(container).toHaveAttribute('aria-live', 'polite');
    });

    it('should apply custom className', () => {
      render(<EmptyBulkActionBar itemLabel="items" className="custom-empty-class" />);

      const container = screen.getByTestId('empty-bulk-action-bar');
      expect(container).toHaveClass('custom-empty-class');
    });

    it('should use custom testId', () => {
      render(<EmptyBulkActionBar itemLabel="items" testId="custom-empty-id" />);

      expect(screen.getByTestId('custom-empty-id')).toBeInTheDocument();
    });
  });

  describe('Variant Styling (Issue #2888)', () => {
    it('should render with default variant styling', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
          variant="default"
        />
      );

      const container = screen.getByTestId('bulk-action-bar');
      expect(container).toHaveClass('bg-muted/50');
      expect(container).toHaveClass('border-border');
    });

    it('should render with floating variant styling', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
          variant="floating"
        />
      );

      const container = screen.getByTestId('bulk-action-bar');
      expect(container).toHaveClass('border-2');
      expect(container).toHaveClass('border-orange-500');
      expect(container).toHaveClass('bg-white');
      expect(container).not.toHaveClass('bg-muted/50');
    });

    it('should default to default variant when not specified', () => {
      render(
        <BulkActionBar
          selectedCount={3}
          totalCount={10}
          actions={defaultActions}
          onClearSelection={mockClearSelection}
        />
      );

      const container = screen.getByTestId('bulk-action-bar');
      expect(container).toHaveClass('bg-muted/50');
    });
  });
});
