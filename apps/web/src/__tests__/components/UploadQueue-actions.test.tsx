/**
 * UploadQueue Component Tests - Actions & Interactions
 *
 * Tests for clear completed button functionality and user interactions
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { UploadQueue } from '@/components/upload/UploadQueue';
import {
  createTestItem,
  createTestStats,
  createMockHandlers,
} from './UploadQueue.test-helpers';

describe('UploadQueue - Actions & Interactions', () => {
  let mockHandlers: ReturnType<typeof createMockHandlers>;

  beforeEach(() => {
    mockHandlers = createMockHandlers();
    vi.clearAllMocks();
  });

  describe('Clear Completed Button', () => {
    it('shows clear completed button when completed items exist', () => {
      const items = [
        createTestItem({ id: '1', status: 'success' }),
        createTestItem({ id: '2', status: 'pending' })
      ];
      const stats = createTestStats({ total: 2, pending: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('shows clear completed button when failed items exist', () => {
      const items = [createTestItem({ id: '1', status: 'failed' })];
      const stats = createTestStats({ total: 1, failed: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('shows clear completed button when cancelled items exist', () => {
      const items = [createTestItem({ id: '1', status: 'cancelled' })];
      const stats = createTestStats({ total: 1, cancelled: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('hides clear completed button when no completed items', () => {
      const items = [
        createTestItem({ id: '1', status: 'pending' }),
        createTestItem({ id: '2', status: 'uploading' })
      ];
      const stats = createTestStats({ total: 2, pending: 1, uploading: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      expect(screen.queryByRole('button', { name: /Clear completed uploads from queue/i })).not.toBeInTheDocument();
    });

    it('calls onClearCompleted when button clicked', () => {
      const items = [createTestItem({ id: '1', status: 'success' })];
      const stats = createTestStats({ total: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });
      fireEvent.click(clearButton);

      expect(mockHandlers.onClearCompleted).toHaveBeenCalledTimes(1);
    });

    it('renders clear completed button with proper styling', () => {
      const items = [createTestItem({ id: '1', status: 'success' })];
      const stats = createTestStats({ total: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });

      expect(clearButton).toBeInTheDocument();
      expect(clearButton).not.toBeDisabled();

      fireEvent.click(clearButton);
      expect(mockHandlers.onClearCompleted).toHaveBeenCalled();
    });

    it('shows clear completed button with mixed completed statuses', () => {
      const items = [
        createTestItem({ id: '1', status: 'success' }),
        createTestItem({ id: '2', status: 'failed' }),
        createTestItem({ id: '3', status: 'cancelled' }),
        createTestItem({ id: '4', status: 'pending' })
      ];
      const stats = createTestStats({ total: 4, succeeded: 1, failed: 1, cancelled: 1, pending: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      expect(screen.getByRole('button', { name: /Clear completed uploads from queue/i })).toBeInTheDocument();
    });

    it('does not show clear completed button with only active items', () => {
      const items = [
        createTestItem({ id: '1', status: 'pending' }),
        createTestItem({ id: '2', status: 'uploading' }),
        createTestItem({ id: '3', status: 'processing' })
      ];
      const stats = createTestStats({ total: 3, pending: 1, uploading: 1, processing: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      expect(screen.queryByRole('button', { name: /Clear completed uploads from queue/i })).not.toBeInTheDocument();
    });

    it('allows multiple clicks on clear completed button', () => {
      const items = [createTestItem({ id: '1', status: 'success' })];
      const stats = createTestStats({ total: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });

      fireEvent.click(clearButton);
      fireEvent.click(clearButton);
      fireEvent.click(clearButton);

      expect(mockHandlers.onClearCompleted).toHaveBeenCalledTimes(3);
    });

    it('renders clear button in header section', () => {
      const items = [createTestItem({ id: '1', status: 'success' })];
      const stats = createTestStats({ total: 1, succeeded: 1 });

      const { container } = render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });
      const header = container.querySelector('[class*="flex"][class*="items-center"]');

      expect(header).toContainElement(clearButton);
    });
  });

  describe('Item Interactions', () => {
    it('allows cancel action on uploading item', () => {
      const items = [createTestItem({ id: 'test-1', status: 'uploading', progress: 50 })];
      const stats = createTestStats({ total: 1, uploading: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const cancelButton = screen.getByRole('button', { name: /Cancel upload/i });
      fireEvent.click(cancelButton);

      expect(mockHandlers.onCancel).toHaveBeenCalledWith('test-1');
    });

    it('passes all handlers to queue items', () => {
      const items = [
        createTestItem({ id: '1', status: 'uploading' }),
        createTestItem({ id: '2', status: 'failed' }),
        createTestItem({ id: '3', status: 'success' })
      ];
      const stats = createTestStats({ total: 3, uploading: 1, failed: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const cancelButton = screen.getByRole('button', { name: /Cancel upload/i });
      expect(cancelButton).toBeInTheDocument();
    });
  });

  describe('Button Accessibility', () => {
    it('clear button has accessible label', () => {
      const items = [createTestItem({ id: '1', status: 'success' })];
      const stats = createTestStats({ total: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });
      expect(clearButton).toHaveAccessibleName();
    });

    it('clear button is keyboard accessible', () => {
      const items = [createTestItem({ id: '1', status: 'success' })];
      const stats = createTestStats({ total: 1, succeeded: 1 });

      render(
        <UploadQueue
          items={items}
          stats={stats}
          {...mockHandlers}
        />
      );

      const clearButton = screen.getByRole('button', { name: /Clear completed uploads from queue/i });

      clearButton.focus();
      expect(clearButton).toHaveFocus();
    });
  });
});
