/**
 * Test helpers for UploadQueueItem component tests
 * Shared utilities, mock data, and type definitions
 */

import type { UploadQueueItem as UploadQueueItemType } from '../../hooks/useUploadQueue';

/**
 * Helper to create test items with default values
 */
export function createTestItem(overrides: Partial<UploadQueueItemType> = {}): UploadQueueItemType {
  return {
    id: 'test-id',
    file: new File(['content'], 'test.pdf', { type: 'application/pdf' }),
    gameId: 'game-123',
    language: 'en',
    status: 'pending',
    progress: 0,
    retryCount: 0,
    ...overrides
  };
}

/**
 * Create a file with custom size
 */
export function createFileWithSize(name: string, size: number): File {
  const file = new File(['content'], name, { type: 'application/pdf' });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

/**
 * Create mock callback functions
 */
export const createMockCallbacks = () => ({
  onCancel: vi.fn(),
  onRetry: vi.fn(),
  onRemove: vi.fn()
});

/**
 * Upload status types
 */
export const UPLOAD_STATUS = {
  PENDING: 'pending' as const,
  UPLOADING: 'uploading' as const,
  PROCESSING: 'processing' as const,
  SUCCESS: 'success' as const,
  FAILED: 'failed' as const,
  CANCELLED: 'cancelled' as const
};

/**
 * File size constants for testing
 */
export const FILE_SIZES = {
  BYTES: 500, // 500 B
  KILOBYTES: 2048, // 2 KB
  MEGABYTES: 5 * 1024 * 1024 // 5 MB
};

/**
 * Expected file size display strings
 */
export const FILE_SIZE_DISPLAY = {
  BYTES: '500 B',
  KILOBYTES: '2.0 KB',
  MEGABYTES: '5.0 MB',
  ZERO: '0 B'
};

/**
 * Common test error messages
 */
export const ERROR_MESSAGES = {
  NETWORK: 'Network error',
  SERVER: 'Server returned 500',
  UPLOAD_FAILED: 'Upload failed'
};

/**
 * Default component props for testing
 */
export const createDefaultProps = () => {
  const callbacks = createMockCallbacks();
  return {
    item: createTestItem(),
    ...callbacks
  };
};
