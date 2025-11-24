/**
 * Test Helpers for useUploadQueue Hook
 *
 * Shared utilities, mocks, and test data factories for useUploadQueue tests
 */

/**
 * Setup fake timers and mock fetch for tests
 */
export function setupTestEnvironment(): {
  mockFetch: Mock;
  cleanup: () => void;
} {
  vi.useFakeTimers();
  const originalFetch = global.fetch;
  const mockFetch = vi.fn();
  global.fetch = mockFetch;

  // Mock successful upload by default
  mockFetch.mockResolvedValue({
    ok: true,
    status: 200,
    json: async () => ({ documentId: 'pdf-123' }),
  });

  return {
    mockFetch,
    cleanup: () => {
      vi.useRealTimers();
      global.fetch = originalFetch;
    },
  };
}

/**
 * Create test file with specified name and content
 */
export function createTestFile(name: string, content: string = 'content'): File {
  return new File([content], name, { type: 'application/pdf' });
}

/**
 * Create multiple test files
 */
export function createTestFiles(count: number, baseName: string = 'test'): File[] {
  return Array.from({ length: count }, (_, i) => createTestFile(`${baseName}${i + 1}.pdf`, `content${i + 1}`));
}

/**
 * Create a large test file
 */
export function createLargeTestFile(sizeInMB: number = 100): File {
  return new File([new ArrayBuffer(sizeInMB * 1024 * 1024)], 'large.pdf');
}

/**
 * Verify queue item has expected properties
 */
export function expectQueueItem(
  item: any,
  expected: {
    file?: File;
    gameId?: string;
    language?: string;
    status?: string;
    progress?: number;
  }
): void {
  if (expected.file) expect(item.file).toBe(expected.file);
  if (expected.gameId) expect(item.gameId).toBe(expected.gameId);
  if (expected.language) expect(item.language).toBe(expected.language);
  if (expected.status) expect(item.status).toBe(expected.status);
  if (expected.progress !== undefined) expect(item.progress).toBe(expected.progress);
}

/**
 * Verify queue stats match expected values
 */
export function expectQueueStats(
  stats: any,
  expected: {
    total?: number;
    pending?: number;
    uploading?: number;
    processing?: number;
    succeeded?: number;
    failed?: number;
  }
): void {
  if (expected.total !== undefined) expect(stats.total).toBe(expected.total);
  if (expected.pending !== undefined) expect(stats.pending).toBe(expected.pending);
  if (expected.uploading !== undefined) expect(stats.uploading).toBe(expected.uploading);
  if (expected.processing !== undefined) expect(stats.processing).toBe(expected.processing);
  if (expected.succeeded !== undefined) expect(stats.succeeded).toBe(expected.succeeded);
  if (expected.failed !== undefined) expect(stats.failed).toBe(expected.failed);
}

/**
 * Mock fetch to simulate slow upload
 */
export function mockSlowUpload(mockFetch: Mock, delayMs: number = 5000): void {
  mockFetch.mockImplementation(() =>
    new Promise((resolve) => {
      setTimeout(() => {
        resolve({
          ok: true,
          json: async () => ({ documentId: 'pdf-123' }),
        });
      }, delayMs);
    })
  );
}

/**
 * Mock fetch to simulate upload failure
 */
export function mockUploadFailure(mockFetch: Mock, status: number = 500, message: string = 'Upload failed'): void {
  mockFetch.mockResolvedValue({
    ok: false,
    status,
    json: async () => ({ error: message }),
  });
}
