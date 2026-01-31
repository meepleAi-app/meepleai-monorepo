/**
 * UploadQueue Tests - Issue #2764 Sprint 4
 *
 * Tests for UploadQueue component:
 * - Queue display with items
 * - Aggregate progress tracking
 * - Stats badges
 * - Empty state
 * - Clear completed button
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 118 lines (85%+)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { UploadQueue } from '../UploadQueue';
import type { UploadQueueItem, UploadQueueStats } from '@/hooks/useUploadQueue';

// Helper to create mock upload queue items
function createMockQueueItem(
  id: string,
  fileName: string,
  options: Partial<UploadQueueItem> = {}
): UploadQueueItem {
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

// Helper to create mock stats
function createMockStats(overrides: Partial<UploadQueueStats> = {}): UploadQueueStats {
  return {
    total: 3,
    pending: 0,
    uploading: 0,
    processing: 0,
    succeeded: 0,
    failed: 0,
    cancelled: 0,
    ...overrides,
  };
}

describe('UploadQueue - Issue #2764 Sprint 4', () => {
  // ============================================================================
  // TEST 1: Empty state
  // ============================================================================
  it('should render empty state when no items', () => {
    // Arrange
    const stats = createMockStats({ total: 0 });

    // Act
    render(
      <UploadQueue
        items={[]}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/no files in queue/i)).toBeInTheDocument();
    expect(screen.getByText(/select files to begin uploading/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Queue header with active uploads
  // ============================================================================
  it('should show active upload count in header', () => {
    // Arrange
    const items = [
      createMockQueueItem('1', 'file1.pdf', { status: 'uploading', progress: 50 }),
      createMockQueueItem('2', 'file2.pdf', { status: 'uploading', progress: 30 }),
    ];
    const stats = createMockStats({ total: 3, uploading: 2, pending: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/uploading 2 of 3 files/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Pending files message
  // ============================================================================
  it('should show pending files count when not uploading', () => {
    // Arrange
    const items = [
      createMockQueueItem('1', 'file1.pdf', { status: 'pending' }),
      createMockQueueItem('2', 'file2.pdf', { status: 'pending' }),
    ];
    const stats = createMockStats({ total: 2, pending: 2 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/2 files waiting to upload/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: All complete message
  // ============================================================================
  it('should show all complete message when finished', () => {
    // Arrange
    const items = [
      createMockQueueItem('1', 'file1.pdf', { status: 'success', progress: 100 }),
      createMockQueueItem('2', 'file2.pdf', { status: 'success', progress: 100 }),
    ];
    const stats = createMockStats({ total: 2, succeeded: 2 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/all uploads complete/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Overall progress bar
  // ============================================================================
  it('should display overall progress bar', () => {
    // Arrange
    const items = [
      createMockQueueItem('1', 'file1.pdf', { status: 'uploading', progress: 80 }),
      createMockQueueItem('2', 'file2.pdf', { status: 'uploading', progress: 40 }),
    ];
    const stats = createMockStats({ total: 2, uploading: 2 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert - Progress bar should exist
    const progressBar = screen.getByRole('progressbar', { name: /overall upload progress/i });
    expect(progressBar).toBeInTheDocument();
    expect(progressBar).toHaveAttribute('aria-valuenow', '60'); // (80+40)/2 = 60
  });

  // ============================================================================
  // TEST 6: Pending badge
  // ============================================================================
  it('should show pending badge when files pending', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'pending' })];
    const stats = createMockStats({ total: 1, pending: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/1 pending/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 7: Uploading badge
  // ============================================================================
  it('should show uploading badge during uploads', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'uploading', progress: 50 })];
    const stats = createMockStats({ total: 1, uploading: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/1 uploading/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 8: Processing badge
  // ============================================================================
  it('should show processing badge when files processing', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'processing', progress: 100 })];
    const stats = createMockStats({ total: 1, processing: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/1 processing/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 9: Succeeded badge
  // ============================================================================
  it('should show succeeded badge when files complete', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'success', progress: 100 })];
    const stats = createMockStats({ total: 1, succeeded: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/1 succeeded/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 10: Failed badge
  // ============================================================================
  it('should show failed badge when files failed', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'failed', error: 'Error' })];
    const stats = createMockStats({ total: 1, failed: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/1 failed/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 11: Cancelled badge
  // ============================================================================
  it('should show cancelled badge when files cancelled', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'cancelled' })];
    const stats = createMockStats({ total: 1, cancelled: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText(/1 cancelled/i)).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 12: Clear completed button visibility
  // ============================================================================
  it('should show clear completed button when items are completed', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'success', progress: 100 })];
    const stats = createMockStats({ total: 1, succeeded: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('button', { name: /clear completed/i })).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 13: Clear completed button hidden when no completed
  // ============================================================================
  it('should hide clear completed button when no completed items', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'uploading', progress: 50 })];
    const stats = createMockStats({ total: 1, uploading: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.queryByRole('button', { name: /clear completed/i })).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 14: Clear completed callback
  // ============================================================================
  it('should call onClearCompleted when clear button clicked', async () => {
    // Arrange
    const user = userEvent.setup();
    const mockOnClearCompleted = vi.fn();
    const items = [createMockQueueItem('1', 'file1.pdf', { status: 'success', progress: 100 })];
    const stats = createMockStats({ total: 1, succeeded: 1 });

    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={mockOnClearCompleted}
      />
    );

    // Act
    const clearButton = screen.getByRole('button', { name: /clear completed/i });
    await user.click(clearButton);

    // Assert
    expect(mockOnClearCompleted).toHaveBeenCalledTimes(1);
  });

  // ============================================================================
  // TEST 15: Renders queue items
  // ============================================================================
  it('should render all queue items', () => {
    // Arrange
    const items = [
      createMockQueueItem('1', 'file1.pdf', { status: 'success', progress: 100 }),
      createMockQueueItem('2', 'file2.pdf', { status: 'uploading', progress: 50 }),
      createMockQueueItem('3', 'file3.pdf', { status: 'pending' }),
    ];
    const stats = createMockStats({ total: 3, succeeded: 1, uploading: 1, pending: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByText('file1.pdf')).toBeInTheDocument();
    expect(screen.getByText('file2.pdf')).toBeInTheDocument();
    expect(screen.getByText('file3.pdf')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 16: Queue list accessibility
  // ============================================================================
  it('should have accessible list structure', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf')];
    const stats = createMockStats({ total: 1, pending: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByRole('list', { name: /upload queue items/i })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
  });

  // ============================================================================
  // TEST 17: Test data attribute
  // ============================================================================
  it('should have testid for queue container', () => {
    // Arrange
    const items = [createMockQueueItem('1', 'file1.pdf')];
    const stats = createMockStats({ total: 1, pending: 1 });

    // Act
    render(
      <UploadQueue
        items={items}
        stats={stats}
        onCancel={vi.fn()}
        onRetry={vi.fn()}
        onRemove={vi.fn()}
        onClearCompleted={vi.fn()}
      />
    );

    // Assert
    expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
  });
});
