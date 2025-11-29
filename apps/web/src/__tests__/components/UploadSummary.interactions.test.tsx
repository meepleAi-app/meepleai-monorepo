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
import { UploadSummary } from '@/components/upload/UploadSummary';
import type { UploadQueueStats } from '../../hooks/useUploadQueue';
import { vi } from 'vitest';

describe('UploadSummary Interactions', () => {
  const mockOnClose = vi.fn();
  const mockOnClearAll = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Button Actions', () => {
    it('calls onClose when close button clicked', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

      expect(
        screen.getByRole('button', { name: /Clear all items from queue/i })
      ).toBeInTheDocument();
    });

    it('calls onClearAll when clear queue button clicked', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

      const clearButton = screen.getByRole('button', { name: /Clear all items from queue/i });
      fireEvent.click(clearButton);

      expect(mockOnClearAll).toHaveBeenCalledTimes(1);
    });

    it('renders close button with proper styling', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

      const closeButton = screen.getByRole('button', { name: /Close upload summary/i });

      // Verify button exists and is clickable
      expect(closeButton).toBeInTheDocument();
      expect(closeButton).not.toBeDisabled();

      // Verify clicking triggers callback
      fireEvent.click(closeButton);
      expect(mockOnClose).toHaveBeenCalled();
    });

    it('renders clear queue button with proper styling', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

      const clearButton = screen.getByRole('button', { name: /Clear all items from queue/i });

      // Verify button exists and is clickable
      expect(clearButton).toBeInTheDocument();
      expect(clearButton).not.toBeDisabled();

      // Verify clicking triggers callback
      fireEvent.click(clearButton);
      expect(mockOnClearAll).toHaveBeenCalled();
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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

      expect(screen.getByRole('button', { name: /Close upload summary/i })).toHaveAttribute(
        'aria-label'
      );
      expect(screen.getByRole('button', { name: /Clear all items from queue/i })).toHaveAttribute(
        'aria-label'
      );
    });

    it('hides decorative icon from screen readers', () => {
      const stats: UploadQueueStats = {
        total: 5,
        pending: 0,
        uploading: 0,
        processing: 0,
        succeeded: 5,
        failed: 0,
        cancelled: 0,
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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 3,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 3,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

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
        cancelled: 0,
      };

      render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={mockOnClearAll} />);

      expect(
        screen.queryByText(/All files have been uploaded and are being processed/i)
      ).not.toBeInTheDocument();
    });
  });
});
