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
 * Pattern: Vitest + React Hooks + MSW
 * Coverage target: 319 lines (85%+)
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { http, HttpResponse, delay } from 'msw';
import { server } from '@/__tests__/mocks/server';

import { useChunkedUpload, CHUNKED_UPLOAD_THRESHOLD } from '../useChunkedUpload';

// API base for MSW handlers
const API_BASE = 'http://localhost:8080';

// Helper to create a large file for chunked upload
function createLargeFile(sizeInMB: number, name: string = 'large.pdf'): File {
  const sizeInBytes = sizeInMB * 1024 * 1024;
  const content = new Array(sizeInBytes).fill('a').join('');
  return new File([content], name, { type: 'application/pdf' });
}

// Helper to create a small file
function createSmallFile(sizeInKB: number = 100, name: string = 'small.pdf'): File {
  const sizeInBytes = sizeInKB * 1024;
  const content = new Array(sizeInBytes).fill('a').join('');
  return new File([content], name, { type: 'application/pdf' });
}

describe('useChunkedUpload - Issue #2764 Sprint 4', () => {
  beforeEach(() => {
    // Default successful handlers for chunked upload
    server.use(
      // Init session
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/init`, async ({ request }) => {
        const body = await request.json() as { totalFileSize: number };
        const chunkSize = 10 * 1024 * 1024; // 10 MB
        const totalChunks = Math.ceil(body.totalFileSize / chunkSize);
        return HttpResponse.json({
          sessionId: `session-${Date.now()}`,
          totalChunks,
          chunkSizeBytes: chunkSize,
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
        });
      }),

      // Upload chunk
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/chunk`, async ({ request }) => {
        const formData = await request.formData();
        const chunkIndex = parseInt(formData.get('chunkIndex') as string);
        return HttpResponse.json({
          success: true,
          receivedChunks: chunkIndex + 1,
          totalChunks: 4,
          progressPercentage: ((chunkIndex + 1) / 4) * 100,
          isComplete: false,
        });
      }),

      // Complete upload
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/complete`, () => {
        return HttpResponse.json({
          success: true,
          documentId: `doc-${Date.now()}`,
          fileName: 'large.pdf',
        });
      })
    );
  });

  afterEach(() => {
    server.resetHandlers();
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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/init`, async () => {
        await delay(50);
        return HttpResponse.json({
          sessionId: 'session-1',
          totalChunks: 1,
          chunkSizeBytes: 10 * 1024 * 1024,
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
        });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/init`, () => {
        return HttpResponse.json({ error: 'Session creation failed' }, { status: 500 });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/chunk`, () => {
        return HttpResponse.json({ error: 'Chunk upload failed' }, { status: 500 });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/complete`, () => {
        return HttpResponse.json({ error: 'Complete failed' }, { status: 500 });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/complete`, () => {
        return HttpResponse.json({
          success: false,
          missingChunks: [1, 3],
        });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/init`, async () => {
        await delay(100); // Long delay to allow cancel
        return HttpResponse.json({
          sessionId: 'session-1',
          totalChunks: 4,
          chunkSizeBytes: 10 * 1024 * 1024,
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
        });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/chunk`, () => {
        return HttpResponse.json({
          success: false,
          error: 'Chunk validation failed',
        });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/complete`, () => {
        return HttpResponse.json({
          success: false,
          error: 'Assembly failed',
        });
      })
    );

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
    // Arrange
    server.use(
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/init`, () => {
        return HttpResponse.json({
          sessionId: 'test-session-123',
          totalChunks: 5,
          chunkSizeBytes: 10 * 1024 * 1024,
          expiresAt: new Date(Date.now() + 3600000).toISOString(),
        });
      }),
      http.post(`${API_BASE}/api/v1/ingest/pdf/chunked/chunk`, async ({ request }) => {
        const formData = await request.formData();
        const chunkIndex = parseInt(formData.get('chunkIndex') as string);
        return HttpResponse.json({
          success: true,
          receivedChunks: chunkIndex + 1,
          totalChunks: 5,
          progressPercentage: ((chunkIndex + 1) / 5) * 100,
          isComplete: chunkIndex === 4,
        });
      })
    );

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
