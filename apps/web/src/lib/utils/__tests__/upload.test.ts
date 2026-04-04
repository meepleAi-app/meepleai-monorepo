/**
 * Upload Utility Tests - Issue #4141
 *
 * Test coverage:
 * - Successful chunked upload
 * - Progress callback
 * - Retry logic on failure
 * - Error handling
 * - Edge cases (small files, large files)
 *
 * Target: >90% coverage
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

const mockLoggerError = vi.hoisted(() => vi.fn());
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: (...args: unknown[]) => mockLoggerError(...args),
  },
  getLogger: () => ({
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: (...args: unknown[]) => mockLoggerError(...args),
  }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

import { uploadChunks } from '../upload';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch as unknown as typeof fetch;

describe('uploadChunks', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    mockLoggerError.mockClear();
  });

  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('should upload a small file in single chunk', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();
    const mockSessionId = 'session-123';

    // Mock chunk upload
    mockFetch.mockResolvedValueOnce({
      ok: true,
      text: async () => '',
    });

    // Mock finalize
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'doc-123' }),
    });

    const result = await uploadChunks(mockFile, mockSessionId, mockProgress);

    expect(result).toBe('doc-123');
    expect(mockProgress).toHaveBeenCalledWith(100);
    expect(mockFetch).toHaveBeenCalledTimes(2); // 1 chunk + finalize
  });

  it('should upload a large file in multiple chunks', async () => {
    // Create 12MB file (3 chunks of 5MB each)
    const mockFile = new File([new ArrayBuffer(12 * 1024 * 1024)], 'large.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();
    const mockSessionId = 'session-456';

    // Mock 3 chunk uploads
    mockFetch
      .mockResolvedValueOnce({ ok: true, text: async () => '' })
      .mockResolvedValueOnce({ ok: true, text: async () => '' })
      .mockResolvedValueOnce({ ok: true, text: async () => '' })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'doc-456' }),
      });

    const result = await uploadChunks(mockFile, mockSessionId, mockProgress);

    expect(result).toBe('doc-456');
    expect(mockProgress).toHaveBeenCalledTimes(3);
    expect(mockProgress).toHaveBeenNthCalledWith(1, 33); // First chunk
    expect(mockProgress).toHaveBeenNthCalledWith(2, 67); // Second chunk
    expect(mockProgress).toHaveBeenNthCalledWith(3, 100); // Third chunk
    expect(mockFetch).toHaveBeenCalledTimes(4); // 3 chunks + finalize
  });

  it('should report progress correctly for multiple chunks', async () => {
    const mockFile = new File([new ArrayBuffer(8 * 1024 * 1024)], 'medium.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();

    // Mock 2 chunk uploads + finalize
    mockFetch
      .mockResolvedValueOnce({ ok: true, text: async () => '' })
      .mockResolvedValueOnce({ ok: true, text: async () => '' })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'doc-789' }),
      });

    await uploadChunks(mockFile, 'session-789', mockProgress);

    expect(mockProgress).toHaveBeenCalledWith(50); // After first chunk
    expect(mockProgress).toHaveBeenCalledWith(100); // After second chunk
  });

  it('should retry failed chunk uploads', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();

    // First attempt fails, second succeeds
    mockFetch
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce({ ok: true, text: async () => '' })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({ documentId: 'doc-retry' }),
      });

    const result = await uploadChunks(mockFile, 'session-retry', mockProgress);

    expect(result).toBe('doc-retry');
    expect(mockLoggerError).toHaveBeenCalled();
  });

  it('should throw after max retries', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();

    // All 3 attempts fail
    mockFetch
      .mockRejectedValueOnce(new Error('Network error 1'))
      .mockRejectedValueOnce(new Error('Network error 2'))
      .mockRejectedValueOnce(new Error('Network error 3'));

    await expect(uploadChunks(mockFile, 'session-fail', mockProgress)).rejects.toThrow(
      'Failed to upload chunk 1/1 after 3 attempts'
    );

    expect(mockFetch).toHaveBeenCalledTimes(3); // 3 retry attempts
  });

  it('should handle HTTP error responses', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();

    mockFetch.mockResolvedValue({
      ok: false,
      status: 500,
      statusText: 'Internal Server Error',
      text: async () => 'Server error',
    });

    await expect(uploadChunks(mockFile, 'session-error', mockProgress)).rejects.toThrow(
      'Failed to upload chunk 1/1 after 3 attempts'
    );
  });

  it('should handle finalize failure', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();

    // Chunk upload succeeds
    mockFetch.mockResolvedValueOnce({
      ok: true,
      text: async () => '',
    });

    // Finalize fails
    mockFetch.mockResolvedValueOnce({
      ok: false,
      status: 400,
      text: async () => 'Invalid session',
    });

    await expect(uploadChunks(mockFile, 'session-finalize-fail', mockProgress)).rejects.toThrow(
      'Failed to finalize upload: HTTP 400'
    );
  });

  it('should send correct chunk metadata', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();
    const mockSessionId = 'session-metadata';

    mockFetch.mockResolvedValueOnce({ ok: true, text: async () => '' }).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'doc-123' }),
    });

    await uploadChunks(mockFile, mockSessionId, mockProgress);

    const chunkCall = mockFetch.mock.calls[0];
    expect(chunkCall[0]).toContain('/upload-chunk');

    const formData = chunkCall[1]?.body as FormData;
    expect(formData.get('sessionId')).toBe(mockSessionId);
    expect(formData.get('chunkIndex')).toBe('0');
    expect(formData.get('totalChunks')).toBe('1');
    expect(formData.get('chunk')).toBeInstanceOf(Blob);
  });

  it('should send correct finalize metadata', async () => {
    const mockFile = new File(['test content'], 'test.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();
    const mockSessionId = 'session-finalize';

    mockFetch.mockResolvedValueOnce({ ok: true, text: async () => '' }).mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'doc-finalize' }),
    });

    await uploadChunks(mockFile, mockSessionId, mockProgress);

    const finalizeCall = mockFetch.mock.calls[1];
    expect(finalizeCall[0]).toContain('/finalize-upload');

    const requestBody = JSON.parse(finalizeCall[1]?.body as string);
    expect(requestBody.sessionId).toBe(mockSessionId);
    expect(requestBody.fileName).toBe('test.pdf');
  });

  it('should handle empty file', async () => {
    const mockFile = new File([], 'empty.pdf', {
      type: 'application/pdf',
    });
    const mockProgress = vi.fn();

    // Empty file has 0 chunks, so only finalize is called
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => ({ documentId: 'doc-empty' }),
      text: async () => '',
    });

    const result = await uploadChunks(mockFile, 'session-empty', mockProgress);

    expect(result).toBe('doc-empty');
    expect(mockProgress).not.toHaveBeenCalled(); // No chunks to upload
    expect(mockFetch).toHaveBeenCalledTimes(1); // Only finalize
  });
});
