/**
 * Test Helpers for Chat Client Tests
 * Shared mocks, utilities, and factories
 */

import { HttpClient } from '../../core/httpClient';
import { downloadFile } from '../../core/httpClient';

// Export downloadFile as mock for test usage
export const mockDownloadFile = vi.fn();

// Mock downloadFile at module level
vi.mock('../../core/httpClient', async () => {
  const actual = await vi.importActual('../../core/httpClient');
  return {
    ...actual,
    downloadFile: mockDownloadFile,
  };
});

/**
 * Create a mock HTTP client for testing
 */
export function createMockHttpClient(): Mocked<HttpClient> {
  return {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn(),
    postFile: vi.fn(),
  } as any;
}

/**
 * Mock thread data factory
 */
export function createMockThread(overrides = {}) {
  return {
    id: 'thread-123',
    title: 'Test Thread',
    gameId: 'game-1',
    status: 'active',
    ...overrides,
  };
}

/**
 * Mock message data factory
 */
export function createMockMessage(overrides = {}) {
  return {
    id: 'msg-123',
    content: 'Test message',
    role: 'user',
    ...overrides,
  };
}

/**
 * Mock comment data factory
 */
export function createMockComment(overrides = {}) {
  return {
    id: 'comment-123',
    atomId: 'atom-1',
    lineNumber: 42,
    commentText: 'Test comment',
    createdAt: new Date().toISOString(),
    ...overrides,
  };
}

/**
 * Mock cache stats factory
 */
export function createMockCacheStats(overrides = {}) {
  return {
    totalEntries: 100,
    hitRate: 0.85,
    memoryUsage: 512000,
    ...overrides,
  };
}

/**
 * Common test expectations
 */
export const commonExpectations = {
  expectVoidReturn: (result: any) => {
    expect(result).toBeUndefined();
  },

  expectNullReturn: (result: any) => {
    expect(result).toBeNull();
  },

  expectEmptyArray: (result: any) => {
    expect(result).toEqual([]);
  },
};

/**
 * URL encoding test helpers
 */
export const urlHelpers = {
  expectEncodedUrl: (mockFn: any, expectedFragment: string) => {
    expect(mockFn).toHaveBeenCalledWith(
      expect.stringContaining(expectedFragment),
      expect.anything()
    );
  },

  expectEncodedUrlWithoutOptions: (mockFn: any, expectedFragment: string) => {
    expect(mockFn).toHaveBeenCalledWith(
      expect.stringContaining(expectedFragment)
    );
  },
};
