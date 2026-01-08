/**
 * UploadSummary Tests - Issue #2308 Week 4 Phase 2
 *
 * Branch coverage tests for UploadSummary component:
 * 1. All succeeded state (green styling)
 * 2. Mixed results with failures (warning styling)
 * 3. Shows failed count stat card conditionally
 * 4. Shows cancelled count stat card conditionally
 * 5. Displays failure alert when failures exist
 * 6. Displays success message when all succeeded
 * 7. Calls onClose and onClearAll handlers
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 113 lines (~2% of total)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { UploadSummary } from '../UploadSummary';
import { UploadQueueStats } from '@/hooks/useUploadQueue';

describe('UploadSummary - Issue #2308 Phase 2', () => {
  // ============================================================================
  // TEST 1: All succeeded state
  // ============================================================================
  it('should display success styling when all uploads succeeded', () => {
    // Arrange
    const stats: UploadQueueStats = {
      total: 5,
      succeeded: 5,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    render(<UploadSummary stats={stats} onClose={vi.fn()} onClearAll={vi.fn()} />);

    // Assert - Success icon
    expect(screen.getByText('✅')).toBeInTheDocument();

    // Assert - Success message
    expect(screen.getByText('All 5 files uploaded successfully!')).toBeInTheDocument();

    // Assert - Green success message box
    expect(
      screen.getByText(/All files have been uploaded and are being processed/i)
    ).toBeInTheDocument();

    // Assert - Stats visible
    const totalStat = screen.getByText('Total').previousElementSibling;
    expect(totalStat).toHaveTextContent('5');

    const succeededStat = screen.getByText('Succeeded').previousElementSibling;
    expect(succeededStat).toHaveTextContent('5');
  });

  // ============================================================================
  // TEST 2: Mixed results with failures
  // ============================================================================
  it('should display warning styling when some uploads failed', () => {
    // Arrange
    const stats: UploadQueueStats = {
      total: 5,
      succeeded: 3,
      failed: 2,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    render(<UploadSummary stats={stats} onClose={vi.fn()} onClearAll={vi.fn()} />);

    // Assert - Warning icon
    expect(screen.getByText('⚠️')).toBeInTheDocument();

    // Assert - Mixed result message
    expect(screen.getByText('3 succeeded, 2 failed')).toBeInTheDocument();

    // Assert - Failure alert displayed
    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Some uploads failed');
    expect(alert).toHaveTextContent('Review the failed items in the queue');
  });

  // ============================================================================
  // TEST 3: Failed count stat card
  // ============================================================================
  it('should show failed stat card only when failures exist', () => {
    // Arrange - With failures
    const statsWithFailures: UploadQueueStats = {
      total: 3,
      succeeded: 1,
      failed: 2,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    const { rerender } = render(
      <UploadSummary stats={statsWithFailures} onClose={vi.fn()} onClearAll={vi.fn()} />
    );

    // Assert - Failed card visible
    expect(
      screen.getByText('2', { selector: 'div.text-2xl.font-bold.text-red-600' })
    ).toBeInTheDocument();
    expect(screen.getByText('Failed')).toBeInTheDocument();

    // Arrange - No failures
    const statsNoFailures: UploadQueueStats = {
      total: 3,
      succeeded: 3,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    rerender(<UploadSummary stats={statsNoFailures} onClose={vi.fn()} onClearAll={vi.fn()} />);

    // Assert - Failed card hidden
    expect(screen.queryByText('Failed')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: Cancelled count stat card
  // ============================================================================
  it('should show cancelled stat card only when cancellations exist', () => {
    // Arrange - With cancellations
    const statsWithCancelled: UploadQueueStats = {
      total: 4,
      succeeded: 2,
      failed: 0,
      cancelled: 2,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    const { rerender } = render(
      <UploadSummary stats={statsWithCancelled} onClose={vi.fn()} onClearAll={vi.fn()} />
    );

    // Assert - Cancelled card visible
    expect(
      screen.getByText('2', { selector: 'div.text-2xl.font-bold.text-gray-600' })
    ).toBeInTheDocument();
    expect(screen.getByText('Cancelled')).toBeInTheDocument();

    // Assert - Message includes cancelled count
    expect(screen.getByText(/2 succeeded, 0 failed, 2 cancelled/i)).toBeInTheDocument();

    // Arrange - No cancellations
    const statsNoCancelled: UploadQueueStats = {
      total: 2,
      succeeded: 2,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    rerender(<UploadSummary stats={statsNoCancelled} onClose={vi.fn()} onClearAll={vi.fn()} />);

    // Assert - Cancelled card hidden
    expect(screen.queryByText('Cancelled')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Failure alert
  // ============================================================================
  it('should display failure alert only when failures exist', () => {
    // Arrange - With failures
    const statsWithFailures: UploadQueueStats = {
      total: 3,
      succeeded: 1,
      failed: 2,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    const { rerender } = render(
      <UploadSummary stats={statsWithFailures} onClose={vi.fn()} onClearAll={vi.fn()} />
    );

    // Assert - Alert visible
    expect(screen.getByRole('alert')).toHaveTextContent('Some uploads failed');

    // Arrange - No failures
    const statsNoFailures: UploadQueueStats = {
      total: 3,
      succeeded: 3,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    rerender(<UploadSummary stats={statsNoFailures} onClose={vi.fn()} onClearAll={vi.fn()} />);

    // Assert - No alert
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 6: Success message
  // ============================================================================
  it('should display success message only when all succeeded', () => {
    // Arrange - All succeeded
    const statsAllSuccess: UploadQueueStats = {
      total: 4,
      succeeded: 4,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    const { rerender } = render(
      <UploadSummary stats={statsAllSuccess} onClose={vi.fn()} onClearAll={vi.fn()} />
    );

    // Assert - Success message visible
    expect(
      screen.getByText(/All files have been uploaded and are being processed/i)
    ).toBeInTheDocument();

    // Arrange - Partial success
    const statsPartial: UploadQueueStats = {
      total: 4,
      succeeded: 3,
      failed: 1,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    rerender(<UploadSummary stats={statsPartial} onClose={vi.fn()} onClearAll={vi.fn()} />);

    // Assert - Success message hidden
    expect(
      screen.queryByText(/All files have been uploaded and are being processed/i)
    ).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 7: Button handlers
  // ============================================================================
  it('should call onClose when Close button clicked', async () => {
    const user = userEvent.setup();
    const mockOnClose = vi.fn();

    // Arrange
    const stats: UploadQueueStats = {
      total: 2,
      succeeded: 2,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    render(<UploadSummary stats={stats} onClose={mockOnClose} onClearAll={vi.fn()} />);

    const closeButton = screen.getByLabelText('Close upload summary');
    await user.click(closeButton);

    // Assert
    expect(mockOnClose).toHaveBeenCalledTimes(1);
  });

  it('should call onClearAll when Clear Queue button clicked', async () => {
    const user = userEvent.setup();
    const mockOnClearAll = vi.fn();

    // Arrange
    const stats: UploadQueueStats = {
      total: 2,
      succeeded: 2,
      failed: 0,
      cancelled: 0,
      pending: 0,
      uploading: 0,
      processing: 0,
    };

    // Act
    render(<UploadSummary stats={stats} onClose={vi.fn()} onClearAll={mockOnClearAll} />);

    const clearButton = screen.getByLabelText('Clear all items from queue');
    await user.click(clearButton);

    // Assert
    expect(mockOnClearAll).toHaveBeenCalledTimes(1);
  });
});
