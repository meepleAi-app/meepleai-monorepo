/**
 * Test Helpers for UploadQueue Component Tests
 * Shared factories, utilities, and mocks
 */

import { screen } from '@testing-library/react';
import { vi, expect } from 'vitest';
import type {
  UploadQueueItem as UploadQueueItemType,
  UploadQueueStats,
} from '../../hooks/useUploadQueue';

/**
 * Helper to create test upload queue items
 */
export function createTestItem(overrides: Partial<UploadQueueItemType> = {}): UploadQueueItemType {
  return {
    id: `item-${Math.random()}`,
    file: new File(['content'], 'test.pdf', { type: 'application/pdf' }),
    gameId: 'game-123',
    language: 'en',
    status: 'pending',
    progress: 0,
    retryCount: 0,
    ...overrides,
  };
}

/**
 * Create test stats with default values
 */
export function createTestStats(overrides: Partial<UploadQueueStats> = {}): UploadQueueStats {
  return {
    total: 0,
    pending: 0,
    uploading: 0,
    processing: 0,
    succeeded: 0,
    failed: 0,
    cancelled: 0,
    ...overrides,
  };
}

/**
 * Create multiple test items with sequential IDs
 */
export function createTestItems(
  count: number,
  itemOverrides: Partial<UploadQueueItemType> = {}
): UploadQueueItemType[] {
  return Array.from({ length: count }, (_, i) =>
    createTestItem({
      id: `item-${i}`,
      file: new File([''], `file${i}.pdf`, { type: 'application/pdf' }),
      ...itemOverrides,
    })
  );
}

/**
 * Mock handlers factory
 */
export function createMockHandlers() {
  return {
    onCancel: vi.fn(),
    onRetry: vi.fn(),
    onRemove: vi.fn(),
    onClearCompleted: vi.fn(),
  };
}

/**
 * Common test scenarios
 */
export const testScenarios = {
  emptyQueue: {
    items: [],
    stats: createTestStats(),
  },

  singlePending: {
    items: [createTestItem({ id: '1', status: 'pending' })],
    stats: createTestStats({ total: 1, pending: 1 }),
  },

  singleUploading: {
    items: [createTestItem({ id: '1', status: 'uploading', progress: 50 })],
    stats: createTestStats({ total: 1, uploading: 1 }),
  },

  mixedStatuses: {
    items: [
      createTestItem({ id: '1', status: 'pending' }),
      createTestItem({ id: '2', status: 'uploading' }),
      createTestItem({ id: '3', status: 'processing' }),
      createTestItem({ id: '4', status: 'success' }),
      createTestItem({ id: '5', status: 'failed' }),
      createTestItem({ id: '6', status: 'cancelled' }),
    ],
    stats: createTestStats({
      total: 6,
      pending: 1,
      uploading: 1,
      processing: 1,
      succeeded: 1,
      failed: 1,
      cancelled: 1,
    }),
  },
};

/**
 * Assertion helpers
 */
export const assertionHelpers = {
  expectProgressBar: (expectedProgress: number) => {
    const progressBar = screen.getByRole('progressbar', { name: /Overall upload progress/i });
    expect(progressBar).toHaveAttribute('aria-valuenow', expectedProgress.toString());
    expect(progressBar).toHaveAttribute('aria-valuemin', '0');
    expect(progressBar).toHaveAttribute('aria-valuemax', '100');
  },

  expectStatusMessage: (message: string | RegExp) => {
    expect(screen.getByText(message)).toBeInTheDocument();
  },

  expectStatCount: (statName: string, count: number) => {
    expect(
      screen.getByText((content, element) => {
        return element?.textContent === `${count} ${statName}`;
      })
    ).toBeInTheDocument();
  },

  expectNoStatDisplayed: (statName: string) => {
    expect(screen.queryByText(new RegExp(statName, 'i'))).not.toBeInTheDocument();
  },
};

/**
 * Progress calculation helper
 */
export function calculateExpectedProgress(items: UploadQueueItemType[]): number {
  if (items.length === 0) return 0;
  const total = items.reduce((sum, item) => sum + item.progress, 0);
  return Math.round(total / items.length);
}
