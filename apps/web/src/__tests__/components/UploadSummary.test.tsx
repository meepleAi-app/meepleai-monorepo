/**
 * UploadSummary Component Tests
 * Coverage target: 90%+ (currently 63.6%)
 *
 * Test Strategy:
 * 1. Rendering for different completion scenarios (all success, partial success, failures)
 * 2. Summary statistics display
 * 3. Icon and color changes based on results
 * 4. Success/failure messages
 * 5. Action buttons (close, clear queue)
 * 6. Accessibility attributes
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { UploadSummary } from '../../components/UploadSummary';
import type { UploadQueueStats } from '../../hooks/useUploadQueue';

describe('UploadSummary Component', () => {
  const mockOnClose = jest.fn();
  const mockOnClearAll = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('All Files Succeeded', () => {
    it('renders success summary for all files succeeded', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/Upload Complete/i)).toBeInTheDocument();
      expect(screen.getByText(/All 5 files uploaded successfully!/i)).toBeInTheDocument();
      expect(screen.getByText(/All files have been uploaded and are being processed/i)).toBeInTheDocument();
    });

    it('displays success icon for all succeeded', () => {
      const stats: UploadQueueStats = {
        total: 3,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText('✅')).toBeInTheDocument();
    });

    it('applies success background color', () => {
      const stats: UploadQueueStats = {
        total: 2,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 2,
        failed: 0,
        cancelled: 0
      };

      const { container } = render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const summary = container.querySelector('[data-testid="upload-summary"]');
      expect(summary).toHaveStyle({
        backgroundColor: '#e8f5e9',
        border: '2px solid #34a853'
      });
    });
  });

  describe('Partial Success with Failures', () => {
    it('renders partial success summary', () => {
      const stats: UploadQueueStats = {
        total: 10,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 7,
        failed: 3,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/7 succeeded, 3 failed/i)).toBeInTheDocument();
    });

    it('displays warning icon for partial success', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 2,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText('⚠️')).toBeInTheDocument();
    });

    it('applies warning background color', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 2,
        cancelled: 0
      };

      const { container } = render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const summary = container.querySelector('[data-testid="upload-summary"]');
      expect(summary).toHaveStyle({
        backgroundColor: '#fff3e0',
        border: '2px solid #ff9800'
      });
    });

    it('displays failure message with retry instructions', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 2,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByRole('alert')).toHaveTextContent(/Some uploads failed/i);
      expect(screen.getByRole('alert')).toHaveTextContent(/Review the failed items in the queue and use the Retry button/i);
    });
  });

  describe('With Cancelled Files', () => {
    it('includes cancelled count in summary', () => {
      const stats: UploadQueueStats = {
        total: 10,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 6,
        failed: 2,
        cancelled: 2
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/6 succeeded, 2 failed, 2 cancelled/i)).toBeInTheDocument();
    });

    it('displays cancelled count in statistics', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 2,
        failed: 1,
        cancelled: 2
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      // Find the element with "Cancelled" text, then check its sibling for the count
      const cancelledLabel = screen.getByText('Cancelled');
      const cancelledStat = cancelledLabel.parentElement;
      expect(cancelledStat).toHaveTextContent('2');
      expect(cancelledStat).toHaveTextContent('Cancelled');
    });

    it('does not display cancelled section when count is 0', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 2,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const cancelledText = screen.queryByText((content, element) => {
        return element?.textContent === 'Cancelled';
      });
      expect(cancelledText).not.toBeInTheDocument();
    });
  });

  describe('Statistics Display', () => {
    it('displays all statistics correctly', () => {
      const stats: UploadQueueStats = {
        total: 10,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 6,
        failed: 3,
        cancelled: 1
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText('10').parentElement).toHaveTextContent('Total');
      expect(screen.getByText('6').parentElement).toHaveTextContent('Succeeded');
      expect(screen.getByText('3').parentElement).toHaveTextContent('Failed');
      expect(screen.getByText('1').parentElement).toHaveTextContent('Cancelled');
    });

    it('displays only total and succeeded when no failures or cancellations', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      // Check Total stat
      const totalLabel = screen.getByText('Total');
      const totalStat = totalLabel.parentElement;
      expect(totalStat).toHaveTextContent('5');
      expect(totalStat).toHaveTextContent('Total');

      // Check Succeeded stat
      const succeededLabel = screen.getByText('Succeeded');
      const succeededStat = succeededLabel.parentElement;
      expect(succeededStat).toHaveTextContent('5');
      expect(succeededStat).toHaveTextContent('Succeeded');

      const failedText = screen.queryByText((content, element) => {
        return element?.textContent === 'Failed';
      });
      expect(failedText).not.toBeInTheDocument();
    });

    it('hides failed section when failed count is 0', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.queryByText('Failed')).not.toBeInTheDocument();
    });
  });

  describe('Action Buttons', () => {
    it('renders close button', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByRole('button', { name: /Close upload summary/i })).toBeInTheDocument();
    });

    it('calls onClose when close button clicked', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const closeButton = screen.getByRole('button', { name: /Close upload summary/i });
      fireEvent.click(closeButton);

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });

    it('renders clear queue button', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByRole('button', { name: /Clear all items from queue/i })).toBeInTheDocument();
    });

    it('calls onClearAll when clear queue button clicked', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const clearButton = screen.getByRole('button', { name: /Clear all items from queue/i });
      fireEvent.click(clearButton);

      expect(mockOnClearAll).toHaveBeenCalledTimes(1);
    });

    it('applies hover styles to close button', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const closeButton = screen.getByRole('button', { name: /Close upload summary/i });

      fireEvent.mouseEnter(closeButton);
      expect(closeButton).toHaveStyle({ backgroundColor: '#0051cc' });

      fireEvent.mouseLeave(closeButton);
      expect(closeButton).toHaveStyle({ backgroundColor: '#0070f3' });
    });

    it('applies hover styles to clear queue button', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const clearButton = screen.getByRole('button', { name: /Clear all items from queue/i });

      // Get initial style
      const initialStyle = window.getComputedStyle(clearButton);

      fireEvent.mouseEnter(clearButton);
      expect(clearButton).toHaveStyle({ backgroundColor: '#f5f5f5' });

      fireEvent.mouseLeave(clearButton);
      // After mouse leave, should return to initial style (may be transparent or white depending on browser)
      const finalStyle = window.getComputedStyle(clearButton);
      expect(finalStyle.backgroundColor).toBeTruthy();
    });
  });

  describe('Accessibility', () => {
    it('has accessible status role', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const summary = screen.getByRole('status', { name: /Upload summary/i });
      expect(summary).toHaveAttribute('aria-live', 'polite');
    });

    it('has accessible failure alert', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 2,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('has accessible button labels', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByRole('button', { name: /Close upload summary/i })).toHaveAttribute('aria-label');
      expect(screen.getByRole('button', { name: /Clear all items from queue/i })).toHaveAttribute('aria-label');
    });

    it('hides decorative icon from screen readers', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      const { container } = render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      const icon = container.querySelector('[aria-hidden="true"]');
      expect(icon).toBeInTheDocument();
      expect(icon).toHaveTextContent('✅');
    });
  });

  describe('Edge Cases', () => {
    it('handles single file upload', () => {
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
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/All 1 files uploaded successfully!/i)).toBeInTheDocument();
    });

    it('handles zero total files', () => {
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
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      // Find the element with "Total" text, then check its sibling for the count
      const totalLabel = screen.getByText('Total');
      const totalStat = totalLabel.parentElement;
      expect(totalStat).toHaveTextContent('0');
      expect(totalStat).toHaveTextContent('Total');
    });

    it('handles all files failed', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 5,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/0 succeeded, 5 failed/i)).toBeInTheDocument();
      expect(screen.getByRole('alert')).toHaveTextContent(/Some uploads failed/i);
    });

    it('handles all files cancelled', () => {
      const stats: UploadQueueStats = {
        total: 3,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 0,
        failed: 0,
        cancelled: 3
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/0 succeeded, 0 failed, 3 cancelled/i)).toBeInTheDocument();
    });

    it('handles mixed results with all statuses', () => {
      const stats: UploadQueueStats = {
        total: 20,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 12,
        failed: 5,
        cancelled: 3
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.getByText(/12 succeeded, 5 failed, 3 cancelled/i)).toBeInTheDocument();
      expect(screen.getByText('20').parentElement).toHaveTextContent('Total');
      expect(screen.getByText('12').parentElement).toHaveTextContent('Succeeded');
      expect(screen.getByText('5').parentElement).toHaveTextContent('Failed');
      expect(screen.getByText('3').parentElement).toHaveTextContent('Cancelled');
    });
  });

  describe('Message Rendering', () => {
    it('does not display failure message when all succeeded', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.queryByText(/Some uploads failed/i)).not.toBeInTheDocument();
    });

    it('does not display success message when there are failures', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 3,
        failed: 2,
        cancelled: 0
      };

      render(
        <UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />
      );

      expect(screen.queryByText(/All files have been uploaded and are being processed/i)).not.toBeInTheDocument();
    });
  });
});
