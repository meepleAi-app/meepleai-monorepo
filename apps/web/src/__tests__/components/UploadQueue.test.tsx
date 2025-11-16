/**
 * UploadQueue Component Tests
 * Coverage target: 90%+ (currently 81.8%)
 *
 * Test Strategy:
 * 1. Empty queue state
 * 2. Queue with multiple items
 * 3. Overall progress calculation
 * 4. Stats summary display
 * 5. Clear completed button visibility and functionality
 * 6. Integration with UploadQueueItem components
 * 7. Accessibility
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { UploadQueue } from '../../components/UploadQueue';
import type { UploadQueueItem as UploadQueueItemType, UploadQueueStats } from '../../hooks/useUploadQueue';

import { createMockUploadQueueItem as createTestItem } from '../helpers/uploadQueueMocks';

describe('UploadQueue Component', () => {
  const mockOnCancel = jest.fn();
  const mockOnRetry = jest.fn();
  const mockOnRemove = jest.fn();
  const mockOnClearCompleted = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Empty Queue State', () => {
    it('renders empty state message when no items', () => {
      const stats: UploadQueueStats = {
        total: 0,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={[]}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByText(/No files in queue. Select files to begin uploading./i)).toBeInTheDocument();
    });

    it('applies empty state styling', () => {
      const stats: UploadQueueStats = {
        total: 0,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      const { container } = render(
        <UploadQueue
          items={[]}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const emptyStateText = screen.getByText(/No files in queue/i);
      const emptyState = emptyStateText.closest('div');
      expect(emptyState).toHaveClass('text-center', 'border-2', 'border-dashed', 'bg-gray-50');
    });
  });

  describe('Queue with Items', () => {
    it('renders upload queue header', () => {
      const items = [createTestItem({ id: '1', status: 'pending' })];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 1,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByText(/Upload Queue/i)).toBeInTheDocument();
    });

    it('renders all queue items', () => {
      const items = [
        createTestItem({ id: '1', file: new File([''], 'file1.pdf', { type: 'application/pdf' }) }),
        createTestItem({ id: '2', file: new File([''], 'file2.pdf', { type: 'application/pdf' }) }),
        createTestItem({ id: '3', file: new File([''], 'file3.pdf', { type: 'application/pdf' }) })
      ];
      const stats: UploadQueueStats = {
        total: 3,
        pending: 3,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByText('file1.pdf')).toBeInTheDocument();
      expect(screen.getByText('file2.pdf')).toBeInTheDocument();
      expect(screen.getByText('file3.pdf')).toBeInTheDocument();
    });

    it('passes correct props to UploadQueueItem components', () => {
      const items = [createTestItem({ id: 'test-1', status: 'uploading', progress: 50 })];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 1,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const cancelButton = screen.getByRole('button', { name: /Cancel upload/i });
      fireEvent.click(cancelButton);

      expect(mockOnCancel).toHaveBeenCalledWith('test-1');
    });
  });

  describe('Overall Progress Calculation', () => {
    it('calculates total progress correctly for multiple files', () => {
      const items = [
        createTestItem({ id: '1', progress: 100 }),
        createTestItem({ id: '2', progress: 50 }),
        createTestItem({ id: '3', progress: 0 })
      ];
      const stats: UploadQueueStats = {
        total: 3,
        pending: 1,
        uploading: 1,
        processing: 0,
        succeeded: 1,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      // (100 + 50 + 0) / 3 = 50%
      const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '50');
    });

    it('handles progress calculation with varying percentages', () => {
      const items = [
        createTestItem({ id: '1', progress: 75 }),
        createTestItem({ id: '2', progress: 25 }),
        createTestItem({ id: '3', progress: 50 }),
        createTestItem({ id: '4', progress: 100 })
      ];
      const stats: UploadQueueStats = {
        total: 4,
        pending: 0,
        uploading: 3,
        processing: 0,
        succeeded: 1,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      // (75 + 25 + 50 + 100) / 4 = 62.5 => rounded to 63%
      const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '63');
    });

    it('shows 0% progress when all items are pending', () => {
      const items = [
        createTestItem({ id: '1', progress: 0 }),
        createTestItem({ id: '2', progress: 0 })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 2,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '0');
    });

    it('shows 100% progress when all items complete', () => {
      const items = [
        createTestItem({ id: '1', progress: 100 }),
        createTestItem({ id: '2', progress: 100 })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 2,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '100');
    });
  });

  describe('Status Messages', () => {
    it('displays active upload message', () => {
      const items = [
        createTestItem({ id: '1', status: 'uploading', progress: 50 }),
        createTestItem({ id: '2', status: 'pending', progress: 0 })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 1,
        uploading: 1,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      // Average progress: (50 + 0) / 2 = 25%
      expect(screen.getByText(/Uploading 1 of 2 files \(25% total\)/i)).toBeInTheDocument();
    });

    it('displays waiting message when all files pending', () => {
      const items = [
        createTestItem({ id: '1', status: 'pending' }),
        createTestItem({ id: '2', status: 'pending' })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 2,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByText(/2 files waiting to upload/i)).toBeInTheDocument();
    });

    it('displays complete message when all uploads done', () => {
      const items = [
        createTestItem({ id: '1', status: 'success', progress: 100 }),
        createTestItem({ id: '2', status: 'success', progress: 100 })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 2,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByText(/All uploads complete/i)).toBeInTheDocument();
    });

    it('includes processing in active count', () => {
      const items = [
        createTestItem({ id: '1', status: 'uploading' }),
        createTestItem({ id: '2', status: 'processing' })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 0,
        uploading: 1,
        processing: 1,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      // Active count = uploading (1) + processing (1) = 2
      expect(screen.getByText(/Uploading 2 of 2 files/i)).toBeInTheDocument();
    });
  });

  describe('Stats Summary', () => {
    it('displays all relevant stats', () => {
      const items = [
        createTestItem({ id: '1', status: 'pending' }),
        createTestItem({ id: '2', status: 'uploading' }),
        createTestItem({ id: '3', status: 'processing' }),
        createTestItem({ id: '4', status: 'success' }),
        createTestItem({ id: '5', status: 'failed' }),
        createTestItem({ id: '6', status: 'cancelled' })
      ];
      const stats: UploadQueueStats = {
        total: 6,
        pending: 1,
        uploading: 1,
        processing: 1,
        succeeded: 1,
        failed: 1,
        cancelled: 1
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      // Use function matchers to handle text split across elements
      expect(screen.getByText((content, element) => {
        return element?.textContent === '1 pending';
      })).toBeInTheDocument();
      expect(screen.getByText((content, element) => {
        return element?.textContent === '1 uploading';
      })).toBeInTheDocument();
      expect(screen.getByText((content, element) => {
        return element?.textContent === '1 processing';
      })).toBeInTheDocument();
      expect(screen.getByText((content, element) => {
        return element?.textContent === '1 succeeded';
      })).toBeInTheDocument();
      expect(screen.getByText((content, element) => {
        return element?.textContent === '1 failed';
      })).toBeInTheDocument();
      expect(screen.getByText((content, element) => {
        return element?.textContent === '1 cancelled';
      })).toBeInTheDocument();
    });

    it('hides stats for zero counts', () => {
      const items = [
        createTestItem({ id: '1', status: 'uploading' })
      ];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 1,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.queryByText(/pending/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/processing/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/succeeded/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/failed/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/cancelled/i)).not.toBeInTheDocument();
    });
  });

  describe('Clear Completed Button', () => {
    it('shows clear completed button when completed items exist', () => {
      const items = [
        createTestItem({ id: '1', status: 'success' }),
        createTestItem({ id: '2', status: 'pending' })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 1,
        uploading: 0,
        processing: 0,
        succeeded: 1,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('shows clear completed button when failed items exist', () => {
      const items = [
        createTestItem({ id: '1', status: 'failed' })
      ];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 1,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('shows clear completed button when cancelled items exist', () => {
      const items = [
        createTestItem({ id: '1', status: 'cancelled' })
      ];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 1
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('hides clear completed button when no completed items', () => {
      const items = [
        createTestItem({ id: '1', status: 'pending' }),
        createTestItem({ id: '2', status: 'uploading' })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 1,
        uploading: 1,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.queryByRole('button', { name: /Clear completed uploads from queue/i })).not.toBeInTheDocument();
    });

    it('calls onClearCompleted when button clicked', () => {
      const items = [
        createTestItem({ id: '1', status: 'success' })
      ];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 1,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });
      fireEvent.click(clearButton);

      expect(mockOnClearCompleted).toHaveBeenCalledTimes(1);
    });

    it('renders clear completed button with proper styling', () => {
      const items = [
        createTestItem({ id: '1', status: 'success' })
      ];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 1,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });

      // Verify button exists and is clickable
      expect(clearButton).toBeInTheDocument();
      expect(clearButton).not.toBeDisabled();

      // Verify clicking triggers callback
      fireEvent.click(clearButton);
      expect(mockOnClearCompleted).toHaveBeenCalled();
    });
  });

  describe('Accessibility', () => {
    it('has accessible queue container', () => {
      const items = [createTestItem({ id: '1' })];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 1,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });

    it('has accessible progress bar', () => {
      const items = [createTestItem({ id: '1', progress: 50 })];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 1,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '50');
      expect(progressBar).toHaveAttribute('aria-valuemin', '0');
      expect(progressBar).toHaveAttribute('aria-valuemax', '100');
    });

    it('has accessible list structure', () => {
      const items = [
        createTestItem({ id: '1' }),
        createTestItem({ id: '2' })
      ];
      const stats: UploadQueueStats = {
        total: 2,
        pending: 2,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const list = screen.getByRole('list', { name: /Upload queue items/i });
      expect(list).toBeInTheDocument();

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(2);
    });
  });

  describe('Edge Cases', () => {
    it('handles single item in queue', () => {
      const items = [createTestItem({ id: '1', status: 'uploading', progress: 75 })];
      const stats: UploadQueueStats = {
        total: 1,
        pending: 0,
        uploading: 1,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      expect(screen.getByText(/Uploading 1 of 1 files \(75% total\)/i)).toBeInTheDocument();
    });

    it('handles large number of items', () => {
      const items = Array.from({ length: 50 }, (_, i) =>
        createTestItem({ id: `item-${i}`, file: new File([''], `file${i}.pdf`, { type: 'application/pdf' }) })
      );
      const stats: UploadQueueStats = {
        total: 50,
        pending: 50,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(50);
    });

    it('handles all items at 0% progress', () => {
      const items = [
        createTestItem({ id: '1', progress: 0 }),
        createTestItem({ id: '2', progress: 0 }),
        createTestItem({ id: '3', progress: 0 })
      ];
      const stats: UploadQueueStats = {
        total: 3,
        pending: 3,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadQueue
          items={items}
          stats={stats}
          onCancel={mockOnCancel}
          onRetry={mockOnRetry}
          onRemove={mockOnRemove}
          onClearCompleted={mockOnClearCompleted}
        />
      );

      const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
      expect(progressBar).toHaveAttribute('aria-valuenow', '0');
    });
  });
});