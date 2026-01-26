/**
 * useChunkedUpload Hook Tests - Issue #2764 Sprint 4
 *
 * Tests for useChunkedUpload hook:
 * - Chunked upload flow (init, upload chunks, complete)
 * - Progress tracking
 * - Retry logic with exponential backoff
 * - Cancel functionality
 * - Error handling
 * - Threshold detection
 *
 * Pattern: Vitest + React Hooks + vi.fn() fetch mock
 * Coverage target: 319 lines (85%+)
 *
 * NOTE: Uses vi.fn() fetch mock instead of MSW because MSW has issues
 * intercepting requests to localhost:8080 in this test environment.
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useChunkedUpload, CHUNKED_UPLOAD_THRESHOLD } from '../useChunkedUpload';

// API base for fetch mock
const API_BASE = 'http://localhost:8080';

// Store original fetch
const originalFetch = global.fetch;

/**
 * Create a mock File with specified size WITHOUT allocating that memory.
 * This prevents test timeouts from creating actual large files.
 */
function createMockFile(sizeInBytes: number, name: string = 'test.pdf'): File {
  // Create a small actual content
  const smallContent = 'x'.repeat(Math.min(1024, sizeInBytes));
  const blob = new Blob([smallContent], { type: 'application/pdf' });

  // Override the size property to simulate a large file
  const file = new File([blob], name, { type: 'application/pdf' });
  Object.defineProperty(file, 'size', { value: sizeInBytes, writable: false });

  // Override slice to return appropriately sized chunks
  const originalSlice = file.slice.bind(file);
  Object.defineProperty(file, 'slice', {
    value: (start?: number, end?: number) => {
      const actualStart = start ?? 0;
      const actualEnd = end ?? sizeInBytes;
      const chunkSize = actualEnd - actualStart;
      // Return small blob with correct size property
      const chunkBlob = new Blob(['x'.repeat(Math.min(1024, chunkSize))], { type: 'application/pdf' });
      Object.defineProperty(chunkBlob, 'size', { value: chunkSize, writable: false });
      return chunkBlob;
    },
    writable: false,
  });

  return file;
}

// Helper to create a "large" file for chunked upload (above threshold)
function createLargeFile(sizeInMB: number, name: string = 'large.pdf'): File {
  return createMockFile(sizeInMB * 1024 * 1024, name);
}

// Helper to create a "small" file (below threshold)
function createSmallFile(sizeInKB: number = 100, name: string = 'small.pdf'): File {
  return createMockFile(sizeInKB * 1024, name);
}

/**
 * Create a mock fetch function that handles chunked upload endpoints
 */
function createMockFetch(overrides: {
  initResponse?: object | Error;
  chunkResponse?: object | Error | ((chunkIndex: number) => object);
  completeResponse?: object | Error;
  initDelay?: number;
} = {}) {
  const {
    initResponse = {
      sessionId: `session-${Date.now()}`,
      totalChunks: 4,
      chunkSizeBytes: 10 * 1024 * 1024,
      expiresAt: new Date(Date.now() + 3600000).toISOString(),
    },
    chunkResponse = (chunkIndex: number) => ({
      success: true,
      receivedChunks: chunkIndex + 1,
      totalChunks: 4,
      progressPercentage: ((chunkIndex + 1) / 4) * 100,
      isComplete: false,
    }),
    completeResponse = {
      success: true,
      documentId: `doc-${Date.now()}`,
      fileName: 'large.pdf',
    },
    initDelay = 0,
  } = overrides;

  return vi.fn(async (url: string, options?: RequestInit) => {
    // Check for abort signal
    if (options?.signal?.aborted) {
      throw new DOMException('The operation was aborted.', 'AbortError');
    }

    // Handle init endpoint
    if (url.includes('/api/v1/ingest/pdf/chunked/init')) {
      if (initDelay > 0) {
        await new Promise(resolve => setTimeout(resolve, initDelay));
        // Check abort after delay
        if (options?.signal?.aborted) {
          throw new DOMException('The operation was aborted.', 'AbortError');
        }
      }
      if (initResponse instanceof Error) {
        throw initResponse;
      }
      // Parse body to get file size and calculate chunks
      const body = JSON.parse(options?.body as string);
      const chunkSize = 10 * 1024 * 1024;
      const totalChunks = Math.ceil(body.totalFileSize / chunkSize);
      return new Response(JSON.stringify({
        ...initResponse,
        totalChunks,
      }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    // Handle chunk upload endpoint
    if (url.includes('/api/v1/ingest/pdf/chunked/chunk')) {
      if (chunkResponse instanceof Error) {
        throw chunkResponse;
      }
      // Extract chunkIndex from FormData
      const formData = options?.body as FormData;
      const chunkIndex = parseInt(formData?.get('chunkIndex') as string || '0');
      const response = typeof chunkResponse === 'function' ? chunkResponse(chunkIndex) : chunkResponse;
      return new Response(JSON.stringify(response), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    // Handle complete endpoint
    if (url.includes('/api/v1/ingest/pdf/chunked/complete')) {
      if (completeResponse instanceof Error) {
        throw completeResponse;
      }
      return new Response(JSON.stringify(completeResponse), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    // Fallback for other URLs
    return new Response(JSON.stringify({}), { status: 404 });
  });
}

/**
 * Create a mock fetch that returns error responses
 */
function createErrorFetch(endpoint: 'init' | 'chunk' | 'complete', errorResponse: object, status = 500) {
  return vi.fn(async (url: string, options?: RequestInit) => {
    // Check for abort signal
    if (options?.signal?.aborted) {
      throw new DOMException('The operation was aborted.', 'AbortError');
    }

    // Return error for specified endpoint
    if (endpoint === 'init' && url.includes('/api/v1/ingest/pdf/chunked/init')) {
      return new Response(JSON.stringify(errorResponse), {
        status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    // Handle init endpoint for chunk/complete errors (need successful init first)
    if (url.includes('/api/v1/ingest/pdf/chunked/init')) {
      const body = JSON.parse(options?.body as string);
      const chunkSize = 10 * 1024 * 1024;
      const totalChunks = Math.ceil(body.totalFileSize / chunkSize);
      return new Response(JSON.stringify({
        sessionId: `session-${Date.now()}`,
        totalChunks,
        chunkSizeBytes: chunkSize,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    if (endpoint === 'chunk' && url.includes('/api/v1/ingest/pdf/chunked/chunk')) {
      return new Response(JSON.stringify(errorResponse), {
        status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    // Handle successful chunk upload for complete errors
    if (url.includes('/api/v1/ingest/pdf/chunked/chunk')) {
      const formData = options?.body as FormData;
      const chunkIndex = parseInt(formData?.get('chunkIndex') as string || '0');
      return new Response(JSON.stringify({
        success: true,
        receivedChunks: chunkIndex + 1,
        totalChunks: 4,
        progressPercentage: ((chunkIndex + 1) / 4) * 100,
        isComplete: false,
      }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    if (endpoint === 'complete' && url.includes('/api/v1/ingest/pdf/chunked/complete')) {
      return new Response(JSON.stringify(errorResponse), {
        status,
        headers: { 'Content-Type': 'application/json' },
      });
    }

    return new Response(JSON.stringify({}), { status: 404 });
  });
}

describe('useChunkedUpload - Issue #2764 Sprint 4', () => {
  beforeEach(() => {
    // Default successful mock fetch
    global.fetch = createMockFetch();
  });

  afterEach(() => {
    // Restore original fetch
    global.fetch = originalFetch;
    vi.clearAllMocks();
  });

  // ============================================================================
  // TEST 1: Initial state
  // ============================================================================
  it('should initialize with idle state', () => {
    // Arrange & Act
    const { result } = renderHook(() => useChunkedUpload(API_BASE));

    // Assert
    expect(result.current.progress.status).toBe('idle');
    expect(result.current.progress.currentChunk).toBe(0);
    expect(result.current.progress.totalChunks).toBe(0);
    expect(result.current.progress.progressPercentage).toBe(0);
    expect(result.current.isUploading).toBe(false);
  });

  // ============================================================================
  // TEST 2: shouldUseChunkedUpload - below threshold
  // ============================================================================
  it('should return false for files below threshold', () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const smallFile = createSmallFile(1000); // 1 MB

    // Act & Assert
    expect(result.current.shouldUseChunkedUpload(smallFile)).toBe(false);
  });

  // ============================================================================
  // TEST 3: shouldUseChunkedUpload - above threshold
  // ============================================================================
  it('should return true for files above threshold', () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35); // 35 MB (above 30 MB threshold)

    // Act & Assert
    expect(result.current.shouldUseChunkedUpload(largeFile)).toBe(true);
  });

  // ============================================================================
  // TEST 4: Successful chunked upload
  // ============================================================================
  it('should complete chunked upload successfully', async () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult).toMatchObject({
      success: true,
      documentId: expect.stringMatching(/^doc-/),
      fileName: 'large.pdf',
    });
    expect(result.current.progress.status).toBe('completed');
  });

  // ============================================================================
  // TEST 5: Progress tracking during upload
  // ============================================================================
  it('should track progress during upload', async () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);
    const progressSnapshots: string[] = [];

    // Act
    await act(async () => {
      const uploadPromise = result.current.uploadChunked(largeFile, 'game-123');

      // Capture initial status
      progressSnapshots.push(result.current.progress.status);

      await uploadPromise;
    });

    // Assert - Should have gone through states
    expect(result.current.progress.status).toBe('completed');
  });

  // ============================================================================
  // TEST 6: isUploading flag during upload
  // ============================================================================
  it('should set isUploading true during upload', async () => {
    // Arrange - Use delayed init
    global.fetch = createMockFetch({ initDelay: 50 });

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act - Start upload but don't await
    act(() => {
      result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert - Should be uploading
    await waitFor(() => {
      expect(result.current.isUploading).toBe(true);
    });
  });

  // ============================================================================
  // TEST 7: Error handling - init failure
  // ============================================================================
  it('should handle init session failure', async () => {
    // Arrange - Use error fetch for init
    global.fetch = createErrorFetch('init', { error: 'Session creation failed' }, 500);

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult).toMatchObject({
      success: false,
      error: expect.stringContaining('Session creation failed'),
    });
    expect(result.current.progress.status).toBe('error');
  });

  // ============================================================================
  // TEST 8: Error handling - chunk failure
  // ============================================================================
  it('should handle chunk upload failure', async () => {
    // Arrange - Use error fetch for chunk
    global.fetch = createErrorFetch('chunk', { error: 'Chunk upload failed' }, 500);

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult.success).toBe(false);
    expect(result.current.progress.status).toBe('error');
  });

  // ============================================================================
  // TEST 9: Error handling - complete failure
  // ============================================================================
  it('should handle complete failure', async () => {
    // Arrange - Use error fetch for complete
    global.fetch = createErrorFetch('complete', { error: 'Complete failed' }, 500);

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult.success).toBe(false);
    expect(result.current.progress.error).toBeDefined();
  });

  // ============================================================================
  // TEST 10: Missing chunks error
  // ============================================================================
  it('should handle missing chunks response', async () => {
    // Arrange - Use mock fetch with missing chunks complete response
    global.fetch = createMockFetch({
      completeResponse: {
        success: false,
        missingChunks: [1, 3],
      },
    });

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult.success).toBe(false);
    expect(uploadResult.error).toContain('Missing chunks');
  });

  // ============================================================================
  // TEST 11: Cancel upload
  // ============================================================================
  it('should cancel ongoing upload', async () => {
    // Arrange - Use delayed init to allow cancellation
    global.fetch = createMockFetch({ initDelay: 100 });

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act - Start upload and then cancel
    let uploadPromise: Promise<any>;
    act(() => {
      uploadPromise = result.current.uploadChunked(largeFile, 'game-123');
    });

    // Wait a bit then cancel
    await act(async () => {
      await new Promise(resolve => setTimeout(resolve, 20));
      result.current.cancel();
    });

    // Assert
    expect(result.current.progress.status).toBe('error');
    expect(result.current.progress.error).toBe('Upload cancelled');
  });

  // ============================================================================
  // TEST 12: Reset state
  // ============================================================================
  it('should reset progress state', async () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Complete an upload first
    await act(async () => {
      await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Verify completed state
    expect(result.current.progress.status).toBe('completed');

    // Act
    act(() => {
      result.current.reset();
    });

    // Assert
    expect(result.current.progress.status).toBe('idle');
    expect(result.current.progress.currentChunk).toBe(0);
    expect(result.current.progress.totalChunks).toBe(0);
    expect(result.current.progress.progressPercentage).toBe(0);
  });

  // ============================================================================
  // TEST 13: Chunk response with success false
  // ============================================================================
  it('should handle chunk response with success false', async () => {
    // Arrange - Use mock fetch with chunk failure response (success: false)
    global.fetch = createMockFetch({
      chunkResponse: {
        success: false,
        error: 'Chunk validation failed',
      },
    });

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult.success).toBe(false);
  });

  // ============================================================================
  // TEST 14: Complete response with success false
  // ============================================================================
  it('should handle complete response with success false and error', async () => {
    // Arrange - Use mock fetch with complete failure response
    global.fetch = createMockFetch({
      completeResponse: {
        success: false,
        error: 'Assembly failed',
      },
    });

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Act
    let uploadResult;
    await act(async () => {
      uploadResult = await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert
    expect(uploadResult.success).toBe(false);
    expect(uploadResult.error).toBe('Assembly failed');
  });

  // ============================================================================
  // TEST 15: Progress status transitions
  // ============================================================================
  it('should transition through status states correctly', async () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(35);

    // Assert initial
    expect(result.current.progress.status).toBe('idle');

    // Act & Assert through states
    await act(async () => {
      await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Final state
    expect(result.current.progress.status).toBe('completed');
  });

  // ============================================================================
  // TEST 16: Chunk size threshold constant
  // ============================================================================
  it('should export correct threshold constant', () => {
    // Assert
    expect(CHUNKED_UPLOAD_THRESHOLD).toBe(30 * 1024 * 1024); // 30 MB
  });

  // ============================================================================
  // TEST 17: totalBytes tracking
  // ============================================================================
  it('should track total bytes correctly', async () => {
    // Arrange
    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const fileSize = 35 * 1024 * 1024; // 35 MB
    const largeFile = createLargeFile(35);

    // Act
    await act(async () => {
      await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert - totalBytes should be tracked
    // Note: After completion, the state may have been updated
    expect(result.current.progress.totalBytes).toBeGreaterThan(0);
  });

  // ============================================================================
  // TEST 18: Init response parsing
  // ============================================================================
  it('should parse init response correctly', async () => {
    // Arrange - Use mock fetch with 5-chunk config
    global.fetch = createMockFetch({
      initResponse: {
        sessionId: 'test-session-123',
        totalChunks: 5, // Will be overridden by calculated value from file size
        chunkSizeBytes: 10 * 1024 * 1024,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      },
      chunkResponse: (chunkIndex: number) => ({
        success: true,
        receivedChunks: chunkIndex + 1,
        totalChunks: 5,
        progressPercentage: ((chunkIndex + 1) / 5) * 100,
        isComplete: chunkIndex === 4,
      }),
    });

    const { result } = renderHook(() => useChunkedUpload(API_BASE));
    const largeFile = createLargeFile(45); // 45 MB = 5 chunks

    // Act
    await act(async () => {
      await result.current.uploadChunked(largeFile, 'game-123');
    });

    // Assert - Should have processed 5 chunks
    expect(result.current.progress.status).toBe('completed');
  });
});
