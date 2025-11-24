/**
 * Shared test helpers for useUploadQueue worker tests
 *
 * Common mocks, utilities, and setup for all worker-specific test suites:
 * - worker-protocol.test.ts
 * - worker-persistence.test.ts
 * - worker-sync.test.ts
 * - worker-buffering.test.ts
 */

import { MockUploadWorker, MockBroadcastChannel } from '../../__tests__/helpers/uploadQueueMocks';

/**
 * Type extension for worker-specific properties being tested
 * These properties are planned for future worker implementation
 * @see Issue #TBD - Web Worker Integration for Upload Queue
 */
export type UseUploadQueueWithWorker = ReturnType<typeof import('../useUploadQueue').useUploadQueue> & {
  isWorkerReady?: boolean;
  workerError?: Error | null;
};

/**
 * Mock localStorage implementation
 */
export const createLocalStorageMock = () => {
  const storage: { [key: string]: string } = {};
  return {
    storage,
    mock: {
      getItem: vi.fn((key: string) => storage[key] || null),
      setItem: vi.fn((key: string, value: string) => {
        storage[key] = value;
      }),
      removeItem: vi.fn((key: string) => {
        delete storage[key];
      }),
      clear: vi.fn(() => {
        Object.keys(storage).forEach(key => delete storage[key]);
      }),
      length: 0,
      key: vi.fn(),
    },
  };
};

/**
 * Setup global mocks for worker tests
 * Must be called BEFORE importing modules that use Worker
 */
export const setupGlobalMocks = () => {
  let mockWorkerInstance: MockUploadWorker;

  // Mock Worker globally
  global.Worker = vi.fn(() => {
    mockWorkerInstance = new MockUploadWorker({ uploadDelay: 10, autoUpload: true });
    return mockWorkerInstance as any;
  }) as any;

  // Mock BroadcastChannel globally
  global.BroadcastChannel = MockBroadcastChannel as any;

  // Mock fetch
  global.fetch = vi.fn();

  return { getMockWorkerInstance: () => mockWorkerInstance };
};

/**
 * Setup localStorage mock
 */
export const setupLocalStorage = () => {
  const { storage, mock } = createLocalStorageMock();
  global.localStorage = mock as any;
  return { storage, clearStorage: () => Object.keys(storage).forEach(key => delete storage[key]) };
};

/**
 * Common beforeEach setup for all worker tests
 */
export const setupWorkerTestEnvironment = async (
  mockWorkerInstance: MockUploadWorker,
  localStorageStorage: { [key: string]: string }
) => {
  vi.clearAllMocks();
  Object.keys(localStorageStorage).forEach(key => delete localStorageStorage[key]);

  // Note: MockBroadcastChannel.clearAll() is slow, relies on global testTimeout: 30s
  MockBroadcastChannel.clearAll();

  // Reset mock worker instance
  (global.Worker as Mock).mockClear();

  (global.fetch as Mock).mockResolvedValue({
    ok: true,
    json: async () => ({ documentId: 'pdf-123' }),
    headers: new Headers(),
  });

  // Clear the queue using the store's clearAll method
  const { uploadQueueStore } = await import('../../stores/UploadQueueStore');
  uploadQueueStore.clearAll();

  // Wait for cleanup to complete
  await new Promise(resolve => setTimeout(resolve, 100));
};

/**
 * Common afterEach cleanup for all worker tests
 */
export const cleanupWorkerTestEnvironment = async () => {
  const { uploadQueueStore } = await import('../../stores/UploadQueueStore');
  uploadQueueStore.clearAll();
};

/**
 * Create a test PDF file
 */
export const createTestPdfFile = (name: string = 'test.pdf', content: string = 'test') => {
  return new File([content], name, { type: 'application/pdf' });
};

/**
 * Create multiple test PDF files
 */
export const createTestPdfFiles = (count: number, prefix: string = 'file') => {
  return Array.from(
    { length: count },
    (_, i) => new File([`content ${i}`], `${prefix}-${i}.pdf`, { type: 'application/pdf' })
  );
};

/**
 * Common test game ID
 */
export const TEST_GAME_ID = '770e8400-e29b-41d4-a716-000000000123';

/**
 * Alternative test game ID
 */
export const TEST_GAME_ID_ALT = '770e8400-e29b-41d4-a716-000000000456';

/**
 * Common test language
 */
export const TEST_LANGUAGE = 'en';

/**
 * Alternative test language
 */
export const TEST_LANGUAGE_ALT = 'it';

/**
 * Wait for queue item to have specific status
 */
export const waitForItemStatus = async (
  result: any,
  expectedStatus: string,
  options: { timeout?: number; index?: number } = {}
) => {
  const { timeout = 5000, index = 0 } = options;
  const { waitFor } = await import('@testing-library/react');

  await waitFor(
    () => {
      const item = result.current.queue[index];
      return item?.status === expectedStatus;
    },
    { timeout }
  );
};

/**
 * Wait for queue length
 */
export const waitForQueueLength = async (result: any, expectedLength: number, timeout: number = 5000) => {
  const { waitFor } = await import('@testing-library/react');

  await waitFor(
    () => {
      expect(result.current.queue.length).toBe(expectedLength);
    },
    { timeout }
  );
};
