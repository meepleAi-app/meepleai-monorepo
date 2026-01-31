/**
 * UploadQueueItem Tests - Issue #2764 Sprint 4
 *
 * Tests for UploadQueueItem component:
 * - Status display (pending, uploading, processing, success, failed, cancelled)
 * - Progress bar for active uploads
 * - Action buttons (cancel, retry, remove)
 * - Error message display
 * - File size formatting
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 159 lines (85%+)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { UploadQueueItem } from '../UploadQueueItem';
import type { UploadQueueItem as UploadQueueItemType, UploadStatus } from '@/hooks/useUploadQueue';

// Helper to create mock upload queue items
function createMockQueueItem(
  id: string,
  fileName: string,
  options: Partial<UploadQueueItemType> = {}
): UploadQueueItemType {
  return {
    id,
    file: new File(['content'], fileName, { type: 'application/pdf' }),
    gameId: 'game-123',
    language: 'en',
    status: 'pending',
    progress: 0,
    retryCount: 0,
    ...options,
  };
}

describe('UploadQueueItem - Issue #2764 Sprint 4', () => {
  // ============================================================================
  // TEST 1: Render file name
  // ============================================================================
  it('should display file name', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test-document.pdf');

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText('test-document.pdf')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: File name title attribute
  // ============================================================================
  it('should have title attribute for long file names', () => {
    // Arrange
    const longName = 'very-long-filename-that-would-be-truncated.pdf';
    const item = createMockQueueItem('1', longName);

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    const fileName = screen.getByText(longName);
    expect(fileName).toHaveAttribute('title', longName);
  });

  // ============================================================================
  // TEST 3: File size display - bytes
  // ============================================================================
  it('should format file size in bytes', () => {
    // Arrange
    const item = createMockQueueItem('1', 'small.pdf');
    Object.defineProperty(item.file, 'size', { value: 500 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/500 B/)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: File size display - KB
  // ============================================================================
  it('should format file size in KB', () => {
    // Arrange
    const item = createMockQueueItem('1', 'medium.pdf');
    Object.defineProperty(item.file, 'size', { value: 2048 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/2\.0 KB/)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: File size display - MB
  // ============================================================================
  it('should format file size in MB', () => {
    // Arrange
    const item = createMockQueueItem('1', 'large.pdf');
    Object.defineProperty(item.file, 'size', { value: 5 * 1024 * 1024 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/5\.0 MB/)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 6: Pending status badge
  // ============================================================================
  it('should show pending status badge', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'pending' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Pending');
  });

  // ============================================================================
  // TEST 7: Uploading status badge
  // ============================================================================
  it('should show uploading status badge', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'uploading', progress: 50 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Uploading');
  });

  // ============================================================================
  // TEST 8: Processing status badge
  // ============================================================================
  it('should show processing status badge', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'processing', progress: 100 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Processing');
  });

  // ============================================================================
  // TEST 9: Success status badge
  // ============================================================================
  it('should show success status badge', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'success', progress: 100 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Success');
  });

  // ============================================================================
  // TEST 10: Failed status badge
  // ============================================================================
  it('should show failed status badge', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'failed', error: 'Upload failed' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Failed');
  });

  // ============================================================================
  // TEST 11: Cancelled status badge
  // ============================================================================
  it('should show cancelled status badge', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'cancelled' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('status')).toHaveTextContent('Cancelled');
  });

  // ============================================================================
  // TEST 12: Progress bar during upload
  // ============================================================================
  it('should show progress bar during uploading', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'uploading', progress: 75 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    const progressBar = screen.getByRole('progressbar', { name: /upload progress for test\.pdf/i });
    expect(progressBar).toBeInTheDocument();
    expect(progressBar).toHaveAttribute('aria-valuenow', '75');
    expect(screen.getByText('75% complete')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 13: Progress bar during processing
  // ============================================================================
  it('should show progress bar during processing', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'processing', progress: 100 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('progressbar')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 14: No progress bar for pending
  // ============================================================================
  it('should not show progress bar for pending status', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'pending' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.queryByRole('progressbar')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 15: Error message display
  // ============================================================================
  it('should display error message when failed', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', {
      status: 'failed',
      error: 'Network connection failed',
    });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    const errorAlert = screen.getByRole('alert');
    expect(errorAlert).toHaveTextContent('Network connection failed');
  });

  // ============================================================================
  // TEST 16: Correlation ID in error
  // ============================================================================
  it('should display correlation ID when available', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', {
      status: 'failed',
      error: 'Server error',
      correlationId: 'corr-123-456',
    });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/error id: corr-123-456/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 17: Cancel button during upload
  // ============================================================================
  it('should show cancel button during uploading', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'uploading', progress: 50 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /cancel upload of test\.pdf/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 18: Cancel button callback
  // ============================================================================
  it('should call onCancel when cancel button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnCancel = vi.fn();
    const item = createMockQueueItem('1', 'test.pdf', { status: 'uploading', progress: 50 });

    render(
      <UploadQueueItem
        item={item}
        onCancel={mockOnCancel}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Act
    const cancelButton = screen.getByRole('button', { name: /cancel upload of test\.pdf/i });
    await user.click(cancelButton);

    // Assert
    expect(mockOnCancel).toHaveBeenCalledWith('1');
  });

  // ============================================================================
  // TEST 19: Retry button when failed
  // ============================================================================
  it('should show retry button when failed', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'failed', error: 'Error' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /retry upload of test\.pdf/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 20: Retry button callback
  // ============================================================================
  it('should call onRetry when retry button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnRetry = vi.fn();
    const item = createMockQueueItem('1', 'test.pdf', { status: 'failed', error: 'Error' });

    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={mockOnRetry}
        onRemove={vi.fn()}
      />
    );

    // Act
    const retryButton = screen.getByRole('button', { name: /retry upload of test\.pdf/i });
    await user.click(retryButton);

    // Assert
    expect(mockOnRetry).toHaveBeenCalledWith('1');
  });

  // ============================================================================
  // TEST 21: Remove button for pending
  // ============================================================================
  it('should show remove button for pending status', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'pending' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /remove test\.pdf from queue/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 22: Remove button for cancelled
  // ============================================================================
  it('should show remove button for cancelled status', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'cancelled' });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /remove test\.pdf from queue/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 23: Remove button callback
  // ============================================================================
  it('should call onRemove when remove button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnRemove = vi.fn();
    const item = createMockQueueItem('1', 'test.pdf', { status: 'pending' });

    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={mockOnRemove}
      />
    );

    // Act
    const removeButton = screen.getByRole('button', { name: /remove test\.pdf from queue/i });
    await user.click(removeButton);

    // Assert
    expect(mockOnRemove).toHaveBeenCalledWith('1');
  });

  // ============================================================================
  // TEST 24: Retry count display
  // ============================================================================
  it('should display retry count when retried', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', {
      status: 'uploading',
      progress: 30,
      retryCount: 2,
    });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/retry 2/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 25: Test ID attribute
  // ============================================================================
  it('should have testid with item id', () => {
    // Arrange
    const item = createMockQueueItem('item-123', 'test.pdf');

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByTestId('upload-queue-item-item-123')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 26: Background color by status
  // ============================================================================
  it('should apply background color based on status', () => {
    // Arrange
    const item = createMockQueueItem('1', 'test.pdf', { status: 'success', progress: 100 });

    // Act
    render(
      <UploadQueueItem
        item={item}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
      />
    );

    // Assert - Success has green background
    const container = screen.getByTestId('upload-queue-item-1');
    expect(container).toHaveStyle({ backgroundColor: '#e8f5e9' });
  });
});
