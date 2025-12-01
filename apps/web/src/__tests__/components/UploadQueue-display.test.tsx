/**
 * UploadQueue Component Tests - Display & UI
 *
 * Tests for empty state, queue rendering, progress, status messages, and stats display
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { UploadQueue } from '@/components/upload/UploadQueue';
import {
  createTestItem,
  createTestStats,
  createTestItems,
  createMockHandlers,
  testScenarios,
  assertionHelpers,
  calculateExpectedProgress,
} from './UploadQueue.test-helpers';

describe('UploadQueue - Display & UI', () => {
  let mockHandlers: ReturnType<typeof createMockHandlers>;

  beforeEach(() => {
    mockHandlers = createMockHandlers();
  });

  describe('Empty Queue State', () => {
    it('renders empty state message when no items', () => {
      render(
        <UploadQueue
          items={testScenarios.emptyQueue.items}
          stats={testScenarios.emptyQueue.stats}
          {...mockHandlers}
        />
      );

      expect(
        screen.getByText(/No files in queue. Select files to begin uploading./i)
      ).toBeInTheDocument();
    });

    it('applies empty state styling', () => {
      render(
        <UploadQueue
          items={testScenarios.emptyQueue.items}
          stats={testScenarios.emptyQueue.stats}
          {...mockHandlers}
        />
      );

      const emptyStateText = screen.getByText(/No files in queue/i);
      const emptyState = emptyStateText.closest('div');
      expect(emptyState).toHaveClass('text-center', 'border-2', 'border-dashed', 'bg-gray-50');
    });
  });

  describe('Queue with Items', () => {
    it('renders upload queue header', () => {
      render(
        <UploadQueue
          items={testScenarios.singlePending.items}
          stats={testScenarios.singlePending.stats}
          {...mockHandlers}
        />
      );

      expect(screen.getByText(/Upload Queue/i)).toBeInTheDocument();
    });

    it('renders all queue items', () => {
      const items = createTestItems(3);
      const stats = createTestStats({ total: 3, pending: 3 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      expect(screen.getByText('file0.pdf')).toBeInTheDocument();
      expect(screen.getByText('file1.pdf')).toBeInTheDocument();
      expect(screen.getByText('file2.pdf')).toBeInTheDocument();
    });

    it('passes correct props to UploadQueueItem components', () => {
      const items = [createTestItem({ id: 'test-1', status: 'uploading', progress: 50 })];
      const stats = createTestStats({ total: 1, uploading: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      const cancelButton = screen.getByRole('button', { name: /Cancel upload/i });
      fireEvent.click(cancelButton);

      expect(mockHandlers.onCancel).toHaveBeenCalledWith('test-1');
    });
  });

  describe('Overall Progress Calculation', () => {
    it('calculates total progress correctly for multiple files', () => {
      const items = [
        createTestItem({ id: '1', progress: 100 }),
        createTestItem({ id: '2', progress: 50 }),
        createTestItem({ id: '3', progress: 0 }),
      ];
      const stats = createTestStats({ total: 3, pending: 1, uploading: 1, succeeded: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      const expectedProgress = calculateExpectedProgress(items);
      assertionHelpers.expectProgressBar(expectedProgress);
    });

    it('handles progress calculation with varying percentages', () => {
      const items = [
        createTestItem({ id: '1', progress: 75 }),
        createTestItem({ id: '2', progress: 25 }),
        createTestItem({ id: '3', progress: 50 }),
        createTestItem({ id: '4', progress: 100 }),
      ];
      const stats = createTestStats({ total: 4, uploading: 3, succeeded: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectProgressBar(63);
    });

    it('shows 0% progress when all items are pending', () => {
      const items = [
        createTestItem({ id: '1', progress: 0 }),
        createTestItem({ id: '2', progress: 0 }),
      ];
      const stats = createTestStats({ total: 2, pending: 2 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectProgressBar(0);
    });

    it('shows 100% progress when all items complete', () => {
      const items = [
        createTestItem({ id: '1', progress: 100 }),
        createTestItem({ id: '2', progress: 100 }),
      ];
      const stats = createTestStats({ total: 2, succeeded: 2 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectProgressBar(100);
    });
  });

  describe('Status Messages', () => {
    it('displays active upload message', () => {
      const items = [
        createTestItem({ id: '1', status: 'uploading', progress: 50 }),
        createTestItem({ id: '2', status: 'pending', progress: 0 }),
      ];
      const stats = createTestStats({ total: 2, pending: 1, uploading: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectStatusMessage(/Uploading 1 of 2 files \(25% total\)/i);
    });

    it('displays waiting message when all files pending', () => {
      const items = createTestItems(2);
      const stats = createTestStats({ total: 2, pending: 2 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectStatusMessage(/2 files waiting to upload/i);
    });

    it('displays complete message when all uploads done', () => {
      const items = [
        createTestItem({ id: '1', status: 'success', progress: 100 }),
        createTestItem({ id: '2', status: 'success', progress: 100 }),
      ];
      const stats = createTestStats({ total: 2, succeeded: 2 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectStatusMessage(/All uploads complete/i);
    });

    it('includes processing in active count', () => {
      const items = [
        createTestItem({ id: '1', status: 'uploading' }),
        createTestItem({ id: '2', status: 'processing' }),
      ];
      const stats = createTestStats({ total: 2, uploading: 1, processing: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectStatusMessage(/Uploading 2 of 2 files/i);
    });
  });

  describe('Stats Summary', () => {
    it('displays all relevant stats', () => {
      render(
        <UploadQueue
          items={testScenarios.mixedStatuses.items}
          stats={testScenarios.mixedStatuses.stats}
          {...mockHandlers}
        />
      );

      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 pending';
        })
      ).toBeInTheDocument();
      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 uploading';
        })
      ).toBeInTheDocument();
      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 processing';
        })
      ).toBeInTheDocument();
      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 succeeded';
        })
      ).toBeInTheDocument();
      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 failed';
        })
      ).toBeInTheDocument();
      expect(
        screen.getByText((content, element) => {
          return element?.textContent === '1 cancelled';
        })
      ).toBeInTheDocument();
    });

    it('hides stats for zero counts', () => {
      const items = [createTestItem({ id: '1', status: 'uploading' })];
      const stats = createTestStats({ total: 1, uploading: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectNoStatDisplayed('pending');
      assertionHelpers.expectNoStatDisplayed('processing');
      assertionHelpers.expectNoStatDisplayed('succeeded');
      assertionHelpers.expectNoStatDisplayed('failed');
      assertionHelpers.expectNoStatDisplayed('cancelled');
    });
  });

  describe('Accessibility', () => {
    it('has accessible queue container', () => {
      render(
        <UploadQueue
          items={testScenarios.singlePending.items}
          stats={testScenarios.singlePending.stats}
          {...mockHandlers}
        />
      );

      expect(screen.getByTestId('upload-queue')).toBeInTheDocument();
    });

    it('has accessible progress bar', () => {
      const items = [createTestItem({ id: '1', progress: 50 })];
      const stats = createTestStats({ total: 1, uploading: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectProgressBar(50);
    });

    it('has accessible list structure', () => {
      const items = createTestItems(2);
      const stats = createTestStats({ total: 2, pending: 2 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      const list = screen.getByRole('list', { name: /Upload queue items/i });
      expect(list).toBeInTheDocument();

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(2);
    });
  });

  describe('Edge Cases', () => {
    it('handles single item in queue', () => {
      const items = [createTestItem({ id: '1', status: 'uploading', progress: 75 })];
      const stats = createTestStats({ total: 1, uploading: 1 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectStatusMessage(/Uploading 1 of 1 files \(75% total\)/i);
    });

    it('handles large number of items', () => {
      const items = createTestItems(50);
      const stats = createTestStats({ total: 50, pending: 50 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      const listItems = screen.getAllByRole('listitem');
      expect(listItems).toHaveLength(50);
    });

    it('handles all items at 0% progress', () => {
      const items = createTestItems(3, { progress: 0 });
      const stats = createTestStats({ total: 3, pending: 3 });

      render(<UploadQueue items={items} stats={stats} {...mockHandlers} />);

      assertionHelpers.expectProgressBar(0);
    });
  });
});
