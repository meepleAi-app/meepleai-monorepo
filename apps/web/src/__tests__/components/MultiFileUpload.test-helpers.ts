/**
 * MultiFileUpload Test Helpers
 * Shared mocks, utilities, and setup for MultiFileUpload test suite
 */

// Mock functions for useUploadQueue hook
export const mockAddFiles = vi.fn();
export const mockRemoveFile = vi.fn();
export const mockCancelUpload = vi.fn();
export const mockRetryUpload = vi.fn();
export const mockClearCompleted = vi.fn();
export const mockClearAll = vi.fn();
export const mockGetStats = vi.fn();
export const mockStartUpload = vi.fn();

// Shared queue state - exported as object so tests can modify the reference
export const mockQueueStateRef = { current: [] as any[] };

/**
 * Mock for useUploadQueue hook
 * Must be called in test setup (vi.mock) to properly mock the hook
 */
export const createMockUseUploadQueue = () => {
  vi.mock('../../hooks/useUploadQueue', () => ({
    useUploadQueue: vi.fn((options) => ({
      queue: mockQueueStateRef.current,
      addFiles: mockAddFiles,
      removeFile: mockRemoveFile,
      cancelUpload: mockCancelUpload,
      retryUpload: mockRetryUpload,
      clearCompleted: mockClearCompleted,
      clearAll: mockClearAll,
      getStats: mockGetStats,
      startUpload: mockStartUpload
    }))
  }));
};

/**
 * Helper to create File objects with proper size and type
 */
export function createMockFile(name: string, size: number, type: string, content = '%PDF-1.4'): File {
  const file = new File([content], name, { type });
  Object.defineProperty(file, 'size', { value: size });
  return file;
}

/**
 * Helper to create FileReader mock - returns a new instance for each FileReader() call
 * This avoids shared state issues between multiple file validations
 */
export function mockFileReader(result: string) {
  (global as any).FileReader = vi.fn(function(this: any) {
    // Create a NEW instance for each FileReader() call to avoid shared state
    this.readAsArrayBuffer = vi.fn(function (this: any) {
      const buffer = new ArrayBuffer(result.length);
      const view = new Uint8Array(buffer);
      for (let i = 0; i < result.length; i++) {
        view[i] = result.charCodeAt(i);
      }
      this.result = buffer;
      // Use setTimeout with 0 delay for consistent async behavior
      setTimeout(() => {
        if (this.onload) {
          this.onload({ target: { result: buffer } });
        }
      }, 0);
    });
    this.onload = null;
    this.onerror = null;
    this.result = null;
  });
}

/**
 * Default props for MultiFileUpload component
 */
export const defaultProps = {
  gameId: 'game-123',
  gameName: 'Test Game',
  language: 'en'
};

/**
 * Default stats object for empty queue
 */
export const emptyStats = {
  total: 0,
  pending: 0,
  uploading: 0,
  processing: 0,
  succeeded: 0,
  failed: 0,
  cancelled: 0
};

/**
 * Helper to create stats object with custom values
 */
export function createStats(overrides: Partial<typeof emptyStats>) {
  return { ...emptyStats, ...overrides };
}

/**
 * Common beforeEach setup for all MultiFileUpload tests
 */
export function setupBeforeEach() {
  vi.clearAllMocks();
  mockQueueStateRef.current = [];
  mockGetStats.mockReturnValue(emptyStats);
  // Reset FileReader mock to default - use '%PDF-' (5 bytes) to match PDF_MAGIC_BYTES constant
  mockFileReader('%PDF-');
}

/**
 * Common afterEach cleanup for all MultiFileUpload tests
 */
export function setupAfterEach() {
  vi.restoreAllMocks();
  mockQueueStateRef.current = [];
}

/**
 * Helper to wait for async file validation and addFiles call
 * @param delayMs - milliseconds to wait (default 100ms for single file)
 */
export async function waitForFileValidation(delayMs = 100) {
  await new Promise(resolve => setTimeout(resolve, delayMs));
}

/**
 * Helper to trigger file selection via input element
 */
export function selectFiles(input: HTMLInputElement, files: File[]) {
  return {
    target: { files }
  };
}

/**
 * Helper to trigger file drop via drag and drop
 */
export function dropFiles(files: File[]) {
  return {
    dataTransfer: { files }
  };
}

/**
 * Helper to get useUploadQueue options from last mock call
 * Useful for testing callbacks passed to the hook
 */
export function getLastUploadQueueOptions() {
  const { useUploadQueue } = require('../../hooks/useUploadQueue');
  const lastCall = useUploadQueue.mock.calls[useUploadQueue.mock.calls.length - 1];
  return lastCall?.[0] || {};
}
