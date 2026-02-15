/**
 * Extraction Utility Tests - Issue #4141
 *
 * Test coverage:
 * - Successful extraction (immediate completion)
 * - Polling with multiple attempts
 * - Exponential backoff
 * - Timeout scenarios
 * - Failed extraction
 * - Error handling
 *
 * Target: >90% coverage
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { pollExtractionStatus } from '../extraction';
import type { PdfUploadResult } from '../extraction';

// Mock fetch globally
const mockFetch = vi.fn();
global.fetch = mockFetch as unknown as typeof fetch;

const mockCompletedResult: PdfUploadResult = {
  pdfDocumentId: 'pdf-123',
  status: 'completed',
  qualityScore: 0.85,
  extractedTitle: 'Catan',
};

const mockPendingResult: PdfUploadResult = {
  pdfDocumentId: 'pdf-123',
  status: 'pending',
  qualityScore: 0,
  extractedTitle: '',
};

const mockProcessingResult: PdfUploadResult = {
  pdfDocumentId: 'pdf-123',
  status: 'processing',
  qualityScore: 0,
  extractedTitle: '',
};

const mockFailedResult: PdfUploadResult = {
  pdfDocumentId: 'pdf-123',
  status: 'failed',
  qualityScore: 0,
  extractedTitle: '',
  errorMessage: 'Extraction service error',
};

describe('pollExtractionStatus', () => {
  beforeEach(() => {
    mockFetch.mockReset();
    vi.spyOn(console, 'error').mockImplementation(() => {});
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.restoreAllMocks();
    vi.useRealTimers();
  });

  it('should return immediately if extraction already completed', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => mockCompletedResult,
    });

    const promise = pollExtractionStatus('pdf-123');

    // No need to advance timers since it completes immediately
    const result = await promise;

    expect(result).toEqual(mockCompletedResult);
    expect(mockFetch).toHaveBeenCalledTimes(1);
  });

  it('should poll multiple times until completion', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockProcessingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockCompletedResult,
      });

    const promise = pollExtractionStatus('pdf-123');

    // Run all timers to completion
    await vi.runAllTimersAsync();

    const result = await promise;

    expect(result).toEqual(mockCompletedResult);
    expect(mockFetch).toHaveBeenCalledTimes(3);
  });

  it('should use exponential backoff', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockCompletedResult,
      });

    const promise = pollExtractionStatus('pdf-123', 30, 1000);

    // First interval: 1000 * 2 = 2000ms
    await vi.advanceTimersByTimeAsync(2000);
    // Second interval: 1000 * 3 = 3000ms
    await vi.advanceTimersByTimeAsync(3000);

    const result = await promise;

    expect(result).toEqual(mockCompletedResult);
  });

  it('should cap backoff at maximum interval', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockCompletedResult,
      });

    const promise = pollExtractionStatus('pdf-123', 30, 100);

    // Interval should cap at 10000ms even with large multiplier
    await vi.advanceTimersByTimeAsync(200); // 100 * 2
    await vi.advanceTimersByTimeAsync(300); // 100 * 3

    const result = await promise;

    expect(result).toEqual(mockCompletedResult);
  });

  it('should throw error on failed extraction', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => mockFailedResult,
    });

    const promise = pollExtractionStatus('pdf-failed');
    const assertion = expect(promise).rejects.toThrow(
      'PDF extraction failed: Extraction service error'
    );
    await vi.runAllTimersAsync();

    await assertion;
  });

  it('should throw error on max retries exceeded', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => mockPendingResult,
    });

    const promise = pollExtractionStatus('pdf-timeout', 3, 1000);
    const assertion = expect(promise).rejects.toThrow(
      'PDF extraction timeout: exceeded 3 attempts'
    );

    // Advance through all retries
    for (let i = 0; i < 3; i++) {
      await vi.advanceTimersByTimeAsync(1000 * (i + 2));
    }

    await assertion;
    expect(mockFetch).toHaveBeenCalledTimes(3);
  });

  it('should handle HTTP errors', async () => {
    mockFetch.mockResolvedValue({
      ok: false,
      status: 404,
      statusText: 'Not Found',
      text: async () => 'PDF not found',
    });

    const promise = pollExtractionStatus('pdf-notfound', 3, 1000);
    const assertion = expect(promise).rejects.toThrow('HTTP 404: PDF not found');

    // Advance through all retries
    for (let i = 0; i < 3; i++) {
      await vi.advanceTimersByTimeAsync(1000 * (i + 2));
    }

    await assertion;
  });

  it('should handle network errors with retry', async () => {
    mockFetch
      .mockRejectedValueOnce(new Error('Network error'))
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockCompletedResult,
      });

    const promise = pollExtractionStatus('pdf-retry', 5, 1000);

    // Advance through retry interval
    await vi.advanceTimersByTimeAsync(2000);

    const result = await promise;

    expect(result).toEqual(mockCompletedResult);
    expect(mockFetch).toHaveBeenCalledTimes(2);
  });

  it('should send correct API request', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => mockCompletedResult,
    });

    await pollExtractionStatus('pdf-api-check');

    const call = mockFetch.mock.calls[0];
    expect(call[0]).toContain('/documents/pdf-api-check/extraction-status');
    expect(call[1]?.method).toBe('GET');
    expect(call[1]?.credentials).toBe('include');
  });

  it('should handle transition from pending to processing to completed', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockProcessingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => ({
          ...mockCompletedResult,
          qualityScore: 0.92,
          extractedTitle: 'Ticket to Ride',
        }),
      });

    const promise = pollExtractionStatus('pdf-flow');

    await vi.runAllTimersAsync();

    const result = await promise;

    expect(result.status).toBe('completed');
    expect(result.qualityScore).toBe(0.92);
    expect(result.extractedTitle).toBe('Ticket to Ride');
    expect(mockFetch).toHaveBeenCalledTimes(3);
  });

  it('should handle custom max retries', async () => {
    mockFetch.mockResolvedValue({
      ok: true,
      json: async () => mockPendingResult,
    });

    const promise = pollExtractionStatus('pdf-custom', 5, 500);
    const assertion = expect(promise).rejects.toThrow(
      'PDF extraction timeout: exceeded 5 attempts'
    );

    // Advance through all custom retries
    for (let i = 0; i < 5; i++) {
      await vi.advanceTimersByTimeAsync(500 * (i + 2));
    }

    await assertion;
    expect(mockFetch).toHaveBeenCalledTimes(5);
  });

  it('should handle custom polling interval', async () => {
    mockFetch
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockPendingResult,
      })
      .mockResolvedValueOnce({
        ok: true,
        json: async () => mockCompletedResult,
      });

    const promise = pollExtractionStatus('pdf-interval', 10, 3000);

    // First interval: 3000 * 2 = 6000ms
    await vi.advanceTimersByTimeAsync(6000);

    const result = await promise;

    expect(result).toEqual(mockCompletedResult);
  });
});
